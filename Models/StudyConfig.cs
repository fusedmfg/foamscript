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
        public double Velocity { get; set; } = 20.0;
        public double Rpm { get; set; } = 1000.0;
        public string InputUnits { get; set; } = "mm";
        public double MeshSize { get; set; } = 0.05;
        public double? FeatureAngle { get; set; }
        public int Cores { get; set; } = 4;
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

        /// <summary>Freestream turbulence intensity as a fraction (e.g., 0.05 = 5%). Default: 0.05.</summary>
        public double TurbulenceIntensity { get; set; } = 0.05;

        /// <summary>Simulation end time in seconds. Default: 1.0.</summary>
        public double EndTime { get; set; } = 1.0;

        /// <summary>PIMPLE outer corrector iterations. Default: 3.</summary>
        public int NOuterCorrectors { get; set; } = 3;

        /// <summary>snappyHexMesh minimum refinement level around geometry. Default: 3.</summary>
        public int RefinementLevelMin { get; set; } = 3;

        /// <summary>snappyHexMesh maximum refinement level around geometry. Default: 4.</summary>
        public int RefinementLevelMax { get; set; } = 4;
    }

    /// <summary>
    /// Domain geometry parameters controlling rotor zone and wind tunnel sizing.
    /// All scales are relative to the detected disc diameter.
    /// </summary>
    public class StudyDomainConfig
    {
        /// <summary>Rotor AMI cylinder radius as a multiple of disc radius. Default: 1.25 (~25% clearance).</summary>
        public double RotorRadiusScale { get; set; } = 1.25;

        /// <summary>Rotor AMI cylinder height as a multiple of disc height. Default: 1.5.</summary>
        public double RotorHeightScale { get; set; } = 1.5;

        /// <summary>Wind tunnel upstream extent in disc diameters. Default: 5.0 (CFD convention).</summary>
        public double TunnelUpstream { get; set; } = 5.0;

        /// <summary>Wind tunnel downstream extent in disc diameters. Default: 10.0 (CFD convention).</summary>
        public double TunnelDownstream { get; set; } = 10.0;

        /// <summary>Wind tunnel radial extent in disc diameters. Default: 5.0 (CFD convention).</summary>
        public double TunnelRadial { get; set; } = 5.0;

        /// <summary>Number of vertices around the generated cylinder geometries. Default: 32.</summary>
        public int MeshResolution { get; set; } = 32;
    }
}
