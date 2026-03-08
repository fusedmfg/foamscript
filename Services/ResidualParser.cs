using System.Text.RegularExpressions;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Parses OpenFOAM solver log files to extract per-iteration residual data
    /// for convergence analysis and plotting.
    /// </summary>
    public class ResidualParser
    {
        // Matches: "smoothSolver:  Solving for Ux, Initial residual = 0.0234, Final residual = 0.00123, No Iterations 3"
        // Also matches: "GAMG:  Solving for p, Initial residual = 0.156, Final residual = 0.00456, No Iterations 8"
        private static readonly Regex ResidualRegex = new(
            @"Solving for (\w+), Initial residual = ([\d.eE+-]+), Final residual = ([\d.eE+-]+)",
            RegexOptions.Compiled);

        // Matches: "Time = 42" (integer iteration for steady-state)
        private static readonly Regex TimeStepRegex = new(
            @"^Time = (\d+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Parses a solver log file and returns residual entries grouped by iteration.
        /// </summary>
        public static List<ResidualEntry> ParseLogFile(string logFilePath)
        {
            if (!File.Exists(logFilePath))
                return new List<ResidualEntry>();

            var content = File.ReadAllText(logFilePath);
            return ParseLogContent(content);
        }

        /// <summary>
        /// Parses solver log content string and returns residual entries.
        /// </summary>
        internal static List<ResidualEntry> ParseLogContent(string content)
        {
            var entries = new List<ResidualEntry>();
            var lines = content.Split('\n');
            int currentIteration = 0;

            foreach (var line in lines)
            {
                // Check for time step marker
                var timeMatch = TimeStepRegex.Match(line);
                if (timeMatch.Success && int.TryParse(timeMatch.Groups[1].Value, out var iteration))
                {
                    currentIteration = iteration;
                    continue;
                }

                // Check for residual line
                var residualMatch = ResidualRegex.Match(line);
                if (residualMatch.Success)
                {
                    var field = residualMatch.Groups[1].Value;
                    if (double.TryParse(residualMatch.Groups[2].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var initialResidual) &&
                        double.TryParse(residualMatch.Groups[3].Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var finalResidual))
                    {
                        entries.Add(new ResidualEntry
                        {
                            Iteration = currentIteration,
                            Field = field,
                            InitialResidual = initialResidual,
                            FinalResidual = finalResidual
                        });
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Finds the solver log file in a case directory.
        /// Searches for log.simpleFoam, log.pimpleFoam, etc.
        /// </summary>
        public static string? FindLogFile(string caseDir)
        {
            var logFiles = new[] { "log.simpleFoam", "log.pimpleFoam", "log.pisoFoam", "log.potentialFoam" };

            foreach (var logFile in logFiles)
            {
                var path = Path.Combine(caseDir, logFile);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
