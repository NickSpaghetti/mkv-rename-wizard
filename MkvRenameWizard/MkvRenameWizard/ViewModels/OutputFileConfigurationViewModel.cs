using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Text.RegularExpressions;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class OutputFileConfigurationViewModel : ViewModelBase
{
    public Dictionary<string, string> FileContentMap;

    public ObservableCollection<string> PreviewList { get; } = new ObservableCollection<string>();

    public bool UseDefaultName { get; set; } = true;
    public bool UseSnakeCase { get; set; }
    public bool UseCustomName { get; set; }
    public string CustomNameRegex { get; set; } = string.Empty;

    public ReactiveCommand<Unit, Unit> UpdatePreviewCommand { get; }

    public OutputFileConfigurationViewModel(Dictionary<string, string> fileContentMap)
    {
        FileContentMap = fileContentMap ?? throw new ArgumentNullException(nameof(fileContentMap));

        // Ensure only one checkbox is selected at a time
        this.WhenAnyValue(
                x => x.UseDefaultName,
                x => x.UseSnakeCase,
                x => x.UseCustomName
            )
            .Subscribe(_ =>
            {
                if (UseDefaultName)
                {
                    UseSnakeCase = false;
                    UseCustomName = false;
                }
                else if (UseSnakeCase)
                {
                    UseDefaultName = false;
                    UseCustomName = false;
                }
                else if (UseCustomName)
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
            var newName = entry.Value;

            if (UseSnakeCase)
            {
                newName = entry.Value.Replace(" ", "_").ToLowerInvariant();
            }
            else if (UseCustomName && !string.IsNullOrEmpty(CustomNameRegex))
            {
                try
                {
                    newName = Regex.Replace(entry.Value, CustomNameRegex, ""); // Or whatever replacement you want
                }
                catch (Exception e)
                {
                    newName = "Invalid Regex";
                }
            }

            PreviewList.Add($"{entry.Key} -> {newName}");
        }
    }
}
