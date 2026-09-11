namespace Raisin.Audio.Tests;

/// <summary>
/// Stands in for whatever an app persists. Property names match what the definitions under
/// test declare, since the setting items reach them by reflection.
/// </summary>
/// <remarks>
/// A class of its own rather than a reach into either app's settings type: these tests are
/// about the editors, and tying them to one host's defaults would make them fail whenever that
/// host changed its mind about what "quiet" means.
/// </remarks>
public sealed class TestSoundSettings : ISoundSettings
{
    public bool SoundEnabled { get; set; } = true;
    public double SoundVolume { get; set; } = 0.7;
    public string SoundTheme { get; set; } = "minimal";
    public string SpeechVoice { get; set; } = "";
    public double SpeechRateValue { get; set; }
    public double SpeechBatchSecondsValue { get; set; } = 1.5;

    bool ISoundSettings.Enabled { get => SoundEnabled; set => SoundEnabled = value; }
    double ISoundSettings.Volume { get => SoundVolume; set => SoundVolume = value; }
    string ISoundSettings.Theme { get => SoundTheme; set => SoundTheme = value; }
    string ISoundSettings.Voice { get => SpeechVoice; set => SpeechVoice = value; }
    double ISoundSettings.SpeechRate => SpeechRateValue;
    double ISoundSettings.SpeechBatchSeconds => SpeechBatchSecondsValue;
}
