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
    /// Generic storage via Coefficients dictionary; Cd/Cl/CmPitch are backward-compat accessors.
    /// </summary>
    public class CaseResult
    {
        public string CaseName { get; set; } = string.Empty;
        public double AngleOfAttack { get; set; }

        /// <summary>
        /// Generic coefficient storage — keys come from TEMPLATE.json results.columns.
        /// This is the primary data store; Cd/Cl/CmPitch are convenience accessors.
        /// </summary>
        public Dictionary<string, double?> Coefficients { get; set; } = new();

        // Backward-compat accessors — read/write through Coefficients dictionary
        public double? Cd
        {
            get => Coefficients.TryGetValue("Cd", out var v) ? v : null;
            set => Coefficients["Cd"] = value;
        }
        public double? Cl
        {
            get => Coefficients.TryGetValue("Cl", out var v) ? v : null;
            set => Coefficients["Cl"] = value;
        }
        public double? CmPitch
        {
            get => Coefficients.TryGetValue("CmPitch", out var v) ? v : null;
            set => Coefficients["CmPitch"] = value;
        }

        public bool Converged { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
