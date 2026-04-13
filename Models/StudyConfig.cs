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

        /// <summary>Units of the model source file: mm, cm, m, in, ft. Default: mm (most common CAD unit). Ignored for STL files.</summary>
        public string ModelSourceUnits { get; set; } = "mm";

        /// <summary>gmsh mesh size scaling factor for STEP/IGES → STL conversion. Default: 1.0. Ignored for STL files.</summary>
        public double MeshSize { get; set; } = 1.0;

        public int Cores { get; set; } = 0;
        public StudyPhysicsConfig Physics { get; set; } = new();
        public StudyDomainConfig Domain { get; set; } = new();
    }

    /// <summary>
    /// Physics and solver parameters for the study.
    /// Nullable properties are resolved from template defaults when not specified by CLI/config.
    /// </summary>
    public class StudyPhysicsConfig
    {
        /// <summary>Kinematic viscosity of air (m²/s). Template default: 1.5e-5 (air at ~20°C, sea level).</summary>
        public double? Nu { get; set; }

        /// <summary>Freestream turbulence intensity as a fraction (e.g., 0.01 = 1%).
        /// Aerospace standard for bluff body external aero (AIAA best practices).</summary>
        public double? TurbulenceIntensity { get; set; }

        /// <summary>Simulation end time. Template default varies by solver type.</summary>
        public double? EndTime { get; set; }

        /// <summary>PIMPLE outer corrector iterations.</summary>
        public int? NOuterCorrectors { get; set; }

        /// <summary>Maximum solver iterations for steady-state (simpleFoam).</summary>
        public int? MaxIterations { get; set; }

        /// <summary>Write interval for steady-state output (every N iterations).</summary>
        public int? WriteInterval { get; set; }

        /// <summary>snappyHexMesh minimum refinement level around geometry. Template-defined (fallback: 0).</summary>
        public int? RefinementLevelMin { get; set; }

        /// <summary>snappyHexMesh maximum refinement level around geometry. Template-defined (fallback: 0).</summary>
        public int? RefinementLevelMax { get; set; }

        /// <summary>Mixing length as fraction of reference length. Template default: 0.07.</summary>
        public double MixingLengthRatio { get; set; } = 0.07;

        /// <summary>nu-tilda initial value multiplier (x nu). Template default: 3.0.</summary>
        public double NuTildaMultiplier { get; set; } = 3.0;
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

        /// <summary>Domain extent margin multiplier (e.g., 1.1 = 10% beyond tunnel STL). Template default: 1.1.</summary>
        public double Margin { get; set; } = 1.1;

        /// <summary>
        /// Span ratio for 2D domains. null = 3D (use radial); 0.8 = STL protrudes through symmetry planes.
        /// </summary>
        public double? SpanRatio { get; set; }
    }
}
