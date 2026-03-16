using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Facade for geometry operations. Delegates to StlConversionService and DomainService.
    /// </summary>
    public class GeometryService
    {
        private readonly StlConversionService _conversionService;
        private readonly DomainService _domainService;

        public GeometryService(StlConversionService conversionService, DomainService domainService)
        {
            _conversionService = conversionService;
            _domainService = domainService;
        }

        public GeometryConversionResult ConvertStepToStl(string inputFile, string outputFile, double meshSize = 1.0, double? featureAngle = null, string inputUnits = "m")
            => _conversionService.ConvertStepToStl(inputFile, outputFile, meshSize, featureAngle, inputUnits);

        public StlValidationResult ValidateStl(string stlFile)
            => _conversionService.ValidateStl(stlFile);

        public DomainGenerationResult GenerateDomain(
            string geometryStlFile,
            string outputDirectory,
            double rotorRadiusScale,
            double rotorHeightScale,
            double tunnelUpstream,
            double tunnelDownstream,
            double tunnelRadial,
            int meshResolution)
            => _domainService.GenerateDomain(geometryStlFile, outputDirectory, rotorRadiusScale, rotorHeightScale, tunnelUpstream, tunnelDownstream, tunnelRadial, meshResolution);

        public DomainGenerationResult GenerateTunnelOnly(
            string stlFile,
            string outputDirectory,
            double tunnelUpstream,
            double tunnelDownstream,
            double tunnelRadial)
            => _domainService.GenerateTunnelOnly(stlFile, outputDirectory, tunnelUpstream, tunnelDownstream, tunnelRadial);

        public BoundingBox? CalculateBoundingBox(string stlFile)
            => _domainService.CalculateBoundingBox(stlFile);
    }
}
