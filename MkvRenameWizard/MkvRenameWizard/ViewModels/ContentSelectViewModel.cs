
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MkvRenameWizard.DataAccess;
using MkvRenameWizard.Models.Mkv;
using MkvRenameWizard.Models.TvMaze;
using MkvRenameWizard.Services;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class ContentSelectViewModel : ViewModelBase
{
    public Show SelectedShow { get; set; }
    public ObservableCollection<string> ContentList { get; } = new ObservableCollection<string>();
    public ObservableCollection<MkvFile> MkvFilesList { get; } = new ObservableCollection<MkvFile>();
    
    public ReactiveCommand<int, Unit> MoveContentItemUpCommand { get; }
    public ReactiveCommand<int, Unit> MoveContentItemDownCommand { get; }
    public ReactiveCommand<int, Unit> MoveMkvFileItemUpCommand { get; }
    public ReactiveCommand<int, Unit> MoveMkvFileItemDownCommand { get; }
    public ReactiveCommand<List<string>, Unit> OpenFilesCommand { get; } 
    
    private readonly IMkvFinderService _mkvFinderService;

    public ContentSelectViewModel(Show selectedShow, ObservableCollection<CheckboxOption<Season>> checkboxOptions)
    {
        SelectedShow = selectedShow;
        foreach (var option in checkboxOptions)
        {
            if (option.IsChecked)
            {
                ContentList.Add(option.Label);
            }
        }

        MoveContentItemUpCommand = ReactiveCommand.Create<int>(index => MoveItemUp(ContentList, index));
        MoveContentItemDownCommand = ReactiveCommand.Create<int>(index => MoveItemDown(ContentList, index));
        MoveMkvFileItemUpCommand = ReactiveCommand.Create<int>(index => MoveItemUp(MkvFilesList, index));
        MoveMkvFileItemDownCommand = ReactiveCommand.Create<int>(index => MoveItemDown(MkvFilesList, index));
        OpenFilesCommand = ReactiveCommand.CreateFromTask<List<string>>(files => ExecuteOpenFilesCommand(MkvFilesList));

        _mkvFinderService = new MkvFinderService();
    }

    private void MoveItemUp<T>(ObservableCollection<T> list, int index)
    {
        if (index > 0)
        {
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(index - 1, item);
        }
    }

    private void MoveItemDown<T>(ObservableCollection<T> list, int index)
    {
        if (index < list.Count - 1)
        {
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(index + 1, item);
        }
    }
    
    
    private async Task ExecuteOpenFilesCommand(ObservableCollection<MkvFile> mkvFileList)
    {
        var app = Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (app == null)
        {
            return;
        }
        var topLevel = TopLevel.GetTopLevel(app.MainWindow);
        if (topLevel == null)
        {
            return;
        }
        
        var files = await  _mkvFinderService.OpenMkvFiles(topLevel);
        if (files == null)
        {
            return;
        }
        
        mkvFileList.Clear();
        foreach (var mkvFiles in files)
        {
            foreach (var mkvFile in mkvFiles.Value.OrderBy(f => f.FullPath))
            {
                mkvFileList.Add(mkvFile);
            }
        }
        
    }
    
}

