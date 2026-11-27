using MkvRenameWizard.Core.Models.Categories;
using MkvRenameWizard.Core.Models.Layers.Exceptions;

namespace MkvRenameWizard.Core.Services.Orchestrations.TvMaze;

internal sealed partial class TvMazeShowOrchestrationService
{
    private async Task<T> TryCatchAsync<T>(Func<Task<T>> returningFunction)
    {
        try
        {
            return await returningFunction();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IValidationCategory)
        {
            loggingBroker.LogError(exception);
            throw new TvMazeShowOrchestrationDependencyValidationException(exception);
        }
        catch (Exception exception) when (exception is IDependencyCategory or IServiceCategory)
        {
            loggingBroker.LogError(exception);
            throw new TvMazeShowOrchestrationDependencyException(exception);
        }
        catch (Exception exception)
        {
            loggingBroker.LogCritical(exception);
            throw new TvMazeShowOrchestrationServiceException(exception);
        }
    }
}
