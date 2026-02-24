using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'mesh-study' verb - meshes all cases in an OpenFOAM study.
    /// </summary>
    [Verb("mesh-study", HelpText = "Generate mesh for all cases in an OpenFOAM study.")]
    public class MeshStudyModel : VerbModel
    {
        [Option('s', "study-dir", Required = true, HelpText = "Path to study directory containing multiple cases")]
        public string StudyDir { get; set; } = string.Empty;

        [Option('p', "parallel", Required = false, Default = false, HelpText = "Run snappyHexMesh in parallel for each case")]
        public bool Parallel { get; set; } = false;

        [Option("cores", Required = false, Default = 4, HelpText = "Number of CPU cores for parallel execution per case")]
        public int Cores { get; set; } = 4;

        [Option("check-quality", Required = false, Default = true, HelpText = "Run checkMesh to verify mesh quality")]
        public bool CheckQuality { get; set; } = true;

        [Option("overwrite", Required = false, Default = true, HelpText = "Overwrite existing meshes")]
        public bool Overwrite { get; set; } = true;

        [Option("continue-on-error", Required = false, Default = true, HelpText = "Continue meshing other cases if one fails")]
        public bool ContinueOnError { get; set; } = true;
    }
}
