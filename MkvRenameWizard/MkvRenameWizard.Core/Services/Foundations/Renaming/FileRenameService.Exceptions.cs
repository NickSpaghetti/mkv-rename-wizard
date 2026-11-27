using MkvRenameWizard.Core.Models.Renaming.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.Renaming;

internal sealed partial class FileRenameService
{
    private T TryCatch<T>(Func<T> returningFunction)
    {
        try
        {
            return returningFunction();
        }
        catch (InvalidRenameOperationException invalidRenameOperationException)
        {
            throw CreateAndLogValidationException(invalidRenameOperationException);
        }
        catch (Exception exception)
        {
            throw CreateAndLogServiceException(new FailedFileRenameServiceException(
                "Failed file rename service error occurred, contact support.",
                exception));
        }
    }

    private FileRenameValidationException CreateAndLogValidationException(Exception exception)
    {
        var validationException = new FileRenameValidationException(exception);
        loggingBroker.LogError(validationException);
        return validationException;
    }

    private FileRenameServiceException CreateAndLogServiceException(Exception exception)
    {
        var serviceException = new FileRenameServiceException(exception);
        loggingBroker.LogCritical(serviceException);
        return serviceException;
    }
}
