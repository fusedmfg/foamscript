using System.Text.Json.Serialization;

namespace foamscript.Models
{
    /// <summary>
    /// Manifest written to study directory during creation.
    /// Captures which template was used and other study-level metadata.
    /// </summary>
    public class StudyManifest
    {
        [JsonPropertyName("templateName")]
        public string TemplateName { get; set; } = string.Empty;
    }
}
