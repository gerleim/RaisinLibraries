using Raisin.WPF.Base.Settings;
using Raisin.Audio;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>
/// The volume slider is a facade over the text the base class saves and compares, so these
/// cover that the two stay in step.
/// </summary>
public class VolumeSettingItemTests
{
    private static readonly SettingDefinition Definition = new()
    {
        Key = "sound.volume",
        DisplayName = "Volume",
        Description = "",
        Category = "Playback",
        PropertyName = "SoundVolume",
        EditorType = SettingEditorType.Double,
        MinDouble = 0,
        MaxDouble = 1,
    };

    private static VolumeSettingItem Item() =>
        new(Definition, () => new TestSoundSettings(), new SoundResolver("", "", []), () => null);

    private static VolumeSettingItem LoadedWith(double volume)
    {
        var item = Item();
        item.LoadFrom(new TestSoundSettings { SoundVolume = volume });
        return item;
    }

    [Fact]
    public void LoadFrom_PutsTheStoredVolumeOnTheSlider()
    {
        Assert.Equal(0.4, LoadedWith(0.4).Value, 3);
    }

    [Fact]
    public void DraggingTheSlider_IsWhatGetsSaved()
    {
        var item = LoadedWith(0.4);
        item.Value = 0.85;

        var target = new TestSoundSettings();
        item.ApplyTo(target);

        Assert.Equal(0.85, target.SoundVolume, 3);
    }

    [Fact]
    public void Value_IsClampedToTheDeclaredRange()
    {
        var item = LoadedWith(0.5);

        item.Value = 4;
        Assert.Equal(1, item.Value, 3);

        item.Value = -2;
        Assert.Equal(0, item.Value, 3);
    }

    [Fact]
    public void Percent_ReadsAsAWholePercentage()
    {
        var item = LoadedWith(0.7);

        Assert.Equal("70%", item.Percent);
    }

    [Fact]
    public void Percent_FollowsTheSlider()
    {
        var item = LoadedWith(0.7);
        item.Value = 0.35;

        Assert.Equal("35%", item.Percent);
    }

    [Fact]
    public void Value_RoundTripsThroughTheTextTheBaseClassSaves()
    {
        // Text is the single source of truth; a mismatch would save a stale volume.
        var item = LoadedWith(0.2);
        item.Value = 0.65;

        Assert.Equal(0.65, double.Parse(item.Text), 3);
    }

    [Fact]
    public void MovingOffTheDefaultMarksTheRowModified()
    {
        var item = LoadedWith(new TestSoundSettings().SoundVolume);
        Assert.False(item.IsModified);

        item.Value = 0.15;

        Assert.True(item.IsModified);
    }

    [Fact]
    public void ResetToDefault_ReturnsTheSliderToTheShippedVolume()
    {
        var item = LoadedWith(0.1);

        item.ResetToDefault();

        Assert.Equal(new TestSoundSettings().SoundVolume, item.Value, 3);
        Assert.False(item.IsModified);
    }

    [Fact]
    public void MinAndMaxComeFromTheDefinition()
    {
        var item = Item();

        Assert.Equal(0, item.Min);
        Assert.Equal(1, item.Max);
    }

    [Fact]
    public void SampleSpec_PrefersTheConfiguredAlertSound()
    {
        // Judging the volume means hearing what the app will actually play.
        Assert.Equal("reward", Item().SampleSpec("reward"));
    }

    [Fact]
    public void SampleSpec_FallsBackToATheme_EventWhenNoAlertIsSet()
    {
        var root = Path.Combine(Path.GetTempPath(), "rt2-vol-" + Guid.NewGuid());
        var theme = Path.Combine(root, "minimal");
        Directory.CreateDirectory(theme);
        File.WriteAllBytes(Path.Combine(theme, "notification.mp3"), [0]);
        File.WriteAllBytes(Path.Combine(theme, "error.mp3"), [0]);
        try
        {
            var item = new VolumeSettingItem(Definition, () => new TestSoundSettings(),
                new SoundResolver("", root, []) { Theme = "minimal" }, () => null);

            Assert.Equal("notification", item.SampleSpec(""));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SampleSpec_FallsBackToTheWindowsBeepWithNoThemeAtAll()
    {
        // The resolver here has no theme root and no fallback folders.
        Assert.Equal("system:Beep", Item().SampleSpec(""));
    }
}
