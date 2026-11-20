using MkvRenameWizard.Models.Renaming;
using ReactiveUI;

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

        if (idChanged) this.RaisePropertyChanged(nameof(OperationId));
        if (sourcePathChanged) this.RaisePropertyChanged(nameof(SourcePath));
        if (sourceNameChanged) this.RaisePropertyChanged(nameof(SourceFileName));
        if (targetChanged) this.RaisePropertyChanged(nameof(TargetPath));

        if (statusChanged)
        {
            this.RaisePropertyChanged(nameof(RenamePreviewStatus));
            this.RaisePropertyChanged(nameof(IsReady));
            this.RaisePropertyChanged(nameof(IsPatternError));
            this.RaisePropertyChanged(nameof(IsConflict));
            this.RaisePropertyChanged(nameof(IsSkipped));
            this.RaisePropertyChanged(nameof(IsDone));
        }
    }
}
