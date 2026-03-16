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

                // Step 5: Render with ScottPlot (geometry bounds = null; renderer has data-driven fallback)
                Console.WriteLine("  Rendering flow visualizations...");
                var result = VtkSliceRenderer.Render(sliceData, geometryBounds: null);

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
