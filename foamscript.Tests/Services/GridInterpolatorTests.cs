using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services;

public class GridInterpolatorTests
{
    [Fact]
    public void Interpolate_UniformField_ProducesUniformGrid()
    {
        var points = new List<(double X, double Z)>
        {
            (0, 0), (1, 0), (0, 1), (1, 1)
        };
        var values = new double[] { 42.0, 42.0, 42.0, 42.0 };

        var grid = GridInterpolator.Interpolate(points, values,
            xMin: 0, xMax: 1, zMin: 0, zMax: 1, nx: 5, nz: 5);

        grid.GetLength(0).Should().Be(5);
        grid.GetLength(1).Should().Be(5);
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 5; col++)
                grid[row, col].Should().BeApproximately(42.0, 0.01);
    }

    [Fact]
    public void Interpolate_LinearGradient_InterpolatesSmoothly()
    {
        var points = new List<(double X, double Z)>
        {
            (0, 0), (10, 0), (0, 10), (10, 10),
            (5, 5)
        };
        var values = new double[] { 0, 10, 0, 10, 5 };

        var grid = GridInterpolator.Interpolate(points, values,
            xMin: 0, xMax: 10, zMin: 0, zMax: 10, nx: 11, nz: 11);

        grid[5, 5].Should().BeApproximately(5.0, 0.5);
        grid[5, 0].Should().BeLessThan(grid[5, 10]);
    }

    [Fact]
    public void Interpolate_RespectsGridDimensions()
    {
        var points = new List<(double X, double Z)> { (0, 0) };
        var values = new double[] { 1.0 };

        var grid = GridInterpolator.Interpolate(points, values,
            xMin: 0, xMax: 1, zMin: 0, zMax: 1, nx: 100, nz: 50);

        grid.GetLength(0).Should().Be(50);  // rows = nz
        grid.GetLength(1).Should().Be(100); // cols = nx
    }
}
