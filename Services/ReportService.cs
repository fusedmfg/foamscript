using foamscript.Models;

namespace foamscript.Services
{
    /// <summary>
    /// Orchestrates data collection, chart generation, and report rendering.
    /// Coordinates between ResultsService, ResidualParser, ChartGenerator,
    /// HtmlReportGenerator, and PdfReportGenerator.
    /// </summary>
    public class ReportService
    {
        private readonly ResultsService _resultsService;
        private readonly ChartGenerator _chartGenerator;
        private readonly HtmlReportGenerator _htmlGenerator;
        private readonly PdfReportGenerator _pdfGenerator;
        private readonly VisualizationService _visualizationService;

        public ReportService(
            ResultsService resultsService,
            ChartGenerator chartGenerator,
            HtmlReportGenerator htmlGenerator,
            PdfReportGenerator pdfGenerator,
            VisualizationService visualizationService)
        {
            _resultsService = resultsService;
            _chartGenerator = chartGenerator;
            _htmlGenerator = htmlGenerator;
            _pdfGenerator = pdfGenerator;
            _visualizationService = visualizationService;
        }

        /// <summary>
        /// Generates a complete analysis report for a study.
        /// </summary>
        public ReportResult GenerateReport(string studyDir, string outputDir, string format, double averageWindow)
        {
            var result = new ReportResult();
            var studyName = Path.GetFileName(studyDir);

            Console.WriteLine($"Generating report for study: {studyName}");
            Console.WriteLine();

            // Step 1: Extract aerodynamic coefficients
            Console.WriteLine("Extracting aerodynamic coefficients...");
            var resultsSummary = _resultsService.ExtractResults(studyDir, averageWindow);
            if (!resultsSummary.IsSuccess || resultsSummary.Cases.Count == 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = resultsSummary.ErrorMessage ?? "No results data found";
                return result;
            }
            Console.WriteLine($"\u2713 Found {resultsSummary.Cases.Count} case(s)");

            // Step 2: Parse residual data from solver logs
            Console.WriteLine("Parsing convergence data...");
            var residualData = CollectResidualData(studyDir, resultsSummary.Cases);
            Console.WriteLine($"\u2713 Parsed residual data for {residualData.Count} case(s)");

            // Step 3: Collect mesh statistics
            Console.WriteLine("Collecting mesh statistics...");
            var meshStats = CollectMeshStats(studyDir, resultsSummary.Cases);

            // Step 4: Read physics config
            var physicsConfig = ReadPhysicsConfig(studyDir);

            // Step 5: Generate flow field visualizations (gracefully skipped if unavailable)
            Console.WriteLine("Generating flow visualizations...");
            SliceVisualizationResult? visualization = null;
            var firstCaseDir = Path.Combine(studyDir, resultsSummary.Cases[0].CaseName);
            var vizOutputDir = Path.Combine(outputDir, "viz");
            try
            {
                visualization = _visualizationService.GenerateSliceVisualization(firstCaseDir, vizOutputDir);
                if (visualization != null)
                    Console.WriteLine("\u2713 Flow visualizations generated");
                else
                    Console.WriteLine("  Skipped (tools unavailable)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Warning: Flow visualization failed: {ex.Message}");
            }

            // Step 6: Generate charts, data exports, and render reports
            Directory.CreateDirectory(outputDir);

            // Always export AIAA-standard CSV data artifact
            Console.WriteLine("Exporting coefficient data...");
            var csvPath = Path.Combine(outputDir, $"{studyName}_coefficients.csv");
            WriteCoefficientCsv(csvPath, studyName, resultsSummary, physicsConfig);
            result.CsvPath = csvPath;
            Console.WriteLine($"\u2713 CSV data: {csvPath}");

            var generateHtml = format is "html" or "both";
            var generatePdf = format is "pdf" or "both";

            if (generateHtml)
            {
                Console.WriteLine("Generating HTML report...");
                var htmlData = BuildHtmlReportData(studyDir, studyName, resultsSummary, residualData, meshStats, physicsConfig, visualization);
                var htmlPath = Path.Combine(outputDir, $"{studyName}_report.html");
                _htmlGenerator.WriteReport(htmlData, htmlPath);
                result.HtmlPath = htmlPath;
                Console.WriteLine($"\u2713 HTML report: {htmlPath}");
            }

            if (generatePdf)
            {
                Console.WriteLine("Generating PDF report...");
                var pdfData = BuildPdfReportData(studyName, resultsSummary, residualData, meshStats, physicsConfig, visualization);
                var pdfPath = Path.Combine(outputDir, $"{studyName}_report.pdf");
                _pdfGenerator.WriteReport(pdfData, pdfPath);
                result.PdfPath = pdfPath;
                Console.WriteLine($"\u2713 PDF report: {pdfPath}");
            }

            result.IsSuccess = true;
            return result;
        }

