using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Renaming;
using MkvRenameWizard.Services;

namespace MkvRenameWizard.ViewModels;

public class RenameResultViewModel : ViewModelBase
{
    public bool IsFullSuccess { get; set => SetProperty(ref field, value); }
    public bool IsUndone { get; set => SetProperty(ref field, value); }
    public bool CanUndo
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UndoCommand?.NotifyCanExecuteChanged();
            }
        }
    }
    public int SuccessCount { get; set => SetProperty(ref field, value); }
    public int FailureCount { get; set => SetProperty(ref field, value); }
    public int TotalCount => SuccessCount + FailureCount;
    public string? ShowSeasonLabel { get; set => SetProperty(ref field, value); }
    public string? TargetFolder { get; set => SetProperty(ref field, value); }

    public ObservableCollection<RenameOperationResult<RenameFileOperation>> OperationResults { get; } = [];

    public string SummaryHeadline => IsUndone
        ? IsFullSuccess
            ? "file(s) restored"
            : $"Restored {SuccessCount} of {TotalCount} file(s)"
        : IsFullSuccess
            ? "file(s) renamed"
            : $"Renamed {SuccessCount} file(s) to {TargetFolder} of {TotalCount} files";

    public string SummarySubtitle => IsUndone
        ? IsFullSuccess
            ? $"All {SuccessCount} file(s) have been restored to their original names."
            : $"{FailureCount} file(s) could not be restored."
        : IsFullSuccess
            ? $"All {SuccessCount} episodes of {ShowSeasonLabel} are now named in your preferred format"
            : FailureCount == TotalCount
                ? "All files failed to rename.  Check permission or whether files are open in another app."
                : $"{FailureCount} file(s) could not be renamed.";

    public string RetryButtonLabel => $"Retry {FailureCount} failed file(s)";
    public string StatsBarLabel => $"{SuccessCount} renamed  |  {FailureCount} failed";

    public string ErrorDetailedText
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Rename errors - {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            foreach (var result in OperationResults)
            {
                if (result.IsSuccessful)
                {
                    continue;
                }
                sb.AppendLine($"  Source:  {result.RenameOperation.SourcePath}");
                sb.AppendLine($"  Target:  {result.RenameOperation.TargetPath}");
                sb.AppendLine($"  Error:   {result.ErrorMessage}");
            }
            return sb.ToString();
        }
    }

    public IRelayCommand OpenFolderCommand { get; }
    public IAsyncRelayCommand CopyErrorDetailsCommand { get; }
    public IRelayCommand OpenLogFolderCommand { get; }

    public event Action? ResetRequested;
    public event Action? RetryFailedRequested;
    public event Action? GoBackRequested;
    public event Action? UndoRequested;

    public IRelayCommand RenameAnotherCommand { get; }
    public IRelayCommand DoneCommand { get; }
    public IRelayCommand RetryFailedCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand UndoCommand { get; }

    private readonly IClipboardService _clipboardService;
    private readonly IFileLoggerService _fileLoggerService;
    private readonly ILogger<RenameResultViewModel> _logger;

    public RenameResultViewModel(IClipboardService clipboardService, IFileLoggerService fileLoggerService, ILogger<RenameResultViewModel> logger)
    {
        _clipboardService = clipboardService;
        _fileLoggerService = fileLoggerService;
        _logger = logger;
        CopyErrorDetailsCommand = new AsyncRelayCommand(CopyErrorDetails);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        OpenLogFolderCommand = new RelayCommand(() => _fileLoggerService.OpenLogDirectory());
        RenameAnotherCommand = new RelayCommand(() => ResetRequested?.Invoke());
        DoneCommand = new RelayCommand(() => ResetRequested?.Invoke());
        RetryFailedCommand = new RelayCommand(() => RetryFailedRequested?.Invoke());
        GoBackCommand = new RelayCommand(() => GoBackRequested?.Invoke());
        UndoCommand = new RelayCommand(() => UndoRequested?.Invoke(), () => CanUndo);
    }

    private Task CopyErrorDetails() => _clipboardService.SetTextAsync(ErrorDetailedText);

    private void OpenFolder()
    {
        var folder = TargetFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder {Folder}", folder);
        }
    }

    public void RePopulateViewModel(
        IEnumerable<RenameOperationResult<RenameFileOperation>> operationResults,
        string showSeasonLabel,
        string targetFolder,
        bool isUndoResult = false)
    {
        OperationResults.Clear();
        foreach (var operationResult in operationResults)
        {
            OperationResults.Add(operationResult);
        }

        SuccessCount = OperationResults.Count(r => r.IsSuccessful);
        FailureCount = OperationResults.Count(r => !r.IsSuccessful);
        ShowSeasonLabel = showSeasonLabel;
        TargetFolder = targetFolder;
        IsUndone = isUndoResult;
        CanUndo = !isUndoResult && SuccessCount > 0;
        IsFullSuccess = FailureCount == 0 && SuccessCount > 0;

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SummaryHeadline));
        OnPropertyChanged(nameof(SummarySubtitle));
        OnPropertyChanged(nameof(RetryButtonLabel));
        OnPropertyChanged(nameof(StatsBarLabel));
        OnPropertyChanged(nameof(ErrorDetailedText));
    }
}
