using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'results' verb - extracts and summarizes force coefficients from a completed study.
    /// </summary>
    [Verb("results", HelpText = "Extract and summarize force coefficients from a completed study.")]
    public class ResultsModel : VerbModel
    {
        [Option('d', "study-dir", Required = true, HelpText = "Path to study directory containing solved cases")]
        public string StudyDir { get; set; } = string.Empty;

        [Option('f', "format", Required = false, Default = "table", HelpText = "Output format: table, csv, json")]
        public string Format { get; set; } = "table";

        [Option("average-window", Required = false, Default = 0.1, HelpText = "Fraction of simulation time to average over (default: 0.1 = last 10%)")]
        public double AverageWindow { get; set; } = 0.1;
    }
}
