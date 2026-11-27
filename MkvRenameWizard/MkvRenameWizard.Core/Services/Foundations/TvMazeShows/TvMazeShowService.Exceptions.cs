using System.Net;
using System.Text.Json;
using MkvRenameWizard.Core.Models.TvMaze.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.TvMazeShows;

internal sealed partial class TvMazeShowService
{
    private async Task<T> TryCatchAsync<T>(Func<Task<T>> returningFunction)
    {
        try
        {
            return await returningFunction();
        }
        catch (InvalidTvMazeQueryException invalidTvMazeQueryException)
        {
            throw CreateAndLogValidationException(invalidTvMazeQueryException);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException httpRequestException)
            when (httpRequestException.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            throw CreateAndLogDependencyValidationException(new FailedTvMazeDependencyException(
                "TVMaze rejected the show search request.",
                httpRequestException));
        }
        catch (HttpRequestException httpRequestException)
        {
            throw CreateAndLogDependencyException(new FailedTvMazeDependencyException(
                "Failed to communicate with TVMaze API.",
                httpRequestException));
        }
        catch (JsonException jsonException)
        {
            throw CreateAndLogDependencyException(new FailedTvMazeDependencyException(
                "Failed to deserialize TVMaze show search response.",
                jsonException));
        }
        catch (Exception exception)
        {
            throw CreateAndLogServiceException(new FailedTvMazeServiceException(
                "Failed TVMaze show service error occurred, contact support.",
                exception));
        }
    }

    private TvMazeValidationException CreateAndLogValidationException(Exception exception)
    {
        var validationException = new TvMazeValidationException(exception);
        loggingBroker.LogError(validationException);
        return validationException;
    }

    private TvMazeDependencyValidationException CreateAndLogDependencyValidationException(Exception exception)
    {
        var dependencyValidationException = new TvMazeDependencyValidationException(exception);
        loggingBroker.LogError(dependencyValidationException);
        return dependencyValidationException;
    }

    private TvMazeDependencyException CreateAndLogDependencyException(Exception exception)
    {
        var dependencyException = new TvMazeDependencyException(exception);
        loggingBroker.LogError(dependencyException);
        return dependencyException;
    }

    private TvMazeServiceException CreateAndLogServiceException(Exception exception)
    {
        var serviceException = new TvMazeServiceException(exception);
        loggingBroker.LogCritical(serviceException);
        return serviceException;
    }
}
