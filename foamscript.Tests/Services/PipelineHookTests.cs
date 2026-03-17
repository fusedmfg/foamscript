using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Services
{
    public class PreProcessHookTests : IDisposable
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly Mock<TemplateMetadataService> _mockMetadataService;
        private readonly MeshService _service;
        private readonly List<string> _tempDirs = new();

        public PreProcessHookTests()
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
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
        }

        private string CreateValidCaseDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path.Combine(dir, "constant"));
            Directory.CreateDirectory(Path.Combine(dir, "system"));
            _tempDirs.Add(dir);
            return dir;
        }

        private TemplateMetadata CreateMetadataWithPreProcess(List<PipelineStep> preProcess)
        {
            return new TemplateMetadata
            {
                Name = "test_template",
                Solver = "simpleFoam",
                Geometry = new GeometryConfig { StlName = "test.stl", RequiredStlFiles = new[] { "test.stl" } },
                PreProcess = preProcess,
                MeshPipeline = new List<PipelineStep>
                {
                    new() { Command = "blockMesh", Args = "-case {caseDir}" }
                }
            };
        }

        [Fact]
        public void MeshCase_WithPreProcessHooks_RunsHooksBeforePipeline()
        {
            var caseDir = CreateValidCaseDir();
            var callOrder = new List<string>();

            var metadata = CreateMetadataWithPreProcess(new List<PipelineStep>
            {
                new() { Command = "surfaceCheck", Args = "{caseDir}" }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("surfaceCheck", It.IsAny<string>()))
                .Callback<string, string>((cmd, _) => callOrder.Add(cmd))
                .Returns(new ProcessResult { ExitCode = 0 });

            _mockProcessExecutor
                .Setup(x => x.Execute("blockMesh", It.IsAny<string>()))
                .Callback<string, string>((cmd, _) => callOrder.Add(cmd))
                .Returns(new ProcessResult { ExitCode = 0 });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            callOrder.Should().ContainInOrder("surfaceCheck", "blockMesh");
        }

        [Fact]
        public void MeshCase_WithOptionalPreProcessFailure_ContinuesToPipeline()
        {
            var caseDir = CreateValidCaseDir();
            var metadata = CreateMetadataWithPreProcess(new List<PipelineStep>
            {
                new() { Command = "optionalScript", Args = "{caseDir}", Optional = true }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("optionalScript", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "script failed" });

            _mockProcessExecutor
                .Setup(x => x.Execute("blockMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0 });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Contains("optionalScript") && w.Contains("optional"));
        }

        [Fact]
        public void MeshCase_WithRequiredPreProcessFailure_AbortsBeforePipeline()
        {
            var caseDir = CreateValidCaseDir();
            var metadata = CreateMetadataWithPreProcess(new List<PipelineStep>
            {
                new() { Command = "requiredScript", Args = "{caseDir}" }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("requiredScript", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "failed" });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("requiredScript");
            _mockProcessExecutor.Verify(x => x.Execute("blockMesh", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void MeshCase_WithNoPreProcessHooks_RunsPipelineNormally()
        {
            var caseDir = CreateValidCaseDir();
            var metadata = CreateMetadataWithPreProcess(new List<PipelineStep>());

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0 });

            var result = _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void MeshCase_PreProcessTokens_ResolvesGeometryDirAndStudyDir()
        {
            var caseDir = CreateValidCaseDir();
            string? capturedArgs = null;

            var metadata = CreateMetadataWithPreProcess(new List<PipelineStep>
            {
                new() { Command = "myScript", Args = "{geometryDir} {studyDir}" }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("myScript", It.IsAny<string>()))
                .Callback<string, string>((_, args) => capturedArgs = args)
                .Returns(new ProcessResult { ExitCode = 0 });

            _mockProcessExecutor
                .Setup(x => x.Execute("blockMesh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0 });

            _service.MeshCase(caseDir, parallel: false, cores: 1, checkQuality: false, overwrite: false);

            capturedArgs.Should().NotBeNull();
            capturedArgs.Should().Contain(Path.Combine("constant", "triSurface"));
            capturedArgs.Should().NotContain("{geometryDir}");
            capturedArgs.Should().NotContain("{studyDir}");
        }
    }

    public class PostProcessHookTests : IDisposable
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly Mock<TemplateMetadataService> _mockMetadataService;
        private readonly SolverService _service;
        private readonly List<string> _tempDirs = new();

        public PostProcessHookTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _mockMetadataService = new Mock<TemplateMetadataService>();
            _service = new SolverService(
                _mockProcessExecutor.Object,
                _mockLoggingService.Object,
                _mockMetadataService.Object);
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
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

        private TemplateMetadata CreateMetadataWithPostProcess(List<PipelineStep> postProcess)
        {
            return new TemplateMetadata
            {
                Name = "test_template",
                Solver = "simpleFoam",
                Geometry = new GeometryConfig { StlName = "test.stl", RequiredStlFiles = new[] { "test.stl" } },
                SolvePipeline = new List<PipelineStep>
                {
                    new() { Command = "simpleFoam", Args = "-case {caseDir}", Parallel = true }
                },
                PostProcess = postProcess
            };
        }

        [Fact]
        public void SolveCase_WithPostProcessHooks_RunsHooksAfterPipeline()
        {
            var caseDir = CreateMeshedCaseDir();
            var callOrder = new List<string>();

            var metadata = CreateMetadataWithPostProcess(new List<PipelineStep>
            {
                new() { Command = "exportData", Args = "-case {caseDir}" }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", It.IsAny<string>()))
                .Callback<string, string>((cmd, _) => callOrder.Add(cmd))
                .Returns(new ProcessResult { ExitCode = 0 });

            _mockProcessExecutor
                .Setup(x => x.Execute("exportData", It.IsAny<string>()))
                .Callback<string, string>((cmd, _) => callOrder.Add(cmd))
                .Returns(new ProcessResult { ExitCode = 0 });

            var result = _service.SolveCase(caseDir, parallel: false, cores: 1);

            result.IsSuccess.Should().BeTrue();
            callOrder.Should().ContainInOrder("simpleFoam", "exportData");
        }

        [Fact]
        public void SolveCase_WithOptionalPostProcessFailure_StillSucceeds()
        {
            var caseDir = CreateMeshedCaseDir();
            var metadata = CreateMetadataWithPostProcess(new List<PipelineStep>
            {
                new() { Command = "optionalExport", Args = "{caseDir}", Optional = true }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0 });

            _mockProcessExecutor
                .Setup(x => x.Execute("optionalExport", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "export failed" });

            var result = _service.SolveCase(caseDir, parallel: false, cores: 1);

            result.IsSuccess.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Contains("optionalExport") && w.Contains("optional"));
        }

        [Fact]
        public void SolveCase_WithRequiredPostProcessFailure_ReturnsFailure()
        {
            var caseDir = CreateMeshedCaseDir();
            var metadata = CreateMetadataWithPostProcess(new List<PipelineStep>
            {
                new() { Command = "requiredExport", Args = "{caseDir}" }
            });

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute("simpleFoam", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0 });

            _mockProcessExecutor
                .Setup(x => x.Execute("requiredExport", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Output = "critical failure" });

            var result = _service.SolveCase(caseDir, parallel: false, cores: 1);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("requiredExport");
        }

        [Fact]
        public void SolveCase_WithNoPostProcessHooks_SolvesNormally()
        {
            var caseDir = CreateMeshedCaseDir();
            var metadata = CreateMetadataWithPostProcess(new List<PipelineStep>());

            _mockMetadataService.Setup(x => x.LoadMetadata(caseDir)).Returns(metadata);

            _mockProcessExecutor
                .Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0 });

            var result = _service.SolveCase(caseDir, parallel: false, cores: 1);

            result.IsSuccess.Should().BeTrue();
        }
    }
}
