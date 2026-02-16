using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'new-study' verb - creates OpenFOAM study from template with AoA sweep.
    /// </summary>
    [Verb("new-study", HelpText = "Create OpenFOAM study from template with angle of attack cases.")]
    public class NewStudyModel : VerbModel
    {
        [Option('o', "output-dir", Required = true, HelpText = "Study directory path (e.g., ~/disc_analysis). Study name derived from this.")]
        public string OutputDir { get; set; } = string.Empty;

        [Option('t', "template", Required = true, HelpText = "Path to template case directory")]
        public string TemplatePath { get; set; } = string.Empty;

        [Option('a', "angles", Required = true, HelpText = "Angles of attack in degrees (comma-separated, e.g., -5,-2.5,0,2.5,5)")]
        public string Angles { get; set; } = string.Empty;

        [Option('v', "velocity", Required = false, Default = 20.0, HelpText = "Free stream velocity magnitude (m/s)")]
        public double Velocity { get; set; } = 20.0;

        [Option('r', "rpm", Required = false, Default = 1000, HelpText = "Disc rotation speed (RPM)")]
        public double Rpm { get; set; } = 1000.0;

        [Option("stl-dir", Required = false, HelpText = "Directory containing STL files to copy (disc.stl, rotor.stl, tunnel.stl)")]
        public string? StlDir { get; set; }

        [Option("cores", Required = false, Default = 4, HelpText = "Number of CPU cores for parallel execution")]
        public int Cores { get; set; } = 4;
    }
}
