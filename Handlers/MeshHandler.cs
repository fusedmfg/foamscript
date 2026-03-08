using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class MeshHandler : ICommandHandler<MeshModel>
    {
        private readonly LoggingService _loggingService;
        private readonly MeshService _meshService;

        public MeshHandler(LoggingService loggingService, MeshService meshService)
        {
            _loggingService = loggingService;
            _meshService = meshService;
        }

        public int Handle(MeshModel model)
        {
            if (CaseDiscovery.IsCase(model.Dir))
                return HandleCase(model);

            if (Directory.Exists(model.Dir))
                return HandleStudy(model);

            Console.WriteLine($"✗ Directory not found: {model.Dir}");
            return -1;
        }

        private int HandleCase(MeshModel model)
        {
            var (cores, parallel) = CoreResolver.Resolve(model.Cores);

            _loggingService.LogInformation($"Meshing case at {model.Dir}");

            Console.WriteLine();
            Console.WriteLine("=== Mesh Generation ===");
            Console.WriteLine();
            Console.WriteLine($"Case directory: {model.Dir}");
            Console.WriteLine($"Cores: {cores} ({(parallel ? "parallel" : "serial")})");
            Console.WriteLine($"Check quality: {(model.CheckQuality ? "Yes" : "No")}");
            Console.WriteLine($"Overwrite: {(model.Overwrite ? "Yes" : "No")}");
            Console.WriteLine();

            var result = _meshService.MeshCase(
                model.Dir,
                parallel,
                cores,
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

        private int HandleStudy(MeshModel model)
        {
            var (cores, parallel) = CoreResolver.Resolve(model.Cores);

            _loggingService.LogInformation($"Meshing study at {model.Dir}");

            Console.WriteLine();
            Console.WriteLine("=== Study Mesh Generation ===");
            Console.WriteLine();
            Console.WriteLine($"Study directory: {model.Dir}");
            Console.WriteLine($"Cores per case: {cores} ({(parallel ? "parallel" : "serial")})");
            Console.WriteLine($"Check quality: {(model.CheckQuality ? "Yes" : "No")}");
            Console.WriteLine($"Overwrite: {(model.Overwrite ? "Yes" : "No")}");
            Console.WriteLine();

            var result = _meshService.MeshStudy(
                model.Dir,
                parallel,
                cores,
                model.CheckQuality,
                model.Overwrite,
                continueOnError: true);

            Console.WriteLine("=== Study Mesh Summary ===");
            Console.WriteLine();
            Console.WriteLine($"Total cases: {result.TotalCases}");
            Console.WriteLine($"✓ Successful: {result.SuccessfulCases}");
            if (result.FailedCases > 0)
            {
                Console.WriteLine($"✗ Failed: {result.FailedCases}");
            }
            Console.WriteLine();

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
                Console.WriteLine($"✗ {result.ErrorMessage}");
                Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                _loggingService.LogError($"Study meshing failed: {result.ErrorMessage}", null!);
                return -1;
            }
        }
    }
}
