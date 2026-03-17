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
