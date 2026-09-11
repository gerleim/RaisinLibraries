using Raisin.WPF.Base;
using Raisin.WPF.Base.Settings;

namespace Raisin.Audio;

/// <summary>
/// Volume as a slider you can hear, rather than a number you type.
/// </summary>
/// <remarks>
/// A facade over <see cref="DoubleSettingItem.Text"/> rather than a separate value: the base
/// class parses that text in ApplyTo and compares it in UpdateIsModified, so keeping it as the
/// single source of truth means saving and the modified marker keep working untouched.
/// <para>
/// Formatting stays in the current culture for the same reason - the base parses with it, and a
/// machine using a comma decimal separator would otherwise fail to apply an invariant "0.7".
/// </para>
/// </remarks>
public sealed class VolumeSettingItem : DoubleSettingItem
{
    private readonly Func<ISoundService?> _sounds;
    private readonly SoundResolver _resolver;
    private readonly Func<string?>? _sampleSpec;

    /// <param name="sampleSpec">
    /// The host's idea of a representative sound - usually whatever its main alert is set to.
    /// Supplied rather than read from settings so this stays free of any one app's setting names.
    /// </param>
    public VolumeSettingItem(
        SettingDefinition definition,
        Func<object> defaultFactory,
        SoundResolver resolver,
        Func<ISoundService?> sounds,
        Func<string?>? sampleSpec = null)
        : base(definition, defaultFactory)
    {
        _resolver = resolver;
        _sounds = sounds;
        _sampleSpec = sampleSpec;
        PreviewCommand = new RelayCommand(Preview);
    }

    public RelayCommand PreviewCommand { get; }

    public double Min => Definition.MinDouble ?? 0;
    public double Max => Definition.MaxDouble ?? 1;

    public double Value
    {
        get => double.TryParse(Text, out var value) ? value : Min;
        set
        {
            var clamped = Math.Clamp(value, Min, Max);
            var text = clamped.ToString("0.##");
            if (text == Text) return;

            Text = text;

            // Apply immediately so the preview - and any sound the app happens to play while
            // the window is open - is at the volume being dragged, not the last saved one.
            var sounds = _sounds();
            if (sounds is not null) sounds.Volume = clamped;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Percent));
        }
    }

    /// <summary>The slider's readout. This setting is a 0-to-1 fraction, so it reads as a percentage.</summary>
    public string Percent => $"{Math.Round(Value * 100)}%";

    public override void LoadFrom(object data)
    {
        base.LoadFrom(data);
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Percent));
    }

    /// <summary>
    /// Something representative to judge the volume by: whatever the alert is set to, falling
    /// back to any event the theme has, and to the Windows beep if there is no theme at all.
    /// </summary>
    /// <param name="alertSound">
    /// The host's representative sound. Passed in rather than read from settings so the choice
    /// can be tested without depending on whatever the running machine happens to have saved.
    /// </param>
    public string SampleSpec(string? alertSound)
    {
        if (!string.IsNullOrWhiteSpace(alertSound)) return alertSound;

        var events = _resolver.GetThemeEventNames();
        if (events.Count > 0)
            return events.Contains("notification", StringComparer.OrdinalIgnoreCase)
                ? "notification"
                : events[0];

        return SoundResolver.SystemPrefix + "Beep";
    }

    private void Preview()
    {
        var sounds = _sounds();
        if (sounds is null) return;

        sounds.Volume = Value;
        sounds.Play(SampleSpec(_sampleSpec?.Invoke()));
    }
}
