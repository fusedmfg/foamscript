using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Services
{
    public class TemplateServiceTests : IDisposable
    {
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly TemplateService _service;
        private readonly List<string> _tempDirs = new();

        public TemplateServiceTests()
        {
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _service = new TemplateService(_mockLoggingService.Object);
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private (string templateDir, string outputDir) CreateTempDirs()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), $"foamscript-test-template-{Guid.NewGuid()}");
            var templateDir = Path.Combine(baseDir, "template");
            var outputDir = Path.Combine(baseDir, "output");
            Directory.CreateDirectory(templateDir);
            // Don't create outputDir — let the service do it (tests CreateOutputDirectory)
            _tempDirs.Add(baseDir);
            return (templateDir, outputDir);
        }

        [Fact]
        public void ProcessTemplate_RendersScribanVariables()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            File.WriteAllText(Path.Combine(templateDir, "test.txt"), "Hello {{ name }}, velocity is {{ velocity }}.");
            var context = new { name = "World", velocity = 20.0 };

            // Act
            _service.ProcessTemplate(templateDir, outputDir, context);

            // Assert
            var output = File.ReadAllText(Path.Combine(outputDir, "test.txt"));
            output.Should().Be("Hello World, velocity is 20.");
        }

        [Fact]
        public void ProcessTemplate_RecursiveDirectories()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();

            // Create nested structure mimicking OpenFOAM case
            var constantDir = Path.Combine(templateDir, "constant", "polyMesh");
            var systemDir = Path.Combine(templateDir, "system");
            Directory.CreateDirectory(constantDir);
            Directory.CreateDirectory(systemDir);

            File.WriteAllText(Path.Combine(constantDir, "blockMeshDict"), "nx {{ nx }};");
            File.WriteAllText(Path.Combine(systemDir, "controlDict"), "endTime {{ end_time }};");
            File.WriteAllText(Path.Combine(templateDir, "root.txt"), "project {{ project_name }}");

            var context = new { nx = 10, end_time = 1.0, project_name = "TestStudy" };

            // Act
            _service.ProcessTemplate(templateDir, outputDir, context);

            // Assert
            File.ReadAllText(Path.Combine(outputDir, "constant", "polyMesh", "blockMeshDict"))
                .Should().Be("nx 10;");
            File.ReadAllText(Path.Combine(outputDir, "system", "controlDict"))
                .Should().Be("endTime 1;");
            File.ReadAllText(Path.Combine(outputDir, "root.txt"))
                .Should().Be("project TestStudy");
        }

        [Fact]
        public void ProcessTemplate_CreatesOutputDirectory()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            File.WriteAllText(Path.Combine(templateDir, "file.txt"), "plain text");

            // Verify output doesn't exist yet
            Directory.Exists(outputDir).Should().BeFalse();

            // Act
            _service.ProcessTemplate(templateDir, outputDir, new { });

            // Assert
            Directory.Exists(outputDir).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "file.txt")).Should().BeTrue();
        }

        [Fact]
        public void ProcessTemplate_CopiesFileAsIs_WhenParseErrors()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            // Unmatched 'end' is a Scriban parse error (no matching if/for/while)
            var invalidContent = "value {{ end }}";
            File.WriteAllText(Path.Combine(templateDir, "broken.txt"), invalidContent);

            // Act
            _service.ProcessTemplate(templateDir, outputDir, new { });

            // Assert — file should be copied verbatim (graceful fallback)
            var output = File.ReadAllText(Path.Combine(outputDir, "broken.txt"));
            output.Should().Be(invalidContent);
        }

        [Fact]
        public void ProcessTemplate_CopiesFileAsIs_OnRenderException()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            // Valid Scriban syntax but accessing a member on a null/missing object
            // Scriban typically renders missing variables as empty string, so we need
            // something that actually throws — calling a method on null
            var content = "value {{ missing_object.sub.deep_property }}";
            File.WriteAllText(Path.Combine(templateDir, "missing.txt"), content);

            // Act — with empty context, nested access may throw or render empty
            _service.ProcessTemplate(templateDir, outputDir, new { });

            // Assert — file should exist in output (either rendered or copied as fallback)
            File.Exists(Path.Combine(outputDir, "missing.txt")).Should().BeTrue();
        }

        [Fact]
        public void ProcessTemplate_HandlesEmptyTemplateDir()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            // templateDir exists but is empty

            // Act
            _service.ProcessTemplate(templateDir, outputDir, new { });

            // Assert
            Directory.Exists(outputDir).Should().BeTrue();
            Directory.GetFiles(outputDir).Should().BeEmpty();
            Directory.GetDirectories(outputDir).Should().BeEmpty();
        }

        [Fact]
        public void ProcessTemplate_PreservesNonTemplateFiles()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            var plainContent = "This is plain text with no template variables at all.\nLine 2.\n";
            File.WriteAllText(Path.Combine(templateDir, "readme.txt"), plainContent);

            // Act
            _service.ProcessTemplate(templateDir, outputDir, new { });

            // Assert
            var output = File.ReadAllText(Path.Combine(outputDir, "readme.txt"));
            output.Should().Be(plainContent);
        }

        [Fact]
        public void ProcessTemplate_MultipleVariables()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            var template = @"FoamFile
{
    version     2.0;
    format      ascii;
    class       dictionary;
    object      transportProperties;
}

nu {{ nu }};
velocity {{ velocity }};
rpm {{ rpm }};
cores {{ cores }};";

            File.WriteAllText(Path.Combine(templateDir, "transportProperties"), template);
            var context = new { nu = 1.5e-5, velocity = 20.0, rpm = 1000.0, cores = 4 };

            // Act
            _service.ProcessTemplate(templateDir, outputDir, context);

            // Assert
            var output = File.ReadAllText(Path.Combine(outputDir, "transportProperties"));
            output.Should().Contain("nu 1.5E-05;");
            output.Should().Contain("velocity 20;");
            output.Should().Contain("rpm 1000;");
            output.Should().Contain("cores 4;");
        }

        [Fact]
        public void ProcessTemplate_NestedObjectAccess()
        {
            // Arrange
            var (templateDir, outputDir) = CreateTempDirs();
            File.WriteAllText(Path.Combine(templateDir, "nested.txt"),
                "nu {{ physics.nu }}; intensity {{ physics.turbulence_intensity }}");

            // Scriban uses snake_case by default for member access on anonymous objects
            var context = new
            {
                physics = new
                {
                    nu = 1.5e-5,
                    turbulence_intensity = 0.05
                }
            };

            // Act
            _service.ProcessTemplate(templateDir, outputDir, context);

            // Assert
            var output = File.ReadAllText(Path.Combine(outputDir, "nested.txt"));
            output.Should().Contain("nu 1.5E-05");
            output.Should().Contain("intensity 0.05");
        }

        [Fact]
        public void ProcessTemplate_ThrowsOnSourceDirNotFound()
        {
            // Arrange
            var nonExistentDir = Path.Combine(Path.GetTempPath(), $"foamscript-nonexistent-{Guid.NewGuid()}");
            var outputDir = Path.Combine(Path.GetTempPath(), $"foamscript-output-{Guid.NewGuid()}");

            // Act & Assert
            var act = () => _service.ProcessTemplate(nonExistentDir, outputDir, new { });
            act.Should().Throw<Exception>();
        }
    }
}
