using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Handlers;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Handlers
{
    public class NewStudyHandlerTests : IDisposable
    {
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<CaseService> _mockCaseService;
        private readonly NewStudyHandler _handler;
        private readonly List<string> _tempFiles = new();
        private readonly List<string> _tempDirs = new();

        public NewStudyHandlerTests()
        {
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _mockProcessExecutor = new Mock<IProcessExecutor>();

            // CaseService requires (IProcessExecutor, GeometryService, TemplateService) — build the chain
            var geometryService = new GeometryService(
                new StlConversionService(_mockProcessExecutor.Object, _mockLoggingService.Object),
                new DomainService(_mockProcessExecutor.Object, _mockLoggingService.Object));
            var templateService = new TemplateService(_mockLoggingService.Object);
            _mockCaseService = new Mock<CaseService>(
                _mockProcessExecutor.Object,
                geometryService,
                templateService);

            _handler = new NewStudyHandler(_mockLoggingService.Object, _mockCaseService.Object);
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateTempJsonFile(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"foamscript-test-config-{Guid.NewGuid()}.json");
            File.WriteAllText(path, content);
            _tempFiles.Add(path);
            return path;
        }

        private static NewStudyModel CreateEmptyModel() => new NewStudyModel
        {
            ProjectName = string.Empty,
            OutputDir = string.Empty,
            ModelSource = string.Empty,
            Angles = string.Empty
        };

        private static NewStudyModel CreateValidCliModel() => new NewStudyModel
        {
            ProjectName = "TestProject",
            OutputDir = Path.GetTempPath(),
            ModelSource = "/tmp/disc.stl",
            Angles = "0,5,10"
        };

        private static string CreateValidJsonConfig(string? outputDir = null)
        {
            outputDir ??= Path.GetTempPath().Replace("\\", "\\\\");
            return $@"{{
    ""projectName"": ""TestProject"",
    ""outputDir"": ""{outputDir}"",
    ""modelSource"": ""/tmp/disc.stl"",
    ""angles"": ""0,5,10"",
    ""velocity"": 20.0,
    ""rpm"": 1000.0
}}";
        }

        [Fact]
        public void Handle_MissingCliArgs_ReturnsNegativeOne()
        {
            // Arrange — all required fields empty, no config file
            var model = CreateEmptyModel();

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void Handle_PartialMissingCliArgs_ReturnsNegativeOne()
        {
            // Arrange — only projectName provided
            var model = CreateEmptyModel();
            model.ProjectName = "MyProject";

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void Handle_ValidJsonConfig_CallsCaseService()
        {
            // Arrange
            var jsonPath = CreateTempJsonFile(CreateValidJsonConfig());
            var model = CreateEmptyModel();
            model.ConfigFile = jsonPath;

            _mockCaseService
                .Setup(cs => cs.CreateStudy(It.IsAny<StudyConfig>(), It.IsAny<string>()))
                .Returns(new StudyResult
                {
                    IsSuccess = true,
                    StudyName = "TestProject",
                    StudyDir = Path.Combine(Path.GetTempPath(), "TestProject"),
                    Cases = new List<CaseInfo>
                    {
                        new CaseInfo { AngleOfAttack = 0, CaseDir = "case_aoa_0", Ux = 20, Uz = 0, Omega = 104.72 },
                        new CaseInfo { AngleOfAttack = 5, CaseDir = "case_aoa_5", Ux = 19.92, Uz = 1.74, Omega = 104.72 },
                        new CaseInfo { AngleOfAttack = 10, CaseDir = "case_aoa_10", Ux = 19.70, Uz = 3.47, Omega = 104.72 }
                    }
                });

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(0);
            _mockCaseService.Verify(
                cs => cs.CreateStudy(
                    It.Is<StudyConfig>(c => c.ProjectName == "TestProject" && c.Angles == "0,5,10"),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public void Handle_JsonConfigMissingFields_ReturnsError()
        {
            // Arrange — JSON missing required 'projectName'
            var json = @"{ ""outputDir"": ""/tmp"", ""modelSource"": ""/tmp/disc.stl"", ""angles"": ""0,5"" }";
            var jsonPath = CreateTempJsonFile(json);
            var model = CreateEmptyModel();
            model.ConfigFile = jsonPath;

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void Handle_JsonConfigNotFound_ReturnsError()
        {
            // Arrange
            var model = CreateEmptyModel();
            model.ConfigFile = Path.Combine(Path.GetTempPath(), "nonexistent-config-file.json");

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void Handle_InvalidJson_ReturnsError()
        {
            // Arrange — malformed JSON
            var jsonPath = CreateTempJsonFile("{ this is not valid json }}}");
            var model = CreateEmptyModel();
            model.ConfigFile = jsonPath;

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void Handle_InvalidTemplateName_ReturnsError()
        {
            // Arrange — valid JSON but with a template name that doesn't exist
            var json = @"{
    ""projectName"": ""TestProject"",
    ""outputDir"": """ + Path.GetTempPath().Replace("\\", "\\\\") + @""",
    ""modelSource"": ""/tmp/disc.stl"",
    ""angles"": ""0,5"",
    ""templateName"": ""nonexistent_template_that_does_not_exist""
}";
            var jsonPath = CreateTempJsonFile(json);
            var model = CreateEmptyModel();
            model.ConfigFile = jsonPath;

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }

        [Fact]
        public void Handle_CaseServiceFailure_ReturnsError()
        {
            // Arrange — CaseService returns failure
            var jsonPath = CreateTempJsonFile(CreateValidJsonConfig());
            var model = CreateEmptyModel();
            model.ConfigFile = jsonPath;

            _mockCaseService
                .Setup(cs => cs.CreateStudy(It.IsAny<StudyConfig>(), It.IsAny<string>()))
                .Returns(new StudyResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Geometry conversion failed"
                });

            // Act
            var result = _handler.Handle(model);

            // Assert
            result.Should().Be(-1);
        }
    }
}
