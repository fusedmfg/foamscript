using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class MeshStudyHandler : ICommandHandler<MeshStudyModel>
    {
        private readonly LoggingService _loggingService;
        private readonly MeshService _meshService;

        public MeshStudyHandler(LoggingService loggingService, MeshService meshService)
        {
            _loggingService = loggingService;
            _meshService = meshService;
        }

        public int Handle(MeshStudyModel model)
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
