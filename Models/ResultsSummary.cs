namespace foamscript.Models
{
    /// <summary>
    /// Summary of extracted results from a completed study.
    /// </summary>
    public class ResultsSummary
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StudyDir { get; set; }
        public List<CaseResult> Cases { get; set; } = new();
    }

    /// <summary>
    /// Extracted force coefficients for a single case.
    /// </summary>
    public class CaseResult
    {
        public string CaseName { get; set; } = string.Empty;
        public double AngleOfAttack { get; set; }
        public double? Cd { get; set; }
        public double? Cl { get; set; }
        public double? CmPitch { get; set; }
        public bool Converged { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
