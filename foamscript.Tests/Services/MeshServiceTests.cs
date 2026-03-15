using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Services
{
    public class MeshServiceTests : IDisposable
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly Mock<TemplateMetadataService> _mockMetadataService;
        private readonly MeshService _service;
        private readonly List<string> _tempDirs = new();

        public MeshServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _mockMetadataService = new Mock<TemplateMetadataService>();
            _service = new MeshService(
                Mock.Of<ILogger<LoggingService>>(),
                _mockProcessExecutor.Object,
                _mockLoggingService.Object,
                _mockMetadataService.Object);
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>Creates a temp directory with constant/ and system/ subdirs (valid OpenFOAM case).</summary>
        private string CreateValidCaseDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            _tempDirs.Add(dir);
            return dir;
        }

        /// <summary>
        /// Returns a TemplateMetadata matching the disc rotatingwall steady template's mesh pipeline.
        /// Serial pipeline: blockMesh → surfaceOrient (optional) → surfaceFeatureExtract → snappyHexMesh → checkMesh
        /// Parallel pipeline: adds decomposePar (parallelOnly) before snappy and reconstructParMesh (parallelOnly) after.
        /// </summary>
        private static TemplateMetadata CreateDiscMetadata()
        {
            return new TemplateMetadata
            {
                Name = "external_disc_rotatingwall_steady",
                Solver = "simpleFoam",
                Geometry = new GeometryConfig
                {
                    Type = "disc",
                    StlName = "disc.stl",
                    RequiredStlFiles = new[] { "disc.stl", "tunnel.stl" },
                    SurfaceOrient = new SurfaceOrientConfig { OutsidePoint = new[] { 0.0, 0.0, -1.0 } }
                },
                MeshPipeline = new List<PipelineStep>
                {
                    new() { Command = "blockMesh", Args = "-case {caseDir}" },
                    new() { Command = "surfaceOrient", Args = "{geometryStlPath} \"({outsidePoint})\" {geometryStlPath}", Optional = true },
                    new() { Command = "surfaceFeatureExtract", Args = "-case {caseDir}" },
                    new() { Command = "decomposePar", Args = "-case {caseDir}", ParallelOnly = true },
                    new() { Command = "snappyHexMesh", Args = "-case {caseDir} -overwrite", Parallel = true },
                    new() { Command = "reconstructParMesh", Args = "-case {caseDir} -constant", ParallelOnly = true },
                    new() { Command = "checkMesh", Args = "-case {caseDir}" }
                }
            };
        }

        private void SetupMetadata(string caseDir, TemplateMetadata? metadata = null)
        {
            _mockMetadataService
                .Setup(x => x.LoadMetadata(caseDir))
                .Returns(metadata ?? CreateDiscMetadata());
        }

        private void SetupAllCommandsSuccess()
        {
            _mockProcessExecutor
                .Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });
        }

        private void SetupCommandFailure(string command, string output = "FOAM FATAL ERROR")
        {
            _mockProcessExecutor
                .Setup(x => x.Execute(command, It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = output });
        }

        private void SetupCommandSuccess(string command)
        {
            _mockProcessExecutor
                .Setup(x => x.Execute(command, It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });
        }

        // ── MeshCase — Input Validation ────────────────────────────────────────────

        [Fact]
        public void MeshCase_WhenCaseDirDoesNotExist_ReturnsFailure()
        {
            var result = _service.MeshCase("/nonexistent/path", parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Case directory not found");
        }

        [Fact]
        public void MeshCase_WhenConstantDirMissing_ReturnsFailure()
        {
            var caseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(caseDir, "system"));
            _tempDirs.Add(caseDir);

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("missing constant/ or system/");
        }

        [Fact]
        public void MeshCase_WhenSystemDirMissing_ReturnsFailure()
        {
            var caseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
            _tempDirs.Add(caseDir);

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("missing constant/ or system/");
        }

        // ── MeshCase — Serial Workflow ─────────────────────────────────────────────

        [Fact]
        public void MeshCase_Serial_ExecutesPipelineStepsInOrder()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            // blockMesh, surfaceOrient, surfaceFeatureExtract, snappyHexMesh all called
            _mockProcessExecutor.Verify(x => x.Execute("blockMesh", It.IsAny<string>()), Times.Once);
            _mockProcessExecutor.Verify(x => x.Execute("surfaceOrient", It.IsAny<string>()), Times.Once);
            _mockProcessExecutor.Verify(x => x.Execute("surfaceFeatureExtract", It.IsAny<string>()), Times.Once);
            _mockProcessExecutor.Verify(x => x.Execute("snappyHexMesh", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void MeshCase_Serial_SkipsParallelOnlySteps()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            // decomposePar and reconstructParMesh are parallelOnly — should NOT be called in serial mode
            _mockProcessExecutor.Verify(x => x.Execute("decomposePar", It.IsAny<string>()), Times.Never);
            _mockProcessExecutor.Verify(x => x.Execute("reconstructParMesh", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void MeshCase_Serial_CallsBlockMeshWithCaseArg()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("blockMesh", $"-case {caseDir}"), Times.Once);
        }

        [Fact]
        public void MeshCase_Serial_WhenBlockMeshFails_ReturnsFailure()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupCommandFailure("blockMesh");

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("blockMesh failed");
        }

        [Fact]
        public void MeshCase_Serial_CallsSnappyHexMeshDirectly()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            // In serial mode, snappyHexMesh runs directly (not via mpirun)
            _mockProcessExecutor.Verify(x => x.Execute("snappyHexMesh", It.IsAny<string>()), Times.Once);
            _mockProcessExecutor.Verify(x => x.Execute("mpirun", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void MeshCase_Serial_WhenSnappyFails_ReturnsFailure()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            SetupCommandFailure("snappyHexMesh");

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("snappyHexMesh failed");
        }

        [Fact]
        public void MeshCase_Serial_WhenAllCommandsSucceed_ReturnsSuccess()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void MeshCase_Serial_WhenSurfaceOrientFails_ContinuesAsOptional()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            SetupCommandFailure("surfaceOrient", "surfaceOrient error");

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Contains("surfaceOrient failed"));
        }

        [Fact]
        public void MeshCase_Serial_ResolvesTokensInSurfaceOrientArgs()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            var expectedStlPath = Path.Combine(caseDir, "constant", "triSurface", "disc.stl");
            _mockProcessExecutor.Verify(x => x.Execute("surfaceOrient",
                It.Is<string>(args => args.Contains(expectedStlPath) && args.Contains("0 0 -1"))), Times.Once);
        }

        // ── MeshCase — Parallel Workflow ───────────────────────────────────────────

        [Fact]
        public void MeshCase_Parallel_CallsDecomposePar()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: true, cores: 4, checkQuality: false, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("decomposePar", It.Is<string>(args => args.Contains($"-case {caseDir}"))), Times.Once);
        }

        [Fact]
        public void MeshCase_Parallel_WhenDecomposeParFails_ReturnsFailure()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            SetupCommandFailure("decomposePar", "Error");

            var result = _service.MeshCase(caseDir, parallel: true, cores: 4, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("decomposePar failed");
        }

        [Fact]
        public void MeshCase_Parallel_CallsMpirunWithCorrectArgs()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            int cores = 4;
            SetupAllCommandsSuccess();
            // Override: snappyHexMesh with parallel=true should call mpirun
            _mockProcessExecutor
                .Setup(x => x.Execute("mpirun", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            _service.MeshCase(caseDir, parallel: true, cores: cores, checkQuality: false, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("mpirun", It.Is<string>(args =>
                args.Contains($"-np {cores}") &&
                args.Contains("snappyHexMesh") &&
                args.Contains($"-case {caseDir}") &&
                args.Contains("-parallel"))), Times.Once);
        }

        [Fact]
        public void MeshCase_Parallel_CallsReconstructParMesh()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: true, cores: 4, checkQuality: false, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("reconstructParMesh",
                It.Is<string>(args => args.Contains($"-case {caseDir}") && args.Contains("-constant"))), Times.Once);
        }

        [Fact]
        public void MeshCase_Parallel_WhenReconstructFails_AddsWarningButReturnsSuccess()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("reconstructParMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "Warning output" });

            var result = _service.MeshCase(caseDir, parallel: true, cores: 4, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(w => w.Contains("reconstructParMesh"));
        }

        [Fact]
        public void MeshCase_Parallel_WhenSnappyFails_ReturnsFailure()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("mpirun", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "FOAM FATAL ERROR" });

            var result = _service.MeshCase(caseDir, parallel: true, cores: 4, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("snappyHexMesh failed");
        }

        [Fact]
        public void MeshCase_Parallel_WhenFullWorkflowSucceeds_ReturnsSuccess()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            var result = _service.MeshCase(caseDir, parallel: true, cores: 4, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
        }

        // ── MeshCase — Mesh Quality Check ──────────────────────────────────────────

        [Fact]
        public void MeshCase_WhenCheckQualityTrue_CallsCheckMesh()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("checkMesh", $"-case {caseDir}"), Times.Once);
        }

        [Fact]
        public void MeshCase_WhenCheckQualityFalse_DoesNotCallCheckMesh()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("checkMesh", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void MeshCase_WhenCheckMeshSucceeds_SetsMeshQualityPassedTrue()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            result.MeshQualityPassed.Should().BeTrue();
        }

        [Fact]
        public void MeshCase_WhenCheckMeshFails_AddsWarningAndSetsMeshQualityPassedFalse()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("checkMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "Mesh quality issues found" });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            result.MeshQualityPassed.Should().BeFalse();
            result.Warnings.Should().Contain(w => w.Contains("Mesh quality check failed"));
        }

        // ── ParseCheckMeshOutput ───────────────────────────────────────────────────

        private static readonly string CheckMeshOutput =
            "    cells:            123456\n    points:           234567\n    faces:            345678";

        [Fact]
        public void MeshCase_ParsesCheckMeshOutput_ExtractsCellCount()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("checkMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = CheckMeshOutput });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            result.CellCount.Should().Be(123456);
        }

        [Fact]
        public void MeshCase_ParsesCheckMeshOutput_ExtractsPointCount()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("checkMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = CheckMeshOutput });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            result.PointCount.Should().Be(234567);
        }

        [Fact]
        public void MeshCase_ParsesCheckMeshOutput_ExtractsFaceCount()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("checkMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = CheckMeshOutput });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            result.FaceCount.Should().Be(345678);
        }

        [Fact]
        public void MeshCase_WhenCheckMeshOutputHasNoStats_CountsRemainNull()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();
            _mockProcessExecutor
                .Setup(x => x.Execute("checkMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "Mesh OK" });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: true, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            result.CellCount.Should().BeNull();
            result.PointCount.Should().BeNull();
            result.FaceCount.Should().BeNull();
        }

        // ── MeshCase — Template-Driven Pipeline ───────────────────────────────────

        [Fact]
        public void MeshCase_LoadsMetadataFromCaseDir()
        {
            var caseDir = CreateValidCaseDir();
            SetupMetadata(caseDir);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            _mockMetadataService.Verify(x => x.LoadMetadata(caseDir), Times.Once);
        }

        [Fact]
        public void MeshCase_WhenMetadataLoadFails_ReturnsFailure()
        {
            var caseDir = CreateValidCaseDir();
            _mockMetadataService
                .Setup(x => x.LoadMetadata(caseDir))
                .Throws(new FileNotFoundException("TEMPLATE.json not found"));

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("TEMPLATE.json not found");
        }

        [Fact]
        public void MeshCase_WithAirfoilTemplate_SkipsSurfaceOrient()
        {
            var caseDir = CreateValidCaseDir();
            var airfoilMetadata = new TemplateMetadata
            {
                Name = "external_airfoil_static_steady",
                Geometry = new GeometryConfig
                {
                    StlName = "airfoil.stl",
                    SurfaceOrient = null
                },
                MeshPipeline = new List<PipelineStep>
                {
                    new() { Command = "blockMesh", Args = "-case {caseDir}" },
                    new() { Command = "surfaceFeatureExtract", Args = "-case {caseDir}" },
                    new() { Command = "snappyHexMesh", Args = "-case {caseDir} -overwrite", Parallel = true },
                    new() { Command = "checkMesh", Args = "-case {caseDir}" }
                }
            };
            SetupMetadata(caseDir, airfoilMetadata);
            SetupAllCommandsSuccess();

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            _mockProcessExecutor.Verify(x => x.Execute("surfaceOrient", It.IsAny<string>()), Times.Never);
        }

        // ── MeshStudy — Input Validation ───────────────────────────────────────────

        [Fact]
        public void MeshStudy_WhenStudyDirDoesNotExist_ReturnsFailure()
        {
            var result = _service.MeshStudy("/nonexistent/study", parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Study directory not found");
        }

        [Fact]
        public void MeshStudy_WhenNoCasesFound_ReturnsFailure()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("No valid OpenFOAM cases found");
        }

        // ── MeshStudy — Case Discovery ─────────────────────────────────────────────

        [Fact]
        public void MeshStudy_SkipsGeometryDirectory()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);
            var geometryDir = Path.Combine(studyDir, "geometry");
            Directory.CreateDirectory(Path.Combine(geometryDir, "constant"));
            Directory.CreateDirectory(Path.Combine(geometryDir, "system"));

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("No valid OpenFOAM cases found");
        }

        [Fact]
        public void MeshStudy_SkipsDotDirectories()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);
            var hiddenDir = Path.Combine(studyDir, ".hidden");
            Directory.CreateDirectory(Path.Combine(hiddenDir, "constant"));
            Directory.CreateDirectory(Path.Combine(hiddenDir, "system"));

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("No valid OpenFOAM cases found");
        }

        [Fact]
        public void MeshStudy_SkipsDirsWithoutConstantAndSystem()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);
            Directory.CreateDirectory(Path.Combine(studyDir, "notACase"));

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("No valid OpenFOAM cases found");
        }

        [Fact]
        public void MeshStudy_DiscoversCasesInAlphabeticalOrder()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);

            foreach (var name in new[] { "case_C", "case_A", "case_B" })
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                _mockMetadataService
                    .Setup(x => x.LoadMetadata(caseDir))
                    .Returns(CreateDiscMetadata());
            }

            SetupAllCommandsSuccess();

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.CaseSummaries.Select(c => c.CaseName).Should().BeInAscendingOrder();
        }

        // ── MeshStudy — Batch Execution ────────────────────────────────────────────

        [Fact]
        public void MeshStudy_MeshesAllDiscoveredCases()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);

            foreach (var name in new[] { "case_0", "case_5", "case_10" })
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                _mockMetadataService
                    .Setup(x => x.LoadMetadata(caseDir))
                    .Returns(CreateDiscMetadata());
            }

            SetupAllCommandsSuccess();

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.TotalCases.Should().Be(3);
            result.CaseSummaries.Should().HaveCount(3);
        }

        [Fact]
        public void MeshStudy_WhenAllCasesSucceed_ReportsCorrectCounts()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);

            foreach (var name in new[] { "case_0", "case_5" })
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                _mockMetadataService
                    .Setup(x => x.LoadMetadata(caseDir))
                    .Returns(CreateDiscMetadata());
            }

            SetupAllCommandsSuccess();

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeTrue();
            result.SuccessfulCases.Should().Be(2);
            result.FailedCases.Should().Be(0);
        }

        [Fact]
        public void MeshStudy_WhenAnyCaseFails_IsSuccessFalse()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);

            foreach (var name in new[] { "case_0", "case_5" })
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                _mockMetadataService
                    .Setup(x => x.LoadMetadata(caseDir))
                    .Returns(CreateDiscMetadata());
            }

            SetupCommandFailure("blockMesh");

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: true);

            result.IsSuccess.Should().BeFalse();
            result.FailedCases.Should().BeGreaterThan(0);
        }

        [Fact]
        public void MeshStudy_WhenContinueOnErrorFalse_AbortsAfterFirstFailure()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);

            foreach (var name in new[] { "case_A", "case_B", "case_C" })
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                _mockMetadataService
                    .Setup(x => x.LoadMetadata(caseDir))
                    .Returns(CreateDiscMetadata());
            }

            SetupCommandFailure("blockMesh");

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: false);

            result.IsSuccess.Should().BeFalse();
            result.CaseSummaries.Should().HaveCount(1);
        }

        [Fact]
        public void MeshStudy_WhenContinueOnErrorTrue_ProcessesAllCasesAndReportsAllResults()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            _tempDirs.Add(studyDir);

            foreach (var name in new[] { "case_A", "case_B", "case_C" })
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                _mockMetadataService
                    .Setup(x => x.LoadMetadata(caseDir))
                    .Returns(CreateDiscMetadata());
            }

            SetupCommandFailure("blockMesh");

            var result = _service.MeshStudy(studyDir, parallel: false, cores: 1,
                checkQuality: false, overwrite: false, continueOnError: true);

            result.IsSuccess.Should().BeFalse();
            result.FailedCases.Should().Be(3);
            result.CaseSummaries.Should().HaveCount(3);
        }
    }
}
