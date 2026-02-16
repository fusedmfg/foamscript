using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'new-study' verb - creates OpenFOAM study from template with AoA sweep.
    /// </summary>
    [Verb("new-study", HelpText = "Create OpenFOAM study from template with angle of attack cases.")]
    public class NewStudyModel : VerbModel
    {
        [Option('n', "project-name", Required = true, HelpText = "Project name (used for study folder and case naming, e.g., 'MyProject')")]
        public string ProjectName { get; set; } = string.Empty;

        [Option('o', "output-dir", Required = true, HelpText = "Parent directory where project folder will be created (e.g., ~/studies)")]
        public string OutputDir { get; set; } = string.Empty;

        [Option('t', "template", Required = true, HelpText = "Path to template case directory")]
        public string TemplatePath { get; set; } = string.Empty;

        [Option('s', "model-source", Required = true, HelpText = "Path to source geometry file (STEP, IGES, or STL)")]
        public string ModelSource { get; set; } = string.Empty;

        [Option('a', "angles", Required = true, HelpText = "Angles of attack in degrees (comma-separated, e.g., -5,-2.5,0,2.5,5)")]
        public string Angles { get; set; } = string.Empty;

        [Option('v', "velocity", Required = false, Default = 20.0, HelpText = "Free stream velocity magnitude (m/s)")]
        public double Velocity { get; set; } = 20.0;

        [Option('r', "rpm", Required = false, Default = 1000, HelpText = "Disc rotation speed (RPM)")]
        public double Rpm { get; set; } = 1000.0;

        [Option('u', "input-units", Required = false, Default = "mm", HelpText = "Source file units (mm, cm, m, in, ft). Only used for STEP/IGES conversion.")]
        public string InputUnits { get; set; } = "mm";

        [Option('m', "mesh-size", Required = false, Default = 0.05, HelpText = "STL mesh size factor for STEP/IGES conversion")]
        public double MeshSize { get; set; } = 0.05;

        [Option("feature-angle", Required = false, HelpText = "Feature angle for edge preservation in degrees (optional)")]
        public double? FeatureAngle { get; set; }

        [Option("cores", Required = false, Default = 4, HelpText = "Number of CPU cores for parallel execution")]
        public int Cores { get; set; } = 4;
    }
}
