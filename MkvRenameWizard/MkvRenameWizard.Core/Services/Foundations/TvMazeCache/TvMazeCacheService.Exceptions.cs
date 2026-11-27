using MkvRenameWizard.Core.Models.TvMaze.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeCache;

internal sealed partial class TvMazeCacheService
{
    private T TryCatch<T>(Func<T> returningFunction)
    {
        try
        {
            return returningFunction();
        }
        catch (InvalidTvMazeQueryException invalidTvMazeQueryException)
        {
            throw CreateAndLogValidationException(invalidTvMazeQueryException);
        }
        catch (Exception exception)
        {
            throw CreateAndLogServiceException(new FailedTvMazeServiceException(
                "Failed TVMaze cache service error occurred, contact support.",
                exception));
        }
    }

    private void TryCatch(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidTvMazeQueryException invalidTvMazeQueryException)
        {
            throw CreateAndLogValidationException(invalidTvMazeQueryException);
        }
        catch (Exception exception)
        {
            throw CreateAndLogServiceException(new FailedTvMazeServiceException(
                "Failed TVMaze cache service error occurred, contact support.",
                exception));
        }
    }

    private TvMazeValidationException CreateAndLogValidationException(Exception exception)
    {
        var validationException = new TvMazeValidationException(exception);
        loggingBroker.LogError(validationException);
        return validationException;
    }

    private TvMazeServiceException CreateAndLogServiceException(Exception exception)
    {
        var serviceException = new TvMazeServiceException(exception);
        loggingBroker.LogCritical(serviceException);
        return serviceException;
    }
}
