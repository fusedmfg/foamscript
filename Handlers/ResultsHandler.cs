using foamscript.Models;
using foamscript.Services;

namespace foamscript.Handlers
{
    public class ResultsHandler : ICommandHandler<ResultsModel>
    {
        private readonly LoggingService _loggingService;
        private readonly ResultsService _resultsService;

        public ResultsHandler(LoggingService loggingService, ResultsService resultsService)
        {
            _loggingService = loggingService;
            _resultsService = resultsService;
        }

        public int Handle(ResultsModel model)
        {
            // Auto-detect: if user pointed at a single case, use its parent as the study dir
            var dir = model.Dir;
            if (CaseDiscovery.IsCase(dir))
            {
                var parent = Directory.GetParent(dir)?.FullName;
                if (parent != null)
                    dir = parent;
            }

            _loggingService.LogInformation($"Extracting results from {dir}");

            var summary = _resultsService.ExtractResults(dir, model.Format, model.AverageWindow);

            if (summary.IsSuccess)
            {
                var output = ResultsService.FormatResults(summary, model.Format);
                Console.Write(output);
                _loggingService.LogInformation("Results extraction completed successfully.");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ Results extraction failed: {summary.ErrorMessage}");
                _loggingService.LogError("Results extraction failed.", null!);
                return -1;
            }
        }
    }
}
