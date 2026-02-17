using System.Text.Json;
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

                    // Handle list-templates verb.
                    case ListTemplatesModel listTemplatesModel:
                        return HandleListTemplates(listTemplatesModel);

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
            // Build config from JSON file or CLI args
            StudyConfig config;
            if (model.ConfigFile != null)
            {
                var loadResult = LoadStudyConfig(model.ConfigFile);
                if (loadResult.Error != null)
                {
                    Console.WriteLine($"✗ {loadResult.Error}");
                    Console.WriteLine();
                    return -1;
                }
                config = loadResult.Config!;
            }
            else
            {
                // Validate required CLI args
                var missing = new List<string>();
                if (string.IsNullOrEmpty(model.ProjectName)) missing.Add("--project-name (-n)");
                if (string.IsNullOrEmpty(model.OutputDir)) missing.Add("--output-dir (-o)");
                if (string.IsNullOrEmpty(model.ModelSource)) missing.Add("--model-source (-s)");
                if (string.IsNullOrEmpty(model.Angles)) missing.Add("--angles (-a)");

                if (missing.Count > 0)
                {
                    Console.WriteLine("✗ Missing required arguments (or provide --config <file.json>):");
                    foreach (var arg in missing)
                        Console.WriteLine($"    {arg}");
                    Console.WriteLine();
                    Console.WriteLine("Run 'foamscript new-study --help' for usage.");
                    return -1;
                }

                config = new StudyConfig
                {
                    ProjectName = model.ProjectName,
                    OutputDir = model.OutputDir,
                    TemplateName = model.TemplatePath,
                    ModelSource = model.ModelSource,
                    Angles = model.Angles,
                    Velocity = model.Velocity,
                    Rpm = model.Rpm,
                    InputUnits = model.InputUnits,
                    MeshSize = model.MeshSize,
                    FeatureAngle = model.FeatureAngle,
                    Cores = model.Cores,
                    Physics = new StudyPhysicsConfig
                    {
                        Nu = model.Nu,
                        TurbulenceIntensity = model.TurbulenceIntensity,
                        EndTime = model.EndTime,
                        NOuterCorrectors = model.NOuterCorrectors,
                        RefinementLevelMin = model.RefinementLevelMin,
                        RefinementLevelMax = model.RefinementLevelMax
                    },
                    Domain = new StudyDomainConfig
                    {
                        RotorRadiusScale = model.RotorRadiusScale,
                        RotorHeightScale = model.RotorHeightScale,
                        TunnelUpstream = model.TunnelUpstream,
                        TunnelDownstream = model.TunnelDownstream,
                        TunnelRadial = model.TunnelRadial,
                        MeshResolution = model.MeshResolution
                    }
                };
            }

            // Resolve template path
            string templatePath;
            try
            {
                templatePath = ResolveTemplatePath(config.TemplateName);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"✗ {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Available templates:");
                var availableTemplatesDir = GetTemplatesDirectory();
                if (Directory.Exists(availableTemplatesDir))
                {
                    foreach (var dir in Directory.GetDirectories(availableTemplatesDir))
                    {
                        Console.WriteLine($"  • {Path.GetFileName(dir)}");
                    }
                }
                Console.WriteLine();
                Console.WriteLine("Use 'foamscript list-templates' for more information");
                return -1;
            }

            _loggingService.LogInformation($"Creating project '{config.ProjectName}' in {config.OutputDir}");

            Console.WriteLine();
            Console.WriteLine("=== New Study Creation ===");
            Console.WriteLine();
            Console.WriteLine($"Project name:     {config.ProjectName}");
            Console.WriteLine($"Output directory: {config.OutputDir}");
            Console.WriteLine($"Study directory:  {Path.Combine(config.OutputDir, config.ProjectName)}");
            Console.WriteLine($"Template:         {templatePath}");
            Console.WriteLine($"Model source:     {config.ModelSource}");
            Console.WriteLine($"Angles:           {config.Angles}");
            Console.WriteLine($"Velocity:         {config.Velocity} m/s");
            Console.WriteLine($"RPM:              {config.Rpm}");
            Console.WriteLine($"Cores:            {config.Cores}");
            Console.WriteLine();
            Console.WriteLine("Geometry / Domain Parameters:");
            Console.WriteLine($"  Input units:        {config.InputUnits} (output in meters)");
            Console.WriteLine($"  Mesh size:          {config.MeshSize}");
            if (config.FeatureAngle.HasValue)
                Console.WriteLine($"  Feature angle:      {config.FeatureAngle.Value}°");
            Console.WriteLine($"  Rotor radius scale: {config.Domain.RotorRadiusScale}x");
            Console.WriteLine($"  Rotor height scale: {config.Domain.RotorHeightScale}x");
            Console.WriteLine($"  Tunnel upstream:    {config.Domain.TunnelUpstream} D");
            Console.WriteLine($"  Tunnel downstream:  {config.Domain.TunnelDownstream} D");
            Console.WriteLine($"  Tunnel radial:      {config.Domain.TunnelRadial} D");
            Console.WriteLine($"  Mesh resolution:    {config.Domain.MeshResolution} segments");
            Console.WriteLine();
            Console.WriteLine("Physics Parameters:");
            Console.WriteLine($"  nu:                   {config.Physics.Nu:E2} m²/s");
            Console.WriteLine($"  Turbulence intensity: {config.Physics.TurbulenceIntensity * 100:F0}%");
            Console.WriteLine($"  End time:             {config.Physics.EndTime} s");
            Console.WriteLine($"  Outer correctors:     {config.Physics.NOuterCorrectors}");
            Console.WriteLine($"  Refinement levels:    {config.Physics.RefinementLevelMin}–{config.Physics.RefinementLevelMax}");
            Console.WriteLine();
            Console.WriteLine("Processing geometry...");

            var result = _caseService.CreateStudy(config, templatePath);

            if (result.IsSuccess)
            {
                Console.WriteLine("✓ Study creation successful!");
                Console.WriteLine();
                Console.WriteLine($"Study name:        {result.StudyName}");
                Console.WriteLine($"Study directory:   {result.StudyDir}");
                Console.WriteLine($"Geometry directory:{Path.Combine(result.StudyDir!, "geometry")}");
                Console.WriteLine();
                Console.WriteLine($"Created {result.Cases.Count} case(s):");

                foreach (var caseInfo in result.Cases)
                {
                    Console.WriteLine($"  • {Path.GetFileName(caseInfo.CaseDir)}");
                    Console.WriteLine($"      AoA: {caseInfo.AngleOfAttack}°");
                    Console.WriteLine($"      Velocity: Ux={caseInfo.Ux:F3} m/s, Uy={caseInfo.Uy:F3} m/s");
                    Console.WriteLine($"      Omega: {caseInfo.Omega:F3} rad/s ({config.Rpm} RPM)");
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

        /// <summary>
        /// Loads and validates a StudyConfig from a JSON file.
        /// </summary>
        private static (StudyConfig? Config, string? Error) LoadStudyConfig(string path)
        {
            if (!File.Exists(path))
                return (null, $"Config file not found: {path}");

            try
            {
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var config = JsonSerializer.Deserialize<StudyConfig>(json, options);

                if (config == null)
                    return (null, $"Failed to parse config file: {path}");

                // Validate required fields
                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(config.ProjectName)) missing.Add("projectName");
                if (string.IsNullOrWhiteSpace(config.OutputDir)) missing.Add("outputDir");
                if (string.IsNullOrWhiteSpace(config.ModelSource)) missing.Add("modelSource");
                if (string.IsNullOrWhiteSpace(config.Angles)) missing.Add("angles");

                if (missing.Count > 0)
                    return (null, $"Config file is missing required fields: {string.Join(", ", missing)}");

                // Ensure nested config objects exist (they may be null if omitted from JSON)
                config.Physics ??= new StudyPhysicsConfig();
                config.Domain ??= new StudyDomainConfig();

                return (config, null);
            }
            catch (JsonException ex)
            {
                return (null, $"Invalid JSON in config file '{path}': {ex.Message}");
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

        private int HandleListTemplates(ListTemplatesModel model)
        {
            _loggingService.LogInformation("Listing available templates");

            Console.WriteLine();
            Console.WriteLine("=== Available OpenFOAM Templates ===");
            Console.WriteLine();

            var templatesDir = GetTemplatesDirectory();

            if (!Directory.Exists(templatesDir))
            {
                Console.WriteLine("✗ Templates directory not found");
                Console.WriteLine($"  Expected location: {templatesDir}");
                return -1;
            }

            var templates = Directory.GetDirectories(templatesDir)
                .Select(d => Path.GetFileName(d))
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .ToList();

            if (templates.Count == 0)
            {
                Console.WriteLine("No templates found");
                return 0;
            }

            foreach (var templateName in templates)
            {
                var templatePath = Path.Combine(templatesDir, templateName!);
                var templateMdPath = Path.Combine(templatePath, "TEMPLATE.md");

                Console.WriteLine($"• {templateName}");

                // Try to read the first line of TEMPLATE.md for description
                if (File.Exists(templateMdPath))
                {
                    try
                    {
                        var lines = File.ReadLines(templateMdPath).Take(10).ToList();
                        // Look for classification info
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("- **Domain**:"))
                            {
                                Console.WriteLine($"  Domain: {line.Replace("- **Domain**:", "").Trim()}");
                            }
                            else if (line.StartsWith("- **Feature**:"))
                            {
                                Console.WriteLine($"  Feature: {line.Replace("- **Feature**:", "").Trim()}");
                            }
                            else if (line.StartsWith("- **Solver**:"))
                            {
                                Console.WriteLine($"  Solver: {line.Replace("- **Solver**:", "").Trim()}");
                                break; // Stop after solver line
                            }
                        }
                    }
                    catch
                    {
                        // If we can't read the template file, just show the name
                    }
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Total templates available: {templates.Count}");
            Console.WriteLine();
            Console.WriteLine("To use a template:");
            Console.WriteLine($"  foamscript new-study -t \"{templates[0]}\" ...");
            Console.WriteLine();
            Console.WriteLine("For more information:");
            Console.WriteLine("  See Templates/TEMPLATES.md in the repository");
            Console.WriteLine();

            return 0;
        }

        /// <summary>
        /// Resolves a template path from either a short name or full path.
        /// </summary>
        private string ResolveTemplatePath(string? templatePathOrName)
        {
            var templatesDir = GetTemplatesDirectory();

            // If null or empty, use default
            if (string.IsNullOrEmpty(templatePathOrName))
            {
                return Path.Combine(templatesDir, "external_disc_rotating-ami_transient");
            }

            // If it's an absolute path or contains path separators, use as-is
            if (Path.IsPathRooted(templatePathOrName) ||
                templatePathOrName.Contains(Path.DirectorySeparatorChar) ||
                templatePathOrName.Contains(Path.AltDirectorySeparatorChar))
            {
                return Path.GetFullPath(templatePathOrName);
            }

            // Otherwise, treat it as a template name and look in Templates directory
            var resolvedPath = Path.Combine(templatesDir, templatePathOrName);

            if (!Directory.Exists(resolvedPath))
            {
                throw new DirectoryNotFoundException($"Template '{templatePathOrName}' not found in {templatesDir}");
            }

            return Path.GetFullPath(resolvedPath);
        }

        /// <summary>
        /// Gets the templates directory path.
        /// </summary>
        private string GetTemplatesDirectory()
        {
            var appDir = AppContext.BaseDirectory;
            var templatesDir = Path.Combine(appDir, "..", "..", "..", "Templates");
            return Path.GetFullPath(templatesDir);
        }
    }
}