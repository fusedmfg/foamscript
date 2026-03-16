namespace foamscript.Models
{
    /// <summary>
    /// Result of grouped environment validation with pass/fail counts.
    /// </summary>
    public class EnvironmentValidationResult
    {
        public bool IsValid { get; set; }
        public string? OpenFoamVersion { get; set; }
        public string? DetectedBashrcPath { get; set; }
        public List<string> DetectedInstallations { get; set; } = new();
        public List<CheckGroup> Groups { get; set; } = new();
        public int TotalChecks => Groups.Sum(g => g.Checks.Count);
        public int PassedChecks => Groups.Sum(g => g.Checks.Count(c => c.Passed));
    }

    /// <summary>
    /// A group of related validation checks (e.g., "Meshing Tools").
    /// </summary>
    public class CheckGroup
    {
        public string Name { get; set; } = "";
        public List<CheckItem> Checks { get; set; } = new();
    }

    /// <summary>
    /// A single validation check with pass/fail status and install hint on failure.
    /// </summary>
    public class CheckItem
    {
        public bool Passed { get; set; }
        public string Name { get; set; } = "";
        public string? Detail { get; set; }
        public string? InstallHint { get; set; }
    }
}
