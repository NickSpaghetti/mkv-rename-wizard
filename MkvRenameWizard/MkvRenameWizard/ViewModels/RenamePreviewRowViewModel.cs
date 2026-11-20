using MkvRenameWizard.Models.Renaming;

namespace MkvRenameWizard.ViewModels;

public sealed class RenamePreviewRowViewModel : ViewModelBase
{
    public RenamePreviewRowViewModel(
        int operationId,
        string sourcePath,
        string sourceFileName,
        string? targetPath,
        RenamePreviewStatus status)
    {
        OperationId = operationId;
        SourcePath = sourcePath;
        SourceFileName = sourceFileName;
        TargetPath = targetPath;
        RenamePreviewStatus = status;
    }

    public int OperationId { get; private set; }
    public string SourcePath { get; private set; }
    public string SourceFileName { get; private set; }
    public string? TargetPath { get; private set; }
    public RenamePreviewStatus RenamePreviewStatus { get; private set; }

    public RenameFileOperation RenameOperation => new(OperationId, SourcePath, TargetPath);

    public bool IsReady => RenamePreviewStatus == RenamePreviewStatus.Ready;
    public bool IsPatternError => RenamePreviewStatus == RenamePreviewStatus.PatternError;
    public bool IsConflict => RenamePreviewStatus == RenamePreviewStatus.Conflict;
    public bool IsSkipped => RenamePreviewStatus == RenamePreviewStatus.Skipped;
    public bool IsDone => RenamePreviewStatus == RenamePreviewStatus.Done;

    public void Update(int operationId, string sourcePath, string sourceFileName, string? targetPath, RenamePreviewStatus status)
    {
        var idChanged = OperationId != operationId;
        var sourcePathChanged = !string.Equals(SourcePath, sourcePath, System.StringComparison.Ordinal);
        var sourceNameChanged = !string.Equals(SourceFileName, sourceFileName, System.StringComparison.Ordinal);
        var targetChanged = !string.Equals(TargetPath, targetPath, System.StringComparison.Ordinal);
        var statusChanged = RenamePreviewStatus != status;

        if (!idChanged && !sourcePathChanged && !sourceNameChanged && !targetChanged && !statusChanged)
        {
            return;
        }

        OperationId = operationId;
        SourcePath = sourcePath;
        SourceFileName = sourceFileName;
        TargetPath = targetPath;
        RenamePreviewStatus = status;

        if (idChanged) OnPropertyChanged(nameof(OperationId));
        if (sourcePathChanged) OnPropertyChanged(nameof(SourcePath));
        if (sourceNameChanged) OnPropertyChanged(nameof(SourceFileName));
        if (targetChanged) OnPropertyChanged(nameof(TargetPath));

        if (statusChanged)
        {
            OnPropertyChanged(nameof(RenamePreviewStatus));
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsPatternError));
            OnPropertyChanged(nameof(IsConflict));
            OnPropertyChanged(nameof(IsSkipped));
            OnPropertyChanged(nameof(IsDone));
        }
    }
}
