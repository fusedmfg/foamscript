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
            _loggingService.LogInformation($"Extracting results from {model.StudyDir}");

            var summary = _resultsService.ExtractResults(model.StudyDir, model.Format, model.AverageWindow);

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
