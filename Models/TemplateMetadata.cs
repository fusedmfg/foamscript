using System.Text.Json.Serialization;

namespace foamscript.Models
{
    /// <summary>
    /// Machine-readable metadata for an OpenFOAM template, loaded from TEMPLATE.json.
    /// Drives geometry handling, reference dimension extraction, pipeline execution, and reporting.
    /// </summary>
    public class TemplateMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("solver")]
        public string Solver { get; set; } = "simpleFoam";

        [JsonPropertyName("geometry")]
        public GeometryConfig Geometry { get; set; } = new();

        [JsonPropertyName("reference")]
        public ReferenceConfig Reference { get; set; } = new();

        [JsonPropertyName("rotation")]
        public RotationConfig Rotation { get; set; } = new();

        [JsonPropertyName("validation")]
        public GeometryValidation? Validation { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, ParameterDef> Parameters { get; set; } = new();

        [JsonPropertyName("domain")]
        public DomainConfig Domain { get; set; } = new();

        [JsonPropertyName("meshPipeline")]
        public List<PipelineStep> MeshPipeline { get; set; } = new();

        [JsonPropertyName("solvePipeline")]
        public List<PipelineStep> SolvePipeline { get; set; } = new();

        [JsonPropertyName("results")]
        public ResultsConfig Results { get; set; } = new();

        [JsonPropertyName("report")]
        public ReportConfig Report { get; set; } = new();

        // Backward-compatibility accessors — allows existing code to keep working
        // while we migrate callers to the nested schema.
        [JsonIgnore] public string GeometryType => Geometry.Type;
        [JsonIgnore] public string GeometryStlName => Geometry.StlName;
        [JsonIgnore] public string ReferenceDimension => Reference.Dimension;
        [JsonIgnore] public string ReferenceAreaFormula => Reference.AreaFormula;
        [JsonIgnore] public string[] RequiredStlFiles => Geometry.RequiredStlFiles;
    }

    public class GeometryConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("stlName")]
        public string StlName { get; set; } = string.Empty;

        [JsonPropertyName("requiredStlFiles")]
        public string[] RequiredStlFiles { get; set; } = [];

        [JsonPropertyName("surfaceOrient")]
        public SurfaceOrientConfig? SurfaceOrient { get; set; }
    }

    public class SurfaceOrientConfig
    {
        [JsonPropertyName("outsidePoint")]
        public double[] OutsidePoint { get; set; } = [];
    }

    public class ReferenceConfig
    {
        [JsonPropertyName("dimension")]
        public string Dimension { get; set; } = string.Empty;

        [JsonPropertyName("areaFormula")]
        public string AreaFormula { get; set; } = string.Empty;
    }

    public class RotationConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

    public class ParameterDef
    {
        [JsonPropertyName("required")]
        public bool Required { get; set; }

        [JsonPropertyName("default")]
        public object? Default { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class DomainConfig
    {
        [JsonPropertyName("upstream")]
        public double Upstream { get; set; }

        [JsonPropertyName("downstream")]
        public double Downstream { get; set; }

        [JsonPropertyName("radial")]
        public double Radial { get; set; }
    }

    public class PipelineStep
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public string Args { get; set; } = string.Empty;

        [JsonPropertyName("parallel")]
        public bool Parallel { get; set; }

        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        [JsonPropertyName("parallelOnly")]
        public bool ParallelOnly { get; set; }
    }

    public class ResultsConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("dataFile")]
        public string DataFile { get; set; } = string.Empty;

        [JsonPropertyName("columns")]
        public Dictionary<string, int> Columns { get; set; } = new();
    }

    public class ReportConfig
    {
        [JsonPropertyName("template")]
        public string Template { get; set; } = string.Empty;

        [JsonPropertyName("standard")]
        public string Standard { get; set; } = string.Empty;

        [JsonPropertyName("postProcess")]
        public List<string> PostProcess { get; set; } = new();
    }

    /// <summary>
    /// Optional geometry size validation rules for a template.
    /// </summary>
    public class GeometryValidation
    {
        [JsonPropertyName("minSize")]
        public double? MinSize { get; set; }

        [JsonPropertyName("maxSize")]
        public double? MaxSize { get; set; }

        [JsonPropertyName("warningMessage")]
        public string? WarningMessage { get; set; }
    }
}
