using System.Text.Json;
using System.Text.Json.Serialization;

namespace foamscript.Models
{
    public class FoamScriptConfig
    {
        [JsonPropertyName("openfoamBashrc")]
        public string OpenFoamBashrc { get; set; } = "";

        [JsonPropertyName("openfoamVersion")]
        public string OpenFoamVersion { get; set; } = "";

        [JsonPropertyName("configuredAt")]
        public DateTime ConfiguredAt { get; set; }

        public static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foamscript");

        public static string ConfigPath =>
            Path.Combine(ConfigDir, "config.json");

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };
    }
}
