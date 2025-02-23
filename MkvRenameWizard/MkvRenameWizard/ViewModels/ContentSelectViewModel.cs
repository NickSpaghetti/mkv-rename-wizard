using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using MkvRenameWizard.Models.TvMaze;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class ContentSelectViewModel : ViewModelBase
{
    public Show SelectedResult { get; set; }
    public ObservableCollection<string> ContentList { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> MkvFilesList { get; } = new ObservableCollection<string>();
    
    public ReactiveCommand<int, Unit> MoveContentItemUpCommand { get; }
    public ReactiveCommand<int, Unit> MoveContentItemDownCommand { get; }
    public ReactiveCommand<int, Unit> MoveMkvFileItemUpCommand { get; }
    public ReactiveCommand<int, Unit> MoveMkvFileItemDownCommand { get; }

    public ContentSelectViewModel(Show selectedResult, ObservableCollection<CheckboxOption<Season>> checkboxOptions)
    {
        SelectedResult = selectedResult;
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
    }

    private void MoveItemUp(ObservableCollection<string> list, int index)
    {
        if (index > 0)
        {
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(index - 1, item);
        }
    }

    private void MoveItemDown(ObservableCollection<string> list, int index)
    {
        if (index < list.Count - 1)
        {
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(index + 1, item);
        }
    }
}

