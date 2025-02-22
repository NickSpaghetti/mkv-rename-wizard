using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using System.Reactive;

namespace MkvRenameWizard.ViewModels;

public class WizardViewModel : ViewModelBase
{
    private int _currentPageIndex;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set => this.RaiseAndSetIfChanged(ref _currentPageIndex, value);
    }

    public ObservableCollection<ViewModelBase> Pages { get; }

    public bool CanGoBack => CurrentPageIndex > 0;
    public bool CanGoForward => CurrentPageIndex < Pages.Count - 1;
    public bool IsLastPage => CurrentPageIndex == Pages.Count - 1;

    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> FinishCommand { get; }

    private readonly ContentSearchViewModel _contentSearchViewModel;
    private readonly ContentSelectViewModel _contentSelectViewModel;

    public WizardViewModel()
    {
        _contentSearchViewModel = new ContentSearchViewModel();
        _contentSelectViewModel = new ContentSelectViewModel(_contentSearchViewModel.SelectedResult, _contentSearchViewModel.CheckboxOptions);

        // Subscribe to changes in ContentSearchViewModel.SelectedResult
        _contentSearchViewModel
            .WhenAnyValue(x => x.SelectedResult)
            .Subscribe((selectedResult) =>
            {
                _contentSelectViewModel.SelectedResult = selectedResult;
            });

        // Subscribe to changes in ContentSearchViewModel.CheckboxOptions
        _contentSearchViewModel
            .WhenAnyValue(x => x.CheckboxOptions)
            .Subscribe(checkboxOptions =>
            {
                _contentSelectViewModel.ContentList.Clear();
                _contentSelectViewModel.MkvFilesList.Clear();
                foreach (var option in checkboxOptions)
                {
                    if (option.IsChecked)
                        _contentSelectViewModel.ContentList.Add(option.Label);
                    else
                        _contentSelectViewModel.MkvFilesList.Add(option.Label);
                }
            });

        Pages = new ObservableCollection<ViewModelBase>
        {
            _contentSearchViewModel,
            _contentSelectViewModel,
        };

        PreviousCommand = ReactiveCommand.Create(Previous, this.WhenAnyValue(x => x.CanGoBack));
        NextCommand = ReactiveCommand.Create(Next, this.WhenAnyValue(x => x.CanGoForward));
        FinishCommand = ReactiveCommand.Create(Finish, this.WhenAnyValue(x => x.IsLastPage));
    }

    private void Previous() => CurrentPageIndex--;
    private void Next() => CurrentPageIndex++;
    private void Finish() { /* Implement finish logic */ }
}
