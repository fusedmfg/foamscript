using foamscript.Handlers;
using foamscript.Models;

namespace foamscript
{
    public class AppService
    {
        private readonly Services.LoggingService _loggingService;
        private readonly ValidateHandler _validateHandler;
        private readonly ConvertHandler _convertHandler;
        private readonly GenerateDomainHandler _generateDomainHandler;
        private readonly NewStudyHandler _newStudyHandler;
        private readonly MeshHandler _meshHandler;
        private readonly MeshStudyHandler _meshStudyHandler;
        private readonly SolveHandler _solveHandler;
        private readonly SolveStudyHandler _solveStudyHandler;
        private readonly ResultsHandler _resultsHandler;
        private readonly ListTemplatesHandler _listTemplatesHandler;

        public AppService(
            Services.LoggingService loggingService,
            ValidateHandler validateHandler,
            ConvertHandler convertHandler,
            GenerateDomainHandler generateDomainHandler,
            NewStudyHandler newStudyHandler,
            MeshHandler meshHandler,
            MeshStudyHandler meshStudyHandler,
            SolveHandler solveHandler,
            SolveStudyHandler solveStudyHandler,
            ResultsHandler resultsHandler,
            ListTemplatesHandler listTemplatesHandler)
        {
            _loggingService = loggingService;
            _validateHandler = validateHandler;
            _convertHandler = convertHandler;
            _generateDomainHandler = generateDomainHandler;
            _newStudyHandler = newStudyHandler;
            _meshHandler = meshHandler;
            _meshStudyHandler = meshStudyHandler;
            _solveHandler = solveHandler;
            _solveStudyHandler = solveStudyHandler;
            _resultsHandler = resultsHandler;
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
                    GenerateDomainModel m => _generateDomainHandler.Handle(m),
                    NewStudyModel m => _newStudyHandler.Handle(m),
                    MeshCaseModel m => _meshHandler.Handle(m),
                    MeshStudyModel m => _meshStudyHandler.Handle(m),
                    SolveCaseModel m => _solveHandler.Handle(m),
                    SolveStudyModel m => _solveStudyHandler.Handle(m),
                    ResultsModel m => _resultsHandler.Handle(m),
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
