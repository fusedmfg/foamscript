using foamscript.Handlers;
using foamscript.Models;
using foamscript.Services;

namespace foamscript
{
    public class AppService
    {
        private readonly LoggingService _loggingService;
        private readonly OpenFoamEnvironment _openFoamEnvironment;
        private readonly IProcessExecutor _processExecutor;
        private readonly ValidateHandler _validateHandler;
        private readonly ConvertHandler _convertHandler;
        private readonly NewStudyHandler _newStudyHandler;
        private readonly MeshHandler _meshHandler;
        private readonly SolveHandler _solveHandler;
        private readonly ReportHandler _reportHandler;
        private readonly ListTemplatesHandler _listTemplatesHandler;

        public AppService(
            LoggingService loggingService,
            OpenFoamEnvironment openFoamEnvironment,
            IProcessExecutor processExecutor,
            ValidateHandler validateHandler,
            ConvertHandler convertHandler,
            NewStudyHandler newStudyHandler,
            MeshHandler meshHandler,
            SolveHandler solveHandler,
            ReportHandler reportHandler,
            ListTemplatesHandler listTemplatesHandler)
        {
            _loggingService = loggingService;
            _openFoamEnvironment = openFoamEnvironment;
            _processExecutor = processExecutor;
            _validateHandler = validateHandler;
            _convertHandler = convertHandler;
            _newStudyHandler = newStudyHandler;
            _meshHandler = meshHandler;
            _solveHandler = solveHandler;
            _reportHandler = reportHandler;
            _listTemplatesHandler = listTemplatesHandler;
        }

        public int Run(VerbModel model)
        {
            try
            {
                // Commands that don't need OpenFOAM environment
                if (model is ValidateModel m1)
                    return _validateHandler.Handle(m1);
                if (model is ListTemplatesModel m2)
                    return _listTemplatesHandler.Handle(m2);

                // Pre-flight: ensure OpenFOAM environment is loaded
                var envError = _openFoamEnvironment.Initialize();
                if (envError != null)
                {
                    Console.Error.WriteLine($"✗ {envError}");
                    return -1;
                }

                // Inject captured env into ProcessExecutor
                if (_processExecutor is ProcessExecutor executor && _openFoamEnvironment.CapturedEnv != null)
                {
                    executor.SetEnvironment(_openFoamEnvironment.CapturedEnv);
                }

                return model switch
                {
                    ConvertModel m => _convertHandler.Handle(m),
                    NewStudyModel m => _newStudyHandler.Handle(m),
                    MeshModel m => _meshHandler.Handle(m),
                    SolveModel m => _solveHandler.Handle(m),
                    ReportModel m => _reportHandler.Handle(m),
                    _ => throw new NotImplementedException($"The verb of type {model.GetType().Name} is not implemented.")
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogError("The application failed to execute the requested command.", ex);
                return -1;
            }
        }
    }
}
