using Raisin.Audio;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>
/// The picker edits a (source, value) pair; settings still hold one string. These cover the
/// translation both ways, which is what decides the source a saved setting opens on.
/// </summary>
public class SoundSpecTests : IDisposable
{
    private const string Theme = "minimal";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "rt2-spec-" + Guid.NewGuid());
    private readonly SoundResolver _resolver;

    public SoundSpecTests()
    {
        var userRoot = Path.Combine(_root, "user");
        var themeRoot = Path.Combine(_root, "bundled");
        var media = Path.Combine(_root, "media");

        Directory.CreateDirectory(Path.Combine(themeRoot, Theme));
        Directory.CreateDirectory(media);

        Write(Path.Combine(themeRoot, Theme), "notification.mp3");
        Write(Path.Combine(themeRoot, Theme), "complete.mp3");
        Write(media, "Alarm01.wav");

        _resolver = new SoundResolver(userRoot, themeRoot, [media]) { Theme = Theme };
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static void Write(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name), [0]);
    }

    // -- Classify -----------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_IsSilent(string? spec)
    {
        Assert.Equal(SoundSource.Silent, SoundSpec.Classify(spec, _resolver));
    }

    [Fact]
    public void SystemPrefix_IsAWindowsSound()
    {
        Assert.Equal(SoundSource.System, SoundSpec.Classify("system:Asterisk", _resolver));
    }

    [Fact]
    public void ASpokenSpec_IsSpeech()
    {
        Assert.Equal(SoundSource.Speech, SoundSpec.Classify("say:{session} needs you", _resolver));
    }

    [Fact]
    public void ASpokenSpec_KeepsItsPhraseWhenEdited()
    {
        const string spec = "say:{session} needs you||{count} sessions need you";

        Assert.Equal("{session} needs you||{count} sessions need you",
                     SoundSpec.ValueOf(spec, SoundSource.Speech));
    }

    [Fact]
    public void AnEmptySpokenSpec_IsSilentRatherThanSpeakingNothing()
    {
        Assert.Equal(SoundKind.None, _resolver.Resolve("say:").Kind);
        Assert.Equal(SoundKind.None, _resolver.Resolve("say:   ").Kind);
    }

    [Fact]
    public void RootedPath_IsACustomFile()
    {
        Assert.Equal(SoundSource.File, SoundSpec.Classify(@"D:\sfx\ding.mp3", _resolver));
    }

    [Fact]
    public void BareEventName_IsAThemeSound()
    {
        Assert.Equal(SoundSource.Theme, SoundSpec.Classify("notification", _resolver));
    }

    [Fact]
    public void FileNameFromTheMediaFolder_IsAMediaSound()
    {
        Assert.Equal(SoundSource.Media, SoundSpec.Classify("Alarm01.wav", _resolver));
    }

    [Fact]
    public void MediaSoundWins_EvenWhenAThemeAnswersToTheSameStem()
    {
        // A spec carrying an extension is a file name, so the Media folder is asked first.
        Write(Path.Combine(_root, "bundled", Theme), "Alarm01.mp3");

        Assert.Equal(SoundSource.Media, SoundSpec.Classify("Alarm01.wav", _resolver));
        Assert.Equal(SoundSource.Theme, SoundSpec.Classify("Alarm01", _resolver));
    }

    [Fact]
    public void UnknownName_StaysUnderTheme()
    {
        // A theme sound whose file went missing should still show what is stored.
        Assert.Equal(SoundSource.Theme, SoundSpec.Classify("no-such-event", _resolver));
    }

    // -- Value and round trip -----------------------------------------

    [Fact]
    public void ValueOf_StripsTheSystemPrefix()
    {
        Assert.Equal("Asterisk", SoundSpec.ValueOf("system:Asterisk", SoundSource.System));
    }

    [Fact]
    public void ValueOf_StripsATheme_Extension()
    {
        Assert.Equal("notification", SoundSpec.ValueOf("notification.mp3", SoundSource.Theme));
    }

    [Fact]
    public void ValueOf_KeepsMediaAndFileNamesWhole()
    {
        Assert.Equal("Alarm01.wav", SoundSpec.ValueOf("Alarm01.wav", SoundSource.Media));
        Assert.Equal(@"D:\sfx\ding.mp3", SoundSpec.ValueOf(@"D:\sfx\ding.mp3", SoundSource.File));
    }

    [Fact]
    public void Compose_AddsThePrefixOnlyForWindowsSounds()
    {
        Assert.Equal("system:Beep", SoundSpec.Compose(SoundSource.System, "Beep"));
        Assert.Equal("notification", SoundSpec.Compose(SoundSource.Theme, "notification"));
        Assert.Equal("Alarm01.wav", SoundSpec.Compose(SoundSource.Media, "Alarm01.wav"));
    }

    [Fact]
    public void Compose_IsBlankForSilentOrAnEmptyValue()
    {
        Assert.Equal("", SoundSpec.Compose(SoundSource.Silent, "notification"));
        Assert.Equal("", SoundSpec.Compose(SoundSource.Theme, ""));
        Assert.Equal("", SoundSpec.Compose(SoundSource.Theme, "   "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("notification")]
    [InlineData("system:Asterisk")]
    [InlineData("Alarm01.wav")]
    [InlineData(@"D:\sfx\ding.mp3")]
    [InlineData("say:{session} needs you")]
    [InlineData("say:{session} needs you||{count} sessions need you")]
    public void EverySpecSurvivesARoundTripThroughThePicker(string spec)
    {
        // Opening the options window and closing it again must not rewrite the setting.
        var source = SoundSpec.Classify(spec, _resolver);
        var value = SoundSpec.ValueOf(spec, source);

        Assert.Equal(spec, SoundSpec.Compose(source, value));
    }

    [Fact]
    public void EveryShippedDefaultRoundTripsAsAThemeSound()
    {
        foreach (var spec in new[] { "notification", "complete" })
        {
            var source = SoundSpec.Classify(spec, _resolver);

            Assert.Equal(SoundSource.Theme, source);
            Assert.Equal(spec, SoundSpec.Compose(source, SoundSpec.ValueOf(spec, source)));
        }
    }

    // -- The picker's source list -------------------------------------

    [Fact]
    public void ThePickerOffersEverySource()
    {
        var offered = SoundSettingItem.AllSources.Select(o => o.Source).ToList();

        Assert.Equal(Enum.GetValues<SoundSource>(), offered);
    }
}
