using Xunit;
using FluentAssertions;
using Moq;
using foamscript.Services;
using foamscript.Models;
using Microsoft.Extensions.Logging;

namespace foamscript.Tests.Services
{
    public class CaseServiceTests : IDisposable
    {
        private readonly Mock<IProcessExecutor> _mockProcessExecutor;
        private readonly Mock<LoggingService> _mockLoggingService;
        private readonly GeometryService _geometryService;
        private readonly TemplateService _templateService;
        private readonly TemplateMetadataService _metadataService;
        private readonly CaseService _service;
        private readonly List<string> _tempDirs = new();

        public CaseServiceTests()
        {
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockLoggingService = new Mock<LoggingService>(Mock.Of<ILogger<LoggingService>>());
            _geometryService = new GeometryService(
                new StlConversionService(_mockProcessExecutor.Object, _mockLoggingService.Object),
                new DomainService(_mockProcessExecutor.Object, _mockLoggingService.Object));
            _templateService = new TemplateService(_mockLoggingService.Object);
            _metadataService = new TemplateMetadataService();
            _service = new CaseService(_mockProcessExecutor.Object, _geometryService, _templateService, _metadataService);
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>Creates a minimal ASCII STL disc file (~0.26m diameter) in the given directory.</summary>
        private string CreateMinimalDiscStl(string dir)
        {
            var stlPath = Path.Combine(dir, "disc.stl");
            // Simple box-like STL representing a disc ~0.26m x 0.02m x 0.26m centered near origin
            File.WriteAllText(stlPath, @"solid disc
  facet normal 0 1 0
    outer loop
      vertex -0.13 0.01 -0.13
      vertex  0.13 0.01 -0.13
      vertex  0.13 0.01  0.13
    endloop
  endfacet
  facet normal 0 1 0
    outer loop
      vertex -0.13 0.01 -0.13
      vertex  0.13 0.01  0.13
      vertex -0.13 0.01  0.13
    endloop
  endfacet
  facet normal 0 -1 0
    outer loop
      vertex -0.13 -0.01 -0.13
      vertex  0.13 -0.01  0.13
      vertex  0.13 -0.01 -0.13
    endloop
  endfacet
  facet normal 0 -1 0
    outer loop
      vertex -0.13 -0.01 -0.13
      vertex -0.13 -0.01  0.13
      vertex  0.13 -0.01  0.13
    endloop
  endfacet
endsolid disc
");
            return stlPath;
        }

        private string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        /// <summary>Creates a minimal template directory with a single Scriban-templated file.</summary>
        private string CreateMinimalTemplate(string baseDir)
        {
            var templateDir = Path.Combine(baseDir, "template");
            Directory.CreateDirectory(Path.Combine(templateDir, "0"));
            Directory.CreateDirectory(Path.Combine(templateDir, "constant"));
            Directory.CreateDirectory(Path.Combine(templateDir, "system"));

            // A simple templated file to verify Scriban rendering works
            File.WriteAllText(Path.Combine(templateDir, "0", "U"),
                "Ux {{ ux }};\nUz {{ uz }};");
            File.WriteAllText(Path.Combine(templateDir, "constant", "transportProperties"),
                "nu {{ nu }};");
            File.WriteAllText(Path.Combine(templateDir, "system", "controlDict"),
                "endTime {{ end_time }};");

            // TEMPLATE.json — required by TemplateMetadataService
            File.WriteAllText(Path.Combine(templateDir, "TEMPLATE.json"), """
            {
              "name": "test_template",
              "description": "Minimal test template",
              "solver": "simpleFoam",
              "geometry": {
                "type": "disc",
                "stlName": "disc.stl",
                "requiredStlFiles": ["disc.stl", "tunnel.stl"]
              },
              "reference": {
                "dimension": "diameter",
                "areaFormula": "circular"
              },
              "rotation": {
                "enabled": true,
                "requiresRotorZone": true
              }
            }
            """);

            return templateDir;
        }

        private StudyConfig CreateDefaultConfig(string outputDir, string modelSource)
        {
            return new StudyConfig
            {
                ProjectName = "TestProject",
                OutputDir = outputDir,
                ModelSource = modelSource,
                Angles = "0",
                Velocity = 20.0,
                Rpm = 1000.0,
                InputUnits = "m",
                MeshSize = 0.05,
                Cores = 4,
                Physics = new StudyPhysicsConfig(),
                Domain = new StudyDomainConfig()
            };
        }

        /// <summary>Sets up mock so GeometryService.GenerateDomain's file-exists check passes.</summary>
        private void SetupFileExists(string path) =>
            _mockProcessExecutor
                .Setup(x => x.Execute("test", $"-f {path}"))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

        // ── CalculateTemplateContext — Unit Tests ──────────────────────────────────

        [Fact]
        public void CalculateTemplateContext_DoesNotContain_LegacyDeltaTProperties()
        {
            var physics = new StudyPhysicsConfig();
            var domain = new StudyDomainConfig();

            var context = CaseService.CalculateTemplateContext(
                ux: 20.0, uz: 0.0, omegaRotation: 100.0,
                cores: 4, refLength: 0.211, aref: 0.035,
                physics: physics, domain: domain);

            // delta_t and max_delta_t were removed — they belonged to the abandoned AMI/transient approach
            var properties = context.GetType().GetProperties();
            var propertyNames = properties.Select(p => p.Name).ToArray();
            propertyNames.Should().NotContain("delta_t");
            propertyNames.Should().NotContain("max_delta_t");
        }

        [Fact]
        public void CalculateTemplateContext_StillContains_OmegaRotation()
        {
            var physics = new StudyPhysicsConfig();
            var domain = new StudyDomainConfig();

            var context = CaseService.CalculateTemplateContext(
                ux: 20.0, uz: 0.0, omegaRotation: 100.0,
                cores: 4, refLength: 0.211, aref: 0.035,
                physics: physics, domain: domain);

            // omega_rotation must survive — used by disc template's rotatingWallVelocity BC
            var omegaProp = context.GetType().GetProperty("omega_rotation");
            omegaProp.Should().NotBeNull();
            ((double)omegaProp!.GetValue(context)!).Should().BeApproximately(100.0, 1e-10);
        }

        // ── CreateStudy — Input Validation ──────────────────────────────────────────

        [Fact]
        public void CreateStudy_WhenTemplateDoesNotExist_ReturnsFailure()
        {
            var tempDir = CreateTempDir();
            var config = CreateDefaultConfig(tempDir, "disc.stl");

            var result = _service.CreateStudy(config, "/nonexistent/template");

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Template directory not found");
        }

        [Fact]
        public void CreateStudy_WhenAnglesInvalid_ReturnsFailure()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var config = CreateDefaultConfig(tempDir, "disc.stl");
            config.Angles = "not,numbers";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid angles");
        }

        [Fact]
        public void CreateStudy_WhenModelSourceDoesNotExist_ReturnsFailure()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var config = CreateDefaultConfig(tempDir, "/nonexistent/disc.stl");

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not found");
        }