        private ReportData BuildHtmlReportData(string studyDir, string studyName, ResultsSummary summary,
            List<CaseResidualData> residuals, List<MeshStatEntry> meshStats, PhysicsConfig config,
            SliceVisualizationResult? visualization)
        {
            var cases = summary.Cases;
            var data = new ReportData
            {
                StudyName = studyName,
                GenerationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                TemplateName = ReadTemplateName(studyDir),
                Velocity = config.Velocity,
                Rpm = config.Rpm,
                TurbulenceIntensity = config.TurbulenceIntensity,
                Nu = config.Nu,
                SolverName = config.SolverName,
                MaxIterations = config.MaxIterations,
                RefinementMin = config.RefinementMin,
                RefinementMax = config.RefinementMax,
                MeshStats = meshStats,
                Cases = cases.Select(c => new CaseResultEntry
                {
                    Aoa = $"{c.AngleOfAttack:F1}",
                    Cd = c.Cd.HasValue ? $"{c.Cd.Value:F6}" : "N/A",
                    Cl = c.Cl.HasValue ? $"{c.Cl.Value:F6}" : "N/A",
                    Cm = c.CmPitch.HasValue ? $"{c.CmPitch.Value:F6}" : "N/A",
                    Ld = c.Cd > 0 ? $"{(c.Cl ?? 0) / c.Cd.Value:F2}" : "N/A",
                    Converged = c.Converged
                }).ToList(),

                // SVG charts
                ClVsAoaChart = _chartGenerator.GeneratePolarChartSvg(cases, "Cl", "Lift Coefficient vs Angle of Attack", "Lift Coefficient, C\u2097"),
                CdVsAoaChart = _chartGenerator.GeneratePolarChartSvg(cases, "Cd", "Drag Coefficient vs Angle of Attack", "Drag Coefficient, C\u2084"),
                CmVsAoaChart = _chartGenerator.GeneratePolarChartSvg(cases, "CmPitch", "Pitching Moment vs Angle of Attack", "Pitching Moment, C\u2098"),
                LdVsAoaChart = _chartGenerator.GeneratePolarChartSvg(cases, "L/D", "Lift-to-Drag Ratio vs Angle of Attack", "L/D"),
                DragPolarChart = _chartGenerator.GenerateDragPolarSvg(cases),

                ConvergenceCharts = residuals.Select(r => new ConvergenceChartEntry
                {
                    CaseName = r.CaseName,
                    Chart = _chartGenerator.GenerateResidualChartSvg(r)
                }).ToList()
            };

            // Add visualization data (base64-encoded for HTML <img> embedding)
            if (visualization != null)
            {
                data.HasVisualization = true;
                if (visualization.PressureSlicePng != null)
                    data.PressureSliceImage = "data:image/png;base64," + Convert.ToBase64String(visualization.PressureSlicePng);
                if (visualization.VelocitySlicePng != null)
                    data.VelocitySliceImage = "data:image/png;base64," + Convert.ToBase64String(visualization.VelocitySlicePng);
            }

            return data;
        }

