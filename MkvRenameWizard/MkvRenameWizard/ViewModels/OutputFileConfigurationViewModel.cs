using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MkvRenameWizard.Helpers;
using MkvRenameWizard.Models.Renaming;
using ReactiveUI;
using Path = System.IO.Path;

namespace MkvRenameWizard.ViewModels;

public class OutputFileConfigurationViewModel : ViewModelBase
{
    public List<RenameEntity> RenameEntities
    {
        get;
        set
        {
            field = value;
            this.RaisePropertyChanged();
            var firstFullPath = value.FirstOrDefault()?.MkvFile.FullPath;
            var sourceDirectory = firstFullPath is not null ? new DirectoryInfo(firstFullPath) : null;

            TargetFolder = string.IsNullOrEmpty(sourceDirectory?.FullName)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : sourceDirectory.FullName;

            RebuildPreview();
        }
    } = new();

    public string CurrentShowName
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RebuildPreview();
        }
    } = string.Empty;
    
    private const string DefaultFileNamePattern = "{S##E##} {Title}";

    public string FileNamePattern
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DefaultFileNamePattern;
    
    public bool IsPatternValid { get; set => this.RaiseAndSetIfChanged(ref field, value); }
    
    public string? TargetFolder { get; set => this.RaiseAndSetIfChanged(ref field, value); }

    public ObservableCollection<PatternError> PatternErrors { get; } = new();
    public ObservableCollection<PatternSegmentViewModel> PatternSegments { get; } = new();

    public IReadOnlyList<PatternToken> AvailableTokens => FilePatternHelper.ValidTokens;
    
    public bool IsTokenTableExpanded { get; set => this.RaiseAndSetIfChanged(ref field, value);}
    
    private string Prefix {get; set => this.RaiseAndSetIfChanged(ref field, value);} = string.Empty;

    public CaseStyle SelectedCaseStyle
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = CaseStyle.Default;
    
    public bool IsCaseDefault
    {
        get => SelectedCaseStyle ==  CaseStyle.Default;
        set
        {
            if(value)
            {
                SelectedCaseStyle = CaseStyle.Default;
            }
        }
    }
    
    public bool IsCaseSnake
    {
        get => SelectedCaseStyle ==  CaseStyle.SnakeCase;
        set
        {
            if(value)
            {
                SelectedCaseStyle = CaseStyle.SnakeCase;
            }
        }
    }
    
    public bool IsCasePascal
    {
        get => SelectedCaseStyle ==  CaseStyle.PascalCase;
        set
        {
            if(value)
            {
                SelectedCaseStyle = CaseStyle.PascalCase;
            }
        }
    }
    
    public bool IsCaseCamel
    {
        get => SelectedCaseStyle ==  CaseStyle.CamelCase;
        set
        {
            if(value)
            {
                SelectedCaseStyle = CaseStyle.CamelCase;
            }
        }
    }
    
   public Func<Task<string?>>? PickFolderAsync { get; set; }

   public ObservableCollection<RenamePreviewItem<RenameFileOperation>> PreviewItems { get; } = new();
   
   public int ReadyCount { get;
       private set => this.RaiseAndSetIfChanged(ref field, value);
   }
   public int ConflictCount { get;
       private set => this.RaiseAndSetIfChanged(ref field, value);
   }
   
   public int SkippedCount { get;
       private set => this.RaiseAndSetIfChanged(ref field, value);
   }
   
   public ReactiveCommand<Unit, bool> ShowTokenTableCommand { get; }
   public ReactiveCommand<Unit, string> ResetPatternCommand { get; }
   public ReactiveCommand<PatternError, Unit> ApplySuggestionCommand { get; set; }
   public ReactiveCommand<Unit, Unit> PickTargetFolderCommand { get; }
   public ReactiveCommand<Unit, Unit> ExecuteRenameCommand { get; }

   private readonly ILogger<OutputFileConfigurationViewModel> _logger;
   public OutputFileConfigurationViewModel(ILogger<OutputFileConfigurationViewModel> logger)
   {
       _logger = logger;

       this.WhenAnyValue(x => x.FileNamePattern)
           .Subscribe(_ =>
           {
               RevalidatePattern();
               RebuildPreview();
           });

       this.WhenAnyValue(x => x.Prefix, x => x.SelectedCaseStyle).Subscribe(_ => RebuildPreview());

       this.WhenAnyValue(x => x.SelectedCaseStyle).Subscribe(_ =>
       {
           this.RaisePropertyChanged(nameof(IsCaseDefault));
           this.RaisePropertyChanged(nameof(IsCaseSnake));
           this.RaisePropertyChanged(nameof(IsCasePascal));
           this.RaisePropertyChanged(nameof(IsCaseCamel));
       });

       ShowTokenTableCommand = ReactiveCommand.Create(() => IsTokenTableExpanded = !IsTokenTableExpanded);
       ResetPatternCommand = ReactiveCommand.Create(() =>
           FileNamePattern = $"{{{Constants.TokenNames.SeasonEpisodePadded}}} {{{Constants.TokenNames.Title}}}");

       ApplySuggestionCommand = ReactiveCommand.Create<PatternError>(ApplySuggestion);
       PickTargetFolderCommand = ReactiveCommand.CreateFromTask(PickTargetFolder);
       
       var canRename = this.WhenAnyValue(x => x.IsPatternValid ,
           x => x.ReadyCount, 
           (valid,ready) => valid && ready > 0);
       ExecuteRenameCommand = ReactiveCommand.Create(() => {}, canRename);

       RevalidatePattern();
   }

   private void RevalidatePattern()
   {
       var errors = FilePatternHelper.Validate(FileNamePattern);
       var segments = FilePatternHelper.Parse(FileNamePattern);
       
       PatternErrors.Clear();
       foreach (var error in errors)
       {
           PatternErrors.Add(error);
       }
       
       PatternSegments.Clear();
       foreach (var segment in segments)
       {
           PatternSegments.Add(new PatternSegmentViewModel(){Text = segment.Text,SegmentType = segment.SegmentType});
       }
       
       IsPatternValid = errors.Count == 0;
       if (!IsPatternValid)
       {
           IsTokenTableExpanded = true;
       }
   }

   private void RebuildPreview()
   {
       PreviewItems.Clear();

       if (!IsPatternValid)
       {
           var i = 0;
           foreach (var entry in RenameEntities)
           {
               PreviewItems.Add(new RenamePreviewItem<RenameFileOperation>(
                   new RenameFileOperation(++i,Path.GetFileName(entry.MkvFile.FullPath) ?? string.Empty,null),
                   RenamePreviewStatus.PatternError)
               );
           }

           SetPreflightCounts();
           return;
       }
       
       var rawItems = new List<RenamePreviewItem<RenameFileOperation>>();
       var index = 0;
       foreach (var entry in RenameEntities)
       {
           index++;
           var source = Path.GetFileName(entry.MkvFile.FullPath) ?? string.Empty;
           if (entry.Episode.EpisodeNumber == null)
           {
               rawItems.Add(new RenamePreviewItem<RenameFileOperation>(new RenameFileOperation(index,source,null),RenamePreviewStatus.Skipped));
               continue;
           }
           
           var target = $"{FilePatternHelper.Apply(FileNamePattern,entry.Episode,CurrentShowName,Prefix,Path.GetExtension(entry.MkvFile.FullPath ?? string.Empty),SelectedCaseStyle)}{Path.GetExtension(entry.MkvFile.FullPath)}";
           var isDone = string.Equals(target, target, StringComparison.OrdinalIgnoreCase);
           var status = isDone ? RenamePreviewStatus.Done : RenamePreviewStatus.Skipped;
           rawItems.Add(new RenamePreviewItem<RenameFileOperation>(new RenameFileOperation(index,source,target),status));
       }

       var conflictNumbers = rawItems
           .Where(r => r.RenamePreviewStatus == RenamePreviewStatus.Ready)
           .GroupBy(r => r.RenameOperation.TargetPath, StringComparer.OrdinalIgnoreCase)
           .Where(g => g.Count() > 1)
           .SelectMany(g => g)
           .ToHashSet();

       foreach (var item in rawItems)
       {
           var finalStatus = item.RenamePreviewStatus;
           if (item.RenamePreviewStatus == RenamePreviewStatus.Ready &&
               conflictNumbers.Contains(item))
           {
               finalStatus = RenamePreviewStatus.Conflict;
           }
           
           PreviewItems.Add(item with { RenamePreviewStatus = finalStatus });
       }

       SetPreflightCounts();

   }

   private void SetPreflightCounts()
   {
       ReadyCount = PreviewItems.Count(p => p.RenamePreviewStatus == RenamePreviewStatus.Ready);
       ConflictCount = PreviewItems.Count(p => p.RenamePreviewStatus == RenamePreviewStatus.Conflict);
       SkippedCount = PreviewItems.Count(p => p.RenamePreviewStatus == RenamePreviewStatus.Skipped);
   }


   private async Task PickTargetFolder()
   {
       if (PickFolderAsync is null)
       {
           return;
       }
       
       var chosen = await PickFolderAsync();
       if (chosen is not null)
       {
           TargetFolder = chosen;
       }
   }

   private void ApplySuggestion(PatternError error)
   {
       if (error.Suggestion is null)
       {
           return;
       }

       FileNamePattern = FileNamePattern.Replace($"{{{error.TokenName}}}", $"{{{error.TokenName}}}");
   }
   
   /// <summary>
   /// Called by the WizardViewModel when the use returns to the rename screen from the result view screen.
   /// Updates each successfully rename entity's source path to the new renamed file path
   /// so the preview table reflects what is on disk.
   ///
   /// Pattern, prefix, CaseStyle, and TAregt folder are preserved.
   /// Succesfully renamed entities will show a "Done" Status
   /// If a user edits the apttern the Done rows flip back to Ready.
   /// </summary>
   /// <param name="results"></param>

   public void ApplyRenameResults(IEnumerable<RenameOperationResult<RenameFileOperation>> results)
   {
       var renamePaths = results
           .Where(r => r.IsSuccessful && r.RenameOperation.TargetPath != null)
           .ToDictionary(r => r.RenameOperation.SourcePath, r => r.RenameOperation.TargetPath, StringComparer.OrdinalIgnoreCase);


       foreach (var entity in RenameEntities)
       {
           if (entity.MkvFile.FullPath != null && renamePaths.TryGetValue(entity.MkvFile.FullPath, out var renamePath))
           {
               entity.MkvFile.FullPath = renamePath;
           }
       }
       RebuildPreview();
   }

   public IEnumerable<RenameFileOperation> BuildRenameOperations()
   {
       if (TargetFolder == null)
       {
           yield break;
       }
       
       var validItemsPreviewItems = PreviewItems.Where(p => 
           p.RenamePreviewStatus == RenamePreviewStatus.Ready && 
           p.RenameOperation.TargetPath != null);
       
       foreach (var item in validItemsPreviewItems)
       {
           if (item.RenameOperation.TargetPath == null)
           {
               continue;
           }
           
           yield return item.RenameOperation with
           {
               TargetPath = Path.Combine(TargetFolder, item.RenameOperation.TargetPath)
           };
               
       }
   }
   
   public void Reset()
   {
       RenameEntities = new List<RenameEntity>();
       CurrentShowName = string.Empty;
       FileNamePattern = DefaultFileNamePattern;
       Prefix = string.Empty;
       SelectedCaseStyle = CaseStyle.Default;
       TargetFolder = string.Empty;
       PreviewItems.Clear();
       PatternErrors.Clear();
       PatternSegments.Clear();
       ReadyCount = 0;
       ConflictCount = 0;
       SkippedCount = 0;
   }
}
