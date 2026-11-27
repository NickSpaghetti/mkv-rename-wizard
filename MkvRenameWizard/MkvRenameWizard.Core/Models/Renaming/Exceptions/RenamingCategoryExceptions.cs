using MkvRenameWizard.Core.Models.Categories;

namespace MkvRenameWizard.Core.Models.Renaming.Exceptions;

public sealed class RenamePlanValidationException(Exception innerException)
    : Exception("Rename plan validation error occurred, fix the errors and try again.", innerException),
      IValidationCategory;

public sealed class RenamePlanServiceException(Exception innerException)
    : Exception("Rename plan service error occurred, contact support.", innerException),
      IServiceCategory;

public sealed class FileRenameValidationException(Exception innerException)
    : Exception("File rename validation error occurred, fix the errors and try again.", innerException),
      IValidationCategory;

public sealed class FileRenameServiceException(Exception innerException)
    : Exception("File rename service error occurred, contact support.", innerException),
      IServiceCategory;
