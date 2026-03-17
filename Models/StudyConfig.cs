namespace foamscript.Models
{
    /// <summary>
    /// Complete study configuration — populated from either CLI args or a JSON config file.
    /// </summary>
    public class StudyConfig
    {
        public string ProjectName { get; set; } = string.Empty;
        public string OutputDir { get; set; } = string.Empty;
        public string? TemplateName { get; set; }
        public string ModelSource { get; set; } = string.Empty;
        public string Angles { get; set; } = string.Empty;
        public double Velocity { get; set; } = 27.0;
        public double Rpm { get; set; } = 925.0;
        public string InputUnits { get; set; } = "mm";
        public double MeshSize { get; set; } = 0.05;
        public double? FeatureAngle { get; set; }
        public int Cores { get; set; } = 0;
        public StudyPhysicsConfig Physics { get; set; } = new();
        public StudyDomainConfig Domain { get; set; } = new();
    }

    /// <summary>
    /// Physics and solver parameters for the study.
    /// </summary>
    public class StudyPhysicsConfig
    {
        /// <summary>Kinematic viscosity of air (m²/s). Default: 1.5e-5 (air at ~20°C, sea level).</summary>
        public double Nu { get; set; } = 1.5e-5;

        /// <summary>Freestream turbulence intensity as a fraction (e.g., 0.01 = 1%). Default: 0.01.
        /// Aerospace standard for bluff body external aero (AIAA best practices).</summary>
        public double TurbulenceIntensity { get; set; } = 0.01;

        /// <summary>Simulation end time in seconds. Default: 1.0.</summary>
        public double EndTime { get; set; } = 1.0;

        /// <summary>PIMPLE outer corrector iterations. Default: 3.</summary>
        public int NOuterCorrectors { get; set; } = 3;

        /// <summary>Maximum solver iterations for steady-state (simpleFoam). Default: 500.</summary>
        public int MaxIterations { get; set; } = 500;

        /// <summary>Write interval for steady-state output (every N iterations). Default: 100.</summary>
        public int WriteInterval { get; set; } = 100;

        /// <summary>snappyHexMesh minimum refinement level around geometry. Template-defined (fallback: 0).</summary>
        public int? RefinementLevelMin { get; set; }

        /// <summary>snappyHexMesh maximum refinement level around geometry. Template-defined (fallback: 0).</summary>
        public int? RefinementLevelMax { get; set; }
    }

    /// <summary>
    /// Domain geometry parameters controlling wind tunnel sizing.
    /// All scales are relative to the detected reference dimension.
    /// </summary>
    public class StudyDomainConfig
    {
        /// <summary>Wind tunnel upstream extent in reference lengths. Default: 5.0 (CFD convention).</summary>
        public double TunnelUpstream { get; set; } = 5.0;

        /// <summary>Wind tunnel downstream extent in reference lengths. Default: 10.0 (CFD convention).</summary>
        public double TunnelDownstream { get; set; } = 10.0;

        /// <summary>Wind tunnel radial extent in reference lengths. Default: 5.0 (CFD convention).</summary>
        public double TunnelRadial { get; set; } = 5.0;
    }
}
