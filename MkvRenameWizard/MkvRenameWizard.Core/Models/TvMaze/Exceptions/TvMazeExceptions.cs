namespace MkvRenameWizard.Core.Models.TvMaze.Exceptions;

internal sealed class InvalidTvMazeQueryException(string message) : Exception(message);

internal sealed class FailedTvMazeDependencyException(string message, Exception innerException)
    : Exception(message, innerException);

internal sealed class FailedTvMazeServiceException(string message, Exception innerException)
    : Exception(message, innerException);
