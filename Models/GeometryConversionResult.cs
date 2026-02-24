namespace foamscript.Models
{
    /// <summary>
    /// Result of a STEP to STL conversion operation.
    /// </summary>
    public class GeometryConversionResult
    {
        /// <summary>
        /// Whether the conversion was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Path to the output STL file (if successful).
        /// </summary>
        public string? OutputFile { get; set; }

        /// <summary>
        /// Error message if conversion failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of triangles in the output mesh (if available).
        /// </summary>
        public int? TriangleCount { get; set; }

        /// <summary>
        /// Number of nodes/vertices in the output mesh (if available).
        /// </summary>
        public int? NodeCount { get; set; }
    }
}
