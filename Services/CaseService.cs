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

                // Copy domain config from template metadata (margin, spanRatio)
                // These may already be set by the handler from TEMPLATE.json, but for JSON config
                // path (which skips the handler's template defaults), apply them from metadata.
                if (config.Domain.Margin == 1.1 && metadata.Domain.Margin != 1.1)
                    config.Domain.Margin = metadata.Domain.Margin;
                config.Domain.SpanRatio ??= metadata.Domain.SpanRatio;

                // Calculate spanHalf: if template specifies spanRatio (2D case), use it; else 0 (3D, uses radial)
                var spanHalf = metadata.Domain.SpanRatio.HasValue
                    ? bbox.Height / 2.0 * metadata.Domain.SpanRatio.Value
                    : 0.0;

                // Create case for each angle
                foreach (var angle in angles)
                {
                    var caseInfo = CreateCase(result.StudyDir, config.ProjectName, templatePath, angle,
                        config.Velocity, omega, config.Cores, geometryDir, refLength, aref,
                        metadata.RequiredStlFiles, config.Physics, config.Domain,
                        spanHalf: spanHalf);
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
        /// Processes geometry: accepts STL directly or auto-converts STEP/IGES to STL.
        /// STEP/IGES files are converted using gmsh with the config's modelSourceUnits (default: mm → meters).
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
                var geomStlName = metadata.GeometryStlName;
                var geomStlPath = Path.Combine(geometryDir, geomStlName);

                // Auto-convert STEP/IGES files to STL
                if (sourceExt == ".step" || sourceExt == ".stp" || sourceExt == ".iges" || sourceExt == ".igs")
                {
                    Console.WriteLine($"  Converting {Path.GetFileName(config.ModelSource)} ({config.ModelSourceUnits}) → STL (meters)...");

                    var conversionResult = _geometryService.ConvertStepToStl(
                        config.ModelSource, geomStlPath, config.MeshSize, config.ModelSourceUnits);

                    if (!conversionResult.IsSuccess)
                    {
                        return (false, $"STEP/IGES conversion failed: {conversionResult.ErrorMessage}", 0.0, null);
                    }

                    Console.WriteLine($"  ✓ Converted to {geomStlName}");
                    if (conversionResult.NodeCount.HasValue && conversionResult.TriangleCount.HasValue)
                    {
                        Console.WriteLine($"    Nodes: {conversionResult.NodeCount:N0}, Triangles: {conversionResult.TriangleCount:N0}");
                    }
                }
                else if (sourceExt == ".stl")
                {
                    // Copy STL directly to geometry directory
                    File.Copy(config.ModelSource, geomStlPath, overwrite: true);
                }
                else
                {
                    return (false, $"Unsupported file format: {sourceExt}. Accepted formats: .stl, .step, .stp, .iges, .igs", 0.0, null);
                }

                // Extract bounding box from the geometry STL
                var bbox = GeometryService.CalculateBoundingBox(geomStlPath);
                if (bbox == null)
                {
                    return (false, $"Failed to parse geometry STL bounding box: {geomStlPath}", 0.0, null);
                }

                // Extract reference dimension from bounding box using metadata rules
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
            string[] requiredStlFiles,
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
                refLength, aref, physics, domain, spanHalf);

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
            int cores, double refLength, double aref,
            StudyPhysicsConfig physics, StudyDomainConfig domain,
            double spanHalf = 0.0)
        {
            // Universal turbulence model constants — NOT template-parameterized.
            // cmu is the standard eddy-viscosity constant used by k-omega SST, k-epsilon, and Spalart-Allmaras.
            // TKE formula k = 1.5*(U*TI)² is the definition of turbulent kinetic energy from intensity.
            // These are physics definitions, not tunable knobs.
            const double cmu = 0.09;

            var nu = physics.Nu ?? 1.5e-5;
            var turbulenceIntensity = physics.TurbulenceIntensity ?? 0.01;
            var endTime = physics.EndTime ?? 1.0;
            var nOuterCorrectors = physics.NOuterCorrectors ?? 1;
            var maxIterations = physics.MaxIterations ?? 500;
            var writeInterval = physics.WriteInterval ?? 100;

            var velocityMagnitude = Math.Sqrt(ux * ux + uz * uz);
            var k = 1.5 * Math.Pow(velocityMagnitude * turbulenceIntensity, 2);
            var mixingLength = physics.MixingLengthRatio * refLength;
            var omegaTurbulence = Math.Sqrt(k) / (Math.Pow(cmu, 0.25) * mixingLength);

            // Domain extents in meters (with configurable margin)
            var margin = domain.Margin;
            var domainUpstream = domain.TunnelUpstream * refLength * margin;
            var domainDownstream = domain.TunnelDownstream * refLength * margin;
            var domainRadial = domain.TunnelRadial * refLength * margin;

            return new
            {
                ux = ux,
                uz = uz,
                p = 0,
                turbulence_intensity = turbulenceIntensity,
                k = k,
                cmu = cmu,
                disc_diameter = refLength,  // backward compat alias
                ref_length = refLength,
                mixing_length = mixingLength,
                omega_turbulence = omegaTurbulence,
                nu = nu,
                omega_rotation = omegaRotation,
                end_time = endTime,
                mag_u_inf = velocityMagnitude,
                n_outer_correctors = nOuterCorrectors,
                refinement_level_min = physics.RefinementLevelMin ?? 0,
                refinement_level_max = physics.RefinementLevelMax ?? 0,
                feature_level = physics.RefinementLevelMax ?? 0,
                cores = cores,
                aref = aref,
                domain_upstream = domainUpstream,
                domain_downstream = domainDownstream,
                domain_radial = domainRadial,
                max_iterations = maxIterations,
                write_interval = writeInterval,
                nu_tilda = physics.NuTildaMultiplier * nu,  // Spalart-Allmaras initial value
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
