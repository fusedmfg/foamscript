using Xunit;
using FluentAssertions;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class TemplateMetadataServiceTests : IDisposable
    {
        private readonly TemplateMetadataService _service = new();
        private readonly List<string> _tempDirs = new();

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        [Fact]
        public void LoadMetadata_ValidJson_ReturnsDeserializedMetadata()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
            {
              "name": "external_airfoil_static_steady",
              "solver": "simpleFoam",
              "geometryType": "airfoil",
              "geometryStlName": "airfoil.stl",
              "referenceDimension": "chord",
              "referenceAreaFormula": "rectangular",
              "requiresRotorZone": false,
              "requiredStlFiles": ["airfoil.stl", "tunnel.stl"]
            }
            """);

            var metadata = _service.LoadMetadata(dir);

            metadata.Name.Should().Be("external_airfoil_static_steady");
            metadata.Solver.Should().Be("simpleFoam");
            metadata.GeometryType.Should().Be("airfoil");
            metadata.GeometryStlName.Should().Be("airfoil.stl");
            metadata.ReferenceDimension.Should().Be("chord");
            metadata.ReferenceAreaFormula.Should().Be("rectangular");
            metadata.RequiresRotorZone.Should().BeFalse();
            metadata.RequiredStlFiles.Should().BeEquivalentTo("airfoil.stl", "tunnel.stl");
        }

        [Fact]
        public void LoadMetadata_MissingFile_ReturnsDiscDefaults()
        {
            var dir = CreateTempDir();

            var metadata = _service.LoadMetadata(dir);

            metadata.GeometryType.Should().Be("disc");
            metadata.GeometryStlName.Should().Be("disc.stl");
            metadata.ReferenceDimension.Should().Be("diameter");
            metadata.ReferenceAreaFormula.Should().Be("circular");
            metadata.RequiresRotorZone.Should().BeTrue();
            metadata.RequiredStlFiles.Should().BeEquivalentTo("disc.stl", "tunnel.stl");
            metadata.Validation.Should().NotBeNull();
            metadata.Validation!.MinSize.Should().Be(0.18);
            metadata.Validation!.MaxSize.Should().Be(0.35);
        }

        [Fact]
        public void LoadMetadata_InvalidJson_ReturnsDiscDefaults()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), "not valid json {{{");

            var metadata = _service.LoadMetadata(dir);

            metadata.GeometryType.Should().Be("disc");
            metadata.RequiresRotorZone.Should().BeTrue();
        }

        [Fact]
        public void LoadMetadata_WithValidation_ParsesValidationBlock()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
            {
              "name": "test",
              "validation": {
                "minSize": 0.18,
                "maxSize": 0.35,
                "warningMessage": "Size outside expected range."
              }
            }
            """);

            var metadata = _service.LoadMetadata(dir);

            metadata.Validation.Should().NotBeNull();
            metadata.Validation!.MinSize.Should().Be(0.18);
            metadata.Validation!.MaxSize.Should().Be(0.35);
            metadata.Validation!.WarningMessage.Should().Be("Size outside expected range.");
        }

        [Fact]
        public void LoadMetadata_NoValidation_ReturnsNullValidation()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
            {
              "name": "external_airfoil_static_steady",
              "requiresRotorZone": false
            }
            """);

            var metadata = _service.LoadMetadata(dir);

            metadata.Validation.Should().BeNull();
        }

        [Fact]
        public void CalculateReferenceDimension_Diameter_ReturnsMaxWidthOrDepth()
        {
            var bbox = new BoundingBox { MinX = -0.13, MaxX = 0.13, MinY = -0.01, MaxY = 0.01, MinZ = -0.13, MaxZ = 0.13 };
            var metadata = new TemplateMetadata { ReferenceDimension = "diameter" };

            var result = TemplateMetadataService.CalculateReferenceDimension(bbox, metadata);

            result.Should().BeApproximately(0.26, 1e-10);
        }

        [Fact]
        public void CalculateReferenceDimension_Chord_ReturnsWidth()
        {
            var bbox = new BoundingBox { MinX = 0, MaxX = 1.0, MinY = -0.06, MaxY = 0.06, MinZ = 0, MaxZ = 0.5 };
            var metadata = new TemplateMetadata { ReferenceDimension = "chord" };

            var result = TemplateMetadataService.CalculateReferenceDimension(bbox, metadata);

            result.Should().BeApproximately(1.0, 1e-10);
        }

        [Fact]
        public void CalculateReferenceArea_Circular_ReturnsPiR2()
        {
            var bbox = new BoundingBox { MinX = -0.13, MaxX = 0.13, MinY = -0.01, MaxY = 0.01, MinZ = -0.13, MaxZ = 0.13 };
            var metadata = new TemplateMetadata { ReferenceAreaFormula = "circular" };
            double refLength = 0.26;

            var result = TemplateMetadataService.CalculateReferenceArea(refLength, bbox, metadata);

            var expected = Math.PI * Math.Pow(0.13, 2);
            result.Should().BeApproximately(expected, 1e-10);
        }

        [Fact]
        public void CalculateReferenceArea_Rectangular_ReturnsChordTimesSpan()
        {
            // Airfoil: X=chord(1.0), Y=span(1.0), Z=thickness(0.12)
            var bbox = new BoundingBox { MinX = 0, MaxX = 1.0, MinY = -0.5, MaxY = 0.5, MinZ = -0.06, MaxZ = 0.06 };
            var metadata = new TemplateMetadata { ReferenceAreaFormula = "rectangular" };
            double refLength = 1.0; // chord

            var result = TemplateMetadataService.CalculateReferenceArea(refLength, bbox, metadata);

            result.Should().BeApproximately(1.0, 1e-10); // chord * span = 1.0 * 1.0
        }

        [Fact]
        public void LoadMetadata_DiscTemplate_MatchesExpectedValues()
        {
            // Load the actual disc template JSON from the project
            var templatePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
                "Templates", "external_disc_rotatingwall_steady");

            if (!Directory.Exists(templatePath))
                return; // Skip if running outside project tree

            var metadata = _service.LoadMetadata(templatePath);

            metadata.Name.Should().Be("external_disc_rotatingwall_steady");
            metadata.Solver.Should().Be("simpleFoam");
            metadata.GeometryType.Should().Be("disc");
            metadata.RequiresRotorZone.Should().BeTrue();
        }
    }
}
