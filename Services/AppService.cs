using foamscript.Handlers;
using foamscript.Models;

namespace foamscript
{
    public class AppService
    {
        private readonly Services.LoggingService _loggingService;
        private readonly ValidateHandler _validateHandler;
        private readonly ConvertHandler _convertHandler;
        private readonly NewStudyHandler _newStudyHandler;
        private readonly MeshHandler _meshHandler;
        private readonly SolveHandler _solveHandler;
        private readonly ReportHandler _reportHandler;
        private readonly ListTemplatesHandler _listTemplatesHandler;

        public AppService(
            Services.LoggingService loggingService,
            ValidateHandler validateHandler,
            ConvertHandler convertHandler,
            NewStudyHandler newStudyHandler,
            MeshHandler meshHandler,
            SolveHandler solveHandler,
            ReportHandler reportHandler,
            ListTemplatesHandler listTemplatesHandler)
        {
            _loggingService = loggingService;
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
                return model switch
                {
                    ValidateModel m => _validateHandler.Handle(m),
                    ConvertModel m => _convertHandler.Handle(m),
                    NewStudyModel m => _newStudyHandler.Handle(m),
                    MeshModel m => _meshHandler.Handle(m),
                    SolveModel m => _solveHandler.Handle(m),
                    ReportModel m => _reportHandler.Handle(m),
                    ListTemplatesModel m => _listTemplatesHandler.Handle(m),
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
