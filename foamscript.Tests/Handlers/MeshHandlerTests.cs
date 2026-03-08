using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Handlers;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Handlers
{
    public class MeshHandlerTests : IDisposable
    {
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly Mock<MeshService> _mockMeshService;
        private readonly MeshHandler _handler;
        private readonly List<string> _tempDirs = new();

        public MeshHandlerTests()
        {
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _mockMeshService = new Mock<MeshService>(
                Mock.Of<ILogger<LoggingService>>(),
                Mock.Of<IProcessExecutor>(),
                _mockLoggingService.Object);

            _handler = new MeshHandler(_mockLoggingService.Object, _mockMeshService.Object);
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
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-mesh-handler-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            _tempDirs.Add(dir);
            return dir;
        }

        private string CreateStudyDir(params string[] caseNames)
        {
            var studyDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-mesh-handler-{Guid.NewGuid()}");
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
            _mockMeshService
                .Setup(x => x.MeshCase(caseDir, true, 4, true, true))
                .Returns(new MeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = caseDir, Parallel = false, Cores = 4, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshCase(caseDir, true, 4, true, true), Times.Once);
        }

        [Fact]
        public void HandleCase_CoresEqualsOne_RemainSerial()
        {
            var caseDir = CreateCaseDir();
            _mockMeshService
                .Setup(x => x.MeshCase(caseDir, false, 1, true, true))
                .Returns(new MeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = caseDir, Parallel = false, Cores = 1, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshCase(caseDir, false, 1, true, true), Times.Once);
        }

        [Fact]
        public void HandleCase_ExplicitParallel_StaysParallel()
        {
            var caseDir = CreateCaseDir();
            _mockMeshService
                .Setup(x => x.MeshCase(caseDir, true, 1, true, true))
                .Returns(new MeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = caseDir, Parallel = true, Cores = 1, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshCase(caseDir, true, 1, true, true), Times.Once);
        }

        // ── Parallel Inference — Study ─────────────────────────────────────────

        [Fact]
        public void HandleStudy_CoresGreaterThanOne_InfersParallel()
        {
            var studyDir = CreateStudyDir("Study_0.0", "Study_5.0");
            _mockMeshService
                .Setup(x => x.MeshStudy(studyDir, true, 8, true, true, true))
                .Returns(new StudyMeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = studyDir, Parallel = false, Cores = 8, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshStudy(studyDir, true, 8, true, true, true), Times.Once);
        }

        [Fact]
        public void HandleStudy_CoresEqualsOne_RemainSerial()
        {
            var studyDir = CreateStudyDir("Study_0.0");
            _mockMeshService
                .Setup(x => x.MeshStudy(studyDir, false, 1, true, true, true))
                .Returns(new StudyMeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = studyDir, Parallel = false, Cores = 1, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshStudy(studyDir, false, 1, true, true, true), Times.Once);
        }

        // ── Default Model Values ───────────────────────────────────────────────

        [Fact]
        public void MeshModel_DefaultCores_IsOne()
        {
            var model = new MeshModel();
            model.Cores.Should().Be(1);
        }

        [Fact]
        public void MeshModel_DefaultParallel_IsFalse()
        {
            var model = new MeshModel();
            model.Parallel.Should().BeFalse();
        }
    }
}
