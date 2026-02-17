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
        private readonly CaseService _caseService;

        public AppService(LoggingService loggingService, MeshService meshService, EnvironmentService environmentService, GeometryService geometryService, CaseService caseService)
        {
            _loggingService = loggingService;
            _meshService = meshService;
            _environmentService = environmentService;
            _geometryService = geometryService;
            _caseService = caseService;
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

                    // Handle generate-domain verb.
                    case GenerateDomainModel generateDomainModel:
                        return HandleGenerateDomain(generateDomainModel);

                    // Handle new-study verb.
                    case NewStudyModel newStudyModel:
                        return HandleNewStudy(newStudyModel);

                    // Handle mesh verb.
                    case MeshCaseModel meshCaseModel:
                        return HandleMesh(meshCaseModel);

                    // Handle mesh-study verb.
                    case MeshStudyModel meshStudyModel:
                        return HandleMeshStudy(meshStudyModel);

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

        private int HandleGenerateDomain(GenerateDomainModel model)
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

        private int HandleNewStudy(NewStudyModel model)
        {
            // Resolve template path: use default if not specified
            var templatePath = model.TemplatePath;
            if (string.IsNullOrEmpty(templatePath))
            {
                // Default to the built-in template in the application directory
                var appDir = AppContext.BaseDirectory;
                templatePath = Path.Combine(appDir, "..", "..", "..", "Templates", "external_disc_rotating-ami_transient");
                templatePath = Path.GetFullPath(templatePath); // Normalize the path
            }

            _loggingService.LogInformation($"Creating project '{model.ProjectName}' in {model.OutputDir}");

            Console.WriteLine();
            Console.WriteLine("=== New Study Creation ===");
            Console.WriteLine();
            Console.WriteLine($"Project name: {model.ProjectName}");
            Console.WriteLine($"Output directory: {model.OutputDir}");
            Console.WriteLine($"Study directory: {Path.Combine(model.OutputDir, model.ProjectName)}");
            Console.WriteLine($"Template: {templatePath}");
            Console.WriteLine($"Model source: {model.ModelSource}");
            Console.WriteLine($"Angles: {model.Angles}");
            Console.WriteLine($"Velocity: {model.Velocity} m/s");
            Console.WriteLine($"RPM: {model.Rpm}");
            Console.WriteLine($"Cores: {model.Cores}");
            Console.WriteLine();
            Console.WriteLine("Geometry Parameters:");
            Console.WriteLine($"  Input units: {model.InputUnits} (output will be in meters)");
            Console.WriteLine($"  Mesh size: {model.MeshSize}");
            if (model.FeatureAngle.HasValue)
            {
                Console.WriteLine($"  Feature angle: {model.FeatureAngle.Value}°");
            }
            Console.WriteLine();
            Console.WriteLine("Processing geometry...");

            var result = _caseService.CreateStudy(
                model.ProjectName,
                model.OutputDir,
                templatePath,
                model.ModelSource,
                model.Angles,
                model.Velocity,
                model.Rpm,
                model.InputUnits,
                model.MeshSize,
                model.FeatureAngle,
                model.Cores);

            if (result.IsSuccess)
            {
                Console.WriteLine("✓ Study creation successful!");
                Console.WriteLine();
                Console.WriteLine($"Study name: {result.StudyName}");
                Console.WriteLine($"Study directory: {result.StudyDir}");
                Console.WriteLine($"Geometry directory: {Path.Combine(result.StudyDir!, "geometry")}");
                Console.WriteLine();
                Console.WriteLine($"Created {result.Cases.Count} case(s):");

                foreach (var caseInfo in result.Cases)
                {
                    Console.WriteLine($"  • {Path.GetFileName(caseInfo.CaseDir)}");
                    Console.WriteLine($"      AoA: {caseInfo.AngleOfAttack}°");
                    Console.WriteLine($"      Velocity: Ux={caseInfo.Ux:F3} m/s, Uy={caseInfo.Uy:F3} m/s");
                    Console.WriteLine($"      Omega: {caseInfo.Omega:F3} rad/s ({model.Rpm} RPM)");
                }

                _loggingService.LogInformation("Study creation completed successfully.");
                Console.WriteLine();
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Study creation failed: {result.ErrorMessage}");
                _loggingService.LogError("Study creation failed.", null!);
                Console.WriteLine();
                return -1;
            }
        }

        private int HandleMesh(MeshCaseModel model)
        {
            _loggingService.LogInformation($"Meshing case at {model.CaseDir}");

            Console.WriteLine();
            Console.WriteLine("=== Mesh Generation ===");
            Console.WriteLine();
            Console.WriteLine($"Case directory: {model.CaseDir}");
            Console.WriteLine($"Parallel: {(model.Parallel ? $"Yes ({model.Cores} cores)" : "No")}");
            Console.WriteLine($"Check quality: {(model.CheckQuality ? "Yes" : "No")}");
            Console.WriteLine($"Overwrite: {(model.Overwrite ? "Yes" : "No")}");
            Console.WriteLine();

            var result = _meshService.MeshCase(
                model.CaseDir,
                model.Parallel,
                model.Cores,
                model.CheckQuality,
                model.Overwrite);

            if (result.IsSuccess)
            {
                Console.WriteLine();
                Console.WriteLine("✓ Mesh generation successful!");
                Console.WriteLine();

                if (result.CellCount.HasValue)
                {
                    Console.WriteLine("Mesh Statistics:");
                    Console.WriteLine($"  Cells: {result.CellCount:N0}");
                    if (result.PointCount.HasValue)
                        Console.WriteLine($"  Points: {result.PointCount:N0}");
                    if (result.FaceCount.HasValue)
                        Console.WriteLine($"  Faces: {result.FaceCount:N0}");
                    Console.WriteLine();
                }

                if (result.MeshQualityPassed.HasValue)
                {
                    Console.WriteLine($"Mesh Quality: {(result.MeshQualityPassed.Value ? "✓ Passed" : "✗ Failed")}");
                    Console.WriteLine();
                }

                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine("Warnings:");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"  ⚠ {warning}");
                    }
                    Console.WriteLine();
                }

                _loggingService.LogInformation("Mesh generation completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Mesh generation failed: {result.ErrorMessage}");
                Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                _loggingService.LogError("Mesh generation failed.", null!);
                Console.WriteLine();
                return -1;
            }
        }

        private int HandleMeshStudy(MeshStudyModel model)
        {
            _loggingService.LogInformation($"Meshing study at {model.StudyDir}");

            Console.WriteLine();
            Console.WriteLine("=== Study Mesh Generation ===");
            Console.WriteLine();
            Console.WriteLine($"Study directory: {model.StudyDir}");
            Console.WriteLine($"Parallel: {(model.Parallel ? $"Yes ({model.Cores} cores per case)" : "No")}");
            Console.WriteLine($"Check quality: {(model.CheckQuality ? "Yes" : "No")}");
            Console.WriteLine($"Overwrite: {(model.Overwrite ? "Yes" : "No")}");
            Console.WriteLine($"Continue on error: {(model.ContinueOnError ? "Yes" : "No")}");
            Console.WriteLine();

            var result = _meshService.MeshStudy(
                model.StudyDir,
                model.Parallel,
                model.Cores,
                model.CheckQuality,
                model.Overwrite,
                model.ContinueOnError);

            if (result.IsSuccess || (model.ContinueOnError && result.SuccessfulCases > 0))
            {
                Console.WriteLine("=== Study Mesh Summary ===");
                Console.WriteLine();
                Console.WriteLine($"Total cases: {result.TotalCases}");
                Console.WriteLine($"✓ Successful: {result.SuccessfulCases}");
                if (result.FailedCases > 0)
                {
                    Console.WriteLine($"✗ Failed: {result.FailedCases}");
                }
                Console.WriteLine();

                // Show summary table
                Console.WriteLine("Case Details:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"{"Case Name",-30} {"Status",-10} {"Cells",-15} {"Quality",-10}");
                Console.WriteLine(new string('-', 80));

                foreach (var caseSummary in result.CaseSummaries)
                {
                    var status = caseSummary.Success ? "✓ OK" : "✗ Failed";
                    var cells = caseSummary.CellCount.HasValue ? $"{caseSummary.CellCount:N0}" : "N/A";
                    var quality = caseSummary.MeshQualityPassed.HasValue
                        ? (caseSummary.MeshQualityPassed.Value ? "✓ Passed" : "✗ Failed")
                        : "N/A";

                    Console.WriteLine($"{caseSummary.CaseName,-30} {status,-10} {cells,-15} {quality,-10}");
                }

                Console.WriteLine(new string('-', 80));
                Console.WriteLine();

                if (result.IsSuccess)
                {
                    _loggingService.LogInformation("Study mesh generation completed successfully.");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"⚠ Study meshing completed with errors: {result.ErrorMessage}");
                    Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                    _loggingService.LogInformation($"Study meshing completed with errors: {result.ErrorMessage}");
                    return -1;
                }
            }
            else
            {
                Console.WriteLine($"✗ Study mesh generation failed: {result.ErrorMessage}");
                Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                _loggingService.LogError("Study mesh generation failed.", null!);
                Console.WriteLine();
                return -1;
            }
        }
    }
}