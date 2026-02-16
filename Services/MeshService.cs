using Microsoft.Extensions.Logging;
namespace foamscript.Services
{
    public class MeshService
    {
        private readonly ILogger<LoggingService> _logger;
        private int _exitCode = 0;

        public MeshService(ILogger<LoggingService> logger)
        {
            _logger = logger;
        }

        public int MeshGeometry()
        {
            try
            {
                // Log start of mesh process.
                _logger.LogInformation("Processing mesh...");

                _exitCode = 0;             
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "");
                _exitCode = -1;
            }

            return _exitCode;
        }
    }       
}