        private PdfReportData BuildPdfReportData(string studyName, ResultsSummary summary,
            List<CaseResidualData> residuals, List<MeshStatEntry> meshStats, PhysicsConfig config,
            SliceVisualizationResult? visualization)
        {
            var cases = summary.Cases;
            return new PdfReportData
            {
                StudyName = studyName,
                GenerationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Velocity = config.Velocity,
                Rpm = config.Rpm,
                TurbulenceIntensity = config.TurbulenceIntensity,
                Nu = config.Nu,
                SolverName = config.SolverName,
                MaxIterations = config.MaxIterations,
                RefinementMin = config.RefinementMin,
                RefinementMax = config.RefinementMax,

                MeshStats = meshStats.Select(m => new PdfMeshStatEntry
                {
                    CaseName = m.CaseName,
                    Aoa = m.Aoa,
                    Cells = m.Cells,
                    Points = m.Points,
                    Faces = m.Faces,
                    QualityPassed = m.QualityPassed
                }).ToList(),

                PolarCharts = new List<byte[]>
                {
                    _chartGenerator.GeneratePolarChartPng(cases, "Cl", "Lift Coefficient vs Angle of Attack", "Lift Coefficient, C\u2097"),
                    _chartGenerator.GeneratePolarChartPng(cases, "Cd", "Drag Coefficient vs Angle of Attack", "Drag Coefficient, C\u2084"),
                    _chartGenerator.GeneratePolarChartPng(cases, "CmPitch", "Pitching Moment vs Angle of Attack", "Pitching Moment, C\u2098"),
                    _chartGenerator.GeneratePolarChartPng(cases, "L/D", "Lift-to-Drag Ratio vs Angle of Attack", "L/D"),
                },
                DragPolarChart = _chartGenerator.GenerateDragPolarPng(cases),

                Cases = cases.Select(c => new CaseResultEntry
                {
                    Aoa = $"{c.AngleOfAttack:F1}",
                    Cd = c.Cd.HasValue ? $"{c.Cd.Value:F6}" : "N/A",
                    Cl = c.Cl.HasValue ? $"{c.Cl.Value:F6}" : "N/A",
                    Cm = c.CmPitch.HasValue ? $"{c.CmPitch.Value:F6}" : "N/A",
                    Ld = c.Cd > 0 ? $"{(c.Cl ?? 0) / c.Cd.Value:F2}" : "N/A",
                    Converged = c.Converged
                }).ToList(),

                ConvergenceCharts = residuals.Select(r => new PdfConvergenceEntry
                {
                    CaseName = r.CaseName,
                    ChartPng = _chartGenerator.GenerateResidualChartPng(r)
                }).ToList(),

                PressureSlicePng = visualization?.PressureSlicePng,
                VelocitySlicePng = visualization?.VelocitySlicePng
            };
        }

        private static List<CaseResidualData> CollectResidualData(string studyDir, List<CaseResult> cases)
        {
            var residuals = new List<CaseResidualData>();

            foreach (var caseResult in cases)
            {
                var caseDir = Path.Combine(studyDir, caseResult.CaseName);
                var logFile = ResidualParser.FindLogFile(caseDir);

                var data = new CaseResidualData
                {
                    CaseName = caseResult.CaseName,
                    CaseDir = caseDir
                };

                if (logFile != null)
                {
                    data.Entries = ResidualParser.ParseLogFile(logFile);
                    if (data.Entries.Count == 0)
                        Console.Error.WriteLine($"  Warning: Solver log found at {logFile} but contains no residual data");
                }
                else
                {
                    Console.Error.WriteLine($"  Warning: No solver log found in {caseDir} — convergence chart will be empty");
                }

                residuals.Add(data);
            }

            return residuals;
        }

