using MkvRenameWizard.Core.Models.MkvFiles.Exceptions;

namespace MkvRenameWizard.Core.Services.Foundations.MkvFiles;

internal sealed partial class MkvFileService
{
    private static void ValidatePaths(IReadOnlyList<string> paths)
    {
        if (paths is null)
        {
            throw new InvalidMkvFileImportException("Paths list is required.");
        }
    }
}
