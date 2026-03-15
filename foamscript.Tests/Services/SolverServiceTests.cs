using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Services
{
    public class SolverServiceTests : IDisposable
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly SolverService _service;
        private readonly List<string> _tempDirs = new();

        public SolverServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _service = new SolverService(_mockProcessExecutor.Object, _mockLoggingService.Object);
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateMeshedCaseDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant", "polyMesh"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            Directory.CreateDirectory(Path.Combine(dir, "0"));
            _tempDirs.Add(dir);
            return dir;
        }

        private string CreateStudyDir(params string[] caseNames)
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-study-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            foreach (var name in caseNames)
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant", "polyMesh"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
                Directory.CreateDirectory(Path.Combine(caseDir, "0"));
            }

            return studyDir;
        }

        private void SetupSerialSolverSuccess(string caseDir) =>
            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

        private void SetupParallelSolverSuccess(string caseDir, int cores = 4)
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 0 });
            // Parallel solver runs via bash with tee for log capture
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(a => a.Contains($"mpirun -np {cores} simpleFoam -case {caseDir} -parallel"))))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("reconstructPar", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 0 });
        }

        // ── SolveCase — Input Validation ─────────────────────────────────────────

        [Fact]
        public void SolveCase_NonExistentDir_ReturnsFailure()
        {
            var result = _service.SolveCase("/nonexistent/case", false, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public void SolveCase_NoMesh_ReturnsFailure()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant")); // no polyMesh
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            Directory.CreateDirectory(Path.Combine(dir, "0"));
            _tempDirs.Add(dir);

            var result = _service.SolveCase(dir, false, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("polyMesh");
        }

        [Fact]
        public void SolveCase_NoInitialConditions_ReturnsFailure()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant", "polyMesh"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            // no 0/ directory
            _tempDirs.Add(dir);

            var result = _service.SolveCase(dir, false, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("initial conditions");
        }

        // ── SolveCase — Serial Workflow ──────────────────────────────────────────

        [Fact]
        public void SolveCase_Serial_CallsSimpleFoam()
        {
            var caseDir = CreateMeshedCaseDir();
            SetupSerialSolverSuccess(caseDir);

            var result = _service.SolveCase(caseDir, false, 4);

            result.IsSuccess.Should().BeTrue();
            _mockProcessExecutor.Verify(
                x => x.Execute("simpleFoam", $"-case {caseDir}"),
                Times.Once);
        }

        [Fact]
        public void SolveCase_Serial_Failure_ReturnsError()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "FOAM FATAL ERROR" });

            var result = _service.SolveCase(caseDir, false, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("simpleFoam failed");
        }

        // ── SolveCase — Parallel Workflow ────────────────────────────────────────

        [Fact]
        public void SolveCase_Parallel_CallsDecomposeMpirunReconstruct()
        {
            var caseDir = CreateMeshedCaseDir();
            SetupParallelSolverSuccess(caseDir);

            var result = _service.SolveCase(caseDir, true, 4);

            result.IsSuccess.Should().BeTrue();
            _mockProcessExecutor.Verify(
                x => x.Execute("decomposePar", $"-case {caseDir}"), Times.Once);
            _mockProcessExecutor.Verify(
                x => x.Execute("bash", It.Is<string>(a => a.Contains($"mpirun -np 4 simpleFoam -case {caseDir} -parallel"))), Times.Once);
            _mockProcessExecutor.Verify(
                x => x.Execute("reconstructPar", $"-case {caseDir}"), Times.Once);
        }

        [Fact]
        public void SolveCase_Parallel_DecomposeFailure_ReturnsError()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 1 });

            var result = _service.SolveCase(caseDir, true, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("decomposePar failed");
        }

        [Fact]
        public void SolveCase_Parallel_SolverFailure_ReturnsError()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(a => a.Contains($"mpirun -np 4 simpleFoam -case {caseDir} -parallel"))))
                .Returns(new ProcessResult { ExitCode = 1 });

            var result = _service.SolveCase(caseDir, true, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("simpleFoam (parallel) failed");
        }

        [Fact]
        public void SolveCase_Parallel_ReconstructFailure_AddsWarning()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("bash", It.Is<string>(a => a.Contains($"mpirun -np 4 simpleFoam -case {caseDir} -parallel"))))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("reconstructPar", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 1 });

            var result = _service.SolveCase(caseDir, true, 4);

            result.IsSuccess.Should().BeTrue();
            result.Warnings.Should().ContainSingle(w => w.Contains("reconstructPar"));
        }

        [Fact]
        public void SolveCase_Parallel_CleansOldProcessorDirs()
        {
            var caseDir = CreateMeshedCaseDir();
            // Create old processor dirs
            Directory.CreateDirectory(Path.Combine(caseDir, "processor0"));
            Directory.CreateDirectory(Path.Combine(caseDir, "processor1"));

            SetupParallelSolverSuccess(caseDir);

            _service.SolveCase(caseDir, true, 4);

            // Old processor dirs should be cleaned
            Directory.Exists(Path.Combine(caseDir, "processor0")).Should().BeFalse();
            Directory.Exists(Path.Combine(caseDir, "processor1")).Should().BeFalse();
        }

        // ── SolveStudy ──────────────────────────────────────────────────────────

        [Fact]
        public void SolveStudy_NonExistentDir_ReturnsFailure()
        {
            var result = _service.SolveStudy("/nonexistent/study", false, 4, false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public void SolveStudy_NoCases_ReturnsFailure()
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-study-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            var result = _service.SolveStudy(studyDir, false, 4, false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("No valid OpenFOAM cases");
        }

        [Fact]
        public void SolveStudy_SolvesAllCases()
        {
            var studyDir = CreateStudyDir("Study_0.0", "Study_5.0");

            // Setup serial solver success for both cases
            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            var result = _service.SolveStudy(studyDir, false, 4, false);

            result.IsSuccess.Should().BeTrue();
            result.TotalCases.Should().Be(2);
            result.SuccessfulCases.Should().Be(2);
            result.FailedCases.Should().Be(0);
        }

        [Fact]
        public void SolveStudy_ContinueOnError_ContinuesAfterFailure()
        {
            var studyDir = CreateStudyDir("Study_0.0", "Study_5.0");
            var case0 = Path.Combine(studyDir, "Study_0.0");
            var case5 = Path.Combine(studyDir, "Study_5.0");

            // First case fails, second succeeds
            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", $"-case {case0}"))
                .Returns(new ProcessResult { ExitCode = 1 });
            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", $"-case {case5}"))
                .Returns(new ProcessResult { ExitCode = 0 });

            var result = _service.SolveStudy(studyDir, false, 4, true);

            result.TotalCases.Should().Be(2);
            result.SuccessfulCases.Should().Be(1);
            result.FailedCases.Should().Be(1);
        }

        [Fact]
        public void SolveStudy_StopOnError_AbortsAfterFirstFailure()
        {
            var studyDir = CreateStudyDir("Study_0.0", "Study_5.0");
            var case0 = Path.Combine(studyDir, "Study_0.0");

            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", $"-case {case0}"))
                .Returns(new ProcessResult { ExitCode = 1 });

            var result = _service.SolveStudy(studyDir, false, 4, false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("aborted");
            // Should not have tried the second case
            result.CaseSummaries.Should().HaveCount(1);
        }

        // ── DetectSolver ───────────────────────────────────────────────────────────

        [Fact]
        public void DetectSolver_NoControlDict_ReturnsSimpleFoam()
        {
            var caseDir = CreateMeshedCaseDir();
            var result = SolverService.DetectSolver(caseDir);
            result.Should().Be("simpleFoam");
        }

        [Fact]
        public void DetectSolver_SimpleFoamControlDict_ReturnsSimpleFoam()
        {
            var caseDir = CreateMeshedCaseDir();
            File.WriteAllText(Path.Combine(caseDir, "system", "controlDict"),
                "application     simpleFoam;\n");

            var result = SolverService.DetectSolver(caseDir);
            result.Should().Be("simpleFoam");
        }

        [Fact]
        public void DetectSolver_PimpleFoamControlDict_ReturnsPimpleFoam()
        {
            var caseDir = CreateMeshedCaseDir();
            File.WriteAllText(Path.Combine(caseDir, "system", "controlDict"),
                "application     pimpleFoam;\n");

            var result = SolverService.DetectSolver(caseDir);
            result.Should().Be("pimpleFoam");
        }

        // ── ParseForceCoeffsFile ─────────────────────────────────────────────────

        // ── Real v2512 format — the primary test fixture must match actual OpenFOAM output ──

        [Fact]
        public void ParseForceCoeffsFile_V2512Format_ReturnsCorrectColumns()
        {
            // Real OpenFOAM v2512 coefficient.dat format — 13 data columns.
            // This test exists because a production bug read Cd(r) as Cl and Cl(f) as CmPitch
            // due to assuming legacy 7-column format. The header-based parser must select the
            // correct Cd, Cl, and CmPitch columns from the full v2512 layout.
            var file = Path.Combine(Path.GetTempPath(), $"coeffs-{Guid.NewGuid()}.dat");
            var content = @"# Time        Cd            Cd(f)         Cd(r)         Cl            Cl(f)         Cl(r)         CmPitch       CmRoll        CmYaw         Cs            Cs(f)         Cs(r)
100	0.055000	0.027000	0.028000	0.170000	0.060000	0.110000	-0.040000	0.001000	0.000500	0.002000	0.001000	0.001000
200	0.055200	0.027100	0.028100	0.171000	0.060500	0.110500	-0.040500	0.001100	0.000600	0.002100	0.001050	0.001050
300	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025
400	0.055150	0.027075	0.028075	0.170750	0.060375	0.110375	-0.040375	0.001075	0.000575	0.002075	0.001038	0.001038
500	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025
600	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025
700	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025
800	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025
900	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025
1000	0.055100	0.027050	0.028050	0.170500	0.060250	0.110250	-0.040250	0.001050	0.000550	0.002050	0.001025	0.001025";
            File.WriteAllText(file, content);

            try
            {
                var result = SolverService.ParseForceCoeffsFile(file, 0.1);

                result.Should().NotBeNull();
                // Cd must be column "Cd" (0.0551), NOT Cd(f) or Cd(r)
                result!.Value.Cd.Should().BeApproximately(0.0551, 0.0005);
                // Cl must be column "Cl" (0.1705), NOT Cl(f)=0.060 or Cd(r)=0.028
                result.Value.Cl.Should().BeApproximately(0.1705, 0.001);
                // CmPitch must be column "CmPitch" (-0.04025), NOT Cl(f)=0.060
                result.Value.CmPitch.Should().BeApproximately(-0.04025, 0.001);
                result.Value.LastTime.Should().BeApproximately(1000, 1);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ParseForceCoeffsFile_V2512Format_DoesNotConfuseSubColumns()
        {
            // Regression test: Cd(f), Cd(r), Cl(f), Cl(r) sub-columns must NOT be
            // confused with the aggregate Cd and Cl columns. The bug was that without
            // header parsing, parts[3] was read as Cl (actually Cd(r)) and parts[5]
            // was read as CmPitch (actually Cl(f)). This test uses values where
            // sub-columns differ dramatically from aggregates to catch such confusion.
            var file = Path.Combine(Path.GetTempPath(), $"coeffs-{Guid.NewGuid()}.dat");
            var content = @"# Time        Cd            Cd(f)         Cd(r)         Cl            Cl(f)         Cl(r)         CmPitch       CmRoll        CmYaw         Cs            Cs(f)         Cs(r)
1000	0.055	0.999	0.888	0.171	0.777	0.666	-0.042	0.555	0.444	0.333	0.222	0.111";
            File.WriteAllText(file, content);

            try
            {
                var result = SolverService.ParseForceCoeffsFile(file);

                result.Should().NotBeNull();
                result!.Value.Cd.Should().BeApproximately(0.055, 0.001, "should read Cd column, not Cd(f)=0.999 or Cd(r)=0.888");
                result.Value.Cl.Should().BeApproximately(0.171, 0.001, "should read Cl column, not Cl(f)=0.777 or Cl(r)=0.666");
                result.Value.CmPitch.Should().BeApproximately(-0.042, 0.001, "should read CmPitch column, not any other column");
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ParseForceCoeffsFile_LegacyFormat_StillWorks()
        {
            // Legacy OpenFOAM format (pre-v2512) with 7 columns. The dynamic header
            // parser must handle both old and new formats gracefully.
            var file = Path.Combine(Path.GetTempPath(), $"coeffs-{Guid.NewGuid()}.dat");
            var content = @"# Time Cd Cs Cl CmRoll CmPitch CmYaw
0.0500	0.050	0.001	0.150	0.0001	0.015	0.0001
0.0600	0.050	0.001	0.150	0.0001	0.015	0.0001
0.0700	0.050	0.001	0.150	0.0001	0.015	0.0001
0.0800	0.050	0.001	0.150	0.0001	0.015	0.0001
0.0900	0.050	0.001	0.150	0.0001	0.015	0.0001
0.1000	0.050	0.001	0.150	0.0001	0.015	0.0001";
            File.WriteAllText(file, content);

            try
            {
                var result = SolverService.ParseForceCoeffsFile(file, 0.1);

                result.Should().NotBeNull();
                result!.Value.Cd.Should().BeApproximately(0.050, 0.001);
                result.Value.Cl.Should().BeApproximately(0.150, 0.001);
                result.Value.CmPitch.Should().BeApproximately(0.015, 0.001);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ParseForceCoeffsFile_EmptyFile_ReturnsNull()
        {
            var file = Path.Combine(Path.GetTempPath(), $"coeffs-{Guid.NewGuid()}.dat");
            File.WriteAllText(file, "# Time        Cd            Cd(f)         Cd(r)         Cl            Cl(f)         Cl(r)         CmPitch       CmRoll        CmYaw         Cs            Cs(f)         Cs(r)\n");

            try
            {
                var result = SolverService.ParseForceCoeffsFile(file);
                result.Should().BeNull();
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public void ParseForceCoeffsFile_NonExistentFile_ReturnsNull()
        {
            var result = SolverService.ParseForceCoeffsFile("/nonexistent/file.dat");
            result.Should().BeNull();
        }

        [Fact]
        public void ParseForceCoeffs_NoCoefficientFile_ReturnsNull()
        {
            var caseDir = CreateMeshedCaseDir();
            var result = SolverService.ParseForceCoeffs(caseDir);
            result.Should().BeNull();
        }

        [Fact]
        public void ParseForceCoeffs_WithCoefficientFile_ReturnsValues()
        {
            // Integration test using real v2512 format — verifies full path resolution + parsing
            var caseDir = CreateMeshedCaseDir();
            var postDir = Path.Combine(caseDir, "postProcessing", "forces", "0");
            Directory.CreateDirectory(postDir);
            File.WriteAllText(Path.Combine(postDir, "coefficient.dat"),
                "# Time        Cd            Cd(f)         Cd(r)         Cl            Cl(f)         Cl(r)         CmPitch       CmRoll        CmYaw         Cs            Cs(f)         Cs(r)\n" +
                "1000\t0.045\t0.022\t0.023\t0.120\t0.055\t0.065\t-0.012\t0.001\t0.0005\t0.002\t0.001\t0.001\n");

            var result = SolverService.ParseForceCoeffs(caseDir);

            result.Should().NotBeNull();
            result!.Value.Cd.Should().BeApproximately(0.045, 0.001);
            result.Value.Cl.Should().BeApproximately(0.120, 0.001);
            result.Value.CmPitch.Should().BeApproximately(-0.012, 0.001);
        }
    }
}
