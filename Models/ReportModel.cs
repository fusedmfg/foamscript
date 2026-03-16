using CommandLine;

namespace foamscript.Models
{
    [Verb("report", HelpText = "Generate AIAA-quality analysis report (HTML + PDF + CSV) with aerodynamic polars, convergence plots, and mesh statistics.")]
    public class ReportModel : VerbModel
    {
        [Option('d', "dir", Required = true, HelpText = "Study or case directory (auto-detects)")]
        public string Dir { get; set; } = string.Empty;

        [Option('f', "format", Required = false, Default = "both", HelpText = "Output format: html, pdf, or both (default: both)")]
        public string Format { get; set; } = "both";


        [Option("average-window", Required = false, Default = 0.1, HelpText = "Fraction of simulation time to average coefficients (default: 0.1 = last 10%)")]
        public double AverageWindow { get; set; } = 0.1;
    }
}
