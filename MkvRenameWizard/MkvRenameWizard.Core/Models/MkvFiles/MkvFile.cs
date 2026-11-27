namespace MkvRenameWizard.Core.Models.MkvFiles;

public sealed record MkvFile(
    string Root,
    string FullPath,
    long? SizeInBytes,
    bool IsIncluded);
