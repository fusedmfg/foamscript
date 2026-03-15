namespace foamscript.Services;

/// <summary>
/// Parses VTK legacy text format files produced by OpenFOAM surface sampling.
/// Extracts point coordinates (x, z from y=0 slice) and scalar/vector field data.
/// Vector fields are converted to magnitude.
/// </summary>
public static class VtkSliceParser
{
    public record SliceData
    {
        public List<(double X, double Z)> Points { get; init; } = new();
        public Dictionary<string, double[]> ScalarFields { get; init; } = new();
    }

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
                if (double.TryParse(part, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    result.Add(val);
                }
            }
            i++;
        }
        return result.ToArray();
    }
}
