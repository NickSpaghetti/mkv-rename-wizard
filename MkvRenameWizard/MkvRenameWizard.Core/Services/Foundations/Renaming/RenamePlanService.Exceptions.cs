using MkvRenameWizard.Core.Models.Renaming.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal sealed partial class RenamePlanService
{
    private T TryCatch<T>(Func<T> returningFunction)
    {
        try
        {
            return returningFunction();
        }
        catch (InvalidRenamePlanRequestException invalidRenamePlanRequestException)
        {
            throw CreateAndLogValidationException(invalidRenamePlanRequestException);
        }
        catch (Exception exception)
        {
            throw CreateAndLogServiceException(new FailedRenamePlanServiceException(
                "Failed rename plan service error occurred, contact support.",
                exception));
        }
    }

    private RenamePlanValidationException CreateAndLogValidationException(Exception exception)
    {
        var validationException = new RenamePlanValidationException(exception);
        loggingBroker.LogError(validationException);
        return validationException;
    }

    private RenamePlanServiceException CreateAndLogServiceException(Exception exception)
    {
        var serviceException = new RenamePlanServiceException(exception);
        loggingBroker.LogCritical(serviceException);
        return serviceException;
    }
}
