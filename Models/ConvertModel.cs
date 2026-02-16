using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'convert' verb - converts STEP files to STL format using gmsh.
    /// </summary>
    [Verb("convert", HelpText = "Convert STEP geometry files to STL format for meshing.")]
    public class ConvertModel : VerbModel
    {
        [Value(0, Required = true, MetaName = "input", HelpText = "Input STEP file path (e.g., model.step)")]
        public string InputFile { get; set; } = string.Empty;

        [Value(1, Required = true, MetaName = "output", HelpText = "Output STL file path (e.g., model.stl)")]
        public string OutputFile { get; set; } = string.Empty;

        [Option('s', "mesh-size", Required = false, HelpText = "Mesh size scaling factor (default: 1.0). Smaller values create finer meshes.")]
        public double MeshSize { get; set; } = 1.0;

        [Option('a', "feature-angle", Required = false, HelpText = "Feature angle in degrees (default: none). Preserves sharp edges above this angle threshold.")]
        public double? FeatureAngle { get; set; }

        [Option('u', "input-units", Required = false, HelpText = "Input file units (mm, cm, m, in, ft). Output will be scaled to meters for OpenFOAM. Default: m (no conversion).")]
        public string InputUnits { get; set; } = "m";

        [Option("validate", Required = false, HelpText = "Validate the output STL file for quality and suitability for snappyHexMesh")]
        public bool Validate { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Show detailed gmsh output")]
        public bool Verbose { get; set; }
    }
}
