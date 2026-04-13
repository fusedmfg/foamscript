using System.Text.Json;
using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class NewStudyHandler : ICommandHandler<NewStudyModel>
    {
        private readonly LoggingService _loggingService;
        private readonly CaseService _caseService;
        private readonly TemplateMetadataService _metadataService;
        private readonly MeshHandler _meshHandler;
        private readonly SolveHandler _solveHandler;
        private readonly ReportHandler _reportHandler;

        public NewStudyHandler(LoggingService loggingService, CaseService caseService,
            TemplateMetadataService metadataService, MeshHandler meshHandler,
            SolveHandler solveHandler, ReportHandler reportHandler)
        {
            _loggingService = loggingService;
            _caseService = caseService;
            _metadataService = metadataService;
            _meshHandler = meshHandler;
            _solveHandler = solveHandler;
            _reportHandler = reportHandler;
        }

        public int Handle(NewStudyModel model)
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
                if (string.IsNullOrEmpty(model.TemplatePath)) missing.Add("--template (-t)");
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
                    Velocity = model.Velocity ?? 0,
                    Rpm = model.Rpm ?? 0,
                    ModelSourceUnits = model.ModelSourceUnits ?? "mm",
                    MeshSize = model.MeshSize ?? 1.0,
                    Cores = model.Cores,
                    Physics = new StudyPhysicsConfig
                    {
                        Nu = model.Nu,
                        TurbulenceIntensity = model.TurbulenceIntensity,
                        EndTime = model.EndTime,
                        NOuterCorrectors = model.NOuterCorrectors,
                        MaxIterations = model.MaxIterations,
                        WriteInterval = model.WriteInterval,
                        RefinementLevelMin = model.RefinementLevelMin,
                        RefinementLevelMax = model.RefinementLevelMax,
                    },
                    Domain = new StudyDomainConfig
                    {
                        TunnelUpstream = model.TunnelUpstream,
                        TunnelDownstream = model.TunnelDownstream,
                        TunnelRadial = model.TunnelRadial,
                    },
                };
            }

            // Resolve cores (0 = auto-detect)
            var requestedCores = config.Cores;
            var (resolvedCores, _) = CoreResolver.Resolve(config.Cores);
            config.Cores = resolvedCores;

            // Pre-template validation (non-nullable params only)
            var validationErrors = new List<string>();
            if (config.Velocity <= 0) validationErrors.Add($"Velocity must be positive (got {config.Velocity})");
            if (config.Physics.RefinementLevelMin.HasValue && config.Physics.RefinementLevelMax.HasValue &&
                config.Physics.RefinementLevelMin.Value > config.Physics.RefinementLevelMax.Value)
                validationErrors.Add($"Refinement min ({config.Physics.RefinementLevelMin}) cannot exceed max ({config.Physics.RefinementLevelMax})");
            if (config.Rpm < 0) validationErrors.Add($"RPM must be non-negative (got {config.Rpm})");

            if (validationErrors.Count > 0)
            {
                Console.WriteLine("✗ Invalid parameter values:");
                foreach (var err in validationErrors)
                    Console.WriteLine($"    {err}");
                Console.WriteLine();
                return -1;
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
                var availableTemplatesDir = ListTemplatesHandler.GetTemplatesDirectory();
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

            // Load template metadata and apply defaults for null config values
            try
            {
                var metadata = _metadataService.LoadMetadata(templatePath);

                // Validate template-required parameters (only for CLI args, not JSON config)
                if (model.ConfigFile == null)
                {
                    var templateErrors = new List<string>();
                    foreach (var (paramName, paramDef) in metadata.Parameters)
                    {
                        if (paramDef.Required)
                        {
                            var isMissing = paramName.ToLowerInvariant() switch
                            {
                                "velocity" => model.Velocity == null,
                                "rpm" => model.Rpm == null,
                                "angles" => string.IsNullOrEmpty(model.Angles),
                                _ => false
                            };

                            if (isMissing)
                            {
                                templateErrors.Add($"Template '{metadata.Name}' requires --{paramName} but it was not provided");
                            }
                        }
                    }

                    if (templateErrors.Count > 0)
                    {
                        Console.WriteLine("✗ Missing required template parameters:");
                        foreach (var err in templateErrors)
                            Console.WriteLine($"    {err}");
                        Console.WriteLine();
                        return -1;
                    }
                }

                // Apply template defaults for optional parameters when config value is null (both CLI and JSON paths)
                foreach (var (paramName, paramDef) in metadata.Parameters)
                {
                    if (paramDef.Default != null)
                    {
                        switch (paramName.ToLowerInvariant())
                        {
                            case "velocity" when config.Velocity == 0:
                                config.Velocity = GetDoubleFromDefault(paramDef.Default);
                                break;
                            case "rpm" when config.Rpm == 0:
                                config.Rpm = GetDoubleFromDefault(paramDef.Default);
                                break;
                            case "refinementmin" when config.Physics.RefinementLevelMin == null:
                                config.Physics.RefinementLevelMin = GetIntFromDefault(paramDef.Default);
                                break;
                            case "refinementmax" when config.Physics.RefinementLevelMax == null:
                                config.Physics.RefinementLevelMax = GetIntFromDefault(paramDef.Default);
                                break;
                            case "nu" when config.Physics.Nu == null:
                                config.Physics.Nu = GetDoubleFromDefault(paramDef.Default);
                                break;
                            case "turbulenceintensity" when config.Physics.TurbulenceIntensity == null:
                                config.Physics.TurbulenceIntensity = GetDoubleFromDefault(paramDef.Default);
                                break;
                            case "endtime" when config.Physics.EndTime == null:
                                config.Physics.EndTime = GetDoubleFromDefault(paramDef.Default);
                                break;
                            case "noutercorrectors" when config.Physics.NOuterCorrectors == null:
                                config.Physics.NOuterCorrectors = GetIntFromDefault(paramDef.Default);
                                break;
                            case "maxiterations" when config.Physics.MaxIterations == null:
                                config.Physics.MaxIterations = GetIntFromDefault(paramDef.Default);
                                break;
                            case "writeinterval" when config.Physics.WriteInterval == null:
                                config.Physics.WriteInterval = GetIntFromDefault(paramDef.Default);
                                break;
                            case "mixinglengthratio":
                                config.Physics.MixingLengthRatio = GetDoubleFromDefault(paramDef.Default);
                                break;
                            case "nutildamultiplier":
                                config.Physics.NuTildaMultiplier = GetDoubleFromDefault(paramDef.Default);
                                break;
                        }
                    }
                }

                // Copy domain config from template metadata (margin and spanRatio)
                config.Domain.Margin = metadata.Domain.Margin;
                config.Domain.SpanRatio = metadata.Domain.SpanRatio;

                // Check STL bounding box against template validation rules
                if (config.ModelSource != null)
                {
                    var scaleWarning = GeometryService.CheckGeometryScale(config.ModelSource, metadata.Validation);
                    if (scaleWarning != null)
                    {
                        Console.WriteLine();
                        Console.WriteLine(GeometryService.FormatScaleWarning(scaleWarning));
                        Console.WriteLine();
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // Template has no TEMPLATE.json — skip parameter validation
            }

            // Post-template validation — nullable physics params must be resolved by now
            var postValidationErrors = new List<string>();
            if (config.Physics.Nu == null) postValidationErrors.Add("Kinematic viscosity (nu) was not specified and template has no default");
            else if (config.Physics.Nu <= 0) postValidationErrors.Add($"Kinematic viscosity (nu) must be positive (got {config.Physics.Nu})");
            if (config.Physics.TurbulenceIntensity == null) postValidationErrors.Add("Turbulence intensity was not specified and template has no default");
            if (config.Physics.EndTime == null) postValidationErrors.Add("End time was not specified and template has no default");
            if (config.Physics.NOuterCorrectors == null) postValidationErrors.Add("Outer correctors was not specified and template has no default");
            if (config.Physics.MaxIterations == null) postValidationErrors.Add("Max iterations was not specified and template has no default");
            if (config.Physics.WriteInterval == null) postValidationErrors.Add("Write interval was not specified and template has no default");

            if (postValidationErrors.Count > 0)
            {
                Console.WriteLine("✗ Missing physics parameters (not specified and no template default):");
                foreach (var err in postValidationErrors)
                    Console.WriteLine($"    {err}");
                Console.WriteLine();
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
            Console.WriteLine($"Cores:            {config.Cores} ({(requestedCores == 0 ? "auto-detected" : "specified")})");
            Console.WriteLine();
            Console.WriteLine("Geometry / Domain Parameters:");
            var sourceExt = Path.GetExtension(config.ModelSource ?? "").ToLowerInvariant();
            if (sourceExt == ".step" || sourceExt == ".stp" || sourceExt == ".iges" || sourceExt == ".igs")
            {
                Console.WriteLine($"  Source units:       {config.ModelSourceUnits} (will convert to meters)");
                Console.WriteLine($"  Mesh size:          {config.MeshSize}");
            }
            Console.WriteLine($"  Tunnel upstream:    {config.Domain.TunnelUpstream} D");
            Console.WriteLine($"  Tunnel downstream:  {config.Domain.TunnelDownstream} D");
            Console.WriteLine($"  Tunnel radial:      {config.Domain.TunnelRadial} D");
            Console.WriteLine();
            Console.WriteLine("Physics Parameters:");
            Console.WriteLine($"  nu:                   {config.Physics.Nu!.Value:E2} m²/s");
            Console.WriteLine($"  Turbulence intensity: {config.Physics.TurbulenceIntensity!.Value * 100:F0}%");
            Console.WriteLine($"  End time:             {config.Physics.EndTime!.Value} s");
            Console.WriteLine($"  Outer correctors:     {config.Physics.NOuterCorrectors!.Value}");
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
                    Console.WriteLine($"      Velocity: Ux={caseInfo.Ux:F3} m/s, Uz={caseInfo.Uz:F3} m/s");
                    Console.WriteLine($"      Omega: {caseInfo.Omega:F3} rad/s ({config.Rpm} RPM)");
                }

                _loggingService.LogInformation("Study creation completed successfully.");
                Console.WriteLine();

                // Execute pipeline stages if --run flag is set (CLI or config)
                if (model.Run || config.Run)
                {
                    return ExecutePipeline(result.StudyDir!, config.Cores, templatePath,
                        model.SkipReport || config.SkipReport);
                }

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
        /// Executes the template-defined pipeline stages (mesh → solve → report) after study creation.
        /// </summary>
        private int ExecutePipeline(string studyDir, int cores, string templatePath, bool skipReport)
        {
            var metadata = _metadataService.LoadMetadata(templatePath);
            var stages = metadata.Pipeline;

            var activeStages = skipReport
                ? stages.Where(s => s != "report").ToList()
                : stages;
            int totalStages = activeStages.Count;
            int stageNum = 0;

            Console.WriteLine($"=== Running Pipeline: {string.Join(" → ", activeStages)} ===");
            Console.WriteLine();

            foreach (var stage in stages)
            {
                if (stage == "report" && skipReport)
                {
                    Console.WriteLine("  Skipping stage: report (--skip-report)");
                    continue;
                }

                stageNum++;
                Console.WriteLine($"=== Pipeline Stage {stageNum}/{totalStages}: {stage} ===");
                Console.WriteLine();

                int stageResult = stage switch
                {
                    "mesh" => _meshHandler.Handle(new MeshModel { Dir = studyDir, Cores = cores }),
                    "solve" => _solveHandler.Handle(new SolveModel { Dir = studyDir, Cores = cores }),
                    "report" => _reportHandler.Handle(new ReportModel { Dir = studyDir }),
                    _ => throw new NotSupportedException($"Unknown pipeline stage '{stage}' in template. Valid stages: mesh, solve, report")
                };

                if (stageResult != 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"✗ Pipeline failed at stage: {stage}");
                    _loggingService.LogError($"Pipeline failed at stage: {stage}", null!);
                    return stageResult;
                }

                Console.WriteLine();
            }

            Console.WriteLine("=== Pipeline Complete ===");
            Console.WriteLine($"Study directory: {studyDir}");
            Console.WriteLine();
            return 0;
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

                // Backward compat: map old "inputUnits" field to ModelSourceUnits
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                if (doc.RootElement.TryGetProperty("inputUnits", out var inputUnitsEl) &&
                    inputUnitsEl.ValueKind == JsonValueKind.String)
                {
                    config.ModelSourceUnits = inputUnitsEl.GetString() ?? "mm";
                }

                return (config, null);
            }
            catch (JsonException ex)
            {
                return (null, $"Invalid JSON in config file '{path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves a template path from either a short name or full path.
        /// </summary>
        private string ResolveTemplatePath(string? templatePathOrName)
        {
            var templatesDir = ListTemplatesHandler.GetTemplatesDirectory();

            // If null or empty, use default (JSON config path may not specify template)
            if (string.IsNullOrEmpty(templatePathOrName))
            {
                return Path.Combine(templatesDir, "external_disc_rotatingwall_steady");
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
        /// Extracts a double from a TEMPLATE.json default value (may be JsonElement or boxed number).
        /// </summary>
        private static double GetDoubleFromDefault(object? value)
        {
            if (value is JsonElement je)
                return je.GetDouble();
            return Convert.ToDouble(value);
        }

        /// <summary>
        /// Extracts an int from a TEMPLATE.json default value (may be JsonElement or boxed number).
        /// </summary>
        private static int GetIntFromDefault(object? value)
        {
            if (value is JsonElement je)
                return je.GetInt32();
            return Convert.ToInt32(value);
        }
    }
}
