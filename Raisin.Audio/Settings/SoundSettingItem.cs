using System.Collections.ObjectModel;
using Microsoft.Win32;
using Raisin.WPF.Base;
using Raisin.WPF.Base.Settings;

namespace Raisin.Audio;

/// <summary>One entry in the source dropdown.</summary>
public sealed record SoundSourceOption(SoundSource Source, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The editor behind every event sound: a source dropdown, then a value control that follows it.
/// </summary>
/// <remarks>
/// Its own type rather than something Raisin.WPF.Base knows about -
/// <see cref="SettingItemTemplateSelector"/> falls through to its base for types it does not
/// know, so this editor needs no change there.
/// <para>
/// What gets stored is still the one string the resolver has always read. Switching source
/// rewrites that string; it does not add a second setting to migrate.
/// </para>
/// </remarks>
public sealed class SoundSettingItem : SettingItemViewModel
{
    public static readonly IReadOnlyList<SoundSourceOption> AllSources =
    [
        new(SoundSource.Silent, "Silent"),
        new(SoundSource.Theme, "Theme sound"),
        new(SoundSource.System, "Windows sound"),
        new(SoundSource.Media, "Media folder"),
        new(SoundSource.File, "Custom file"),
        new(SoundSource.Speech, "Spoken text"),
    ];

    /// <summary>
    /// What switching to spoken text starts from when the host has no wording for this event.
    /// Deliberately bare: an app that cares supplies its own, and a sentence invented here
    /// would be a sentence about the wrong domain.
    /// </summary>
    public const string GenericPhrase = "{subject}";

    /// <summary>
    /// The starting wording for this particular event, so the source is useful the moment it is
    /// picked and says something true rather than the same sentence on every row.
    /// </summary>
    public string DefaultPhrase =>
        _spokenDefaults is not null
        && _spokenDefaults.TryGetValue(Definition.PropertyName, out var phrase)
            ? phrase
            : GenericPhrase;

    private readonly SoundResolver _resolver;
    private readonly Func<ISoundService?> _sounds;
    private readonly IReadOnlyDictionary<string, string>? _spokenDefaults;
    private readonly Func<SoundContext>? _previewContext;

    /// <summary>Set while loading a stored value, so rebuilding the list does not overwrite it.</summary>
    private bool _loading;

    /// <param name="spokenDefaults">
    /// The starting wording per property name, when the host has one. Keyed the same way the
    /// registry declares its settings.
    /// </param>
    /// <param name="previewContext">
    /// Something for a spoken preview to talk about. A preview of "{symbol} filled" with
    /// nothing to fill in would just say "filled".
    /// </param>
    public SoundSettingItem(
        SettingDefinition definition,
        Func<object> defaultFactory,
        SoundResolver resolver,
        Func<ISoundService?> sounds,
        IReadOnlyDictionary<string, string>? spokenDefaults = null,
        Func<SoundContext>? previewContext = null)
        : base(definition, defaultFactory)
    {
        _resolver = resolver;
        _sounds = sounds;
        _spokenDefaults = spokenDefaults;
        _previewContext = previewContext;

        PreviewCommand = new RelayCommand(Preview);
        BrowseCommand = new RelayCommand(Browse);
    }

    public IReadOnlyList<SoundSourceOption> Sources => AllSources;

    public RelayCommand PreviewCommand { get; }
    public RelayCommand BrowseCommand { get; }

    /// <summary>The values offered for the current source. Empty for Silent and Custom file.</summary>
    public ObservableCollection<string> Values { get; } = [];

    private SoundSourceOption _selectedSource = AllSources[0];
    public SoundSourceOption SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (value is null || !SetProperty(ref _selectedSource, value)) return;

            RefreshValues();
            OnPropertyChanged(nameof(IsFileSource));
            OnPropertyChanged(nameof(HasValueList));
            OnPropertyChanged(nameof(IsSpeechSource));
            OnPropertyChanged(nameof(CanPreview));
            if (!_loading) UpdateIsModified();
        }
    }

    private string _selectedValue = "";
    public string SelectedValue
    {
        get => _selectedValue;
        set
        {
            if (!SetProperty(ref _selectedValue, value ?? "")) return;
            OnPropertyChanged(nameof(CanPreview));
            OnPropertyChanged(nameof(SpokenSingular));
            OnPropertyChanged(nameof(SpokenPlural));
            if (!_loading) UpdateIsModified();
        }
    }

    /// <summary>True when the value is a path typed or browsed for, rather than picked from a list.</summary>
    public bool IsFileSource => SelectedSource.Source == SoundSource.File;

    /// <summary>True when there is a list to choose from - everything except Silent, a file and speech.</summary>
    public bool HasValueList => SelectedSource.Source is SoundSource.Theme
        or SoundSource.System or SoundSource.Media;

    /// <summary>True when the row edits words to speak rather than a sound to find.</summary>
    public bool IsSpeechSource => SelectedSource.Source == SoundSource.Speech;

    /// <summary>What to say about a single one. Half of the stored phrase.</summary>
    public string SpokenSingular
    {
        get => SpeechPhrase.Parse(SelectedValue).Singular;
        set => SelectedValue = (SpeechPhrase.Parse(SelectedValue) with { Singular = value ?? "" }).Compose();
    }

    /// <summary>What to say when a batch covered several. Blank falls back to one sentence each.</summary>
    public string SpokenPlural
    {
        get => SpeechPhrase.Parse(SelectedValue).Plural;
        set => SelectedValue = (SpeechPhrase.Parse(SelectedValue) with { Plural = value ?? "" }).Compose();
    }

    public bool CanPreview => Spec.Length > 0;

    /// <summary>What actually gets written to settings.</summary>
    public string Spec => SoundSpec.Compose(SelectedSource.Source, SelectedValue);

    public override void LoadFrom(object data)
    {
        var spec = GetProperty(data, Definition.PropertyName)?.ToString() ?? "";
        var source = SoundSpec.Classify(spec, _resolver);

        _loading = true;
        try
        {
            SelectedSource = AllSources.First(o => o.Source == source);
            SelectedValue = SoundSpec.ValueOf(spec, source);
        }
        finally { _loading = false; }
    }

    public override void ApplyTo(object data)
        => SetPropertyValue(data, Definition.PropertyName, Spec);

    public override void UpdateIsModified()
    {
        var stored = GetProperty(CreateDefault(), Definition.PropertyName)?.ToString() ?? "";
        IsModified = !string.Equals(Spec, stored, StringComparison.OrdinalIgnoreCase);
    }

    public override bool MatchesSearch(string query)
    {
        if (base.MatchesSearch(query)) return true;
        return !string.IsNullOrWhiteSpace(query)
            && Spec.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rebuilds the value list for the current source, keeping the selection when the new list
    /// still offers it - switching to Windows sounds and back should not lose the event name.
    /// </summary>
    private void RefreshValues()
    {
        var previous = SelectedValue;

        Values.Clear();
        foreach (var value in ValuesFor(SelectedSource.Source))
            Values.Add(value);

        if (_loading) return;

        if (Values.Contains(previous, StringComparer.OrdinalIgnoreCase)) SelectedValue = previous;
        else if (SelectedSource.Source == SoundSource.File) SelectedValue = previous;
        else if (SelectedSource.Source == SoundSource.Speech)
            SelectedValue = LooksLikeAPhrase(previous) ? previous : DefaultPhrase;
        else SelectedValue = Values.Count > 0 ? Values[0] : "";
    }

    /// <summary>A sound name carried over from another source is not a sentence; a phrase is.</summary>
    private static bool LooksLikeAPhrase(string value) =>
        value.Contains('{') || value.Contains(' ');

    private IEnumerable<string> ValuesFor(SoundSource source) => source switch
    {
        SoundSource.Theme => _resolver.GetThemeEventNames(),
        SoundSource.System => SoundResolver.SystemSoundNames,
        SoundSource.Media => _resolver.GetMediaFolderSounds(),
        _ => [],
    };

    private void Preview()
    {
        var spec = Spec;
        if (spec.Length == 0) return;

        // Straight to the service, so a muted or throttled setting is heard as it really is.
        _sounds()?.Play(spec, _previewContext?.Invoke());
    }

    private void Browse()
    {
        var extensions = string.Join(";", SoundResolver.SupportedExtensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Title = $"Sound for \"{DisplayName}\"",
            Filter = $"Audio files ({extensions})|{extensions}|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (IsFileSource && SelectedValue.Length > 0)
        {
            try
            {
                var folder = System.IO.Path.GetDirectoryName(SelectedValue);
                if (folder is not null && System.IO.Directory.Exists(folder))
                    dialog.InitialDirectory = folder;
            }
            catch (ArgumentException) { }
        }

        if (dialog.ShowDialog() != true) return;

        SelectedSource = AllSources.First(o => o.Source == SoundSource.File);
        SelectedValue = dialog.FileName;
    }
}
