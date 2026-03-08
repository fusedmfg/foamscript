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

        [Option('p', "parallel", Required = false, Default = false, HelpText = "Run solver in parallel with MPI (auto-enabled when --cores > 1)")]
        public bool Parallel { get; set; } = false;

        [Option("cores", Required = false, Default = 1, HelpText = "Number of CPU cores (implies parallel when > 1)")]
        public int Cores { get; set; } = 1;
    }
}
