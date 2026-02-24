using foamscript.Models;
using Microsoft.Extensions.DependencyInjection;

namespace foamscript.Services
{
    /// <summary>
    /// Facade for geometry operations. Delegates to StlConversionService and DomainService.
    /// </summary>
    public class GeometryService
    {
        private readonly StlConversionService _conversionService;
        private readonly DomainService _domainService;

        /// <summary>
        /// DI constructor used by the host container.
        /// </summary>
        [ActivatorUtilitiesConstructor]
        public GeometryService(StlConversionService conversionService, DomainService domainService)
        {
            _conversionService = conversionService;
            _domainService = domainService;
        }

        /// <summary>
        /// Backward-compatible constructor for existing tests.
        /// </summary>
        public GeometryService(IProcessExecutor processExecutor, LoggingService loggingService)
            : this(new StlConversionService(processExecutor, loggingService),
                   new DomainService(processExecutor, loggingService))
        {
        }

        public GeometryConversionResult ConvertStepToStl(string inputFile, string outputFile, double meshSize = 1.0, double? featureAngle = null, string inputUnits = "m")
            => _conversionService.ConvertStepToStl(inputFile, outputFile, meshSize, featureAngle, inputUnits);

        public StlValidationResult ValidateStl(string stlFile)
            => _conversionService.ValidateStl(stlFile);

        public DomainGenerationResult GenerateDomain(
            string discStlFile,
            string outputDirectory,
            double rotorRadiusScale,
            double rotorHeightScale,
            double tunnelUpstream,
            double tunnelDownstream,
            double tunnelRadial,
            int meshResolution)
            => _domainService.GenerateDomain(discStlFile, outputDirectory, rotorRadiusScale, rotorHeightScale, tunnelUpstream, tunnelDownstream, tunnelRadial, meshResolution);
    }
}
