using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace MkvRenameWizard.ViewModels;

public class ContentSearchViewModel : ViewModelBase
{
    private string _searchText;
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    private ObservableCollection<string> _searchResults;
    public ObservableCollection<string> SearchResults
    {
        get => _searchResults;
        set => this.RaiseAndSetIfChanged(ref _searchResults, value);
    }
    
    private string _selectedResult;
    public string SelectedResult
    {
        get => _selectedResult;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedResult, value);
            UpdateCheckboxOptions();
        }
    }

    private ObservableCollection<CheckboxOption> _checkboxOptions;
    public ObservableCollection<CheckboxOption> CheckboxOptions
    {
        get => _checkboxOptions;
        set => this.RaiseAndSetIfChanged(ref _checkboxOptions, value);
    }

    public ReactiveCommand<Unit, Unit> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }

    public ContentSearchViewModel()
    {
        SearchResults = new ObservableCollection<string>();
        CheckboxOptions = new ObservableCollection<CheckboxOption>();
        SearchCommand = ReactiveCommand.Create(ExecuteSearchCommand);
        NextCommand = ReactiveCommand.Create(ExecuteNextCommand);
    }

    public void ExecuteNextCommand()
    {
        // Implement next command logic
    }

    public void ExecuteSearchCommand()
    {
        var results = new List<string> { "Result 1", "Result 2", "Result 3" };
        SearchResults.Clear();
        foreach (var result in results)
        {
            SearchResults.Add(result);
        }
    }

    private void UpdateCheckboxOptions()
    {
        CheckboxOptions.Clear();
        if (SelectedResult != null)
        {
            // Add dynamic checkbox options based on the selected result
            CheckboxOptions.Add(new CheckboxOption($"Option 1 for {SelectedResult}"));
            CheckboxOptions.Add(new CheckboxOption($"Option 2 for {SelectedResult}"));
            CheckboxOptions.Add(new CheckboxOption($"Option 3 for {SelectedResult}"));
        }
    }
}