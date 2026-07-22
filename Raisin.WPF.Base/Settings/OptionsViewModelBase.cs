using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Raisin.WPF.Base.Settings;

public abstract class OptionsViewModelBase : ViewModelBase
{
    public List<SettingItemViewModel> AllSettings { get; } = [];
    public ObservableCollection<object> DisplayItems { get; } = [];
    public List<string> Categories { get; }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) _debounceTimer.Start(); }
    }

    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value) && value is not null) CategorySelected?.Invoke(value); }
    }

    public event Action<string>? CategorySelected;

    private readonly DispatcherTimer _debounceTimer;

    protected OptionsViewModelBase()
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounceTimer.Tick += (_, _) => { _debounceTimer.Stop(); RefreshDisplay(); };

        Categories = [.. GetCategoryOrder()];
        var defaultFactory = GetDefaultFactory();
        var categoryOrder = GetCategoryOrder();

        foreach (var def in GetRegistry().OrderBy(d =>
            Array.IndexOf(categoryOrder, d.Category) * 10000 + d.Order))
        {
            var item = CreateSettingItem(def, defaultFactory);
            if (item is null) continue;

            item.LoadFrom(LoadCurrentSettings());
            item.UpdateIsModified();
            AllSettings.Add(item);
        }

        RefreshDisplay();
    }

    protected abstract List<SettingDefinition> GetRegistry();
    protected abstract string[] GetCategoryOrder();
    protected abstract object CreateDefaultSettings();
    protected abstract object LoadCurrentSettings();
    protected abstract void SaveSettings(object settings);
    protected abstract Func<object> GetDefaultFactory();

    protected virtual SettingItemViewModel? CreateSettingItem(
        SettingDefinition def, Func<object> defaultFactory)
    {
        return SettingItemFactory.TryCreate(def, defaultFactory);
    }

    public virtual void RefreshDisplay()
    {
        DisplayItems.Clear();
        var query = SearchText.Trim();

        foreach (var category in GetCategoryOrder())
        {
            var items = AllSettings
                .Where(s => s.Category == category && s.MatchesSearch(query))
                .ToList();
            if (items.Count == 0) continue;

            DisplayItems.Add(new CategoryHeaderItem(category));
            foreach (var item in items)
                DisplayItems.Add(item);
        }
    }

    public virtual void Apply()
    {
        var settings = CreateDefaultSettings();
        foreach (var item in AllSettings)
            item.ApplyTo(settings);
        SaveSettings(settings);

        foreach (var item in AllSettings)
            item.UpdateIsModified();
    }

    public void ResetCategory(string category)
    {
        foreach (var item in AllSettings.Where(s => s.Category == category))
        {
            item.ResetToDefault();
            item.UpdateIsModified();
        }
    }

    public void ScrollToCategory(string category)
    {
        _debounceTimer.Stop();
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            _searchText = "";
            OnPropertyChanged(nameof(SearchText));
            RefreshDisplay();
        }
        SelectedCategory = category;
    }
}
