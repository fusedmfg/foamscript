using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'mesh' verb - meshes a single case or all cases in a study.
    /// Auto-detects whether the path is a case (has constant/ + system/) or a study directory.
    /// </summary>
    [Verb("mesh", HelpText = "Generate mesh for an OpenFOAM case or study directory.")]
    public class MeshModel : VerbModel
    {
        [Option('d', "dir", Required = true, HelpText = "Path to case or study directory")]
        public string Dir { get; set; } = string.Empty;

        [Option('p', "parallel", Required = false, Default = false, HelpText = "Run snappyHexMesh in parallel (auto-enabled when --cores > 1)")]
        public bool Parallel { get; set; } = false;

        [Option("cores", Required = false, Default = 1, HelpText = "Number of CPU cores (implies parallel when > 1)")]
        public int Cores { get; set; } = 1;

        [Option("check-quality", Required = false, Default = true, HelpText = "Run checkMesh to verify mesh quality")]
        public bool CheckQuality { get; set; } = true;

        [Option("overwrite", Required = false, Default = true, HelpText = "Overwrite existing mesh (use -overwrite flag with snappyHexMesh)")]
        public bool Overwrite { get; set; } = true;
    }
}