        private static List<MeshStatEntry> CollectMeshStats(string studyDir, List<CaseResult> cases)
        {
            var stats = new List<MeshStatEntry>();

            foreach (var caseResult in cases)
            {
                var caseDir = Path.Combine(studyDir, caseResult.CaseName);
                var aoa = CaseDiscovery.ParseAngleFromCaseName(caseResult.CaseName);

                // Try to read mesh stats from checkMesh log
                var checkMeshLog = Path.Combine(caseDir, "log.checkMesh");
                int? cells = null, points = null, faces = null;
                bool qualityPassed = true;

                if (File.Exists(checkMeshLog))
                {
                    var content = File.ReadAllText(checkMeshLog);
                    cells = ParseMeshStat(content, @"cells:\s+(\d+)");
                    points = ParseMeshStat(content, @"points:\s+(\d+)");
                    faces = ParseMeshStat(content, @"faces:\s+(\d+)");
                    qualityPassed = content.Contains("Mesh OK");
                }

                // Also try to read from the polyMesh/owner header
                if (!cells.HasValue)
                {
                    var ownerFile = Path.Combine(caseDir, "constant", "polyMesh", "owner");
                    if (File.Exists(ownerFile))
                    {
                        try
                        {
                            var lines = File.ReadLines(ownerFile).Take(25).ToList();
                            foreach (var line in lines)
                            {
                                if (line.Trim().All(char.IsDigit) && line.Trim().Length > 3)
                                {
                                    if (int.TryParse(line.Trim(), out var count))
                                    {
                                        cells = count;
                                        break;
                                    }
                                }
                            }
                        }
                        catch { /* Non-critical */ }
                    }
                }

                stats.Add(new MeshStatEntry
                {
                    CaseName = caseResult.CaseName,
                    Aoa = aoa.HasValue ? $"{aoa.Value:F1}" : "N/A",
                    Cells = cells.HasValue ? $"{cells.Value:N0}" : "N/A",
                    Points = points.HasValue ? $"{points.Value:N0}" : "N/A",
                    Faces = faces.HasValue ? $"{faces.Value:N0}" : "N/A",
                    QualityPassed = qualityPassed
                });
            }

            return stats;
        }

        private static int? ParseMeshStat(string content, string pattern)
        {
            var match = System.Text.RegularExpressions.Regex.Match(content, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
                return value;
            return null;
        }

        /// <summary>
        /// Writes an AIAA-standard CSV data file with coefficient summary and reference conditions.
        /// This is the primary machine-readable data artifact from the report command.
        /// </summary>
        internal static void WriteCoefficientCsv(string csvPath, string studyName,
            ResultsSummary summary, PhysicsConfig config)
        {
            using var writer = new System.IO.StreamWriter(csvPath);

            // Header block with reference conditions (prefixed with # for parsers to skip)
            writer.WriteLine($"# AIAA Aerodynamic Coefficient Data — {studyName}");
            writer.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"# Solver: {config.SolverName}");
            writer.WriteLine($"# Velocity: {config.Velocity} m/s");
            writer.WriteLine($"# RPM: {config.Rpm}");
            writer.WriteLine($"# Nu: {config.Nu} m^2/s");
            writer.WriteLine($"# Turbulence Intensity: {config.TurbulenceIntensity}%");
            writer.WriteLine($"# Refinement: {config.RefinementMin}-{config.RefinementMax}");
            writer.WriteLine($"# Max Iterations: {config.MaxIterations}");
            writer.WriteLine("#");

            // Column headers
            writer.WriteLine("AoA_deg,Cd,Cl,CmPitch,L_over_D,Converged");

            // Data rows
            foreach (var c in summary.Cases)
            {
                var ld = (c.Cl.HasValue && c.Cd.HasValue && c.Cd.Value != 0)
                    ? $"{c.Cl.Value / c.Cd.Value:F6}"
                    : "";
                writer.WriteLine(string.Join(",",
                    $"{c.AngleOfAttack:F1}",
                    c.Cd?.ToString("F6") ?? "",
                    c.Cl?.ToString("F6") ?? "",
                    c.CmPitch?.ToString("F6") ?? "",
                    ld,
                    c.Converged));
            }
        }

        private static string ReadTemplateName(string studyDir)
        {
            var manifestPath = Path.Combine(studyDir, "study.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = System.Text.Json.JsonSerializer.Deserialize<Models.StudyManifest>(json);
                    if (manifest != null && !string.IsNullOrEmpty(manifest.TemplateName))
                        return manifest.TemplateName;
                }
                catch { }
            }
            return "external_disc_rotatingwall_steady"; // backward compat fallback
        }

