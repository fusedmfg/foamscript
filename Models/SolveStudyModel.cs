using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'solve-study' verb - runs the solver on all cases in a study.
    /// </summary>
    [Verb("solve-study", HelpText = "Run OpenFOAM solver on all cases in a study directory.")]
    public class SolveStudyModel : VerbModel
    {
        [Option('d', "study-dir", Required = true, HelpText = "Path to study directory containing case subdirectories")]
        public string StudyDir { get; set; } = string.Empty;

        [Option('p', "parallel", Required = false, Default = false, HelpText = "Run solver in parallel with MPI")]
        public bool Parallel { get; set; } = false;

        [Option("cores", Required = false, Default = 4, HelpText = "Number of CPU cores for parallel execution")]
        public int Cores { get; set; } = 4;

        [Option("continue-on-error", Required = false, Default = false, HelpText = "Continue solving remaining cases if one fails")]
        public bool ContinueOnError { get; set; } = false;
    }
}
