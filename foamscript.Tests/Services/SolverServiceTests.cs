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
                .Setup(x => x.Execute("pimpleFoam", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

        private void SetupParallelSolverSuccess(string caseDir, int cores = 4)
        {
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir} -force"))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("mpirun", $"-np {cores} pimpleFoam -case {caseDir} -parallel"))
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
        public void SolveCase_Serial_CallsPimpleFoam()
        {
            var caseDir = CreateMeshedCaseDir();
            SetupSerialSolverSuccess(caseDir);

            var result = _service.SolveCase(caseDir, false, 4);

            result.IsSuccess.Should().BeTrue();
            _mockProcessExecutor.Verify(
                x => x.Execute("pimpleFoam", $"-case {caseDir}"),
                Times.Once);
        }

        [Fact]
        public void SolveCase_Serial_Failure_ReturnsError()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("pimpleFoam", $"-case {caseDir}"))
                .Returns(new ProcessResult { ExitCode = 1, Output = "FOAM FATAL ERROR" });

            var result = _service.SolveCase(caseDir, false, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("pimpleFoam failed");
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
                x => x.Execute("decomposePar", $"-case {caseDir} -force"), Times.Once);
            _mockProcessExecutor.Verify(
                x => x.Execute("mpirun", $"-np 4 pimpleFoam -case {caseDir} -parallel"), Times.Once);
            _mockProcessExecutor.Verify(
                x => x.Execute("reconstructPar", $"-case {caseDir}"), Times.Once);
        }

        [Fact]
        public void SolveCase_Parallel_DecomposeFailure_ReturnsError()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir} -force"))
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
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir} -force"))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("mpirun", $"-np 4 pimpleFoam -case {caseDir} -parallel"))
                .Returns(new ProcessResult { ExitCode = 1 });

            var result = _service.SolveCase(caseDir, true, 4);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("pimpleFoam (parallel) failed");
        }

        [Fact]
        public void SolveCase_Parallel_ReconstructFailure_AddsWarning()
        {
            var caseDir = CreateMeshedCaseDir();
            _mockProcessExecutor
                .Setup(x => x.Execute("decomposePar", $"-case {caseDir} -force"))
                .Returns(new ProcessResult { ExitCode = 0 });
            _mockProcessExecutor
                .Setup(x => x.Execute("mpirun", $"-np 4 pimpleFoam -case {caseDir} -parallel"))
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
                .Setup(x => x.Execute("pimpleFoam", It.IsAny<string>()))
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
                .Setup(x => x.Execute("pimpleFoam", $"-case {case0}"))
                .Returns(new ProcessResult { ExitCode = 1 });
            _mockProcessExecutor
                .Setup(x => x.Execute("pimpleFoam", $"-case {case5}"))
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
                .Setup(x => x.Execute("pimpleFoam", $"-case {case0}"))
                .Returns(new ProcessResult { ExitCode = 1 });

            var result = _service.SolveStudy(studyDir, false, 4, false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("aborted");
            // Should not have tried the second case
            result.CaseSummaries.Should().HaveCount(1);
        }

        // ── ParseForceCoeffsFile ─────────────────────────────────────────────────

        [Fact]
        public void ParseForceCoeffsFile_ValidData_ReturnsAveragedCoeffs()
        {
            var file = Path.Combine(Path.GetTempPath(), $"coeffs-{Guid.NewGuid()}.dat");
            var content = @"# Time Cd Cs Cl CmRoll CmPitch CmYaw
0.0000	0.040	0.001	0.100	0.0001	0.010	0.0001
0.0100	0.042	0.001	0.110	0.0001	0.011	0.0001
0.0200	0.044	0.001	0.120	0.0001	0.012	0.0001
0.0300	0.046	0.001	0.130	0.0001	0.013	0.0001
0.0400	0.048	0.001	0.140	0.0001	0.014	0.0001
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
                result.Value.LastTime.Should().BeApproximately(0.1, 0.001);
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
            File.WriteAllText(file, "# Time Cd Cs Cl CmRoll CmPitch CmYaw\n");

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
            var caseDir = CreateMeshedCaseDir();
            var postDir = Path.Combine(caseDir, "postProcessing", "forces", "0");
            Directory.CreateDirectory(postDir);
            File.WriteAllText(Path.Combine(postDir, "coefficient.dat"),
                "# Time Cd Cs Cl CmRoll CmPitch CmYaw\n0.1\t0.045\t0.001\t0.120\t0.0001\t0.012\t0.0001\n");

            var result = SolverService.ParseForceCoeffs(caseDir);

            result.Should().NotBeNull();
            result!.Value.Cd.Should().BeApproximately(0.045, 0.001);
            result.Value.Cl.Should().BeApproximately(0.120, 0.001);
        }
    }
}
