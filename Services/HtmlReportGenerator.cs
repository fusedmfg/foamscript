using Scriban;
using Scriban.Runtime;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Generates self-contained HTML analysis reports using Scriban templates
    /// with embedded SVG charts.
    /// </summary>
    public class HtmlReportGenerator
    {
        /// <summary>
        /// Renders the HTML report template with chart data and returns the HTML string.
        /// </summary>
        public string GenerateReport(ReportData data)
        {
            var templatePath = FindTemplatePath();
            var templateContent = File.ReadAllText(templatePath);

            var template = Template.Parse(templateContent);
            if (template.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Report template parse error: {string.Join(", ", template.Messages)}");
            }

            var scriptObject = new ScriptObject();
            scriptObject.Import(data, renamer: member => ConvertToSnakeCase(member.Name));

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);
            // Allow raw HTML output for SVG charts
            context.StrictVariables = false;

            return template.Render(context);
        }

        /// <summary>
        /// Generates and writes the HTML report to disk.
        /// </summary>
        public void WriteReport(ReportData data, string outputPath)
        {
            var html = GenerateReport(data);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, html);
        }

        private static string FindTemplatePath()
        {
            // Check relative to executable
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(exeDir, "Templates", "report", "report.html"),
                Path.Combine(exeDir, "..", "Templates", "report", "report.html"),
                Path.Combine(exeDir, "..", "..", "..", "Templates", "report", "report.html"),
                Path.Combine(exeDir, "..", "..", "..", "..", "Templates", "report", "report.html"),
            };

            foreach (var candidate in candidates)
            {
                var resolved = Path.GetFullPath(candidate);
                if (File.Exists(resolved))
                    return resolved;
            }

            throw new FileNotFoundException(
                "Could not find report template. Expected at Templates/report/report.html relative to executable.");
        }

        private static string ConvertToSnakeCase(string name)
        {
            return string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
        }
    }

    /// <summary>
    /// Data model for the HTML/PDF report template.
    /// Property names are converted to snake_case for Scriban (e.g., StudyName -> study_name).
    /// </summary>
    public class ReportData
    {
        public string StudyName { get; set; } = string.Empty;
        public string GenerationDate { get; set; } = string.Empty;
        public string Version { get; set; } = "0.3.0";
        public string TemplateName { get; set; } = string.Empty;

        // Physics config
        public string Velocity { get; set; } = string.Empty;
        public string Rpm { get; set; } = string.Empty;
        public string TurbulenceIntensity { get; set; } = string.Empty;
        public string Nu { get; set; } = string.Empty;
        public string SolverName { get; set; } = string.Empty;
        public string MaxIterations { get; set; } = string.Empty;
        public string RefinementMin { get; set; } = string.Empty;
        public string RefinementMax { get; set; } = string.Empty;

        // Mesh stats
        public List<MeshStatEntry> MeshStats { get; set; } = new();

        // Charts (SVG strings for HTML, PNG bytes for PDF)
        public string ClVsAoaChart { get; set; } = string.Empty;
        public string CdVsAoaChart { get; set; } = string.Empty;
        public string CmVsAoaChart { get; set; } = string.Empty;
        public string LdVsAoaChart { get; set; } = string.Empty;
        public string DragPolarChart { get; set; } = string.Empty;

        // Case results table
        public List<CaseResultEntry> Cases { get; set; } = new();

        // Convergence charts (one per case)
        public List<ConvergenceChartEntry> ConvergenceCharts { get; set; } = new();

        // Flow visualization (base64-encoded PNG for <img> embedding)
        public string PressureSliceImage { get; set; } = string.Empty;
        public string VelocitySliceImage { get; set; } = string.Empty;
        public bool HasVisualization { get; set; }
    }

    public class MeshStatEntry
    {
        public string CaseName { get; set; } = string.Empty;
        public string Aoa { get; set; } = string.Empty;
        public string Cells { get; set; } = "N/A";
        public string Points { get; set; } = "N/A";
        public string Faces { get; set; } = "N/A";
        public bool QualityPassed { get; set; }
    }

    public class CaseResultEntry
    {
        public string Aoa { get; set; } = string.Empty;
        public string Cd { get; set; } = "N/A";
        public string Cl { get; set; } = "N/A";
        public string Cm { get; set; } = "N/A";
        public string Ld { get; set; } = "N/A";
        public bool Converged { get; set; }
    }

    public class ConvergenceChartEntry
    {
        public string CaseName { get; set; } = string.Empty;
        public string Chart { get; set; } = string.Empty;
    }
}
