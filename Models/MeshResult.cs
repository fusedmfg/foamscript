namespace foamscript.Models
{
    /// <summary>
    /// Result of mesh generation operation.
    /// </summary>
    public class MeshResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CaseDir { get; set; }
        public int? CellCount { get; set; }
        public int? PointCount { get; set; }
        public int? FaceCount { get; set; }
        public bool? MeshQualityPassed { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
