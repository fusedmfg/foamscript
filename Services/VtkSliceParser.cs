using System.Globalization;

namespace foamscript.Services;

/// <summary>
/// Parses surface sampling output from OpenFOAM postProcess.
/// Supports both VTK legacy text format and OpenFOAM raw format.
/// Raw format is preferred (simpler, always text).
/// </summary>
public static class VtkSliceParser
{
    public record SliceData
    {
        public List<(double X, double Z)> Points { get; init; } = new();
        public Dictionary<string, double[]> ScalarFields { get; init; } = new();
    }

    /// <summary>
    /// Parses multiple OpenFOAM raw surface files into a single SliceData.
    /// Each .raw file has format: "# fieldName POINT_DATA N" header,
    /// then "x y z val1 [val2 val3]" per line.
    /// Scalar fields have 1 value column, vector fields have 3 (magnitude computed).
    /// </summary>
    public static SliceData ParseRawFiles(Dictionary<string, string> fieldFiles)
    {
        var allPoints = new List<(double X, double Z)>();
        var fields = new Dictionary<string, double[]>();
        bool pointsCaptured = false;

        foreach (var (fieldName, content) in fieldFiles)
        {
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var points = new List<(double X, double Z)>();
            var values = new List<double>();
            int numComponents = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#'))
                {
                    // Detect vector vs scalar from header: "# x y z  U_x U_y U_z" vs "# x y z  p"
                    var headerParts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    // Count value columns after x,y,z (skip '#', 'x', 'y', 'z')
                    var valueColCount = headerParts.Length - 4; // subtract #, x, y, z
                    if (valueColCount > 0)
                        numComponents = valueColCount;
                    continue;
                }

                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    points.Add((x, z));
                }

                if (numComponents >= 3 && parts.Length >= 6)
                {
                    // Vector field — compute magnitude from components at indices 3,4,5
                    if (double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var vx) &&
                        double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var vy) &&
                        double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var vz))
                    {
                        values.Add(Math.Sqrt(vx * vx + vy * vy + vz * vz));
                    }
                }
                else if (parts.Length >= 4)
                {
                    // Scalar field — single value in column 4 (0-indexed: 3)
                    if (double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    {
                        values.Add(val);
                    }
                }
            }

            // Use points from first file (all files share same mesh points)
            if (!pointsCaptured && points.Count > 0)
            {
                allPoints = points;
                pointsCaptured = true;
            }

            if (values.Count > 0)
            {
                fields[fieldName] = values.ToArray();
            }
        }

        return new SliceData { Points = allPoints, ScalarFields = fields };
    }

    /// <summary>
    /// Parses VTK legacy text format (kept for backward compatibility / testing).
    /// </summary>
    public static SliceData Parse(string vtkContent)
    {
        if (string.IsNullOrWhiteSpace(vtkContent))
            return new SliceData();

        var lines = vtkContent.Split('\n', StringSplitOptions.None);
        var points = new List<(double X, double Z)>();
        var fields = new Dictionary<string, double[]>();
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("POINTS"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var nPoints = int.Parse(parts[1]);
                i++;
                points = ParsePoints(lines, ref i, nPoints);
                continue;
            }

            if (line.StartsWith("FIELD"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var nArrays = int.Parse(parts[2]);
                i++;

                for (int a = 0; a < nArrays && i < lines.Length; a++)
                {
                    var header = lines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var name = header[0];
                    var numComponents = int.Parse(header[1]);
                    var numTuples = int.Parse(header[2]);
                    i++;

                    var values = ParseFloats(lines, ref i, numTuples * numComponents);

                    if (numComponents == 1)
                    {
                        fields[name] = values;
                    }
                    else if (numComponents == 3)
                    {
                        var magnitudes = new double[numTuples];
                        for (int t = 0; t < numTuples; t++)
                        {
                            var vx = values[t * 3];
                            var vy = values[t * 3 + 1];
                            var vz = values[t * 3 + 2];
                            magnitudes[t] = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                        }
                        fields[name] = magnitudes;
                    }
                }
                continue;
            }

            i++;
        }

        return new SliceData { Points = points, ScalarFields = fields };
    }

    private static List<(double X, double Z)> ParsePoints(string[] lines, ref int i, int nPoints)
    {
        var points = new List<(double, double)>(nPoints);
        var values = ParseFloats(lines, ref i, nPoints * 3);
        for (int p = 0; p < nPoints; p++)
        {
            var x = values[p * 3];
            var z = values[p * 3 + 2];
            points.Add((x, z));
        }
        return points;
    }

    private static double[] ParseFloats(string[] lines, ref int i, int count)
    {
        var result = new List<double>(count);
        while (result.Count < count && i < lines.Length)
        {
            var parts = lines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (double.TryParse(part, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var val))
                {
                    result.Add(val);
                }
            }
            i++;
        }
        return result.ToArray();
    }
}
