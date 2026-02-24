using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class SolveStudyHandler : ICommandHandler<SolveStudyModel>
    {
        private readonly LoggingService _loggingService;
        private readonly SolverService _solverService;

        public SolveStudyHandler(LoggingService loggingService, SolverService solverService)
        {
            _loggingService = loggingService;
            _solverService = solverService;
        }

        public int Handle(SolveStudyModel model)
        {
            _loggingService.LogInformation($"Solving study at {model.StudyDir}");

            Console.WriteLine();
            Console.WriteLine("=== Study Solver Execution ===");
            Console.WriteLine();
            Console.WriteLine($"Study directory: {model.StudyDir}");
            Console.WriteLine($"Parallel: {(model.Parallel ? $"Yes ({model.Cores} cores per case)" : "No")}");
            Console.WriteLine($"Continue on error: {(model.ContinueOnError ? "Yes" : "No")}");
            Console.WriteLine();

            var result = _solverService.SolveStudy(model.StudyDir, model.Parallel, model.Cores, model.ContinueOnError);

            Console.WriteLine();
            Console.WriteLine("=== Study Solve Summary ===");
            Console.WriteLine();
            Console.WriteLine($"Total cases: {result.TotalCases}");
            Console.WriteLine($"✓ Successful: {result.SuccessfulCases}");
            if (result.FailedCases > 0)
                Console.WriteLine($"✗ Failed: {result.FailedCases}");
            Console.WriteLine();

            Console.WriteLine($"{"Case Name",-30} {"Status",-10} {"Cd",10} {"Cl",10} {"Cm",10}");
            Console.WriteLine(new string('-', 75));

            foreach (var cs in result.CaseSummaries)
            {
                var status = cs.Success ? "✓ OK" : "✗ Failed";
                var cd = cs.Cd.HasValue ? $"{cs.Cd.Value:F6}" : "N/A";
                var cl = cs.Cl.HasValue ? $"{cs.Cl.Value:F6}" : "N/A";
                var cm = cs.CmPitch.HasValue ? $"{cs.CmPitch.Value:F6}" : "N/A";
                Console.WriteLine($"{cs.CaseName,-30} {status,-10} {cd,10} {cl,10} {cm,10}");
            }

            Console.WriteLine(new string('-', 75));
            Console.WriteLine();

            if (result.IsSuccess)
            {
                _loggingService.LogInformation("Study solver execution completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine($"⚠ {result.ErrorMessage}");
                Console.WriteLine($"  See log file for details: {_loggingService.GetLogFilePath()}");
                return -1;
            }
        }
    }
}
