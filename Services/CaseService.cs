using foamscript.Models;
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

        public CaseService(IProcessExecutor processExecutor, GeometryService geometryService, TemplateService templateService)
        {
            _processExecutor = processExecutor;
            _geometryService = geometryService;
            _templateService = templateService;
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
                result.StudyDir = Path.GetFullPath(Path.Combine(config.OutputDir, config.ProjectName));

                // Validate template exists
                if (!Directory.Exists(templatePath))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Template directory not found: {templatePath}";
                    return result;
                }

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
                var geometryResult = ProcessGeometry(geometryDir, config);
                if (!geometryResult.Success)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = geometryResult.ErrorMessage;
                    return result;
                }

                var discDiameter = geometryResult.DiscDiameter;

                // Convert RPM to rad/s
                var omega = RpmToRadPerSec(config.Rpm);

                // Create case for each angle
                foreach (var angle in angles)
                {
                    var caseInfo = CreateCase(result.StudyDir, config.ProjectName, templatePath, angle,
                        config.Velocity, omega, config.Cores, geometryDir, discDiameter, config.Physics, config.Domain);
                    result.Cases.Add(caseInfo);
                }

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
        private (bool Success, string? ErrorMessage, double DiscDiameter) ProcessGeometry(
            string geometryDir, StudyConfig config)
        {
            try
            {
                // Validate source file exists
                if (!File.Exists(config.ModelSource))
                {
                    return (false, $"Model source file not found: {config.ModelSource}", 0.0);
                }

                var sourceExt = Path.GetExtension(config.ModelSource).ToLowerInvariant();
                var sourceFileName = Path.GetFileName(config.ModelSource);
                var sourceCopyPath = Path.Combine(geometryDir, sourceFileName);

                // Copy source file to geometry directory
                File.Copy(config.ModelSource, sourceCopyPath, overwrite: true);

                string discStlPath;

                // If STEP or IGES, convert to STL
                if (sourceExt == ".step" || sourceExt == ".stp" || sourceExt == ".iges" || sourceExt == ".igs")
                {
                    discStlPath = Path.Combine(geometryDir, "disc.stl");

                    var conversionResult = _geometryService.ConvertStepToStl(
                        sourceCopyPath,
                        discStlPath,
                        config.MeshSize,
                        config.FeatureAngle,
                        config.InputUnits
                    );

                    if (!conversionResult.IsSuccess)
                    {
                        return (false, $"Failed to convert geometry: {conversionResult.ErrorMessage}", 0.0);
                    }
                }
                else if (sourceExt == ".stl")
                {
                    // If already STL, just rename/copy to disc.stl
                    discStlPath = Path.Combine(geometryDir, "disc.stl");
                    if (sourceCopyPath != discStlPath)
                    {
                        File.Copy(sourceCopyPath, discStlPath, overwrite: true);
                    }
                }
                else
                {
                    return (false, $"Unsupported file format: {sourceExt}. Supported formats: .step, .stp, .iges, .igs, .stl", 0.0);
                }

                var domain = config.Domain;

                // Generate rotor and tunnel STL files from disc
                var domainResult = _geometryService.GenerateDomain(
                    discStlPath,
                    geometryDir,
                    rotorRadiusScale: domain.RotorRadiusScale,
                    rotorHeightScale: domain.RotorHeightScale,
                    tunnelUpstream: domain.TunnelUpstream,
                    tunnelDownstream: domain.TunnelDownstream,
                    tunnelRadial: domain.TunnelRadial,
                    meshResolution: domain.MeshResolution
                );
                if (!domainResult.IsSuccess)
                {
                    return (false, $"Failed to generate domain: {domainResult.ErrorMessage}", 0.0);
                }

                // Extract disc diameter from bounding box
                var discDiameter = domainResult.DiscBoundingBox?.Diameter ?? 0.0;

                return (true, null, discDiameter);
            }
            catch (Exception ex)
            {
                return (false, $"Geometry processing failed: {ex.Message}", 0.0);
            }
        }

        /// <summary>
        /// Creates a single case directory for a specific angle of attack.
        /// </summary>
        private CaseInfo CreateCase(string studyDir, string studyName, string templatePath,
            double angle, double velocity, double omega, int cores,
            string geometryDir, double discDiameter, StudyPhysicsConfig physics, StudyDomainConfig domain)
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
            var context = CalculateTemplateContext(caseInfo.Ux, caseInfo.Uz, omega, cores, discDiameter, physics, domain);

            // Process template with Scriban
            _templateService.ProcessTemplate(templatePath, caseDir, context);

            // Copy STL files from geometry directory
            CopyStlFiles(geometryDir, caseDir);

            return caseInfo;
        }

        /// <summary>
        /// Calculates all template context parameters for Scriban rendering.
        /// </summary>
        private static object CalculateTemplateContext(double ux, double uz, double omegaRotation,
            int cores, double discDiameter, StudyPhysicsConfig physics, StudyDomainConfig domain)
        {
            const double cmu = 0.09; // Standard k-omega SST turbulence model constant

            var velocityMagnitude = Math.Sqrt(ux * ux + uz * uz);
            var k = 1.5 * Math.Pow(velocityMagnitude * physics.TurbulenceIntensity, 2);
            var mixingLength = 0.07 * discDiameter;
            var omegaTurbulence = Math.Sqrt(k) / (Math.Pow(cmu, 0.25) * mixingLength);

            // Reference area: pi * (diameter/2)^2
            var aref = Math.PI * Math.Pow(discDiameter / 2.0, 2);

            // Domain extents in meters (with 10% margin beyond tunnel STL)
            const double margin = 1.1;
            var domainUpstream = domain.TunnelUpstream * discDiameter * margin;
            var domainDownstream = domain.TunnelDownstream * discDiameter * margin;
            var domainRadial = domain.TunnelRadial * discDiameter * margin;

            // Compute safe initial deltaT targeting Co ≈ 0.125 at finest refinement level
            // (4× safety factor — snappyHexMesh creates smaller cells than theoretical estimate)
            var domainLength = domainUpstream + domainDownstream;
            var nxCells = Math.Ceiling(domainLength / discDiameter * 4);
            var baseCellSize = domainLength / nxCells;
            var fineCellSize = baseCellSize / Math.Pow(2, physics.RefinementLevelMax);
            var deltaT = 0.125 * fineCellSize / Math.Max(velocityMagnitude, 1.0);

            // Cap maxDeltaT: constrained by both flow time and mesh motion Courant number
            // Mesh motion velocity at rotor boundary (rotor radius ≈ 1.5× disc radius)
            var rotorRadius = discDiameter * 0.75;
            var meshMotionVelocity = Math.Abs(omegaRotation) * rotorRadius;
            var maxDeltaTFlow = physics.EndTime / 100.0;
            var maxDeltaTMeshMotion = meshMotionVelocity > 0
                ? 0.5 * fineCellSize / meshMotionVelocity
                : maxDeltaTFlow;
            var maxDeltaT = Math.Min(maxDeltaTFlow, maxDeltaTMeshMotion);

            return new
            {
                ux = ux,
                uz = uz,
                p = 0,
                turbulence_intensity = physics.TurbulenceIntensity,
                k = k,
                cmu = cmu,
                disc_diameter = discDiameter,
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
                write_interval = physics.WriteInterval
            };
        }

        /// <summary>
        /// Copies STL files to case constant/triSurface directory.
        /// </summary>
        private static void CopyStlFiles(string stlDir, string caseDir)
        {
            var triSurfaceDir = Path.Combine(caseDir, "constant", "triSurface");
            Directory.CreateDirectory(triSurfaceDir);

            var stlFiles = new[] { "disc.stl", "tunnel.stl" };

            foreach (var stlFile in stlFiles)
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
            catch
            {
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
