using MkvRenameWizard.Core.Models.Categories;
using MkvRenameWizard.Core.Models.Layers.Exceptions;

namespace MkvRenameWizard.Core.Services.Aggregations;

internal sealed partial class MkvRenameWizardService
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
            throw new MkvRenameWizardDependencyValidationException(exception);
        }
        catch (Exception exception) when (exception is IDependencyCategory or IServiceCategory)
        {
            loggingBroker.LogError(exception);
            throw new MkvRenameWizardDependencyException(exception);
        }
        catch (Exception exception)
        {
            loggingBroker.LogCritical(exception);
            throw new MkvRenameWizardServiceException(exception);
        }
    }

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
            throw new MkvRenameWizardDependencyValidationException(exception);
        }
        catch (Exception exception) when (exception is IDependencyCategory or IServiceCategory)
        {
            loggingBroker.LogError(exception);
            throw new MkvRenameWizardDependencyException(exception);
        }
        catch (Exception exception)
        {
            loggingBroker.LogCritical(exception);
            throw new MkvRenameWizardServiceException(exception);
        }
    }
}
