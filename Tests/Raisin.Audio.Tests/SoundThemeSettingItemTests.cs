using Raisin.WPF.Base.Settings;
using Raisin.Audio;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>
/// Switching theme has to reach playback straight away. The options pane only writes settings
/// on a one-second autosave that every mouse-up restarts - including the click on a row's play
/// button - so a preview taken from saved settings would play the theme you just switched away
/// from.
/// </summary>
public class SoundThemeSettingItemTests : IDisposable
{
    private const string First = "minimal";
    private const string Second = "arcade";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "rt2-theme-" + Guid.NewGuid());
    private readonly SoundResolver _resolver;

    private sealed class FakeSounds : ISoundService
    {
        public bool Enabled { get; set; } = true;
        public double Volume { get; set; }
        public string Theme { get; set; } = First;
        public IReadOnlyList<string> AvailableThemes => [];
        public string Voice { get; set; } = "";
        public IReadOnlyList<string> AvailableVoices => [];
        public List<string> Played { get; } = [];

        public void Play(string? spec, SoundContext? context = null) => Played.Add(spec ?? "");
        public void CancelPending(string? subject) { }
        public bool IsSpeaking => false;
        public void StopSpeaking() { }
#pragma warning disable CS0067   // the picker tests never raise these
        public event Action<long, int, int>? SpeechProgress;
        public event Action<long>? SpeechFinished;
#pragma warning restore CS0067
        public string? Said { get; private set; }
        public string? SaidVoice { get; private set; }
        public long Say(string text, string? voice = null, double? rate = null) { Said = text; SaidVoice = voice; return 1; }
        public void Preload(string? spec) { }
    }

    public SoundThemeSettingItemTests()
    {
        foreach (var theme in new[] { First, Second })
        {
            var folder = Path.Combine(_root, theme);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "notification.mp3"), [0]);
        }

        _resolver = new SoundResolver("", _root, []) { Theme = First };
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static readonly SettingDefinition Definition = new()
    {
        Key = "sound.theme",
        DisplayName = "Sound theme",
        Description = "",
        Category = "Playback",
        PropertyName = "SoundTheme",
        EditorType = SettingEditorType.Choice,
        Choices = [First, Second],
    };

    private SoundThemeSettingItem Item(FakeSounds sounds)
    {
        var item = new SoundThemeSettingItem(Definition, () => new TestSoundSettings(), _resolver, () => sounds);
        item.LoadFrom(new TestSoundSettings { SoundTheme = First });
        return item;
    }

    [Fact]
    public void PickingATheme_ReachesTheSoundServiceImmediately()
    {
        var sounds = new FakeSounds();
        var item = Item(sounds);

        item.SelectedValue = Second;

        Assert.Equal(Second, sounds.Theme);
    }

    [Fact]
    public void PickingATheme_MovesThePickersResolverToo()
    {
        // The event dropdowns list names from this resolver; a user theme can add its own.
        var item = Item(new FakeSounds());

        item.SelectedValue = Second;

        Assert.Equal(Second, _resolver.Theme);
    }

    [Fact]
    public void AfterSwitching_AnEventNameResolvesToTheNewThemesFile()
    {
        // The reported symptom, at the level that actually decides which file plays.
        var item = Item(new FakeSounds());

        item.SelectedValue = Second;

        Assert.Equal(Path.Combine(_root, Second, "notification.mp3"),
                     _resolver.Resolve("notification").Value);
    }

    [Fact]
    public void LoadingTheStoredValue_IsNotTreatedAsPickingOne()
    {
        var sounds = new FakeSounds { Theme = "untouched" };

        var item = new SoundThemeSettingItem(Definition, () => new TestSoundSettings(), _resolver, () => sounds);
        item.LoadFrom(new TestSoundSettings { SoundTheme = Second });

        Assert.Equal("untouched", sounds.Theme);
        Assert.Equal(Second, item.SelectedValue);
    }

    [Fact]
    public void ABlankSelectionIsIgnored()
    {
        var sounds = new FakeSounds();
        var item = Item(sounds);

        item.SelectedValue = "";

        Assert.Equal(First, sounds.Theme);
        Assert.Equal(First, _resolver.Theme);
    }

    [Fact]
    public void TheChoiceStillSavesTheNormalWay()
    {
        // Applying live must not stop the value reaching TestSoundSettings when the pane saves.
        var item = Item(new FakeSounds());
        item.SelectedValue = Second;

        var target = new TestSoundSettings();
        item.ApplyTo(target);

        Assert.Equal(Second, target.SoundTheme);
    }
}
