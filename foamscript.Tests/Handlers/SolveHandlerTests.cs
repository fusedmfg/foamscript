using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Handlers;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Handlers
{
    public class SolveHandlerTests : IDisposable
    {
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly Mock<SolverService> _mockSolverService;
        private readonly SolveHandler _handler;
        private readonly List<string> _tempDirs = new();

        public SolveHandlerTests()
        {
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _mockSolverService = new Mock<SolverService>(
                Mock.Of<IProcessExecutor>(),
                _mockLoggingService.Object);

            _handler = new SolveHandler(_mockLoggingService.Object, _mockSolverService.Object);
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateCaseDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-solve-handler-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            _tempDirs.Add(dir);
            return dir;
        }

        private string CreateStudyDir(params string[] caseNames)
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-solve-handler-{Guid.NewGuid()}");
            Directory.CreateDirectory(studyDir);
            _tempDirs.Add(studyDir);

            foreach (var name in caseNames)
            {
                var caseDir = Path.Combine(studyDir, name);
                Directory.CreateDirectory(Path.Combine(caseDir, "constant"));
                Directory.CreateDirectory(Path.Combine(caseDir, "system"));
            }

            return studyDir;
        }

        // ── Parallel Inference — Single Case ───────────────────────────────────

        [Fact]
        public void HandleCase_CoresGreaterThanOne_InfersParallel()
        {
            var caseDir = CreateCaseDir();
            _mockSolverService
                .Setup(x => x.SolveCase(caseDir, true, 4))
                .Returns(new SolveResult { IsSuccess = true });

            var model = new SolveModel { Dir = caseDir, Parallel = false, Cores = 4 };
            _handler.Handle(model);

            // Verify SolveCase was called with parallel=true even though model.Parallel=false
            _mockSolverService.Verify(x => x.SolveCase(caseDir, true, 4), Times.Once);
        }

        [Fact]
        public void HandleCase_CoresEqualsOne_RemainSerial()
        {
            var caseDir = CreateCaseDir();
            _mockSolverService
                .Setup(x => x.SolveCase(caseDir, false, 1))
                .Returns(new SolveResult { IsSuccess = true });

            var model = new SolveModel { Dir = caseDir, Parallel = false, Cores = 1 };
            _handler.Handle(model);

            _mockSolverService.Verify(x => x.SolveCase(caseDir, false, 1), Times.Once);
        }

        [Fact]
        public void HandleCase_ExplicitParallel_StaysParallel()
        {
            var caseDir = CreateCaseDir();
            _mockSolverService
                .Setup(x => x.SolveCase(caseDir, true, 1))
                .Returns(new SolveResult { IsSuccess = true });

            var model = new SolveModel { Dir = caseDir, Parallel = true, Cores = 1 };
            _handler.Handle(model);

            _mockSolverService.Verify(x => x.SolveCase(caseDir, true, 1), Times.Once);
        }

        // ── Parallel Inference — Study ─────────────────────────────────────────

        [Fact]
        public void HandleStudy_CoresGreaterThanOne_InfersParallel()
        {
            var studyDir = CreateStudyDir("Study_0.0", "Study_5.0");
            _mockSolverService
                .Setup(x => x.SolveStudy(studyDir, true, 8, true))
                .Returns(new StudySolveResult { IsSuccess = true });

            var model = new SolveModel { Dir = studyDir, Parallel = false, Cores = 8 };
            _handler.Handle(model);

            _mockSolverService.Verify(x => x.SolveStudy(studyDir, true, 8, true), Times.Once);
        }

        [Fact]
        public void HandleStudy_CoresEqualsOne_RemainSerial()
        {
            var studyDir = CreateStudyDir("Study_0.0");
            _mockSolverService
                .Setup(x => x.SolveStudy(studyDir, false, 1, true))
                .Returns(new StudySolveResult { IsSuccess = true });

            var model = new SolveModel { Dir = studyDir, Parallel = false, Cores = 1 };
            _handler.Handle(model);

            _mockSolverService.Verify(x => x.SolveStudy(studyDir, false, 1, true), Times.Once);
        }

        // ── Default Model Values ───────────────────────────────────────────────

        [Fact]
        public void SolveModel_DefaultCores_IsOne()
        {
            var model = new SolveModel();
            model.Cores.Should().Be(1);
        }

        [Fact]
        public void SolveModel_DefaultParallel_IsFalse()
        {
            var model = new SolveModel();
            model.Parallel.Should().BeFalse();
        }
    }
}
