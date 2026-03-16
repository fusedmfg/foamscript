using foamscript.Models;
using ScottPlot;

namespace foamscript.Services;

/// <summary>
/// Renders flow field slice data as heatmap images using ScottPlot.
/// Replaces the Python matplotlib rendering pipeline.
/// </summary>
public static class VtkSliceRenderer
{
    private const int ImageWidth = 2400;
    private const int ImageHeight = 1200;
    private const int GridNx = 500;
    private const int GridNz = 350;

    public static SliceVisualizationResult Render(
        VtkSliceParser.SliceData sliceData,
        BoundingBox? geometryBounds = null)
    {
        var result = new SliceVisualizationResult();

        if (sliceData.Points.Count == 0)
            return result;

        var (xMin, xMax, zMin, zMax) = CalculateViewBounds(sliceData.Points, geometryBounds);

        if (sliceData.ScalarFields.TryGetValue("p", out var pValues))
        {
            result.PressureSlicePng = RenderField(
                sliceData.Points, pValues, xMin, xMax, zMin, zMax,
                "Static Pressure \u2014 y=0 Slice", "Pa",
                new ScottPlot.Colormaps.MellowRainbow());
        }

        if (sliceData.ScalarFields.TryGetValue("U", out var uValues))
        {
            result.VelocitySlicePng = RenderField(
                sliceData.Points, uValues, xMin, xMax, zMin, zMax,
                "Velocity Magnitude \u2014 y=0 Slice", "m/s",
                new ScottPlot.Colormaps.Viridis());
        }

        return result;
    }

    private static byte[] RenderField(
        List<(double X, double Z)> points, double[] values,
        double xMin, double xMax, double zMin, double zMax,
        string title, string unit, IColormap colormap)
    {
        // Clamp to 1st/99th percentile to reduce outlier influence
        var sorted = values.OrderBy(v => v).ToArray();
        var lo = sorted[Math.Max(0, (int)(sorted.Length * 0.01))];
        var hi = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.99))];
        var clamped = values.Select(v => Math.Clamp(v, lo, hi)).ToArray();

        var grid = GridInterpolator.Interpolate(points, clamped,
            xMin, xMax, zMin, zMax, GridNx, GridNz);

        var plot = new Plot();
        var hm = plot.Add.Heatmap(grid);
        hm.Colormap = colormap;
        hm.Position = new CoordinateRect(xMin, xMax, zMin, zMax);

        // Add colorbar with unit label so the reader knows what colors mean
        var cb = plot.Add.ColorBar(hm);
        cb.Label = unit;

        plot.Title(title);
        plot.XLabel("x (m)");
        plot.YLabel("z (m)");
        plot.Axes.SetLimits(xMin, xMax, zMin, zMax);

        return plot.GetImageBytes(ImageWidth, ImageHeight, ImageFormat.Png);
    }

    internal static (double xMin, double xMax, double zMin, double zMax)
        CalculateViewBounds(List<(double X, double Z)> points, BoundingBox? geoBounds)
    {
        if (geoBounds != null)
        {
            // AIAA-standard geometry-referenced framing:
            // Asymmetric padding: 1.5x upstream, 3x downstream, 2x vertical
            var charLen = Math.Max(
                geoBounds.MaxX - geoBounds.MinX,
                geoBounds.MaxZ - geoBounds.MinZ);
            var cx = (geoBounds.MinX + geoBounds.MaxX) / 2.0;
            var cz = (geoBounds.MinZ + geoBounds.MaxZ) / 2.0;
            return (
                cx - 1.5 * charLen,
                cx + 3.0 * charLen,
                cz - 2.0 * charLen,
                cz + 2.0 * charLen);
        }

        // Fallback: 10% padding around data extent
        var xs = points.Select(p => p.X).ToArray();
        var zs = points.Select(p => p.Z).ToArray();
        var padX = (xs.Max() - xs.Min()) * 0.1;
        var padZ = (zs.Max() - zs.Min()) * 0.1;
        return (xs.Min() - padX, xs.Max() + padX, zs.Min() - padZ, zs.Max() + padZ);
    }
}
