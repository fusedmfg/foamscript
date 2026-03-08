using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class ReportHandler : ICommandHandler<ReportModel>
    {
        private readonly LoggingService _loggingService;
        private readonly ReportService _reportService;

        public ReportHandler(LoggingService loggingService, ReportService reportService)
        {
            _loggingService = loggingService;
            _reportService = reportService;
        }

        public int Handle(ReportModel model)
        {
            // Auto-detect: if user pointed at a single case, use its parent as the study dir
            var dir = model.Dir;
            if (CaseDiscovery.IsCase(dir))
            {
                var parent = Directory.GetParent(dir)?.FullName;
                if (parent != null)
                    dir = parent;
            }

            _loggingService.LogInformation($"Generating report for {dir}");

            var studyName = Path.GetFileName(dir);
            var outputDir = model.OutputDir ?? Path.Combine(dir, "report");
            var format = model.Format.ToLowerInvariant();

            if (format is not "html" and not "pdf" and not "both")
            {
                Console.WriteLine($"\u2717 Invalid format '{model.Format}'. Use: html, pdf, or both");
                return -1;
            }

            Console.WriteLine();
            Console.WriteLine("=== Aerodynamic Analysis Report ===");
            Console.WriteLine();
            Console.WriteLine($"Study:     {studyName}");
            Console.WriteLine($"Directory: {dir}");
            Console.WriteLine($"Format:    {format}");
            Console.WriteLine($"Output:    {outputDir}");
            Console.WriteLine();

            var result = _reportService.GenerateReport(dir, outputDir, format, model.AverageWindow);

            if (result.IsSuccess)
            {
                Console.WriteLine();
                Console.WriteLine("=== Report Generation Complete ===");
                Console.WriteLine();
                if (result.HtmlPath != null)
                    Console.WriteLine($"  HTML: {result.HtmlPath}");
                if (result.PdfPath != null)
                    Console.WriteLine($"  PDF:  {result.PdfPath}");
                Console.WriteLine();

                _loggingService.LogInformation("Report generation completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine($"\u2717 Report generation failed: {result.ErrorMessage}");
                _loggingService.LogError("Report generation failed.", null!);
                return -1;
            }
        }
    }
}
