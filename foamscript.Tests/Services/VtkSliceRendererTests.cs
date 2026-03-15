using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services;

public class VtkSliceRendererTests
{
    [Fact]
    public void RenderSlice_ProducesPngBytes()
    {
        var sliceData = new VtkSliceParser.SliceData
        {
            Points = new List<(double X, double Z)>
            {
                (0, 0), (1, 0), (0, 1), (1, 1), (0.5, 0.5)
            },
            ScalarFields = new Dictionary<string, double[]>
            {
                ["p"] = new[] { 100.0, 200.0, 150.0, 180.0, 160.0 },
                ["U"] = new[] { 10.0, 20.0, 15.0, 18.0, 16.0 }
            }
        };

        var result = VtkSliceRenderer.Render(sliceData);

        result.PressureSlicePng.Should().NotBeNull();
        result.PressureSlicePng!.Length.Should().BeGreaterThan(100);
        result.VelocitySlicePng.Should().NotBeNull();
        result.VelocitySlicePng!.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void RenderSlice_WithGeometryBounds_UsesAiaaFraming()
    {
        var sliceData = new VtkSliceParser.SliceData
        {
            Points = new List<(double X, double Z)>
            {
                (-1, -1), (2, -1), (-1, 1), (2, 1), (0.5, 0)
            },
            ScalarFields = new Dictionary<string, double[]>
            {
                ["p"] = new[] { 100.0, 200.0, 150.0, 180.0, 160.0 }
            }
        };

        var bounds = new Models.BoundingBox
        {
            MinX = -0.1, MaxX = 0.1, MinZ = -0.05, MaxZ = 0.05
        };

        var result = VtkSliceRenderer.Render(sliceData, geometryBounds: bounds);
        result.PressureSlicePng.Should().NotBeNull();
    }

    [Fact]
    public void RenderSlice_EmptyData_ReturnsNull()
    {
        var sliceData = new VtkSliceParser.SliceData();
        var result = VtkSliceRenderer.Render(sliceData);

        result.PressureSlicePng.Should().BeNull();
        result.VelocitySlicePng.Should().BeNull();
    }
}
