namespace foamscript.Models
{
    /// <summary>
    /// Warning produced when STL bounding box dimensions fall outside the template's expected range,
    /// suggesting the geometry may not be in meters.
    /// </summary>
    public class GeometryScaleWarning
    {
        public double MaxExtent { get; set; }
        public double MinSize { get; set; }
        public double MaxSize { get; set; }
        public string? LikelySourceUnits { get; set; }
        public string? TemplateWarningMessage { get; set; }
    }
}
