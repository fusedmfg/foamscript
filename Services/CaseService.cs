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

        public CaseService(IProcessExecutor processExecutor, GeometryService geometryService)
        {
            _processExecutor = processExecutor;
            _geometryService = geometryService;
        }

        /// <summary>
        /// Creates a new study with multiple cases for angle of attack sweep.
        /// </summary>
        public StudyResult CreateStudy(string outputDir, string templatePath, string modelSource,
            string anglesString, double velocity, double rpm, string inputUnits, double meshSize,
            double? featureAngle, int cores)
        {
            var result = new StudyResult();

            try
            {
                // Extract study name from output directory
                var studyName = Path.GetFileName(outputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                result.StudyName = studyName;
                result.StudyDir = Path.GetFullPath(outputDir);

                // Validate template exists
                if (!Directory.Exists(templatePath))
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Template directory not found: {templatePath}";
                    return result;
                }

                // Parse angles
                var angles = ParseAngles(anglesString);
                if (angles.Length == 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Invalid angles format: {anglesString}. Use comma-separated values (e.g., -5,-2.5,0,2.5,5)";
                    return result;
                }

                // Create study directory
                Directory.CreateDirectory(result.StudyDir);

                // Create geometry directory for master geometry files
                var geometryDir = Path.Combine(result.StudyDir, "geometry");
                Directory.CreateDirectory(geometryDir);

                // Process geometry (convert and generate domain)
                var geometryResult = ProcessGeometry(geometryDir, modelSource, inputUnits, meshSize, featureAngle);
                if (!geometryResult.Success)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = geometryResult.ErrorMessage;
                    return result;
                }

                // Convert RPM to rad/s
                var omega = RpmToRadPerSec(rpm);

                // Create case for each angle
                foreach (var angle in angles)
                {
                    var caseInfo = CreateCase(result.StudyDir, studyName, templatePath, angle, velocity, omega, cores, geometryDir);
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
        private (bool Success, string? ErrorMessage) ProcessGeometry(string geometryDir, string modelSource,
            string inputUnits, double meshSize, double? featureAngle)
        {
            try
            {
                // Validate source file exists
                if (!File.Exists(modelSource))
                {
                    return (false, $"Model source file not found: {modelSource}");
                }

                var sourceExt = Path.GetExtension(modelSource).ToLowerInvariant();
                var sourceFileName = Path.GetFileName(modelSource);
                var sourceCopyPath = Path.Combine(geometryDir, sourceFileName);

                // Copy source file to geometry directory
                File.Copy(modelSource, sourceCopyPath, overwrite: true);

                string discStlPath;

                // If STEP or IGES, convert to STL
                if (sourceExt == ".step" || sourceExt == ".stp" || sourceExt == ".iges" || sourceExt == ".igs")
                {
                    discStlPath = Path.Combine(geometryDir, "disc.stl");

                    var conversionResult = _geometryService.ConvertStepToStl(
                        sourceCopyPath,
                        discStlPath,
                        meshSize,
                        featureAngle,
                        inputUnits
                    );

                    if (!conversionResult.IsSuccess)
                    {
                        return (false, $"Failed to convert geometry: {conversionResult.ErrorMessage}");
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
                    return (false, $"Unsupported file format: {sourceExt}. Supported formats: .step, .stp, .iges, .igs, .stl");
                }

                // Generate rotor and tunnel STL files from disc using default parameters
                var domainResult = _geometryService.GenerateDomain(
                    discStlPath,
                    geometryDir,
                    rotorRadiusScale: 1.2,
                    rotorHeightScale: 1.5,
                    tunnelUpstream: 3.0,
                    tunnelDownstream: 7.0,
                    tunnelRadial: 4.0,
                    meshResolution: 32
                );
                if (!domainResult.IsSuccess)
                {
                    return (false, $"Failed to generate domain: {domainResult.ErrorMessage}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Geometry processing failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a single case directory for a specific angle of attack.
        /// </summary>
        private CaseInfo CreateCase(string studyDir, string studyName, string templatePath,
            double angle, double velocity, double omega, int cores, string geometryDir)
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

            // Copy template to case directory
            CopyDirectory(templatePath, caseDir);

            // Calculate velocity components for this angle
            var angleRad = angle * Math.PI / 180.0;
            caseInfo.Ux = velocity * Math.Cos(angleRad);
            caseInfo.Uy = velocity * Math.Sin(angleRad);

            // Update caseSettings file
            UpdateCaseSettings(caseDir, caseInfo.Ux, caseInfo.Uy, omega, cores);

            // Copy STL files from geometry directory
            CopyStlFiles(geometryDir, caseDir);

            return caseInfo;
        }

        /// <summary>
        /// Updates the constant/caseSettings file with calculated parameters.
        /// </summary>
        private void UpdateCaseSettings(string caseDir, double ux, double uy, double omega, int cores)
        {
            var caseSettingsPath = Path.Combine(caseDir, "constant", "caseSettings");

            if (!File.Exists(caseSettingsPath))
            {
                throw new FileNotFoundException($"caseSettings file not found: {caseSettingsPath}");
            }

            var lines = File.ReadAllLines(caseSettingsPath);
            var updatedLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Update velocity components
                if (trimmed.StartsWith("Ux "))
                {
                    updatedLines.Add($"Ux {ux:F6};");
                }
                else if (trimmed.StartsWith("Uy "))
                {
                    updatedLines.Add($"Uy {uy:F6};");
                }
                // Update rotation speed (rad/s)
                else if (trimmed.StartsWith("constant_dynamicMeshDict_omega "))
                {
                    updatedLines.Add($"constant_dynamicMeshDict_omega {omega:F6};");
                }
                // Update number of CPU cores
                else if (trimmed.StartsWith("system_decomposeParDict_numberOfSubdomains "))
                {
                    updatedLines.Add($"system_decomposeParDict_numberOfSubdomains {cores};");
                }
                else
                {
                    updatedLines.Add(line);
                }
            }

            File.WriteAllLines(caseSettingsPath, updatedLines);
        }

        /// <summary>
        /// Copies STL files to case constant/triSurface directory.
        /// </summary>
        private void CopyStlFiles(string stlDir, string caseDir)
        {
            var triSurfaceDir = Path.Combine(caseDir, "constant", "triSurface");
            Directory.CreateDirectory(triSurfaceDir);

            var stlFiles = new[] { "disc.stl", "rotor.stl", "tunnel.stl" };

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
        /// Recursively copies a directory.
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Create destination directory
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, overwrite: true);
            }

            // Recursively copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(subDir, destSubDir);
            }
        }

        /// <summary>
        /// Parses comma-separated angles string into array of doubles.
        /// </summary>
        private double[] ParseAngles(string anglesString)
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
        private double RpmToRadPerSec(double rpm)
        {
            return rpm * 2.0 * Math.PI / 60.0;
        }
    }
}
