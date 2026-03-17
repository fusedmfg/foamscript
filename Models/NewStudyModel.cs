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

        [Option('t', "template", Required = false, HelpText = "Template name or path (required — use 'foamscript list-templates' to see available templates)")]
        public string? TemplatePath { get; set; }

        [Option('v', "velocity", Required = false, HelpText = "Freestream velocity magnitude (m/s)")]
        public double? Velocity { get; set; }

        [Option('r', "rpm", Required = false, HelpText = "Rotational speed (RPM)")]
        public double? Rpm { get; set; }

        [Option('u', "input-units", Required = false, Default = "mm", HelpText = "Source file units (mm, cm, m, in, ft). Only used for STEP/IGES conversion. Default: mm")]
        public string InputUnits { get; set; } = "mm";

        [Option('m', "mesh-size", Required = false, Default = 0.05, HelpText = "STL mesh size factor for STEP/IGES conversion (default: 0.05)")]
        public double MeshSize { get; set; } = 0.05;

        [Option("feature-angle", Required = false, HelpText = "Feature angle for edge preservation in degrees (optional)")]
        public double? FeatureAngle { get; set; }

        [Option("cores", Required = false, Default = 0, HelpText = "Number of CPU cores for parallel execution (0 = auto-detect, default: auto-detect)")]
        public int Cores { get; set; } = 0;

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

        [Option("refinement-min", Required = false, HelpText = "snappyHexMesh minimum refinement level (template-defined, fallback: 0)")]
        public int? RefinementLevelMin { get; set; }

        [Option("refinement-max", Required = false, HelpText = "snappyHexMesh maximum refinement level (template-defined, fallback: 0)")]
        public int? RefinementLevelMax { get; set; }

        // ── Domain geometry parameters ────────────────────────────────────────

        [Option("tunnel-upstream", Required = false, Default = 5.0, HelpText = "Wind tunnel upstream extent in reference lengths (default: 5.0)")]
        public double TunnelUpstream { get; set; } = 5.0;

        [Option("tunnel-downstream", Required = false, Default = 10.0, HelpText = "Wind tunnel downstream extent in reference lengths (default: 10.0)")]
        public double TunnelDownstream { get; set; } = 10.0;

        [Option("tunnel-radial", Required = false, Default = 5.0, HelpText = "Wind tunnel radial extent in reference lengths (default: 5.0)")]
        public double TunnelRadial { get; set; } = 5.0;
    }
}
