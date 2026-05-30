using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Models.Renaming;
using MkvRenameWizard.Services;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class RenameResultViewModel : ViewModelBase
{
    public bool IsFullSuccess { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public int SuccessCount { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public int FailureCount { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    public int TotalCount => this.SuccessCount + this.FailureCount;
    public string? ShowSeasonLabel { get;set => this.RaiseAndSetIfChanged(ref field, value); }
    public string? TargetFolder { get;set => this.RaiseAndSetIfChanged(ref field, value); }

    public ObservableCollection<RenameOperationResult<RenameFileOperation>> OperationResults { get; } = [];

    public string SummaryHeadline => IsFullSuccess
        ? "file(s) renamed"
        : $"Renamed {SuccessCount} file(s) to {TargetFolder} of {TotalCount} files";

    public string SummarySubtitle => IsFullSuccess
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

    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyErrorDetailsCommand { get; }

    public event Action? ResetRequested;
    public event Action? RetryFailedRequested;
    public event Action? GoBackRequested;
    
    public ReactiveCommand<Unit, Unit> RenameAnotherCommand => ReactiveCommand.Create(() => ResetRequested?.Invoke());
    public ReactiveCommand<Unit, Unit> DoneCommand => ReactiveCommand.Create(() => ResetRequested?.Invoke());
    public ReactiveCommand<Unit, Unit> RetryFailedCommand => ReactiveCommand.Create(() => RetryFailedRequested?.Invoke());
    public ReactiveCommand<Unit, Unit> GoBackCommand => ReactiveCommand.Create(() => GoBackRequested?.Invoke());
    
    

    private readonly IClipboardService _clipboardService;
    private readonly ILogger<RenameResultViewModel> _logger;
    public RenameResultViewModel(IClipboardService clipboardService, ILogger<RenameResultViewModel> logger)
    {
        _clipboardService = clipboardService;
        _logger = logger;
        CopyErrorDetailsCommand = ReactiveCommand.CreateFromTask(CopyErrorDetails);
        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);
    }

    private Task CopyErrorDetails()
    {
        return _clipboardService.SetTextAsync(ErrorDetailedText);
    }

    private void OpenFolder()
    {
        var folder = TargetFolder;
        if (!string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        

        try
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    public void RePopulateViewModel(IEnumerable<RenameOperationResult<RenameFileOperation>> operationResults
    ,string showSeasonLabel, string targetFolder)
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
        IsFullSuccess = FailureCount == 0 && SuccessCount > 0;
        
        this.RaisePropertyChanged(nameof(SummaryHeadline));
        this.RaisePropertyChanged(nameof(SummarySubtitle));
        this.RaisePropertyChanged(nameof(RetryButtonLabel));
        this.RaisePropertyChanged(nameof(StatsBarLabel));
        this.RaisePropertyChanged(nameof(ErrorDetailedText));
        

    }
}