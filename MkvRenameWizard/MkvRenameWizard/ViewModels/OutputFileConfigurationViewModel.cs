using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using Avalonia.Controls.Shapes;
using MkvRenameWizard.Models.Mkv;
using ReactiveUI;
using Path = System.IO.Path;

namespace MkvRenameWizard.ViewModels;

public class OutputFileConfigurationViewModel : ViewModelBase
{
    public Dictionary<string, MkvFile> FileContentMap;

    public ObservableCollection<string> PreviewList { get; } = new ObservableCollection<string>();

    public bool UseDefaultName { get; set; } = true;
    public bool UseSnakeCase { get; set; }
    public bool UsePascalCase { get; set; }

    public ReactiveCommand<Unit, Unit> UpdatePreviewCommand { get; }
    
    private string _prefix = "e"; // Initialize in the ViewModel as well

    public string Prefix
    {
        get => _prefix;
        set => this.RaiseAndSetIfChanged(ref _prefix, value);
    }

    public OutputFileConfigurationViewModel(Dictionary<string, MkvFile> fileContentMap)
    {
        FileContentMap = fileContentMap ?? throw new ArgumentNullException(nameof(fileContentMap));

        // Ensure only one checkbox is selected at a time
        this.WhenAnyValue(
                x => x.UseDefaultName,
                x => x.UseSnakeCase,
                x => x.UsePascalCase
            )
            .Subscribe(_ =>
            {
                if (UseDefaultName)
                {
                    UseSnakeCase = false;
                    UsePascalCase = false;
                }
                else if (UseSnakeCase)
                {
                    UseDefaultName = false;
                    UsePascalCase = false;
                }
                else if (UsePascalCase)
                {
                    UseDefaultName = false;
                    UseSnakeCase = false;
                }
                UpdatePreviewList();
            });

        UpdatePreviewCommand = ReactiveCommand.Create(UpdatePreviewList);
        
        UpdatePreviewList();
    }

    private void UpdatePreviewList()
    {
        PreviewList.Clear();

        foreach (var entry in FileContentMap)
        {
            var newName = Path.GetInvalidFileNameChars().Aggregate(entry.Key, (current, c) 
                => current.Replace(c.ToString(), string.Empty));

            if (UseSnakeCase)
            {
                newName = newName.Replace(" ", "_").ToLowerInvariant();
            }
            else if (UsePascalCase)
            {
                try
                {
                    var snakeCase = newName.Replace(" ", "_").ToLowerInvariant();
                    var words = snakeCase.Replace("_", " ").Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    newName = string.Join("", words.Select(word => word.First().ToString().ToUpper() + word[1..].ToLower()));
                }
                catch (Exception e)
                {
                    newName = "Invalid Regex";
                }
            }

            PreviewList.Add($"{Path.GetFileName(entry.Value.FullPath)} -> {newName}");
        }
    }
}
