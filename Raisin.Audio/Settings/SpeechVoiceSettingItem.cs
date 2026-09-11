using Raisin.WPF.Base;
using Raisin.WPF.Base.Settings;

namespace Raisin.Audio;

/// <summary>
/// The voice dropdown, with a button to hear it.
/// </summary>
/// <remarks>
/// Voice names say nothing about how a voice sounds - David, Mark and Zira are not
/// descriptions - so picking one without hearing it is guesswork. The sample is spoken through
/// <see cref="ISoundService.Say"/>, which skips the batching window: a second and a half of
/// silence after clicking a test button reads as a broken button.
/// <para>
/// Like the theme dropdown, the choice applies as it is picked rather than when the pane
/// autosaves, so the voice you hear is the voice you just selected.
/// </para>
/// </remarks>
public sealed class SpeechVoiceSettingItem : ChoiceSettingItem
{
    /// <summary>Neutral on purpose: this is judging the voice, not the wording of an alert.</summary>
    public const string Sample = "This is how spoken alerts will sound.";

    private readonly Func<ISoundService?> _sounds;
    private bool _loading;

    public SpeechVoiceSettingItem(
        SettingDefinition definition,
        Func<object> defaultFactory,
        Func<ISoundService?> sounds)
        : base(definition, defaultFactory)
    {
        _sounds = sounds;
        PreviewCommand = new RelayCommand(Preview);

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SelectedValue)) ApplyNow();
        };
    }

    public RelayCommand PreviewCommand { get; }

    /// <summary>The blank entry means "whatever Windows defaults to", which is still testable.</summary>
    public bool CanPreview => true;

    public override void LoadFrom(object data)
    {
        _loading = true;
        try { base.LoadFrom(data); }
        finally { _loading = false; }
    }

    private void ApplyNow()
    {
        if (_loading) return;

        // Through the service where there is one, mirroring the theme dropdown. With no
        // service the value is still saved by ApplyTo when the pane applies.
        var sounds = _sounds();
        if (sounds is not null) sounds.Voice = SelectedValue ?? "";
    }

    private void Preview()
    {
        // Passed explicitly rather than left to settings: the pane may not have saved yet.
        _sounds()?.Say(Sample, SelectedValue);
    }
}
