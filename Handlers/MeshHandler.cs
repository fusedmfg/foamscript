using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class MeshHandler : ICommandHandler<MeshCaseModel>
    {
        private readonly LoggingService _loggingService;
        private readonly MeshService _meshService;

        public MeshHandler(LoggingService loggingService, MeshService meshService)
        {
            _loggingService = loggingService;
            _meshService = meshService;
        }

        public int Handle(MeshCaseModel model)
        {
            _loggingService.LogInformation($"Meshing case at {model.CaseDir}");

            Console.WriteLine();
            Console.WriteLine("=== Mesh Generation ===");
            Console.WriteLine();
            Console.WriteLine($"Case directory: {model.CaseDir}");
            Console.WriteLine($"Parallel: {(model.Parallel ? $"Yes ({model.Cores} cores)" : "No")}");
            Console.WriteLine($"Check quality: {(model.CheckQuality ? "Yes" : "No")}");
            Console.WriteLine($"Overwrite: {(model.Overwrite ? "Yes" : "No")}");
            Console.WriteLine();

            var result = _meshService.MeshCase(
                model.CaseDir,
                model.Parallel,
                model.Cores,
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
    }
}
