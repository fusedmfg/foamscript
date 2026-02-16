using foamscript.Services;
using foamscript.Models;

namespace foamscript
{
    public class AppService
    {
        private readonly LoggingService _loggingService;
        private readonly MeshService _meshService;
        private readonly EnvironmentService _environmentService;
        private readonly GeometryService _geometryService;

        public AppService(LoggingService loggingService, MeshService meshService, EnvironmentService environmentService, GeometryService geometryService)
        {
            _loggingService = loggingService;
            _meshService = meshService;
            _environmentService = environmentService;
            _geometryService = geometryService;
        }

        public int Run(VerbModel model)
        {
            try
            {
                switch (model)
                {
                    // Handle validate verb.
                    case ValidateModel validateModel:
                        return HandleValidate(validateModel);

                    // Handle convert verb.
                    case ConvertModel convertModel:
                        return HandleConvert(convertModel);

                    // Handle mesh verb.
                    case MeshModel meshModel:
                        _loggingService.LogInformation("Running mesh model...");
                        return _meshService.MeshGeometry();

                    // Handle unimplemented verb types here.
                    default:
                        throw new NotImplementedException($"The verb of type {model.GetType().Name} is not implemented.");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("The application failed to execute the requested command.", ex);
                return -1;
            }
        }

        private int HandleValidate(ValidateModel model)
        {
            _loggingService.LogInformation("Validating OpenFOAM environment...");

            var result = _environmentService.ValidateEnvironment();

            if (!model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("=== OpenFOAM Environment Validation ===");
                Console.WriteLine();
            }

            // Version check
            if (result.Version != null)
            {
                if (!model.Quiet)
                {
                    Console.WriteLine($"✓ OpenFOAM Version: {result.Version} detected");
                }
            }
            else
            {
                // Display the smart error message with auto-detection info
                Console.WriteLine($"✗ {result.ErrorMessage}");
                return -1;
            }

            // Environment variables
            if (model.Verbose && !model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("Environment Variables:");
                foreach (var (key, value) in result.EnvironmentVariables)
                {
                    Console.WriteLine($"  ✓ {key}: {value}");
                }
                foreach (var missing in result.MissingVariables)
                {
                    Console.WriteLine($"  ✗ {missing}: NOT SET");
                }
            }
            else if (result.MissingVariables.Count > 0)
            {
                Console.WriteLine($"✗ Missing environment variables: {string.Join(", ", result.MissingVariables)}");
            }

            // Tools
            if (model.Verbose && !model.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine("Required Tools:");
                foreach (var (tool, path) in result.AvailableTools)
                {
                    Console.WriteLine($"  ✓ {tool}: {path}");
                }
                foreach (var missing in result.MissingTools)
                {
                    Console.WriteLine($"  ✗ {missing}: NOT FOUND");
                }
            }
            else if (result.MissingTools.Count > 0)
            {
                Console.WriteLine($"✗ Missing tools: {string.Join(", ", result.MissingTools)}");
            }
            else if (!model.Quiet)
            {
                Console.WriteLine("✓ All required tools found");
            }

            // Summary
            if (!model.Quiet)
            {
                Console.WriteLine();
                if (result.IsValid)
                {
                    Console.WriteLine("✓ Summary: All checks passed. FoamScript is ready to use.");
                    _loggingService.LogInformation("Environment validation successful.");
                }
                else
                {
                    Console.WriteLine("✗ Summary: Some checks failed. Please fix the issues above.");
                    _loggingService.LogError("Environment validation failed.", null!);
                }
                Console.WriteLine();
            }

            return result.IsValid ? 0 : -1;
        }

        private int HandleConvert(ConvertModel model)
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