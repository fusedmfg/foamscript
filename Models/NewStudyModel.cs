using CommandLine;

namespace foamscript.Models
{
    /// <summary>
    /// Model for the 'new-study' verb - creates OpenFOAM study from template with AoA sweep.
    /// Supply either a --config JSON file or all required CLI options.
    /// </summary>
    [Verb("new-study", HelpText = "Create OpenFOAM study from template with angle of attack cases.")]
    public class NewStudyModel : VerbModel
    {
        // ── Config file (alternative to CLI args) ────────────────────────────

        [Option('c', "config", Required = false, HelpText = "Path to a JSON study config file. When provided, all other options are read from the file.")]
        public string? ConfigFile { get; set; }

        // ── Required study inputs (required when not using --config) ──────────

        [Option('n', "project-name", Required = false, HelpText = "Project name (used for study folder and case naming, e.g., 'MyProject')")]
        public string ProjectName { get; set; } = string.Empty;

        [Option('o', "output-dir", Required = false, HelpText = "Parent directory where project folder will be created (e.g., ~/studies)")]
        public string OutputDir { get; set; } = string.Empty;

        [Option('s', "model-source", Required = false, HelpText = "Path to source geometry file (STEP, IGES, or STL)")]
        public string ModelSource { get; set; } = string.Empty;

        [Option('a', "angles", Required = false, HelpText = "Angles of attack in degrees (comma-separated, e.g., -5,-2.5,0,2.5,5)")]
        public string Angles { get; set; } = string.Empty;

        // ── Optional study inputs (with sensible defaults) ────────────────────

        [Option('t', "template", Required = false, HelpText = "Template name or path (defaults to external_disc_rotatingwall_steady)")]
        public string? TemplatePath { get; set; }

        [Option('v', "velocity", Required = false, Default = 20.0, HelpText = "Free stream velocity magnitude (m/s, default: 20.0)")]
        public double Velocity { get; set; } = 20.0;

        [Option('r', "rpm", Required = false, Default = 1000.0, HelpText = "Disc rotation speed (RPM, default: 1000)")]
        public double Rpm { get; set; } = 1000.0;

        [Option('u', "input-units", Required = false, Default = "mm", HelpText = "Source file units (mm, cm, m, in, ft). Only used for STEP/IGES conversion. Default: mm")]
        public string InputUnits { get; set; } = "mm";

        [Option('m', "mesh-size", Required = false, Default = 0.05, HelpText = "STL mesh size factor for STEP/IGES conversion (default: 0.05)")]
        public double MeshSize { get; set; } = 0.05;

        [Option("feature-angle", Required = false, HelpText = "Feature angle for edge preservation in degrees (optional)")]
        public double? FeatureAngle { get; set; }

        [Option("cores", Required = false, Default = 4, HelpText = "Number of CPU cores for parallel execution (default: 4)")]
        public int Cores { get; set; } = 4;

        // ── Physics parameters ────────────────────────────────────────────────

        [Option("nu", Required = false, Default = 1.5e-5, HelpText = "Kinematic viscosity of air (m²/s, default: 1.5e-5 for air at 20°C sea level)")]
        public double Nu { get; set; } = 1.5e-5;

        [Option("turbulence-intensity", Required = false, Default = 0.01, HelpText = "Freestream turbulence intensity as a fraction (default: 0.01 = 1%)")]
        public double TurbulenceIntensity { get; set; } = 0.01;

        [Option("end-time", Required = false, Default = 1.0, HelpText = "Simulation end time in seconds (default: 1.0)")]
        public double EndTime { get; set; } = 1.0;

        [Option("outer-correctors", Required = false, Default = 3, HelpText = "PIMPLE outer corrector iterations (default: 3)")]
        public int NOuterCorrectors { get; set; } = 3;

        [Option("max-iterations", Required = false, Default = 500, HelpText = "Maximum solver iterations for steady-state simpleFoam (default: 500)")]
        public int MaxIterations { get; set; } = 500;

        [Option("write-interval", Required = false, Default = 100, HelpText = "Write results every N iterations (default: 100)")]
        public int WriteInterval { get; set; } = 100;

        [Option("refinement-min", Required = false, Default = 5, HelpText = "snappyHexMesh minimum refinement level (default: 5)")]
        public int RefinementLevelMin { get; set; } = 5;

        [Option("refinement-max", Required = false, Default = 6, HelpText = "snappyHexMesh maximum refinement level (default: 6)")]
        public int RefinementLevelMax { get; set; } = 6;

        // ── Domain geometry parameters ────────────────────────────────────────

        [Option("rotor-radius-scale", Required = false, Default = 1.25, HelpText = "Rotor AMI cylinder radius as a multiple of disc radius (default: 1.25)")]
        public double RotorRadiusScale { get; set; } = 1.25;

        [Option("rotor-height-scale", Required = false, Default = 1.5, HelpText = "Rotor AMI cylinder height as a multiple of disc height (default: 1.5)")]
        public double RotorHeightScale { get; set; } = 1.5;

        [Option("tunnel-upstream", Required = false, Default = 5.0, HelpText = "Wind tunnel upstream extent in disc diameters (default: 5.0)")]
        public double TunnelUpstream { get; set; } = 5.0;

        [Option("tunnel-downstream", Required = false, Default = 10.0, HelpText = "Wind tunnel downstream extent in disc diameters (default: 10.0)")]
        public double TunnelDownstream { get; set; } = 10.0;

        [Option("tunnel-radial", Required = false, Default = 5.0, HelpText = "Wind tunnel radial extent in disc diameters (default: 5.0)")]
        public double TunnelRadial { get; set; } = 5.0;

        [Option("mesh-resolution", Required = false, Default = 32, HelpText = "Number of vertices around generated cylinder geometries (default: 32)")]
        public int MeshResolution { get; set; } = 32;
    }
}
