using Raisin.WPF.Base.Settings;
using Raisin.Audio;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>
/// The voice row: a dropdown whose entries cannot be judged without hearing them, so it carries
/// a test button that must speak the voice currently selected - not the one last saved.
/// </summary>
public class SpeechVoiceSettingItemTests
{
    private sealed class FakeSounds : ISoundService
    {
        public bool Enabled { get; set; } = true;
        public double Volume { get; set; }
        public string Theme { get; set; } = "";
        public IReadOnlyList<string> AvailableThemes => [];
        public string Voice { get; set; } = "";
        public IReadOnlyList<string> AvailableVoices => ["Microsoft David", "Microsoft Zira"];
        public List<(string Text, string? Voice)> Said { get; } = [];

        public void Play(string? spec, SoundContext? context = null) { }
        public void CancelPending(string? subject) { }
        public bool IsSpeaking => false;
        public void StopSpeaking() { }
#pragma warning disable CS0067   // the picker tests never raise these
        public event Action<long, int, int>? SpeechProgress;
        public event Action<long>? SpeechFinished;
#pragma warning restore CS0067
        public void Preload(string? spec) { }
        public long Say(string text, string? voice = null, double? rate = null)
        {
            Said.Add((text, voice));
            return Said.Count;
        }
    }

    private static readonly SettingDefinition Definition = new()
    {
        Key = "sound.speech-voice",
        DisplayName = "Speech voice",
        Description = "",
        Category = "Playback",
        PropertyName = "SpeechVoice",
        EditorType = SettingEditorType.Choice,
        Choices = ["", "Microsoft David", "Microsoft Zira"],
    };

    private static SpeechVoiceSettingItem Item(FakeSounds sounds, string stored = "")
    {
        var item = new SpeechVoiceSettingItem(Definition, () => new TestSoundSettings(), () => sounds);
        item.LoadFrom(new TestSoundSettings { SpeechVoice = stored });
        return item;
    }

    [Fact]
    public void TheTestButtonSpeaksTheSample()
    {
        var sounds = new FakeSounds();

        Item(sounds).PreviewCommand.Execute(null);

        Assert.Single(sounds.Said);
        Assert.Equal(SpeechVoiceSettingItem.Sample, sounds.Said[0].Text);
    }

    [Fact]
    public void TheTestButtonUsesTheVoiceJustPicked()
    {
        // Not the saved one: the pane autosaves a second after the last click, and the click on
        // the test button restarts that timer, so the new voice is never saved before it plays.
        var sounds = new FakeSounds();
        var item = Item(sounds, "Microsoft David");

        item.SelectedValue = "Microsoft Zira";
        item.PreviewCommand.Execute(null);

        Assert.Equal("Microsoft Zira", sounds.Said[0].Voice);
    }

    [Fact]
    public void TheDefaultVoiceIsStillTestable()
    {
        // Blank means "whatever Windows defaults to", which is a real choice worth hearing.
        var sounds = new FakeSounds();
        var item = Item(sounds);

        Assert.True(item.CanPreview);

        item.PreviewCommand.Execute(null);

        Assert.Equal("", sounds.Said[0].Voice);
    }

    [Fact]
    public void PickingAVoiceAppliesItWithoutWaitingForTheSave()
    {
        var sounds = new FakeSounds();
        var item = Item(sounds, "Microsoft David");

        item.SelectedValue = "Microsoft Zira";

        Assert.Equal("Microsoft Zira", sounds.Voice);
    }

    [Fact]
    public void TheChoiceStillSavesTheNormalWay()
    {
        var item = Item(new FakeSounds());
        item.SelectedValue = "Microsoft Zira";

        var target = new TestSoundSettings();
        item.ApplyTo(target);

        Assert.Equal("Microsoft Zira", target.SpeechVoice);
    }

    [Fact]
    public void NoSoundService_DoesNotThrow()
    {
        var item = new SpeechVoiceSettingItem(Definition, () => new TestSoundSettings(), () => null);
        item.LoadFrom(new TestSoundSettings());

        item.PreviewCommand.Execute(null);      // must not throw
    }
}
