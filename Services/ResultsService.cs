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

            sb.AppendLine($"=== Study Results: {studyName} ===");
            sb.AppendLine();
            sb.AppendLine($"{"AoA (°)",-10} {"Cd",10} {"Cl",10} {"Cm",10} {"Cl/Cd",10} {"Status",10}");
            sb.AppendLine($"{new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)}");

            foreach (var c in summary.Cases)
            {
                var cd = c.Cd.HasValue ? $"{c.Cd.Value:F6}" : "N/A";
                var cl = c.Cl.HasValue ? $"{c.Cl.Value:F6}" : "N/A";
                var cm = c.CmPitch.HasValue ? $"{c.CmPitch.Value:F6}" : "N/A";
                var clCd = (c.Cl.HasValue && c.Cd.HasValue && c.Cd.Value != 0)
                    ? $"{c.Cl.Value / c.Cd.Value:F2}"
                    : "N/A";
                var status = c.Converged ? "OK" : "No data";

                sb.AppendLine($"{c.AngleOfAttack,-10:F1} {cd,10} {cl,10} {cm,10} {clCd,10} {status,10}");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        internal static string FormatCsv(ResultsSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AoA,Cd,Cl,CmPitch,Cl/Cd,Converged");

            foreach (var c in summary.Cases)
            {
                var clCd = (c.Cl.HasValue && c.Cd.HasValue && c.Cd.Value != 0)
                    ? $"{c.Cl.Value / c.Cd.Value:F6}"
                    : "";
                sb.AppendLine($"{c.AngleOfAttack:F1},{c.Cd?.ToString("F6") ?? ""},{c.Cl?.ToString("F6") ?? ""},{c.CmPitch?.ToString("F6") ?? ""},{clCd},{c.Converged}");
            }

            return sb.ToString();
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
