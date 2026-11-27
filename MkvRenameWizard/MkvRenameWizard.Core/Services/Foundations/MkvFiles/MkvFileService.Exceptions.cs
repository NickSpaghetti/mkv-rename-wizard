using MkvRenameWizard.Core.Models.MkvFiles.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.MkvFiles;

internal sealed partial class MkvFileService
{
    private T TryCatch<T>(Func<T> returningFunction)
    {
        try
        {
            return returningFunction();
        }
        catch (InvalidMkvFileImportException invalidMkvFileImportException)
        {
            throw CreateAndLogValidationException(invalidMkvFileImportException);
        }
        catch (Exception exception)
        {
            throw CreateAndLogServiceException(new FailedMkvFileServiceException(
                "Failed MKV file service error occurred, contact support.",
                exception));
        }
    }

    private MkvFileValidationException CreateAndLogValidationException(Exception exception)
    {
        var validationException = new MkvFileValidationException(exception);
        loggingBroker.LogError(validationException);
        return validationException;
    }

    private MkvFileServiceException CreateAndLogServiceException(Exception exception)
    {
        var serviceException = new MkvFileServiceException(exception);
        loggingBroker.LogCritical(serviceException);
        return serviceException;
    }
}
