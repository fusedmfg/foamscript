namespace foamscript.Models
{
    /// <summary>
    /// Result of batch study meshing operation.
    /// </summary>
    public class StudyMeshResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StudyDir { get; set; }
        public int TotalCases { get; set; }
        public int SuccessfulCases { get; set; }
        public int FailedCases { get; set; }
        public List<CaseMeshSummary> CaseSummaries { get; set; } = new List<CaseMeshSummary>();
    }

    /// <summary>
    /// Summary of meshing result for a single case within a study.
    /// </summary>
    public class CaseMeshSummary
    {
        public string CaseName { get; set; } = string.Empty;
        public string CaseDir { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? CellCount { get; set; }
        public bool? MeshQualityPassed { get; set; }
    }
}
