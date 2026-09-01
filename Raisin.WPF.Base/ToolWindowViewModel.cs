namespace Raisin.WPF.Base;

public abstract class ToolWindowViewModel : ViewModelBase
{
    private static readonly List<ToolWindowViewModel> _allInstances = [];
    private static readonly object _lock = new();

    private string _baseTitle = "";

    private string _title = "";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _contentId = "";
    public string ContentId
    {
        get => _contentId;
        set => SetProperty(ref _contentId, value);
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private bool _isNew;
    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }

    private bool _isMaximizedOverlay;
    public bool IsMaximizedOverlay
    {
        get => _isMaximizedOverlay;
        set => SetProperty(ref _isMaximizedOverlay, value);
    }

    /// <summary>Called to remove this VM from its parent collection and close the tab.</summary>
    public Action? CloseAction { get; set; }

    protected ToolWindowViewModel()
    {
        lock (_lock) _allInstances.Add(this);
    }

    protected void UpdateBaseTitle(string baseTitle)
    {
        _baseTitle = baseTitle;
        RefreshTitles();
    }

    protected void UnregisterInstance()
    {
        lock (_lock) _allInstances.Remove(this);
        RefreshTitles();
    }

    private static void RefreshTitles()
    {
        List<ToolWindowViewModel> snapshot;
        lock (_lock) { snapshot = _allInstances.ToList(); }
        // Windows that never called UpdateBaseTitle set Title themselves; numbering them
        // together would collapse unrelated titles into a bare " (2)".
        var groups = snapshot.Where(vm => vm._baseTitle.Length > 0)
                             .GroupBy(vm => vm._baseTitle);
        foreach (var group in groups)
        {
            var list = group.ToList();
            if (list.Count == 1)
            {
                list[0].Title = list[0]._baseTitle;
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                    list[i].Title = $"{list[i]._baseTitle} ({i + 1})";
            }
        }
    }

    /// <summary>
    /// Closes the window. Instances register themselves on construction, so overrides
    /// must call <c>base.OnClose()</c> to stop counting toward duplicate-title
    /// numbering once the tab is gone.
    /// </summary>
    public virtual void OnClose()
    {
        UnregisterInstance();
    }
}
