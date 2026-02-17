using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'generate-domain' verb - automatically generates rotor and tunnel STL files from disc geometry.
    /// </summary>
    [Verb("generate-domain", HelpText = "Generate rotor and tunnel STL files from disc geometry for snappyHexMesh.")]
    public class GenerateDomainModel : VerbModel
    {
        [Value(0, Required = true, MetaName = "disc-stl", HelpText = "Input disc STL file path (e.g., disc.stl)")]
        public string DiscStlFile { get; set; } = string.Empty;

        [Option('o', "output-dir", Required = false, HelpText = "Output directory for generated STL files (default: current directory)")]
        public string OutputDirectory { get; set; } = ".";

        [Option("rotor-radius-scale", Required = false, HelpText = "Rotor AMI cylinder radius as a multiple of disc radius (default: 1.25)")]
        public double RotorRadiusScale { get; set; } = 1.25;

        [Option("rotor-height-scale", Required = false, HelpText = "Rotor AMI cylinder height as a multiple of disc height (default: 1.5)")]
        public double RotorHeightScale { get; set; } = 1.5;

        [Option("tunnel-upstream", Required = false, HelpText = "Tunnel upstream length in disc diameters (default: 5)")]
        public double TunnelUpstream { get; set; } = 5.0;

        [Option("tunnel-downstream", Required = false, HelpText = "Tunnel downstream length in disc diameters (default: 10)")]
        public double TunnelDownstream { get; set; } = 10.0;

        [Option("tunnel-radial", Required = false, HelpText = "Tunnel radial extent in disc diameters (default: 5)")]
        public double TunnelRadial { get; set; } = 5.0;

        [Option("mesh-resolution", Required = false, HelpText = "Mesh resolution for generated geometries (segments around cylinder, default: 32)")]
        public int MeshResolution { get; set; } = 32;

        [Option("validate", Required = false, HelpText = "Validate generated STL files")]
        public bool Validate { get; set; }
    }
}
