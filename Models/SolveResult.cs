namespace foamscript.Models
{
    /// <summary>
    /// Result of solving a single OpenFOAM case.
    /// </summary>
    public class SolveResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CaseDir { get; set; }
        public double? SimulationTime { get; set; }
        public bool? Converged { get; set; }
        public double? Cd { get; set; }
        public double? Cl { get; set; }
        public double? CmPitch { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Result of solving all cases in a study.
    /// </summary>
    public class StudySolveResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StudyDir { get; set; }
        public int TotalCases { get; set; }
        public int SuccessfulCases { get; set; }
        public int FailedCases { get; set; }
        public List<CaseSolveSummary> CaseSummaries { get; set; } = new();
    }

    /// <summary>
    /// Summary of solver execution for a single case within a study.
    /// </summary>
    public class CaseSolveSummary
    {
        public string CaseName { get; set; } = string.Empty;
        public string CaseDir { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double? SimulationTime { get; set; }
        public bool? Converged { get; set; }
        public double? Cd { get; set; }
        public double? Cl { get; set; }
        public double? CmPitch { get; set; }
    }
}
