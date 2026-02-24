using foamscript.Models;

namespace foamscript.Handlers
{
    public interface ICommandHandler<TModel> where TModel : VerbModel
    {
        int Handle(TModel model);
    }
}
