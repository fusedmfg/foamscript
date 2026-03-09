using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Generates flow field visualizations using a two-phase approach:
    /// Phase 1: pvpython extracts slice data from the OpenFOAM case (no rendering).
    /// Phase 2: python3 + matplotlib renders contour plots (headless via Agg backend).
    /// Gracefully degrades if pvpython or python3 is not available.
    /// </summary>
    public class VisualizationService
    {
        private readonly IProcessExecutor _processExecutor;

        public VisualizationService(IProcessExecutor processExecutor)
        {
            _processExecutor = processExecutor;
        }

        /// <summary>
        /// Generates pressure and velocity slice visualizations for a case.
        /// Returns paths to generated PNGs, or null if visualization tools are unavailable.
        /// </summary>
        public virtual SliceVisualizationResult? GenerateSliceVisualization(string caseDir, string outputDir)
        {
            // Locate the Python scripts (bundled alongside the executable)
            var scriptDir = FindScriptDirectory();
            if (scriptDir == null)
            {
                Console.Error.WriteLine("  Warning: Visualization scripts not found — skipping flow visualization");
                return null;
            }

            var extractScript = Path.Combine(scriptDir, "extract_slice.py");
            var renderScript = Path.Combine(scriptDir, "render_slice.py");

            if (!File.Exists(extractScript) || !File.Exists(renderScript))
            {
                Console.Error.WriteLine("  Warning: Visualization scripts not found — skipping flow visualization");
                return null;
            }

            // Check if pvpython is available
            var pvCheck = _processExecutor.Execute("which", "pvpython");
            if (pvCheck.ExitCode != 0)
            {
                Console.Error.WriteLine("  Warning: pvpython not found — skipping flow visualization");
                Console.Error.WriteLine("  Install ParaView for flow field visualizations");
                return null;
            }

            // Check if python3 + matplotlib is available
            var pyCheck = _processExecutor.Execute("python3", "-c \"import matplotlib\"");
            if (pyCheck.ExitCode != 0)
            {
                Console.Error.WriteLine("  Warning: python3 with matplotlib not found — skipping flow visualization");
                return null;
            }

            Directory.CreateDirectory(outputDir);
            var jsonPath = Path.Combine(outputDir, "slice_data.json");

            // Phase 1: Extract slice data via pvpython
            Console.WriteLine("  Extracting flow field slice data...");
            var extractResult = _processExecutor.Execute(
                "pvpython", $"--force-offscreen-rendering {extractScript} {caseDir} {jsonPath}");

            if (extractResult.ExitCode != 0 || !File.Exists(jsonPath))
            {
                Console.Error.WriteLine($"  Warning: Slice data extraction failed (exit code {extractResult.ExitCode})");
                if (!string.IsNullOrEmpty(extractResult.Error))
                    Console.Error.WriteLine($"  {extractResult.Error.Split('\n').LastOrDefault(l => !l.Contains("Authorization"))?.Trim()}");
                return null;
            }

            // Phase 2: Render contour plots via python3 + matplotlib
            Console.WriteLine("  Rendering flow visualizations...");
            var renderResult = _processExecutor.Execute(
                "python3", $"{renderScript} {jsonPath} {outputDir}");

            if (renderResult.ExitCode != 0)
            {
                Console.Error.WriteLine($"  Warning: Visualization rendering failed (exit code {renderResult.ExitCode})");
                return null;
            }

            var pressurePath = Path.Combine(outputDir, "p_slice.png");
            var velocityPath = Path.Combine(outputDir, "umag_slice.png");

            var result = new SliceVisualizationResult();
            if (File.Exists(pressurePath))
            {
                result.PressureSlicePath = pressurePath;
                result.PressureSlicePng = File.ReadAllBytes(pressurePath);
            }
            if (File.Exists(velocityPath))
            {
                result.VelocitySlicePath = velocityPath;
                result.VelocitySlicePng = File.ReadAllBytes(velocityPath);
            }

            // Clean up intermediate JSON
            try { File.Delete(jsonPath); } catch { /* Non-critical */ }

            return result;
        }

        /// <summary>
        /// Locates the directory containing visualization Python scripts.
        /// Searches relative to the executable, then common development paths.
        /// </summary>
        private static string? FindScriptDirectory()
        {
            var candidates = new[]
            {
                // Relative to executable (production)
                Path.Combine(AppContext.BaseDirectory, "Templates", "report"),
                // Development paths
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Templates", "report"),
                // Absolute fallback
                Path.Combine(Directory.GetCurrentDirectory(), "Templates", "report"),
            };

            foreach (var candidate in candidates)
            {
                var normalized = Path.GetFullPath(candidate);
                if (Directory.Exists(normalized) &&
                    File.Exists(Path.Combine(normalized, "extract_slice.py")))
                {
                    return normalized;
                }
            }

            return null;
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
