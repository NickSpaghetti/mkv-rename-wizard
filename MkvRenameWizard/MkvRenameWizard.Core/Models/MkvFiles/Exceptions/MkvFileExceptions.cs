namespace MkvRenameWizard.Core.Models.MkvFiles.Exceptions;

internal sealed class InvalidMkvFileImportException(string message) : Exception(message);

internal sealed class FailedMkvFileDependencyException(string message, Exception innerException)
    : Exception(message, innerException);

internal sealed class FailedMkvFileServiceException(string message, Exception innerException)
    : Exception(message, innerException);
