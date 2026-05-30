using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MkvRenameWizard.ViewModels;

namespace MkvRenameWizard.Views;

public partial class OutputFileConfigurationView : UserControl
{
    public OutputFileConfigurationView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WirePickFolder();
    }

    private void WirePickFolder()
    {
        if (DataContext is not OutputFileConfigurationViewModel outputFileConfigurationViewModel)
        {
            return;
        }

        outputFileConfigurationViewModel.PickFolderAsync = async () =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return null;
            }

            var startPath = !string.IsNullOrEmpty(outputFileConfigurationViewModel.TargetFolder) &&
                            Directory.Exists(outputFileConfigurationViewModel.TargetFolder)
                ? outputFileConfigurationViewModel.TargetFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            IStorageFolder? startFolder = null;
            startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startPath);

            IReadOnlyList<IStorageFolder?> results = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Chose destination folder",
                    AllowMultiple = false,
                    SuggestedStartLocation = startFolder
                });

            return results.Count > 0 ? results[0]?.TryGetLocalPath() : null;
        };
    }
    
}