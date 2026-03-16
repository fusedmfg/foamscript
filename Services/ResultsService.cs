using System.Text;
using System.Text.Json;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Service for extracting and formatting force coefficient results from completed OpenFOAM studies.
    /// </summary>
    public class ResultsService
    {
        /// <summary>
        /// Extracts force coefficient results from all cases in a study directory.
        /// </summary>
        public ResultsSummary ExtractResults(string studyDir, double averageWindow = 0.1)
        {
            var summary = new ResultsSummary { StudyDir = studyDir };

            try
            {
                if (!Directory.Exists(studyDir))
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = $"Study directory not found: {studyDir}";
                    return summary;
                }

                var caseDirs = CaseDiscovery.DiscoverCases(studyDir);

                if (caseDirs.Count == 0)
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = "No valid OpenFOAM cases found in study directory";
                    return summary;
                }

                foreach (var caseDir in caseDirs)
                {
                    var caseName = Path.GetFileName(caseDir);
                    var angle = CaseDiscovery.ParseAngleFromCaseName(caseName);

                    var caseResult = new CaseResult
                    {
                        CaseName = caseName,
                        AngleOfAttack = angle ?? 0.0
                    };

                    var coeffs = SolverService.ParseForceCoeffs(caseDir);
                    if (coeffs != null)
                    {
                        caseResult.Cd = coeffs.Value.Cd;
                        caseResult.Cl = coeffs.Value.Cl;
                        caseResult.CmPitch = coeffs.Value.CmPitch;
                        caseResult.Converged = true;
                    }
                    else
                    {
                        caseResult.Converged = false;
                        caseResult.ErrorMessage = "No force coefficient data found (missing postProcessing/forces/0/coefficient.dat)";
                    }

                    summary.Cases.Add(caseResult);
                }

                // Sort by angle of attack
                summary.Cases.Sort((a, b) => a.AngleOfAttack.CompareTo(b.AngleOfAttack));

                summary.IsSuccess = true;
                return summary;
            }
            catch (Exception ex)
            {
                summary.IsSuccess = false;
                summary.ErrorMessage = $"Results extraction failed: {ex.Message}";
                return summary;
            }
        }

        /// <summary>
        /// Formats results for display.
        /// </summary>
        public static string FormatResults(ResultsSummary summary, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "csv" => FormatCsv(summary),
                "json" => FormatJson(summary),
                _ => FormatTable(summary)
            };
        }

        internal static string FormatTable(ResultsSummary summary)
        {
            var sb = new StringBuilder();
            var studyName = Path.GetFileName(summary.StudyDir);

            // Discover coefficient column names from the first case's Coefficients dictionary
            var coeffKeys = GetCoefficientKeys(summary);

            sb.AppendLine($"=== Study Results: {studyName} ===");
            sb.AppendLine();

            // Header: AoA + dynamic coefficient columns + Status
            sb.Append($"{"AoA (°)",-10}");
            foreach (var key in coeffKeys)
                sb.Append($" {key,10}");
            sb.AppendLine($" {"Status",10}");

            sb.Append($"{new string('-', 10)}");
            foreach (var _ in coeffKeys)
                sb.Append($" {new string('-', 10)}");
            sb.AppendLine($" {new string('-', 10)}");

            foreach (var c in summary.Cases)
            {
                sb.Append($"{c.AngleOfAttack,-10:F1}");
                foreach (var key in coeffKeys)
                {
                    var val = c.Coefficients.TryGetValue(key, out var v) && v.HasValue
                        ? $"{v.Value:F6}" : "N/A";
                    sb.Append($" {val,10}");
                }
                var status = c.Converged ? "OK" : "No data";
                sb.AppendLine($" {status,10}");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        internal static string FormatCsv(ResultsSummary summary)
        {
            var sb = new StringBuilder();
            var coeffKeys = GetCoefficientKeys(summary);

            // Header: AoA + dynamic coefficient columns + Converged
            sb.AppendLine(string.Join(",", new[] { "AoA" }.Concat(coeffKeys).Append("Converged")));

            foreach (var c in summary.Cases)
            {
                var values = new List<string> { $"{c.AngleOfAttack:F1}" };
                foreach (var key in coeffKeys)
                {
                    var val = c.Coefficients.TryGetValue(key, out var v) && v.HasValue
                        ? v.Value.ToString("F6") : "";
                    values.Add(val);
                }
                values.Add(c.Converged.ToString());
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Discovers the coefficient column names from the cases' Coefficients dictionaries.
        /// Returns a consistent ordered list of keys across all cases.
        /// </summary>
        internal static List<string> GetCoefficientKeys(ResultsSummary summary)
        {
            var keys = new List<string>();
            foreach (var c in summary.Cases)
            {
                foreach (var key in c.Coefficients.Keys)
                {
                    if (!keys.Contains(key))
                        keys.Add(key);
                }
            }
            // If no cases have coefficients, fall back to standard columns
            if (keys.Count == 0)
                keys.AddRange(new[] { "Cd", "Cl", "CmPitch" });
            return keys;
        }

        internal static string FormatJson(ResultsSummary summary)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(summary, options);
        }
    }
}
