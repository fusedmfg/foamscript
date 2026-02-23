using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'solve' verb - runs the OpenFOAM solver on a meshed case.
    /// </summary>
    [Verb("solve", HelpText = "Run OpenFOAM solver (pimpleFoam) on a meshed case.")]
    public class SolveCaseModel : VerbModel
    {
        [Option('c', "case-dir", Required = true, HelpText = "Path to case directory (must be meshed first)")]
        public string CaseDir { get; set; } = string.Empty;

        [Option('p', "parallel", Required = false, Default = false, HelpText = "Run solver in parallel with MPI")]
        public bool Parallel { get; set; } = false;

        [Option("cores", Required = false, Default = 4, HelpText = "Number of CPU cores for parallel execution")]
        public int Cores { get; set; } = 4;
    }
}
