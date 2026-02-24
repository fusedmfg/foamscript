using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class GenerateDomainHandler : ICommandHandler<GenerateDomainModel>
    {
        private readonly LoggingService _loggingService;
        private readonly GeometryService _geometryService;

        public GenerateDomainHandler(LoggingService loggingService, GeometryService geometryService)
        {
            _loggingService = loggingService;
            _geometryService = geometryService;
        }

        public int Handle(GenerateDomainModel model)
        {
            _loggingService.LogInformation($"Generating domain from {model.DiscStlFile}");

            Console.WriteLine();
            Console.WriteLine("=== Domain Generation (Rotor + Tunnel) ===");
            Console.WriteLine();
            Console.WriteLine($"Input disc STL: {model.DiscStlFile}");
            Console.WriteLine($"Output directory: {model.OutputDirectory}");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine($"  Rotor radius scale: {model.RotorRadiusScale}x");
            Console.WriteLine($"  Rotor height scale: {model.RotorHeightScale}x");
            Console.WriteLine($"  Tunnel upstream: {model.TunnelUpstream} disc diameters");
            Console.WriteLine($"  Tunnel downstream: {model.TunnelDownstream} disc diameters");
            Console.WriteLine($"  Tunnel radial: {model.TunnelRadial} disc diameters");
            Console.WriteLine($"  Mesh resolution: {model.MeshResolution} segments");
            Console.WriteLine();

            var result = _geometryService.GenerateDomain(
                model.DiscStlFile,
                model.OutputDirectory,
                model.RotorRadiusScale,
                model.RotorHeightScale,
                model.TunnelUpstream,
                model.TunnelDownstream,
                model.TunnelRadial,
                model.MeshResolution);

            if (result.IsSuccess)
            {
                Console.WriteLine("✓ Domain generation successful!");
                Console.WriteLine();
                Console.WriteLine("Disc Geometry:");
                Console.WriteLine($"  Bounding box: [{result.DiscBoundingBox!.MinX:F4}, {result.DiscBoundingBox.MaxX:F4}] × [{result.DiscBoundingBox.MinY:F4}, {result.DiscBoundingBox.MaxY:F4}] × [{result.DiscBoundingBox.MinZ:F4}, {result.DiscBoundingBox.MaxZ:F4}]");
                Console.WriteLine($"  Dimensions: {result.DiscBoundingBox.Width:F4} × {result.DiscBoundingBox.Height:F4} × {result.DiscBoundingBox.Depth:F4} m");
                Console.WriteLine($"  Diameter: {result.DiscBoundingBox.Diameter:F4} m");
                Console.WriteLine();
                Console.WriteLine("Generated Files:");
                Console.WriteLine($"  Rotor:  {result.RotorStlFile}");
                Console.WriteLine($"    Radius: {result.RotorDimensions!.Radius:F4} m, Height: {result.RotorDimensions.Height:F4} m");
                Console.WriteLine($"  Tunnel: {result.TunnelStlFile}");
                Console.WriteLine($"    Extents: [{result.TunnelDimensions!.MinX:F4}, {result.TunnelDimensions.MaxX:F4}] × [{result.TunnelDimensions.MinY:F4}, {result.TunnelDimensions.MaxY:F4}] × [{result.TunnelDimensions.MinZ:F4}, {result.TunnelDimensions.MaxZ:F4}]");

                _loggingService.LogInformation("Domain generation completed successfully.");

                // Validate generated files if requested
                if (model.Validate)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== Validating Generated Geometries ===");
                    Console.WriteLine();

                    // Validate rotor
                    Console.WriteLine("Validating rotor.stl...");
                    var rotorValidation = _geometryService.ValidateStl(result.RotorStlFile!);
                    if (rotorValidation.IsValid)
                    {
                        Console.WriteLine("  ✓ Rotor validation passed!");
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Rotor validation failed: {rotorValidation.ErrorMessage}");
                        return -1;
                    }

                    // Validate tunnel
                    Console.WriteLine("Validating tunnel.stl...");
                    var tunnelValidation = _geometryService.ValidateStl(result.TunnelStlFile!);
                    if (tunnelValidation.IsValid)
                    {
                        Console.WriteLine("  ✓ Tunnel validation passed!");
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Tunnel validation failed: {tunnelValidation.ErrorMessage}");
                        return -1;
                    }
                }

                Console.WriteLine();
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Domain generation failed: {result.ErrorMessage}");
                _loggingService.LogError("Domain generation failed.", null!);
                Console.WriteLine();
                return -1;
            }
        }
    }
}
