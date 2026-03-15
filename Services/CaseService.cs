using foamscript.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace foamscript.Services
{
    /// <summary>
    /// Service for OpenFOAM case management operations.
    /// </summary>
    public class CaseService
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly GeometryService _geometryService;
        private readonly TemplateService _templateService;
        private readonly TemplateMetadataService _metadataService;

        public CaseService(IProcessExecutor processExecutor, GeometryService geometryService,
            TemplateService templateService, TemplateMetadataService metadataService)
        {
            _processExecutor = processExecutor;
            _geometryService = geometryService;
            _templateService = templateService;
            _metadataService = metadataService;
        }

        /// <summary>
        /// Creates a new study with multiple cases for angle of attack sweep.
        /// </summary>
        public virtual StudyResult CreateStudy(StudyConfig config, string templatePath)
        {
            var result = new StudyResult();

            try
            {
                result.StudyName = config.ProjectName;

                // Avoid double nesting when output dir already ends with project name
                // e.g., --output-dir /run/MyStudy --project-name MyStudy → /run/MyStudy (not /run/MyStudy/MyStudy)
                var outputDirName = Path.GetFileName(Path.GetFullPath(config.OutputDir));
                result.StudyDir = string.Equals(outputDirName, config.ProjectName, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFullPath(config.OutputDir)
                    : Path.GetFullPath(Path.Combine(config.OutputDir, config.ProjectName));

                // Validate template exists
                if (!Directory.Exists(templatePath))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Template directory not found: {templatePath}";
                    return result;
                }

                // Load template metadata (falls back to disc defaults if TEMPLATE.json missing)
                var metadata = _metadataService.LoadMetadata(templatePath);

                // Parse angles
                var angles = ParseAngles(config.Angles);
                if (angles.Length == 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Invalid angles format: {config.Angles}. Use comma-separated values (e.g., -5,-2.5,0,2.5,5)";
                    return result;
                }

                // Create study directory
                Directory.CreateDirectory(result.StudyDir);

                // Create geometry directory for master geometry files
                var geometryDir = Path.Combine(result.StudyDir, "geometry");
                Directory.CreateDirectory(geometryDir);

                // Process geometry (convert and generate domain)
                var geometryResult = ProcessGeometry(geometryDir, config, metadata);
                if (!geometryResult.Success)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = geometryResult.ErrorMessage;
                    return result;
                }

                var refLength = geometryResult.RefLength;
                var bbox = geometryResult.BoundingBox!;
                var aref = TemplateMetadataService.CalculateReferenceArea(refLength, bbox, metadata);

                // Validate geometry dimensions from template metadata
                if (metadata.Validation != null)
                {
                    if ((metadata.Validation.MinSize.HasValue && refLength < metadata.Validation.MinSize.Value) ||
                        (metadata.Validation.MaxSize.HasValue && refLength > metadata.Validation.MaxSize.Value))
                    {
                        Console.WriteLine($"⚠ Warning: {metadata.Validation.WarningMessage}");
                    }
                }

                // Convert RPM to rad/s
                var omega = RpmToRadPerSec(config.Rpm);

                // Create case for each angle
                foreach (var angle in angles)
                {
                    var caseInfo = CreateCase(result.StudyDir, config.ProjectName, templatePath, angle,
                        config.Velocity, omega, config.Cores, geometryDir, refLength, aref,
                        metadata.RequiredStlFiles, metadata.RequiresRotorZone, config.Physics, config.Domain,
                        spanHalf: bbox.Height / 2.0);
                    result.Cases.Add(caseInfo);
                }

                // Write study manifest for downstream tools (e.g., ReportService)
                var manifest = new StudyManifest { TemplateName = metadata.Name };
                var manifestJson = JsonSerializer.Serialize(manifest,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(result.StudyDir, "study.json"), manifestJson);

                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Failed to create study: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Processes geometry: copies source file, converts if needed, generates domain.
        /// </summary>
        private (bool Success, string? ErrorMessage, double RefLength, BoundingBox? BoundingBox) ProcessGeometry(
            string geometryDir, StudyConfig config, TemplateMetadata metadata)
        {
            try
            {
                // Validate source file exists
                if (!File.Exists(config.ModelSource))
                {
                    return (false, $"Model source file not found: {config.ModelSource}", 0.0, null);
                }

                var sourceExt = Path.GetExtension(config.ModelSource).ToLowerInvariant();
                var sourceFileName = Path.GetFileName(config.ModelSource);
                var sourceCopyPath = Path.Combine(geometryDir, sourceFileName);

                // Copy source file to geometry directory
                File.Copy(config.ModelSource, sourceCopyPath, overwrite: true);

                var geomStlName = metadata.GeometryStlName;
                string geomStlPath;

                // If STEP or IGES, convert to STL
                if (sourceExt == ".step" || sourceExt == ".stp" || sourceExt == ".iges" || sourceExt == ".igs")
                {
                    geomStlPath = Path.Combine(geometryDir, geomStlName);

                    var conversionResult = _geometryService.ConvertStepToStl(
                        sourceCopyPath,
                        geomStlPath,
                        config.MeshSize,
                        config.FeatureAngle,
                        config.InputUnits
                    );

                    if (!conversionResult.IsSuccess)
                    {
                        return (false, $"Failed to convert geometry: {conversionResult.ErrorMessage}", 0.0, null);
                    }
                }
                else if (sourceExt == ".stl")
                {
                    // If already STL, copy to expected name
                    geomStlPath = Path.Combine(geometryDir, geomStlName);
                    if (sourceCopyPath != geomStlPath)
                    {
                        File.Copy(sourceCopyPath, geomStlPath, overwrite: true);
                    }
                }
                else
                {
                    return (false, $"Unsupported file format: {sourceExt}. Supported formats: .step, .stp, .iges, .igs, .stl", 0.0, null);
                }

                var domain = config.Domain;
                DomainGenerationResult domainResult;

                if (metadata.RequiresRotorZone)
                {
                    // Generate rotor and tunnel STL files
                    domainResult = _geometryService.GenerateDomain(
                        geomStlPath,
                        geometryDir,
                        rotorRadiusScale: domain.RotorRadiusScale,
                        rotorHeightScale: domain.RotorHeightScale,
                        tunnelUpstream: domain.TunnelUpstream,
                        tunnelDownstream: domain.TunnelDownstream,
                        tunnelRadial: domain.TunnelRadial,
                        meshResolution: domain.MeshResolution
                    );
                }
                else
                {
                    // Generate only tunnel (no rotor zone needed)
                    domainResult = _geometryService.GenerateTunnelOnly(
                        geomStlPath,
                        geometryDir,
                        tunnelUpstream: domain.TunnelUpstream,
                        tunnelDownstream: domain.TunnelDownstream,
                        tunnelRadial: domain.TunnelRadial
                    );
                }

                if (!domainResult.IsSuccess)
                {
                    return (false, $"Failed to generate domain: {domainResult.ErrorMessage}", 0.0, null);
                }

                // Extract reference dimension from bounding box using metadata rules
                var bbox = domainResult.DiscBoundingBox!;
                var refLength = TemplateMetadataService.CalculateReferenceDimension(bbox, metadata);

                return (true, null, refLength, bbox);
            }
            catch (Exception ex)
            {
                return (false, $"Geometry processing failed: {ex.Message}", 0.0, null);
            }
        }

        /// <summary>
        /// Creates a single case directory for a specific angle of attack.
        /// </summary>
        private CaseInfo CreateCase(string studyDir, string studyName, string templatePath,
            double angle, double velocity, double omega, int cores,
            string geometryDir, double refLength, double aref,
            string[] requiredStlFiles, bool requiresRotorZone,
            StudyPhysicsConfig physics, StudyDomainConfig domain,
            double spanHalf = 0.0)
        {
            var caseInfo = new CaseInfo
            {
                AngleOfAttack = angle,
                Omega = omega
            };

            // Create case directory: {studyDir}/{studyName}_{angle}/
            var caseName = $"{studyName}_{angle:F1}";
            var caseDir = Path.Combine(studyDir, caseName);
            caseInfo.CaseDir = caseDir;

            // Calculate velocity components for this angle
            var angleRad = angle * Math.PI / 180.0;
            caseInfo.Ux = velocity * Math.Cos(angleRad);
            caseInfo.Uz = velocity * Math.Sin(angleRad);

            // Calculate all template parameters
            var context = CalculateTemplateContext(caseInfo.Ux, caseInfo.Uz, omega, cores,
                refLength, aref, requiresRotorZone, physics, domain, spanHalf);

            // Process template with Scriban
            _templateService.ProcessTemplate(templatePath, caseDir, context);

            // Copy STL files from geometry directory
            CopyStlFiles(geometryDir, caseDir, requiredStlFiles);

            return caseInfo;
        }

        /// <summary>
        /// Calculates all template context parameters for Scriban rendering.
        /// </summary>
        internal static object CalculateTemplateContext(double ux, double uz, double omegaRotation,
            int cores, double refLength, double aref, bool requiresRotorZone,
            StudyPhysicsConfig physics, StudyDomainConfig domain,
            double spanHalf = 0.0)
        {
            const double cmu = 0.09; // Standard k-omega SST turbulence model constant

            var velocityMagnitude = Math.Sqrt(ux * ux + uz * uz);
            var k = 1.5 * Math.Pow(velocityMagnitude * physics.TurbulenceIntensity, 2);
            var mixingLength = 0.07 * refLength;
            var omegaTurbulence = Math.Sqrt(k) / (Math.Pow(cmu, 0.25) * mixingLength);

            // Domain extents in meters (with 10% margin beyond tunnel STL)
            const double margin = 1.1;
            var domainUpstream = domain.TunnelUpstream * refLength * margin;
            var domainDownstream = domain.TunnelDownstream * refLength * margin;
            var domainRadial = domain.TunnelRadial * refLength * margin;

            // Compute safe initial deltaT targeting Co ≈ 0.125 at finest refinement level
            var domainLength = domainUpstream + domainDownstream;
            var nxCells = Math.Ceiling(domainLength / refLength * 4);
            var baseCellSize = domainLength / nxCells;
            var fineCellSize = baseCellSize / Math.Pow(2, physics.RefinementLevelMax);
            var deltaT = 0.125 * fineCellSize / Math.Max(velocityMagnitude, 1.0);

            // Cap maxDeltaT
            var maxDeltaTFlow = physics.EndTime / 100.0;
            double maxDeltaT;

            if (requiresRotorZone)
            {
                // Mesh motion velocity at rotor boundary (rotor radius ≈ 1.5× geometry radius)
                var rotorRadius = refLength * 0.75;
                var meshMotionVelocity = Math.Abs(omegaRotation) * rotorRadius;
                var maxDeltaTMeshMotion = meshMotionVelocity > 0
                    ? 0.5 * fineCellSize / meshMotionVelocity
                    : maxDeltaTFlow;
                maxDeltaT = Math.Min(maxDeltaTFlow, maxDeltaTMeshMotion);
            }
            else
            {
                maxDeltaT = maxDeltaTFlow;
            }

            return new
            {
                ux = ux,
                uz = uz,
                p = 0,
                turbulence_intensity = physics.TurbulenceIntensity,
                k = k,
                cmu = cmu,
                disc_diameter = refLength,  // backward compat alias
                ref_length = refLength,
                mixing_length = mixingLength,
                omega_turbulence = omegaTurbulence,
                nu = physics.Nu,
                omega_rotation = omegaRotation,
                end_time = physics.EndTime,
                mag_u_inf = velocityMagnitude,
                n_outer_correctors = physics.NOuterCorrectors,
                refinement_level_min = physics.RefinementLevelMin,
                refinement_level_max = physics.RefinementLevelMax,
                feature_level = physics.RefinementLevelMax,
                cores = cores,
                aref = aref,
                domain_upstream = domainUpstream,
                domain_downstream = domainDownstream,
                domain_radial = domainRadial,
                delta_t = deltaT,
                max_delta_t = maxDeltaT,
                max_iterations = physics.MaxIterations,
                write_interval = physics.WriteInterval,
                nu_tilda = 3.0 * physics.Nu,  // Spalart-Allmaras initial value (~3x molecular viscosity)
                domain_span_half = spanHalf > 0 ? spanHalf : domainRadial  // Y half-extent for 2D domains
            };
        }

        /// <summary>
        /// Copies STL files to case constant/triSurface directory.
        /// </summary>
        private static void CopyStlFiles(string stlDir, string caseDir, string[] stlFileNames)
        {
            var triSurfaceDir = Path.Combine(caseDir, "constant", "triSurface");
            Directory.CreateDirectory(triSurfaceDir);

            foreach (var stlFile in stlFileNames)
            {
                var sourcePath = Path.Combine(stlDir, stlFile);
                if (File.Exists(sourcePath))
                {
                    var destPath = Path.Combine(triSurfaceDir, stlFile);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }
        }

        /// <summary>
        /// Parses comma-separated angles string into array of doubles.
        /// </summary>
        private static double[] ParseAngles(string anglesString)
        {
            try
            {
                return anglesString
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => double.Parse(s.Trim()))
                    .ToArray();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to parse angles '{anglesString}': {ex.Message}");
                return Array.Empty<double>();
            }
        }

        /// <summary>
        /// Converts RPM to radians per second.
        /// </summary>
        private static double RpmToRadPerSec(double rpm)
        {
            return rpm * 2.0 * Math.PI / 60.0;
        }
    }
}
