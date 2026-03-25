using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DevTools.Uno.Sample;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private string _searchQuery = "DevTools bindings";
    private double _actionOpacity = 0.82;
    private bool _isFeatureEnabled = true;
    private string _selectedCategory = "Bindings";
    private BindingSampleItem? _selectedResult;

    public MainPageViewModel()
    {
        Categories = ["Bindings", "Templates", "Resources", "Memory"];
        Results =
        [
            new BindingSampleItem("SearchBox", "TwoWay {Binding} on TextBox.Text with live query updates."),
            new BindingSampleItem("ElementName", "Mirror text bound directly to SearchBox.Text."),
            new BindingSampleItem("x:Bind", "Compiled bindings drive summary text and progress."),
            new BindingSampleItem("TemplateBinding", "Custom tile template forwards Title and Body through TemplateBinding."),
        ];
        _selectedResult = Results[0];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Categories { get; }

    public ObservableCollection<BindingSampleItem> Results { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public double ActionOpacity
    {
        get => _actionOpacity;
        set
        {
            if (SetProperty(ref _actionOpacity, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public bool IsFeatureEnabled
    {
        get => _isFeatureEnabled;
        set
        {
            if (SetProperty(ref _isFeatureEnabled, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public BindingSampleItem? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                RaiseDerivedProperties();
            }
        }
    }

    public string ActionButtonText => IsFeatureEnabled ? "Show flyout" : "Feature disabled";

    public string StatusText =>
        $"Query: {FormatQuery(SearchQuery)} | Category: {SelectedCategory} | Feature: {(IsFeatureEnabled ? "enabled" : "disabled")}";

    public string SearchSummary =>
        $"DataContext binding sees {SearchQuery.Length} character(s) and {Results.Count} sample target(s).";

    public string TileTitle => $"Template binding surface: {SelectedCategory}";

    public string TileBody =>
        $"Selected target: {SelectedResult?.Title ?? "none"} | Opacity: {ActionOpacity:0.00} | Query: {FormatQuery(SearchQuery)}";

    public string CompiledBindingSummary =>
        $"Compiled summary -> progress {ProgressValue:0.#}, feature {(IsFeatureEnabled ? "on" : "off")}, selected {SelectedResult?.Title ?? "none"}.";

    public double ProgressValue => Math.Round(ActionOpacity * 100d, 1);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void RaiseDerivedProperties()
    {
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SearchSummary));
        OnPropertyChanged(nameof(TileTitle));
        OnPropertyChanged(nameof(TileBody));
        OnPropertyChanged(nameof(CompiledBindingSummary));
        OnPropertyChanged(nameof(ProgressValue));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatQuery(string? query)
        => string.IsNullOrWhiteSpace(query) ? "(empty)" : $"\"{query}\"";
}

public sealed record BindingSampleItem(string Title, string Description);
