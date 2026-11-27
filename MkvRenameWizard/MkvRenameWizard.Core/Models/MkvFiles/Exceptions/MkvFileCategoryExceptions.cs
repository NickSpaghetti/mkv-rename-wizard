using MkvRenameWizard.Core.Models.Categories;

namespace MkvRenameWizard.Core.Models.MkvFiles.Exceptions;

public sealed class MkvFileValidationException(Exception innerException)
    : Exception("MKV file validation error occurred, fix the errors and try again.", innerException),
      IValidationCategory;

public sealed class MkvFileDependencyException(Exception innerException)
    : Exception("MKV file dependency error occurred, contact support.", innerException),
      IDependencyCategory;

public sealed class MkvFileServiceException(Exception innerException)
    : Exception("MKV file service error occurred, contact support.", innerException),
      IServiceCategory;
