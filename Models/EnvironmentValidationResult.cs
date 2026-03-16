namespace foamscript.Models
{
    /// <summary>
    /// Result of OpenFOAM environment validation.
    /// </summary>
    public class EnvironmentValidationResult
    {
        public bool IsValid { get; set; }
        public string? Version { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> MissingTools { get; set; } = new();
        public List<string> MissingVariables { get; set; } = new();
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public Dictionary<string, string> AvailableTools { get; set; } = new();
        public Dictionary<string, string> OptionalTools { get; set; } = new();
        public List<string> MissingOptionalTools { get; set; } = new();

        /// <summary>
        /// List of detected OpenFOAM installation paths (for diagnostics).
        /// </summary>
        public List<string> DetectedInstallations { get; set; } = new();

        /// <summary>
        /// Path to detected bashrc file (if found).
        /// </summary>
        public string? DetectedBashrcPath { get; set; }
    }

    /// <summary>
    /// Result of checking an environment variable.
    /// </summary>
    public class EnvironmentVariableCheck
    {
        public bool IsValid { get; set; }
        public string? Value { get; set; }
    }

    /// <summary>
    /// Result of checking tool availability.
    /// </summary>
    public class ToolCheck
    {
        public bool IsAvailable { get; set; }
        public string? Path { get; set; }
    }
}
