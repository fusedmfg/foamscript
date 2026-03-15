namespace foamscript.Services;

/// <summary>
/// Interpolates scattered (x, z, value) data onto a regular grid
/// using inverse distance weighting (IDW).
/// </summary>
public static class GridInterpolator
{
    /// <summary>
    /// Interpolates irregular point data to a regular NxM grid.
    /// Returns double[nz, nx] (rows = Z, cols = X) for ScottPlot Heatmap.
    /// </summary>
    public static double[,] Interpolate(
        List<(double X, double Z)> points,
        double[] values,
        double xMin, double xMax, double zMin, double zMax,
        int nx, int nz, double power = 2.0, int kNearest = 16)
    {
        var grid = new double[nz, nx];
        var dx = (xMax - xMin) / Math.Max(nx - 1, 1);
        var dz = (zMax - zMin) / Math.Max(nz - 1, 1);

        for (int row = 0; row < nz; row++)
        {
            var gz = zMax - row * dz; // Top row = zMax (image convention)
            for (int col = 0; col < nx; col++)
            {
                var gx = xMin + col * dx;
                grid[row, col] = IdwInterpolate(points, values, gx, gz, power, kNearest);
            }
        }

        return grid;
    }

    private static double IdwInterpolate(
        List<(double X, double Z)> points, double[] values,
        double gx, double gz, double power, int kNearest)
    {
        var distances = new (double Dist, int Index)[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            var ddx = points[i].X - gx;
            var ddz = points[i].Z - gz;
            distances[i] = (ddx * ddx + ddz * ddz, i);
        }

        Array.Sort(distances, (a, b) => a.Dist.CompareTo(b.Dist));
        var k = Math.Min(kNearest, points.Count);

        if (distances[0].Dist < 1e-20)
            return values[distances[0].Index];

        double weightSum = 0;
        double valueSum = 0;

        for (int i = 0; i < k; i++)
        {
            var dist = Math.Sqrt(distances[i].Dist);
            var w = 1.0 / Math.Pow(dist, power);
            weightSum += w;
            valueSum += w * values[distances[i].Index];
        }

        return valueSum / weightSum;
    }
}
