using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Generates flow field visualizations using OpenFOAM postProcess + C# rendering:
    /// 1. Writes surfaceSampling function object dict to system/
    /// 2. Runs postProcess to generate raw surface data
    /// 3. Parses raw output with VtkSliceParser.ParseRawFiles
    /// 4. Renders heatmaps with VtkSliceRenderer (ScottPlot)
    /// Gracefully degrades if postProcess fails.
    /// </summary>
    public class VisualizationService
    {
        private readonly IProcessExecutor _processExecutor;

        private const string SurfaceSamplingDict = """
            FoamFile
            {
                version     2.0;
                format      ascii;
                class       dictionary;
                object      surfaceSampling;
            }

            surfaceSampling
            {
                type            surfaces;
                libs            (sampling);
                writeControl    writeTime;
                surfaceFormat   raw;
                fields          (p U);
                surfaces
                {
                    yNormal
                    {
                        type        cuttingPlane;
                        point       (0 0 0);
                        normal      (0 1 0);
                        interpolate true;
                    }
                }
            }
            """;

        public VisualizationService(IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        /// <summary>
        /// Generates pressure and velocity slice visualizations for a case.
        /// Returns rendered PNGs, or null if postProcess or rendering fails.
        /// </summary>
        public virtual SliceVisualizationResult? GenerateSliceVisualization(string caseDir, string outputDir)
        {
            var systemDir = Path.Combine(caseDir, "system");
            if (!Directory.Exists(systemDir))
            {
                Console.Error.WriteLine("  Warning: Case system/ directory not found — skipping flow visualization");
                return null;
            }

            var dictPath = Path.Combine(systemDir, "surfaceSampling");

            try
            {
                // Step 1: Write surfaceSampling function object dict
                File.WriteAllText(dictPath, SurfaceSamplingDict);

                // Step 2: Run OpenFOAM postProcess
                Console.WriteLine("  Running postProcess surface sampling...");
                var postResult = _processExecutor.Execute(
                    "postProcess", $"-case {caseDir} -func surfaceSampling -latestTime");

                if (postResult.ExitCode != 0)
                {
                    Console.Error.WriteLine($"  Warning: postProcess failed (exit code {postResult.ExitCode}) — skipping flow visualization");
                    return null;
                }

                // Step 3: Find VTK output in postProcessing/surfaceSampling/<latestTime>/
                var postProcDir = Path.Combine(caseDir, "postProcessing", "surfaceSampling");
                if (!Directory.Exists(postProcDir))
                {
                    Console.Error.WriteLine("  Warning: postProcessing output not found — skipping flow visualization");
                    return null;
                }

                var latestTimeDir = Directory.GetDirectories(postProcDir)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => double.TryParse(d.Name, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    .OrderByDescending(d => double.Parse(d.Name, System.Globalization.CultureInfo.InvariantCulture))
                    .FirstOrDefault();

                if (latestTimeDir == null)
                {
                    Console.Error.WriteLine("  Warning: No time directories in postProcessing output — skipping flow visualization");
                    return null;
                }

                var rawFiles = Directory.GetFiles(latestTimeDir.FullName, "*.raw");
                if (rawFiles.Length == 0)
                {
                    Console.Error.WriteLine("  Warning: No raw surface files found in postProcessing output — skipping flow visualization");
                    return null;
                }

                // Step 4: Parse raw files and render
                // Each field produces a separate .raw file (e.g., p_yNormal.raw, U_yNormal.raw)
                Console.WriteLine("  Parsing surface slice data...");
                var fieldFiles = new Dictionary<string, string>();
                foreach (var rawFile in rawFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(rawFile);
                    // Extract field name: "p_yNormal" -> "p", "U_yNormal" -> "U"
                    var fieldName = fileName.Split('_')[0];
                    fieldFiles[fieldName] = File.ReadAllText(rawFile);
                }
                var sliceData = VtkSliceParser.ParseRawFiles(fieldFiles);

                if (sliceData.Points.Count == 0)
                {
                    Console.Error.WriteLine("  Warning: Surface slice contains no points — skipping flow visualization");
                    return null;
                }

                // Step 5: Read geometry bounding box from STL for AIAA-standard view framing
                var geometryBounds = FindGeometryBounds(caseDir);

                // Step 6: Render with ScottPlot
                Console.WriteLine("  Rendering flow visualizations...");
                var result = VtkSliceRenderer.Render(sliceData, geometryBounds);

                // Write PNGs to output directory
                Directory.CreateDirectory(outputDir);
                if (result.PressureSlicePng != null)
                {
                    result.PressureSlicePath = Path.Combine(outputDir, "p_slice.png");
                    File.WriteAllBytes(result.PressureSlicePath, result.PressureSlicePng);
                }
                if (result.VelocitySlicePng != null)
                {
                    result.VelocitySlicePath = Path.Combine(outputDir, "umag_slice.png");
                    File.WriteAllBytes(result.VelocitySlicePath, result.VelocitySlicePng);
                }

                return result;
            }
            finally
            {
                // Clean up surfaceSampling dict
                try { File.Delete(dictPath); } catch { /* Non-critical */ }
            }
        }
        /// <summary>
        /// Finds the geometry STL in constant/triSurface/ and computes its bounding box.
        /// Returns null if no geometry STL found (falls back to data-extent framing).
        /// Skips tunnel.stl — looks for the actual geometry (disc.stl, airfoil.stl, etc.).
        /// </summary>
        private static BoundingBox? FindGeometryBounds(string caseDir)
        {
            var triSurfDir = Path.Combine(caseDir, "constant", "triSurface");
            if (!Directory.Exists(triSurfDir))
                return null;

            // Find geometry STL (skip tunnel.stl which is the wind tunnel domain)
            var stlFiles = Directory.GetFiles(triSurfDir, "*.stl")
                .Where(f => !Path.GetFileName(f).Equals("tunnel.stl", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (stlFiles.Length == 0)
                return null;

            var stlFile = stlFiles[0];
            try
            {
                var lines = File.ReadAllLines(stlFile);
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                double minZ = double.MaxValue, maxZ = double.MinValue;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("vertex"))
                    {
                        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4 &&
                            double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                            double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                            double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var z))
                        {
                            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                            minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
                        }
                    }
                }

                if (minX == double.MaxValue)
                    return null;

                return new BoundingBox
                {
                    MinX = minX, MaxX = maxX,
                    MinY = minY, MaxY = maxY,
                    MinZ = minZ, MaxZ = maxZ
                };
            }
            catch
            {
                return null; // Non-critical — fall back to data-extent framing
            }
        }
    }

    /// <summary>
    /// Result of slice visualization generation.
    /// </summary>
    public class SliceVisualizationResult
    {
        public string? PressureSlicePath { get; set; }
        public string? VelocitySlicePath { get; set; }
        public byte[]? PressureSlicePng { get; set; }
        public byte[]? VelocitySlicePng { get; set; }
    }
}