        // ── CreateStudy — Angle Parsing ─────────────────────────────────────────────

        [Fact]
        public void CreateStudy_SingleAngle_CreatesOneCase()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            result.Cases.Should().HaveCount(1);
            result.Cases[0].AngleOfAttack.Should().Be(0.0);
        }

        [Fact]
        public void CreateStudy_MultipleAngles_CreatesCorrectNumberOfCases()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "-5,0,5,10";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            result.Cases.Should().HaveCount(4);
            result.Cases.Select(c => c.AngleOfAttack).Should().BeEquivalentTo(new[] { -5.0, 0.0, 5.0, 10.0 });
        }

        [Fact]
        public void CreateStudy_NegativeAngles_ParsedCorrectly()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "-10,-5";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            result.Cases.Should().HaveCount(2);
            result.Cases[0].AngleOfAttack.Should().Be(-10.0);
            result.Cases[1].AngleOfAttack.Should().Be(-5.0);
        }

        // ── CreateStudy — Velocity Decomposition ────────────────────────────────────

        [Fact]
        public void CreateStudy_AtZeroAngle_VelocityIsAllUx()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Velocity = 20.0;
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            result.Cases[0].Ux.Should().BeApproximately(20.0, 1e-10);
            result.Cases[0].Uz.Should().BeApproximately(0.0, 1e-10);
        }

        [Fact]
        public void CreateStudy_AtNonZeroAngle_VelocityDecomposedCorrectly()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Velocity = 20.0;
            config.Angles = "5";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            var angleRad = 5.0 * Math.PI / 180.0;
            result.Cases[0].Ux.Should().BeApproximately(20.0 * Math.Cos(angleRad), 1e-10);
            result.Cases[0].Uz.Should().BeApproximately(20.0 * Math.Sin(angleRad), 1e-10);
        }

        // ── CreateStudy — RPM Conversion ────────────────────────────────────────────

        [Fact]
        public void CreateStudy_ConvertsRpmToRadPerSec()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Rpm = 1000.0;
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            var expectedOmega = 1000.0 * 2.0 * Math.PI / 60.0;
            result.Cases[0].Omega.Should().BeApproximately(expectedOmega, 1e-10);
        }

        // ── CreateStudy — Directory Structure ───────────────────────────────────────

        [Fact]
        public void CreateStudy_CreatesStudyDirectory()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            result.StudyDir.Should().NotBeNull();
            Directory.Exists(result.StudyDir).Should().BeTrue();
        }

        [Fact]
        public void CreateStudy_CreatesGeometryDirectory()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            var geometryDir = Path.Combine(result.StudyDir!, "geometry");
            Directory.Exists(geometryDir).Should().BeTrue();
        }

        [Fact]
        public void CreateStudy_CopiesStlFilesToTriSurface()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            var triSurfaceDir = Path.Combine(result.Cases[0].CaseDir, "constant", "triSurface");
            Directory.Exists(triSurfaceDir).Should().BeTrue();
            File.Exists(Path.Combine(triSurfaceDir, "disc.stl")).Should().BeTrue();
            File.Exists(Path.Combine(triSurfaceDir, "tunnel.stl")).Should().BeTrue();
        }

        [Fact]
        public void CreateStudy_CaseDirectoryNamedWithAngle()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Angles = "5";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            Path.GetFileName(result.Cases[0].CaseDir).Should().Be("TestProject_5.0");
        }

        // ── CreateStudy — Template Rendering ────────────────────────────────────────

        [Fact]
        public void CreateStudy_RendersTemplateWithCorrectVelocity()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Velocity = 25.0;
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            var uFile = File.ReadAllText(Path.Combine(result.Cases[0].CaseDir, "0", "U"));
            uFile.Should().Contain("Ux 25");
        }

        [Fact]
        public void CreateStudy_RendersTemplateWithPhysicsParams()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "TestProject", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.Physics.Nu = 1.6e-5;
            config.Physics.EndTime = 2.5;
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            var nuFile = File.ReadAllText(Path.Combine(result.Cases[0].CaseDir, "constant", "transportProperties"));
            // Scriban formats doubles — check for the value in any reasonable format
            (nuFile.Contains("1.6E-05") || nuFile.Contains("1.6e-05") || nuFile.Contains("1.6E-5")).Should().BeTrue();

            var controlDict = File.ReadAllText(Path.Combine(result.Cases[0].CaseDir, "system", "controlDict"));
            controlDict.Should().Contain("2.5");
        }

        // ── CreateStudy — STEP File Handling ────────────────────────────────────────

        [Fact]
        public void CreateStudy_WithStepFile_CallsGmshForConversion()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var stepFile = Path.Combine(tempDir, "disc.step");
            File.WriteAllText(stepFile, "fake step content");

            // gmsh check will find the file
            _mockProcessExecutor
                .Setup(x => x.Execute("test", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 0, Output = "" });

            // gmsh conversion — will fail because no real gmsh, but we verify the call
            _mockProcessExecutor
                .Setup(x => x.Execute("gmsh", It.IsAny<string>()))
                .Returns(new ProcessResult { ExitCode = 1, Error = "gmsh not available" });

            var config = CreateDefaultConfig(tempDir, stepFile);
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            // Should fail because gmsh mock returns failure, but it should have tried
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("convert");
            _mockProcessExecutor.Verify(x => x.Execute("gmsh", It.IsAny<string>()), Times.Once);
        }

        // ── CreateStudy — StudyResult Metadata ──────────────────────────────────────

        [Fact]
        public void CreateStudy_SetsStudyNameAndDir()
        {
            var tempDir = CreateTempDir();
            var templateDir = CreateMinimalTemplate(tempDir);
            var discStl = CreateMinimalDiscStl(tempDir);
            SetupFileExists(Path.Combine(tempDir, "MyDiscStudy", "geometry", "disc.stl"));
            var config = CreateDefaultConfig(tempDir, discStl);
            config.ProjectName = "MyDiscStudy";
            config.Angles = "0";

            var result = _service.CreateStudy(config, templateDir);

            result.IsSuccess.Should().BeTrue();
            result.StudyName.Should().Be("MyDiscStudy");
            result.StudyDir.Should().EndWith("MyDiscStudy");
        }
    }
}
