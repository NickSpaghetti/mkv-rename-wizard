using MkvRenameWizard.Core.Models.Categories;
using MkvRenameWizard.Core.Models.Layers.Exceptions;

namespace MkvRenameWizard.Core.Services.Orchestrations.Renaming;

internal sealed partial class RenameOrchestrationService
{
    private T TryCatch<T>(Func<T> returningFunction)
    {
        try
        {
            return returningFunction();
        }
        catch (Exception exception) when (exception is IValidationCategory)
        {
            loggingBroker.LogError(exception);
            throw new RenameOrchestrationDependencyValidationException(exception);
        }
        catch (Exception exception) when (exception is IDependencyCategory or IServiceCategory)
        {
            loggingBroker.LogError(exception);
            throw new RenameOrchestrationDependencyException(exception);
        }
        catch (Exception exception)
        {
            loggingBroker.LogCritical(exception);
            throw new RenameOrchestrationServiceException(exception);
        }
    }
}
