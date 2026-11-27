namespace MkvRenameWizard.Core.Models.Renaming.Exceptions;

internal sealed class InvalidRenamePlanRequestException(string message) : Exception(message);

internal sealed class InvalidRenameOperationException(string message) : Exception(message);

internal sealed class FailedRenamePlanServiceException(string message, Exception innerException)
    : Exception(message, innerException);

internal sealed class FailedFileRenameServiceException(string message, Exception innerException)
    : Exception(message, innerException);
