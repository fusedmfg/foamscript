using System.Text.Json.Serialization;

namespace foamscript.Models
{
    /// <summary>
    /// Machine-readable metadata for an OpenFOAM template, loaded from TEMPLATE.json.
    /// Drives geometry handling, reference dimension extraction, and domain generation.
    /// </summary>
    public class TemplateMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("solver")]
        public string Solver { get; set; } = "simpleFoam";

        [JsonPropertyName("geometryType")]
        public string GeometryType { get; set; } = "disc";

        [JsonPropertyName("geometryStlName")]
        public string GeometryStlName { get; set; } = "disc.stl";

        [JsonPropertyName("referenceDimension")]
        public string ReferenceDimension { get; set; } = "diameter";

        [JsonPropertyName("referenceAreaFormula")]
        public string ReferenceAreaFormula { get; set; } = "circular";

        [JsonPropertyName("requiresRotorZone")]
        public bool RequiresRotorZone { get; set; } = true;

        [JsonPropertyName("requiredStlFiles")]
        public string[] RequiredStlFiles { get; set; } = ["disc.stl", "tunnel.stl"];

        [JsonPropertyName("validation")]
        public GeometryValidation? Validation { get; set; }
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
