using Raisin.WPF.Base.Settings;

namespace Raisin.Audio;

/// <summary>
/// The theme dropdown, taking effect the moment it is picked rather than when the pane saves.
/// </summary>
/// <remarks>
/// Playback reads the theme from settings, and settings are only written by the options pane's
/// one-second autosave - which every mouse-up restarts, the click on a row's play button
/// included. So previewing a sound straight after switching theme would play the old theme, and
/// clicking again would keep pushing the save further out. Applying on change removes the race
/// and matches <see cref="VolumeSettingItem"/>, which already applies as the slider is dragged.
/// </remarks>
public sealed class SoundThemeSettingItem : ChoiceSettingItem
{
    private readonly SoundResolver _resolver;
    private readonly Func<ISoundService?> _sounds;

    /// <summary>Set while loading, so reading the stored value does not count as picking one.</summary>
    private bool _loading;

    public SoundThemeSettingItem(
        SettingDefinition definition,
        Func<object> defaultFactory,
        SoundResolver resolver,
        Func<ISoundService?> sounds)
        : base(definition, defaultFactory)
    {
        _resolver = resolver;
        _sounds = sounds;

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SelectedValue)) ApplyNow();
        };
    }

    public override void LoadFrom(object data)
    {
        _loading = true;
        try { base.LoadFrom(data); }
        finally { _loading = false; }
    }

    private void ApplyNow()
    {
        if (_loading || string.IsNullOrWhiteSpace(SelectedValue)) return;

        // The picker's own resolver decides what the event dropdowns list; a user theme can
        // carry names the bundled ones do not.
        _resolver.Theme = SelectedValue;

        // With no service there is nothing playing to keep in step; the value is still saved
        // by ApplyTo when the pane applies, so nothing is lost by doing nothing here.
        var sounds = _sounds();
        if (sounds is not null) sounds.Theme = SelectedValue;
    }
}
