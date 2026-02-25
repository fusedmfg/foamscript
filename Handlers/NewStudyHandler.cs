using System.Text.Json;
using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class NewStudyHandler : ICommandHandler<NewStudyModel>
    {
        private readonly LoggingService _loggingService;
        private readonly CaseService _caseService;

        public NewStudyHandler(LoggingService loggingService, CaseService caseService)
        {
            _loggingService = loggingService;
            _caseService = caseService;
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
                        MaxIterations = model.MaxIterations,
                        WriteInterval = model.WriteInterval,
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

        /// <summary>
        /// Resolves a template path from either a short name or full path.
        /// </summary>
        private string ResolveTemplatePath(string? templatePathOrName)
        {
            var templatesDir = ListTemplatesHandler.GetTemplatesDirectory();

            // If null or empty, use default
            if (string.IsNullOrEmpty(templatePathOrName))
            {
                return Path.Combine(templatesDir, "external_disc_mrf_steady");
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
    }
}
