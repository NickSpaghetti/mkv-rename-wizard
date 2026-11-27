using MkvRenameWizard.Core.Models.Categories;

namespace MkvRenameWizard.Core.Models.TvMaze.Exceptions;

public sealed class TvMazeValidationException(Exception innerException)
    : Exception("TVMaze validation error occurred, fix the errors and try again.", innerException),
      IValidationCategory;

public sealed class TvMazeDependencyValidationException(Exception innerException)
    : Exception("TVMaze dependency validation error occurred, fix the errors and try again.", innerException),
      IDependencyCategory;

public sealed class TvMazeDependencyException(Exception innerException)
    : Exception("TVMaze dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class TvMazeServiceException(Exception innerException)
    : Exception("TVMaze service error occurred, contact support.", innerException),
      IServiceCategory;
