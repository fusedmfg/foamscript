namespace foamscript.Models
{
    /// <summary>
    /// Result of domain generation operation.
    /// </summary>
    public class DomainGenerationResult
    {
        /// <summary>
        /// Whether the domain generation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message if generation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Path to the generated rotor STL file.
        /// </summary>
        public string? RotorStlFile { get; set; }

        /// <summary>
        /// Path to the generated tunnel STL file.
        /// </summary>
        public string? TunnelStlFile { get; set; }

        /// <summary>
        /// Disc bounding box dimensions.
        /// </summary>
        public BoundingBox? DiscBoundingBox { get; set; }

        /// <summary>
        /// Rotor dimensions.
        /// </summary>
        public CylinderDimensions? RotorDimensions { get; set; }

        /// <summary>
        /// Tunnel dimensions.
        /// </summary>
        public BoxDimensions? TunnelDimensions { get; set; }
    }

    /// <summary>
    /// Bounding box with min/max coordinates.
    /// </summary>
    public class BoundingBox
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double Depth => MaxZ - MinZ;
        public double Diameter => Math.Max(Width, Math.Max(Height, Depth));
    }

    /// <summary>
    /// Cylinder dimensions (for rotor).
    /// </summary>
    public class CylinderDimensions
    {
        public double Radius { get; set; }
        public double Height { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }
    }

    /// <summary>
    /// Box dimensions (for tunnel).
    /// </summary>
    public class BoxDimensions
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
    }
}
