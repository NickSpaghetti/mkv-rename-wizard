using MkvRenameWizard.Core.Models.MkvFiles;

namespace MkvRenameWizard.Core.Services.Foundations.MkvFiles;

internal interface IMkvFileService
{
    FileImportResult ImportFromPaths(IReadOnlyList<string> paths);
}
