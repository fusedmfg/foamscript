using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class ConvertHandler : ICommandHandler<ConvertModel>
    {
        private readonly LoggingService _loggingService;
        private readonly GeometryService _geometryService;

        public ConvertHandler(LoggingService loggingService, GeometryService geometryService)
        {
            _loggingService = loggingService;
            _geometryService = geometryService;
        }

        public int Handle(ConvertModel model)
        {
            _loggingService.LogInformation($"Converting {model.InputFile} to {model.OutputFile}");

            Console.WriteLine();
            Console.WriteLine("=== Geometry Conversion (STEP → STL) ===");
            Console.WriteLine();
            Console.WriteLine($"Input:  {model.InputFile}");
            Console.WriteLine($"Output: {model.OutputFile}");
            Console.WriteLine($"Input units: {model.InputUnits} (output will be in meters)");
            Console.WriteLine($"Mesh size scaling: {model.MeshSize}");
            if (model.FeatureAngle.HasValue)
            {
                Console.WriteLine($"Feature angle: {model.FeatureAngle.Value}°");
            }
            Console.WriteLine();
            Console.WriteLine("Running gmsh...");

            var result = _geometryService.ConvertStepToStl(model.InputFile, model.OutputFile, model.MeshSize, model.FeatureAngle, model.InputUnits);

            Console.WriteLine();

            if (result.IsSuccess)
            {
                Console.WriteLine("✓ Conversion successful!");

                if (result.NodeCount.HasValue && result.TriangleCount.HasValue)
                {
                    Console.WriteLine($"  Nodes: {result.NodeCount:N0}");
                    Console.WriteLine($"  Triangles: {result.TriangleCount:N0}");
                }

                Console.WriteLine($"  Output: {result.OutputFile}");
                _loggingService.LogInformation("Conversion completed successfully.");

                // Perform validation if requested
                if (model.Validate)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== STL Validation ===");
                    Console.WriteLine();
                    Console.WriteLine("Running surfaceCheck to validate geometry quality...");

                    var validationResult = _geometryService.ValidateStl(model.OutputFile);

                    Console.WriteLine();

                    if (validationResult.IsValid)
                    {
                        Console.WriteLine("✓ Validation passed! STL is suitable for snappyHexMesh.");
                        Console.WriteLine($"  Watertight: Yes (0 open edges)");
                        Console.WriteLine($"  Manifold: Yes");
                        Console.WriteLine($"  Unconnected parts: {validationResult.UnconnectedParts}");
                        Console.WriteLine($"  Normal zones: {validationResult.NormalZones}");
                        Console.WriteLine($"  Self-intersecting: No");
                        _loggingService.LogInformation("STL validation passed.");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Validation failed: {validationResult.ErrorMessage}");
                        Console.WriteLine();
                        Console.WriteLine("Validation Details:");
                        Console.WriteLine($"  Illegal triangles: {validationResult.IllegalTriangles}");
                        Console.WriteLine($"  Watertight: {(validationResult.IsWatertight ? "Yes" : $"No ({validationResult.OpenEdges} open edges)")}");
                        Console.WriteLine($"  Manifold: {(validationResult.IsManifold ? "Yes" : $"No ({validationResult.NonManifoldEdges} non-manifold edges)")}");
                        Console.WriteLine($"  Unconnected parts: {validationResult.UnconnectedParts} (expected 1)");
                        Console.WriteLine($"  Normal zones: {validationResult.NormalZones} (expected 1)");
                        Console.WriteLine($"  Self-intersecting: {(validationResult.IsSelfIntersecting ? $"Yes ({validationResult.SelfIntersectionCount} locations)" : "No")}");
                        _loggingService.LogError("STL validation failed.", null!);
                        Console.WriteLine();
                        return -1;
                    }
                }

                Console.WriteLine();
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Conversion failed: {result.ErrorMessage}");
                _loggingService.LogError("Conversion failed.", null!);
                Console.WriteLine();
                return -1;
            }
        }
    }
}
