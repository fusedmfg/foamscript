using Xunit;
using FluentAssertions;
using foamscript.Services;
using foamscript.Models;

namespace foamscript.Tests.Services
{
    public class GeometryScaleWarningTests : IDisposable
    {
        private readonly string _tempDir;

        public GeometryScaleWarningTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"foamscript_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateStlWithExtent(double size)
        {
            var half = size / 2.0;
            var stlPath = Path.Combine(_tempDir, "test.stl");
            var content = $@"solid test
  facet normal 0 0 1
    outer loop
      vertex {-half} {-half} {-half}
      vertex {half} {-half} {-half}
      vertex {half} {half} {half}
    endloop
  endfacet
endsolid test";
            File.WriteAllText(stlPath, content);
            return stlPath;
        }

        [Fact]
        public void CheckGeometryScale_WithinRange_ReturnsNull()
        {
            var stl = CreateStlWithExtent(0.25);
            var validation = new GeometryValidation { MinSize = 0.18, MaxSize = 0.35 };

            var result = GeometryService.CheckGeometryScale(stl, validation);

            result.Should().BeNull();
        }

        [Fact]
        public void CheckGeometryScale_NullValidation_ReturnsNull()
        {
            var stl = CreateStlWithExtent(0.25);

            var result = GeometryService.CheckGeometryScale(stl, null);

            result.Should().BeNull();
        }

        [Fact]
        public void CheckGeometryScale_NoMinMaxSet_ReturnsNull()
        {
            var stl = CreateStlWithExtent(0.25);
            var validation = new GeometryValidation { WarningMessage = "some message" };

            var result = GeometryService.CheckGeometryScale(stl, validation);

            result.Should().BeNull();
        }

        [Fact]
        public void CheckGeometryScale_MillimeterScale_ReturnsWarningWithMmHint()
        {
            // 250mm disc when template expects 0.18-0.35m
            var stl = CreateStlWithExtent(250.0);
            var validation = new GeometryValidation
            {
                MinSize = 0.18,
                MaxSize = 0.35,
                WarningMessage = "Check your units"
            };

            var result = GeometryService.CheckGeometryScale(stl, validation);

            result.Should().NotBeNull();
            result!.LikelySourceUnits.Should().Be("mm");
            result.TemplateWarningMessage.Should().Be("Check your units");
        }

        [Fact]
        public void CheckGeometryScale_InchScale_ReturnsWarningWithInchHint()
        {
            // ~10 inches when template expects 0.18-0.35m
            var stl = CreateStlWithExtent(10.0);
            var validation = new GeometryValidation { MinSize = 0.18, MaxSize = 0.35 };

            var result = GeometryService.CheckGeometryScale(stl, validation);

            result.Should().NotBeNull();
            result!.LikelySourceUnits.Should().Be("in");
        }

        [Fact]
        public void CheckGeometryScale_CentimeterScale_ReturnsWarningWithCmHint()
        {
            // 25cm when template expects 0.18-0.35m
            var stl = CreateStlWithExtent(25.0);
            var validation = new GeometryValidation { MinSize = 0.18, MaxSize = 0.35 };

            var result = GeometryService.CheckGeometryScale(stl, validation);

            result.Should().NotBeNull();
            result!.LikelySourceUnits.Should().Be("cm");
        }

        [Fact]
        public void CheckGeometryScale_TooSmall_ReturnsWarningWithNullUnits()
        {
            // Way too small, no obvious unit match
            var stl = CreateStlWithExtent(0.001);
            var validation = new GeometryValidation { MinSize = 0.18, MaxSize = 0.35 };

            var result = GeometryService.CheckGeometryScale(stl, validation);

            result.Should().NotBeNull();
            result!.LikelySourceUnits.Should().BeNull();
        }

        [Fact]
        public void CheckGeometryScale_NonexistentFile_ReturnsNull()
        {
            var result = GeometryService.CheckGeometryScale(
                Path.Combine(_tempDir, "nonexistent.stl"),
                new GeometryValidation { MinSize = 0.18, MaxSize = 0.35 });

            result.Should().BeNull();
        }

        [Fact]
        public void FormatScaleWarning_WithAllFields_FormatsCorrectly()
        {
            var warning = new GeometryScaleWarning
            {
                MaxExtent = 210.0,
                MinSize = 0.18,
                MaxSize = 0.35,
                LikelySourceUnits = "mm",
                TemplateWarningMessage = "Disc diameter outside expected range."
            };

            var formatted = GeometryService.FormatScaleWarning(warning);

            formatted.Should().Contain("210");
            formatted.Should().Contain("0.18");
            formatted.Should().Contain("0.35");
            formatted.Should().Contain("Disc diameter outside expected range.");
            formatted.Should().Contain("mm");
            formatted.Should().Contain("foamscript convert");
        }

        [Fact]
        public void FormatScaleWarning_WithoutLikelyUnits_OmitsConvertSuggestion()
        {
            var warning = new GeometryScaleWarning
            {
                MaxExtent = 0.001,
                MinSize = 0.18,
                MaxSize = 0.35
            };

            var formatted = GeometryService.FormatScaleWarning(warning);

            formatted.Should().Contain("0.001");
            formatted.Should().NotContain("foamscript convert");
        }
    }
}
