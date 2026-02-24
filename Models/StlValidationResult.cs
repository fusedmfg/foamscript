namespace foamscript.Models
{
    /// <summary>
    /// Result of STL surface quality validation using OpenFOAM's surfaceCheck utility.
    /// </summary>
    public class StlValidationResult
    {
        /// <summary>
        /// Whether the STL file passed all validation checks (suitable for snappyHexMesh).
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of illegal triangles detected (0 = good).
        /// </summary>
        public int IllegalTriangles { get; set; }

        /// <summary>
        /// Number of unconnected surface regions (1 = good).
        /// </summary>
        public int UnconnectedParts { get; set; }

        /// <summary>
        /// Number of normal orientation zones (1 = good).
        /// </summary>
        public int NormalZones { get; set; }

        /// <summary>
        /// Number of edges connected to only one face - indicates open boundaries (0 = watertight).
        /// </summary>
        public int OpenEdges { get; set; }

        /// <summary>
        /// Number of non-manifold edges (connected to >2 faces) - (0 = good).
        /// </summary>
        public int NonManifoldEdges { get; set; }

        /// <summary>
        /// Whether the surface is self-intersecting (false = good).
        /// </summary>
        public bool IsSelfIntersecting { get; set; }

        /// <summary>
        /// Number of self-intersection points detected.
        /// </summary>
        public int SelfIntersectionCount { get; set; }

        /// <summary>
        /// Minimum triangle quality (0=collapsed, 1=equilateral). Higher is better.
        /// </summary>
        public double? MinTriangleQuality { get; set; }

        /// <summary>
        /// Average triangle quality (0=collapsed, 1=equilateral). Higher is better.
        /// </summary>
        public double? AvgTriangleQuality { get; set; }

        /// <summary>
        /// Gets whether the surface is watertight (no open edges).
        /// </summary>
        public bool IsWatertight => OpenEdges == 0;

        /// <summary>
        /// Gets whether the surface is manifold (all edges connect exactly 2 faces).
        /// </summary>
        public bool IsManifold => OpenEdges == 0 && NonManifoldEdges == 0;
    }
}
