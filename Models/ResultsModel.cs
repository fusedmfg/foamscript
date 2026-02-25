using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'results' verb - extracts and summarizes force coefficients from a completed study.
    /// Auto-detects whether the path is a case or study directory.
    /// </summary>
    [Verb("results", HelpText = "Extract and summarize force coefficients from a solved case or study.")]
    public class ResultsModel : VerbModel
    {
        [Option('d', "dir", Required = true, HelpText = "Path to case or study directory containing solved cases")]
        public string Dir { get; set; } = string.Empty;

        [Option('f', "format", Required = false, Default = "table", HelpText = "Output format: table, csv, json")]
        public string Format { get; set; } = "table";

        [Option("average-window", Required = false, Default = 0.1, HelpText = "Fraction of simulation time to average over (default: 0.1 = last 10%)")]
        public double AverageWindow { get; set; } = 0.1;
    }
}
