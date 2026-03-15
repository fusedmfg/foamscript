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
                _mockLoggingService.Object,
                Mock.Of<TemplateMetadataService>());

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

        // ── Core Resolution — Single Case ──────────────────────────────────────

        [Fact]
        public void HandleCase_ExplicitCores_UsesSpecifiedCount()
        {
            var caseDir = CreateCaseDir();
            _mockMeshService
                .Setup(x => x.MeshCase(caseDir, true, 4, true, true))
                .Returns(new MeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = caseDir, Cores = 4, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshCase(caseDir, true, 4, true, true), Times.Once);
        }

        [Fact]
        public void HandleCase_SingleCore_RunsSerial()
        {
            var caseDir = CreateCaseDir();
            _mockMeshService
                .Setup(x => x.MeshCase(caseDir, false, 1, true, true))
                .Returns(new MeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = caseDir, Cores = 1, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshCase(caseDir, false, 1, true, true), Times.Once);
        }

        [Fact]
        public void HandleCase_ZeroCores_AutoDetectsParallel()
        {
            var caseDir = CreateCaseDir();
            var expectedCores = Environment.ProcessorCount;
            var expectedParallel = expectedCores > 1;

            _mockMeshService
                .Setup(x => x.MeshCase(caseDir, expectedParallel, expectedCores, true, true))
                .Returns(new MeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = caseDir, Cores = 0, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshCase(caseDir, expectedParallel, expectedCores, true, true), Times.Once);
        }

        // ── Core Resolution — Study ────────────────────────────────────────────

        [Fact]
        public void HandleStudy_ExplicitCores_UsesSpecifiedCount()
        {
            var studyDir = CreateStudyDir("Study_0.0", "Study_5.0");
            _mockMeshService
                .Setup(x => x.MeshStudy(studyDir, true, 8, true, true, true))
                .Returns(new StudyMeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = studyDir, Cores = 8, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshStudy(studyDir, true, 8, true, true, true), Times.Once);
        }

        [Fact]
        public void HandleStudy_SingleCore_RunsSerial()
        {
            var studyDir = CreateStudyDir("Study_0.0");
            _mockMeshService
                .Setup(x => x.MeshStudy(studyDir, false, 1, true, true, true))
                .Returns(new StudyMeshResult { IsSuccess = true });

            var model = new MeshModel { Dir = studyDir, Cores = 1, CheckQuality = true, Overwrite = true };
            _handler.Handle(model);

            _mockMeshService.Verify(x => x.MeshStudy(studyDir, false, 1, true, true, true), Times.Once);
        }

        // ── Default Model Values ───────────────────────────────────────────────

        [Fact]
        public void MeshModel_DefaultCores_IsZero()
        {
            var model = new MeshModel();
            model.Cores.Should().Be(0);
        }
    }
}
