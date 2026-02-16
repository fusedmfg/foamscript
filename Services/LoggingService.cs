using Microsoft.Extensions.Logging;
namespace foamscript.Services
{
    public class LoggingService
    {
        private readonly ILogger<LoggingService> _logger;

        public LoggingService(ILogger<LoggingService> logger)
        {
            _logger = logger;
        }

        public void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }

        public void LogError(string message, Exception ex)
        {
            _logger.LogError(ex, message);
        }

        public void LogError(string message)
        {
            _logger.LogError(message);
        }

        public string GetLogFilePath()
        {
            var timestamp = Environment.GetEnvironmentVariable("FOAMSCRIPT_TIMESTAMP") ?? DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            return Path.Combine(logDir, $"foamscript-{timestamp}.txt");
        }
    }
}