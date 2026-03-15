using System.Text.Json;
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
              "geometry": {
                "type": "airfoil",
                "stlName": "airfoil.stl",
                "requiredStlFiles": ["airfoil.stl", "tunnel.stl"]
              },
              "reference": {
                "dimension": "chord",
                "areaFormula": "rectangular"
              },
              "rotation": {
                "enabled": false,
                "requiresRotorZone": false
              }
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
        public void LoadMetadata_MissingFile_ThrowsFileNotFoundException()
        {
            var dir = CreateTempDir();

            var act = () => _service.LoadMetadata(dir);

            act.Should().Throw<FileNotFoundException>()
                .WithMessage("*TEMPLATE.json*");
        }

        [Fact]
        public void LoadMetadata_InvalidJson_ThrowsJsonException()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), "not valid json {{{");

            var act = () => _service.LoadMetadata(dir);

            act.Should().Throw<JsonException>();
        }

        [Fact]
        public void LoadMetadata_WithValidation_ParsesValidationBlock()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
            {
              "name": "test",
              "geometry": { "type": "disc", "stlName": "disc.stl", "requiredStlFiles": ["disc.stl"] },
              "reference": { "dimension": "diameter", "areaFormula": "circular" },
              "rotation": { "enabled": true, "requiresRotorZone": true },
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
              "geometry": { "type": "airfoil", "stlName": "airfoil.stl", "requiredStlFiles": ["airfoil.stl"] },
              "reference": { "dimension": "chord", "areaFormula": "rectangular" },
              "rotation": { "enabled": false, "requiresRotorZone": false }
            }
            """);

            var metadata = _service.LoadMetadata(dir);

            metadata.Validation.Should().BeNull();
        }

        [Fact]
        public void CalculateReferenceDimension_Diameter_ReturnsMaxWidthOrDepth()
        {
            var bbox = new BoundingBox { MinX = -0.13, MaxX = 0.13, MinY = -0.01, MaxY = 0.01, MinZ = -0.13, MaxZ = 0.13 };
            var metadata = new TemplateMetadata
            {
                Reference = new ReferenceConfig { Dimension = "diameter", AreaFormula = "circular" }
            };

            var result = TemplateMetadataService.CalculateReferenceDimension(bbox, metadata);

            result.Should().BeApproximately(0.26, 1e-10);
        }

        [Fact]
        public void CalculateReferenceDimension_Chord_ReturnsWidth()
        {
            var bbox = new BoundingBox { MinX = 0, MaxX = 1.0, MinY = -0.06, MaxY = 0.06, MinZ = 0, MaxZ = 0.5 };
            var metadata = new TemplateMetadata
            {
                Reference = new ReferenceConfig { Dimension = "chord", AreaFormula = "rectangular" }
            };

            var result = TemplateMetadataService.CalculateReferenceDimension(bbox, metadata);

            result.Should().BeApproximately(1.0, 1e-10);
        }

        [Fact]
        public void CalculateReferenceArea_Circular_ReturnsPiR2()
        {
            var bbox = new BoundingBox { MinX = -0.13, MaxX = 0.13, MinY = -0.01, MaxY = 0.01, MinZ = -0.13, MaxZ = 0.13 };
            var metadata = new TemplateMetadata
            {
                Reference = new ReferenceConfig { Dimension = "diameter", AreaFormula = "circular" }
            };
            double refLength = 0.26;

            var result = TemplateMetadataService.CalculateReferenceArea(refLength, bbox, metadata);

            var expected = Math.PI * Math.Pow(0.13, 2);
            result.Should().BeApproximately(expected, 1e-10);
        }

        [Fact]
        public void CalculateReferenceArea_Rectangular_ReturnsChordTimesSpan()
        {
            var bbox = new BoundingBox { MinX = 0, MaxX = 1.0, MinY = -0.5, MaxY = 0.5, MinZ = -0.06, MaxZ = 0.06 };
            var metadata = new TemplateMetadata
            {
                Reference = new ReferenceConfig { Dimension = "chord", AreaFormula = "rectangular" }
            };
            double refLength = 1.0;

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

        [Fact]
        public void LoadMetadata_ExpandedSchema_ParsesAllSections()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
            {
              "name": "full_template",
              "description": "A fully specified template",
              "solver": "simpleFoam",
              "geometry": {
                "type": "disc",
                "stlName": "disc.stl",
                "requiredStlFiles": ["disc.stl", "tunnel.stl"],
                "surfaceOrient": "outward"
              },
              "reference": {
                "dimension": "diameter",
                "areaFormula": "circular"
              },
              "rotation": {
                "enabled": true,
                "requiresRotorZone": true
              },
              "parameters": {
                "velocity": { "required": true, "default": 27.0, "description": "Freestream velocity (m/s)" },
                "rpm": { "required": false, "default": 925, "description": "Rotational speed" }
              },
              "domain": {
                "upstream": 10.0,
                "downstream": 20.0,
                "radial": 8.0
              },
              "meshPipeline": [
                { "command": "surfaceFeatureExtract", "args": "", "parallel": false, "optional": false },
                { "command": "blockMesh", "args": "", "parallel": false, "optional": false },
                { "command": "snappyHexMesh", "args": "-overwrite", "parallel": true, "optional": false }
              ],
              "solvePipeline": [
                { "command": "potentialFoam", "args": "-writePhi", "parallel": true, "optional": true },
                { "command": "simpleFoam", "args": "", "parallel": true, "optional": false }
              ],
              "results": {
                "type": "forceCoefficients",
                "dataFile": "postProcessing/forceCoeffs/0/coefficient.dat",
                "columns": { "Cd": 1, "Cl": 2, "CmPitch": 3 }
              },
              "report": {
                "template": "report.html",
                "standard": "AIAA",
                "postProcess": ["extract_slice", "render_slice"]
              },
              "validation": {
                "minSize": 0.18,
                "maxSize": 0.35,
                "warningMessage": "Outside range."
              }
            }
            """);

            var metadata = _service.LoadMetadata(dir);

            // Top-level
            metadata.Name.Should().Be("full_template");
            metadata.Description.Should().Be("A fully specified template");
            metadata.Solver.Should().Be("simpleFoam");

            // Geometry
            metadata.Geometry.Type.Should().Be("disc");
            metadata.Geometry.StlName.Should().Be("disc.stl");
            metadata.Geometry.RequiredStlFiles.Should().BeEquivalentTo("disc.stl", "tunnel.stl");
            metadata.Geometry.SurfaceOrient.Should().Be("outward");

            // Reference
            metadata.Reference.Dimension.Should().Be("diameter");
            metadata.Reference.AreaFormula.Should().Be("circular");

            // Rotation
            metadata.Rotation.Enabled.Should().BeTrue();
            metadata.Rotation.RequiresRotorZone.Should().BeTrue();

            // Parameters
            metadata.Parameters.Should().ContainKey("velocity");
            metadata.Parameters["velocity"].Required.Should().BeTrue();
            metadata.Parameters["velocity"].Description.Should().Be("Freestream velocity (m/s)");
            metadata.Parameters.Should().ContainKey("rpm");
            metadata.Parameters["rpm"].Required.Should().BeFalse();

            // Domain
            metadata.Domain.Upstream.Should().Be(10.0);
            metadata.Domain.Downstream.Should().Be(20.0);
            metadata.Domain.Radial.Should().Be(8.0);

            // MeshPipeline
            metadata.MeshPipeline.Should().HaveCount(3);
            metadata.MeshPipeline[0].Command.Should().Be("surfaceFeatureExtract");
            metadata.MeshPipeline[2].Command.Should().Be("snappyHexMesh");
            metadata.MeshPipeline[2].Parallel.Should().BeTrue();
            metadata.MeshPipeline[2].Args.Should().Be("-overwrite");

            // SolvePipeline
            metadata.SolvePipeline.Should().HaveCount(2);
            metadata.SolvePipeline[0].Command.Should().Be("potentialFoam");
            metadata.SolvePipeline[0].Optional.Should().BeTrue();
            metadata.SolvePipeline[1].Command.Should().Be("simpleFoam");

            // Results
            metadata.Results.Type.Should().Be("forceCoefficients");
            metadata.Results.DataFile.Should().Be("postProcessing/forceCoeffs/0/coefficient.dat");
            metadata.Results.Columns.Should().ContainKey("Cd").WhoseValue.Should().Be(1);
            metadata.Results.Columns.Should().ContainKey("Cl").WhoseValue.Should().Be(2);
            metadata.Results.Columns.Should().ContainKey("CmPitch").WhoseValue.Should().Be(3);

            // Report
            metadata.Report.Template.Should().Be("report.html");
            metadata.Report.Standard.Should().Be("AIAA");
            metadata.Report.PostProcess.Should().BeEquivalentTo("extract_slice", "render_slice");

            // Validation
            metadata.Validation.Should().NotBeNull();
            metadata.Validation!.MinSize.Should().Be(0.18);
            metadata.Validation!.MaxSize.Should().Be(0.35);

            // Backward-compat accessors
            metadata.GeometryType.Should().Be("disc");
            metadata.GeometryStlName.Should().Be("disc.stl");
            metadata.ReferenceDimension.Should().Be("diameter");
            metadata.ReferenceAreaFormula.Should().Be("circular");
            metadata.RequiresRotorZone.Should().BeTrue();
            metadata.RequiredStlFiles.Should().BeEquivalentTo("disc.stl", "tunnel.stl");
        }

        [Fact]
        public void LoadMetadata_NullSurfaceOrient_ParsesAsNull()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "TEMPLATE.json"), """
            {
              "name": "no_orient",
              "geometry": {
                "type": "airfoil",
                "stlName": "airfoil.stl",
                "requiredStlFiles": ["airfoil.stl"]
              },
              "reference": { "dimension": "chord", "areaFormula": "rectangular" },
              "rotation": { "enabled": false, "requiresRotorZone": false }
            }
            """);

            var metadata = _service.LoadMetadata(dir);

            metadata.Geometry.SurfaceOrient.Should().BeNull();
        }
    }
}
