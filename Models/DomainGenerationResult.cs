namespace foamscript.Models
{
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
}
