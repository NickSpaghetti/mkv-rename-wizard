using MkvRenameWizard.Core.Models.Categories;

namespace MkvRenameWizard.Core.Models.Layers.Exceptions;

public sealed class TvMazeShowOrchestrationDependencyValidationException(Exception innerException)
    : Exception("TVMaze show orchestration dependency validation error occurred, fix the errors and try again.", innerException),
      IDependencyCategory;

public sealed class TvMazeShowOrchestrationDependencyException(Exception innerException)
    : Exception("TVMaze show orchestration dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class TvMazeShowOrchestrationServiceException(Exception innerException)
    : Exception("TVMaze show orchestration service error occurred, contact support.", innerException),
      IServiceCategory;

public sealed class TvMazeSeasonOrchestrationDependencyValidationException(Exception innerException)
    : Exception("TVMaze season orchestration dependency validation error occurred, fix the errors and try again.", innerException),
      IDependencyCategory;

public sealed class TvMazeSeasonOrchestrationDependencyException(Exception innerException)
    : Exception("TVMaze season orchestration dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class TvMazeSeasonOrchestrationServiceException(Exception innerException)
    : Exception("TVMaze season orchestration service error occurred, contact support.", innerException),
      IServiceCategory;

public sealed class TvMazeCoordinationDependencyValidationException(Exception innerException)
    : Exception("TVMaze coordination dependency validation error occurred, fix the errors and try again.", innerException),
      IDependencyCategory;

public sealed class TvMazeCoordinationDependencyException(Exception innerException)
    : Exception("TVMaze coordination dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class TvMazeCoordinationServiceException(Exception innerException)
    : Exception("TVMaze coordination service error occurred, contact support.", innerException),
      IServiceCategory;

public sealed class RenameOrchestrationValidationException(Exception innerException)
    : Exception("Rename orchestration validation error occurred, fix the errors and try again.", innerException),
      IValidationCategory;

public sealed class RenameOrchestrationDependencyValidationException(Exception innerException)
    : Exception("Rename orchestration dependency validation error occurred, fix the errors and try again.", innerException),
      IDependencyCategory;

public sealed class RenameOrchestrationDependencyException(Exception innerException)
    : Exception("Rename orchestration dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class RenameOrchestrationServiceException(Exception innerException)
    : Exception("Rename orchestration service error occurred, contact support.", innerException),
      IServiceCategory;

public sealed class MkvRenameWizardValidationException(Exception innerException)
    : Exception("MkvRenameWizard validation error occurred, fix the errors and try again.", innerException),
      IValidationCategory;

public sealed class MkvRenameWizardDependencyValidationException(Exception innerException)
    : Exception("MkvRenameWizard dependency validation error occurred, fix the errors and try again.", innerException),
      IDependencyCategory;

public sealed class MkvRenameWizardDependencyException(Exception innerException)
    : Exception("MkvRenameWizard dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class MkvRenameWizardServiceException(Exception innerException)
    : Exception("MkvRenameWizard service error occurred, contact support.", innerException),
      IServiceCategory;