        internal static PhysicsConfig ReadPhysicsConfig(string studyDir)
        {
            var config = new PhysicsConfig();

            // Try to read from the first case's controlDict and initial conditions
            var cases = CaseDiscovery.DiscoverCases(studyDir);
            if (cases.Count == 0) return config;

            var firstCase = cases[0];

            // Read solver name from controlDict
            var controlDict = Path.Combine(firstCase, "system", "controlDict");
            if (File.Exists(controlDict))
            {
                var content = File.ReadAllText(controlDict);
                var appMatch = System.Text.RegularExpressions.Regex.Match(content, @"application\s+(\w+)");
                if (appMatch.Success)
                    config.SolverName = appMatch.Groups[1].Value;

                var endTimeMatch = System.Text.RegularExpressions.Regex.Match(content, @"endTime\s+([\d.]+)");
                if (endTimeMatch.Success)
                    config.MaxIterations = endTimeMatch.Groups[1].Value;
            }

            // Read velocity from 0/U
            var uFile = Path.Combine(firstCase, "0", "U");
            if (File.Exists(uFile))
            {
                var content = File.ReadAllText(uFile);
                var velMatch = System.Text.RegularExpressions.Regex.Match(content, @"internalField\s+uniform\s+\(([\d.e+-]+)\s+([\d.e+-]+)\s+([\d.e+-]+)\)");
                if (velMatch.Success)
                {
                    if (double.TryParse(velMatch.Groups[1].Value, out var ux) &&
                        double.TryParse(velMatch.Groups[3].Value, out var uz))
                    {
                        var velocity = Math.Sqrt(ux * ux + uz * uz);
                        config.Velocity = $"{velocity:F1}";
                    }
                }
            }

            // Read nu from transportProperties
            var transportProps = Path.Combine(firstCase, "constant", "transportProperties");
            if (File.Exists(transportProps))
            {
                var content = File.ReadAllText(transportProps);
                var nuMatch = System.Text.RegularExpressions.Regex.Match(content, @"nu\s+.*?([\d.eE+-]+)\s*;");
                if (nuMatch.Success)
                    config.Nu = nuMatch.Groups[1].Value;
            }

            // Read RPM from 0/U (rotatingWallVelocity omega)
            if (File.Exists(uFile))
            {
                var content = File.ReadAllText(uFile);
                var omegaMatch = System.Text.RegularExpressions.Regex.Match(content, @"omega\s+(?:constant\s+)?([\d.eE+-]+)");
                if (omegaMatch.Success && double.TryParse(omegaMatch.Groups[1].Value, out var omega))
                {
                    var rpm = omega * 60.0 / (2.0 * Math.PI);
                    config.Rpm = $"{rpm:F0}";
                }
            }

            // Read refinement from snappyHexMeshDict
            var snappyDict = Path.Combine(firstCase, "system", "snappyHexMeshDict");
            if (File.Exists(snappyDict))
            {
                var content = File.ReadAllText(snappyDict);
                var levelMatch = System.Text.RegularExpressions.Regex.Match(content, @"level\s*\(\s*(\d+)\s+(\d+)\s*\)");
                if (levelMatch.Success)
                {
                    config.RefinementMin = levelMatch.Groups[1].Value;
                    config.RefinementMax = levelMatch.Groups[2].Value;
                }
            }

            // Read TI from k (back-calculate)
            var kFile = Path.Combine(firstCase, "0", "k");
            if (File.Exists(kFile) && config.Velocity != string.Empty)
            {
                var content = File.ReadAllText(kFile);
                var kMatch = System.Text.RegularExpressions.Regex.Match(content, @"internalField\s+uniform\s+([\d.eE+-]+)");
                if (kMatch.Success && double.TryParse(kMatch.Groups[1].Value, out var k) &&
                    double.TryParse(config.Velocity, out var vel) && vel > 0)
                {
                    var ti = Math.Sqrt(2.0 * k / 3.0) / vel * 100.0;
                    config.TurbulenceIntensity = $"{ti:F1}";
                }
            }

            return config;
        }
    }

    /// <summary>
    /// Physics configuration extracted from OpenFOAM case files.
    /// </summary>
    public class PhysicsConfig
    {
        public string Velocity { get; set; } = "N/A";
        public string Rpm { get; set; } = "N/A";
        public string TurbulenceIntensity { get; set; } = "N/A";
        public string Nu { get; set; } = "N/A";
        public string SolverName { get; set; } = "simpleFoam";
        public string MaxIterations { get; set; } = "500";
        public string RefinementMin { get; set; } = "N/A";
        public string RefinementMax { get; set; } = "N/A";
    }

    /// <summary>
    /// Result of report generation.
    /// </summary>
    public class ReportResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? HtmlPath { get; set; }
        public string? PdfPath { get; set; }
        public string? CsvPath { get; set; }
    }
}
