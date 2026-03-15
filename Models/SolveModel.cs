using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'solve' verb - runs the OpenFOAM solver on a single case or all cases in a study.
    /// Auto-detects whether the path is a case (has constant/ + system/) or a study directory.
    /// </summary>
    [Verb("solve", HelpText = "Run OpenFOAM solver on a case or study directory.")]
    public class SolveModel : VerbModel
    {
        [Option('d', "dir", Required = true, HelpText = "Path to case or study directory (must be meshed first)")]
        public string Dir { get; set; } = string.Empty;

        [Option("cores", Required = false, Default = 0, HelpText = "Number of CPU cores (0 = auto-detect all available; set FOAMSCRIPT_MAX_CORES to limit)")]
        public int Cores { get; set; } = 0;
    }
}
