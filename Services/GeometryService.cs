using System.Globalization;
using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Facade for geometry operations. Delegates to StlConversionService for STEP→STL conversion.
    /// </summary>
    public class GeometryService
    {
        private readonly StlConversionService _conversionService;

        public GeometryService(StlConversionService conversionService)
        {
            _conversionService = conversionService;
        }

        public GeometryConversionResult ConvertStepToStl(string inputFile, string outputFile, double meshSize = 1.0, double? featureAngle = null, string inputUnits = "m")
            => _conversionService.ConvertStepToStl(inputFile, outputFile, meshSize, featureAngle, inputUnits);

        public StlValidationResult ValidateStl(string stlFile)
            => _conversionService.ValidateStl(stlFile);

        /// <summary>
        /// Checks whether the STL bounding box suggests non-meter units based on template validation rules.
        /// Returns null if no warning is needed (within range, no validation rules, or unreadable STL).
        /// </summary>
        public static GeometryScaleWarning? CheckGeometryScale(string stlFile, GeometryValidation? validation)
        {
            if (validation == null || (validation.MinSize == null && validation.MaxSize == null))
                return null;

            var bbox = CalculateBoundingBox(stlFile);
            if (bbox == null)
                return null;

            var maxExtent = bbox.Diameter;
            var minSize = validation.MinSize ?? 0;
            var maxSize = validation.MaxSize ?? double.MaxValue;

            if (maxExtent >= minSize && maxExtent <= maxSize)
                return null;

            // Heuristic: guess likely source units based on ratio to expected range
            var midExpected = (minSize + maxSize) / 2.0;
            string? likelyUnits = null;
            if (midExpected > 0)
            {
                var ratio = maxExtent / midExpected;
                if (ratio > 500 && ratio < 2000)
                    likelyUnits = "mm";
                else if (ratio > 15 && ratio < 50)
                    likelyUnits = "in";
                else if (ratio > 50 && ratio < 500)
                    likelyUnits = "cm";
            }

            return new GeometryScaleWarning
            {
                MaxExtent = maxExtent,
                MinSize = minSize,
                MaxSize = maxSize,
                LikelySourceUnits = likelyUnits,
                TemplateWarningMessage = validation.WarningMessage
            };
        }

        /// <summary>
        /// Formats a GeometryScaleWarning into a user-facing warning string.
        /// </summary>
        public static string FormatScaleWarning(GeometryScaleWarning warning)
        {
            var msg = $"⚠ Geometry max extent ({warning.MaxExtent.ToString("G4", CultureInfo.InvariantCulture)} m) " +
                      $"is outside expected range ({warning.MinSize.ToString("G3", CultureInfo.InvariantCulture)}–" +
                      $"{warning.MaxSize.ToString("G3", CultureInfo.InvariantCulture)} m).";

            if (warning.TemplateWarningMessage != null)
                msg += $"\n  {warning.TemplateWarningMessage}";

            if (warning.LikelySourceUnits != null)
                msg += $"\n  This looks like {warning.LikelySourceUnits} units. " +
                       $"Run 'foamscript convert --input-units {warning.LikelySourceUnits} ...' to rescale.";

            return msg;
        }

        /// <summary>
        /// Calculates bounding box of an STL file by parsing vertices.
        /// </summary>
        public static BoundingBox? CalculateBoundingBox(string stlFile)
        {
            try
            {
                var lines = File.ReadAllLines(stlFile);
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                double minZ = double.MaxValue, maxZ = double.MinValue;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("vertex"))
                    {
                        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4)
                        {
                            if (double.TryParse(parts[1], out double x) &&
                                double.TryParse(parts[2], out double y) &&
                                double.TryParse(parts[3], out double z))
                            {
                                minX = Math.Min(minX, x);
                                maxX = Math.Max(maxX, x);
                                minY = Math.Min(minY, y);
                                maxY = Math.Max(maxY, y);
                                minZ = Math.Min(minZ, z);
                                maxZ = Math.Max(maxZ, z);
                            }
                        }
                    }
                }

                if (minX == double.MaxValue)
                {
                    return null; // No vertices found
                }

                return new BoundingBox
                {
                    MinX = minX,
                    MaxX = maxX,
                    MinY = minY,
                    MaxY = maxY,
                    MinZ = minZ,
                    MaxZ = maxZ
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to calculate bounding box: {ex.Message}");
                return null;
            }
        }
    }
}
