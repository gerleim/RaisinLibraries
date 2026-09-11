using Raisin.Audio;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>
/// Covers the parts of the sound stack that do not need a dispatcher: spec resolution against
/// the theme layout, and the rate limit. Playback itself is MediaPlayer, which needs a running
/// WPF Application, so it is left to the app rather than faked here.
/// </summary>
public class SoundResolverTests : IDisposable
{
    private const string Theme = "minimal";
    private const string OtherTheme = "arcade";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "rt2-sound-" + Guid.NewGuid());
    private readonly string _userRoot;
    private readonly string _themeRoot;
    private readonly string _fallback;

    public SoundResolverTests()
    {
        _userRoot = Path.Combine(_root, "user");
        _themeRoot = Path.Combine(_root, "bundled");
        _fallback = Path.Combine(_root, "media");

        Directory.CreateDirectory(_userRoot);
        Directory.CreateDirectory(Path.Combine(_themeRoot, Theme));
        Directory.CreateDirectory(Path.Combine(_themeRoot, OtherTheme));
        Directory.CreateDirectory(_fallback);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private SoundResolver Resolver() =>
        new(_userRoot, _themeRoot, [_fallback]) { Theme = Theme };

    /// <summary>Writes a placeholder sound and returns its full path.</summary>
    private static string Write(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, [0]);
        return path;
    }

    private string WriteBundled(string theme, string name) => Write(Path.Combine(_themeRoot, theme), name);
    private string WriteUser(string name) => Write(_userRoot, name);
    private string WriteUserTheme(string theme, string name) => Write(Path.Combine(_userRoot, theme), name);
    private string WriteFallback(string name) => Write(_fallback, name);

    // -- Resolution ---------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptySpec_IsSilent(string? spec)
    {
        var resolved = Resolver().Resolve(spec);

        Assert.Equal(SoundKind.None, resolved.Kind);
        Assert.True(resolved.IsSilent);
    }

    [Fact]
    public void Resolve_SystemSpec_ReturnsCanonicalName()
    {
        var resolved = Resolver().Resolve("system:asterisk");

        Assert.Equal(SoundKind.System, resolved.Kind);
        Assert.Equal("Asterisk", resolved.Value);
    }

    [Fact]
    public void Resolve_UnknownSystemSound_FallsBackToBeep()
    {
        var resolved = Resolver().Resolve("system:NoSuchSound");

        Assert.Equal(SoundKind.System, resolved.Kind);
        Assert.Equal("Beep", resolved.Value);
    }

    [Fact]
    public void Resolve_EventName_ComesFromActiveTheme()
    {
        var expected = WriteBundled(Theme, "notification.mp3");
        WriteBundled(OtherTheme, "notification.mp3");

        var resolved = Resolver().Resolve("notification");

        Assert.Equal(SoundKind.File, resolved.Kind);
        Assert.Equal(expected, resolved.Value);
    }

    [Fact]
    public void Resolve_EventName_FollowsTheThemeWhenItChanges()
    {
        WriteBundled(Theme, "notification.mp3");
        var expected = WriteBundled(OtherTheme, "notification.mp3");

        var resolver = Resolver();
        resolver.Theme = OtherTheme;

        Assert.Equal(expected, resolver.Resolve("notification").Value);
    }

    [Fact]
    public void Resolve_ThemeQualifiedName_IgnoresTheActiveTheme()
    {
        WriteBundled(Theme, "success.mp3");
        var expected = WriteBundled(OtherTheme, "success.mp3");

        var resolved = Resolver().Resolve($"{OtherTheme}/success");

        Assert.Equal(expected, resolved.Value);
    }

    [Fact]
    public void Resolve_UserThemeFolderShadowsBundledTheme()
    {
        WriteBundled(Theme, "error.mp3");
        var expected = WriteUserTheme(Theme, "error.mp3");

        Assert.Equal(expected, Resolver().Resolve("error").Value);
    }

    [Fact]
    public void Resolve_FlatUserFolderShadowsBundledTheme()
    {
        WriteBundled(Theme, "error.mp3");
        var expected = WriteUser("error.mp3");

        Assert.Equal(expected, Resolver().Resolve("error").Value);
    }

    [Fact]
    public void Resolve_FallsBackToWindowsMediaFolder()
    {
        var expected = WriteFallback("chimes.wav");

        Assert.Equal(expected, Resolver().Resolve("chimes.wav").Value);
    }

    [Fact]
    public void Resolve_ExtensionlessName_ProbesSupportedExtensions()
    {
        var expected = WriteBundled(Theme, "ping.mp3");

        Assert.Equal(expected, Resolver().Resolve("ping").Value);
    }

    [Fact]
    public void Resolve_ExplicitExtension_IsHonoured()
    {
        var expected = WriteBundled(Theme, "ping.mp3");

        Assert.Equal(expected, Resolver().Resolve("ping.mp3").Value);
    }

    [Fact]
    public void Resolve_UnsupportedExtensionInName_StillProbes()
    {
        // "my.sound" is not a media extension, so it is treated as a name to complete.
        var expected = WriteBundled(Theme, "my.sound.mp3");

        Assert.Equal(expected, Resolver().Resolve("my.sound").Value);
    }

    [Fact]
    public void Resolve_MissingName_IsSilent()
    {
        Assert.Equal(SoundKind.None, Resolver().Resolve("nothing-here").Kind);
    }

    [Fact]
    public void Resolve_RootedPath_UsedDirectly()
    {
        var outside = Path.Combine(_root, "loose.mp3");
        File.WriteAllBytes(outside, [0]);

        var resolved = Resolver().Resolve(outside);

        Assert.Equal(SoundKind.File, resolved.Kind);
        Assert.Equal(outside, resolved.Value);
    }

    [Fact]
    public void Resolve_MissingRootedPath_IsSilent()
    {
        Assert.Equal(SoundKind.None, Resolver().Resolve(Path.Combine(_root, "absent.mp3")).Kind);
    }

    [Fact]
    public void Resolve_InvalidCharacters_IsSilentRatherThanThrowing()
    {
        Assert.Equal(SoundKind.None, Resolver().Resolve("bad|name?").Kind);
    }

    [Fact]
    public void Resolve_TrimsSurroundingWhitespace()
    {
        var expected = WriteBundled(Theme, "ping.mp3");

        Assert.Equal(expected, Resolver().Resolve("  ping  ").Value);
    }

    [Fact]
    public void Resolve_EmptyTheme_StillFindsFlatUserSounds()
    {
        var expected = WriteUser("ping.mp3");
        var resolver = Resolver();
        resolver.Theme = "";

        Assert.Equal(expected, resolver.Resolve("ping").Value);
    }

    // -- Themes and choices -------------------------------------------

    [Fact]
    public void GetAvailableThemes_ListsBundledAndUserThemes()
    {
        WriteBundled(Theme, "ping.mp3");
        WriteBundled(OtherTheme, "ping.mp3");
        WriteUserTheme("homemade", "ping.mp3");

        var themes = Resolver().GetAvailableThemes();

        Assert.Equal([OtherTheme, "homemade", Theme], themes);
    }

    [Fact]
    public void GetAvailableThemes_SkipsFoldersWithNoSounds()
    {
        WriteBundled(Theme, "ping.mp3");
        Directory.CreateDirectory(Path.Combine(_themeRoot, "empty"));

        Assert.DoesNotContain("empty", Resolver().GetAvailableThemes());
    }

    [Fact]
    public void GetAvailableThemes_ListsAThemeOnlyOnce()
    {
        WriteBundled(Theme, "ping.mp3");
        WriteUserTheme(Theme, "ping.mp3");

        Assert.Single(Resolver().GetAvailableThemes(), t => t == Theme);
    }

    [Fact]
    public void GetThemeEventNames_StripsExtensionsAndSkipsNonAudio()
    {
        WriteBundled(Theme, "notification.mp3");
        WriteBundled(Theme, "error.mp3");
        WriteBundled(Theme, "readme.txt");

        var names = Resolver().GetThemeEventNames();

        Assert.Equal(["error", "notification"], names);
    }

    [Fact]
    public void GetThemeEventNames_IncludesNamesTheUserAddedToATheme()
    {
        WriteBundled(Theme, "notification.mp3");
        WriteUserTheme(Theme, "custom.mp3");

        Assert.Equal(["custom", "notification"], Resolver().GetThemeEventNames());
    }

    [Fact]
    public void GetMediaFolderSounds_ListsTheFallbackFolder()
    {
        // Its own list, not mixed into the theme events: the picker asks for a source first.
        WriteFallback("Alarm01.wav");
        WriteFallback("Windows Notify.wav");
        WriteBundled(Theme, "notification.mp3");

        var media = Resolver().GetMediaFolderSounds();

        Assert.Equal(["Alarm01.wav", "Windows Notify.wav"], media);
        Assert.DoesNotContain("notification.mp3", media);
    }

    [Fact]
    public void GetMediaFolderSounds_SkipsFilesWeCannotPlay()
    {
        WriteFallback("Alarm01.wav");
        WriteFallback("desktop.ini");

        Assert.DoesNotContain("desktop.ini", Resolver().GetMediaFolderSounds());
    }

    [Fact]
    public void GetMediaFolderSounds_IsEmptyWhenThereIsNoFallbackFolder()
    {
        var resolver = new SoundResolver(_userRoot, _themeRoot, []);

        Assert.Empty(resolver.GetMediaFolderSounds());
    }
}

public class SoundThrottleTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void TryPlay_FirstCallAllowed()
    {
        var throttle = new SoundThrottle(TimeSpan.FromMilliseconds(150), new FakeTime());

        Assert.True(throttle.TryPlay("beep"));
    }

    [Fact]
    public void TryPlay_WithinInterval_Blocked()
    {
        var time = new FakeTime();
        var throttle = new SoundThrottle(TimeSpan.FromMilliseconds(150), time);

        Assert.True(throttle.TryPlay("beep"));
        time.Now = time.Now.AddMilliseconds(149);

        Assert.False(throttle.TryPlay("beep"));
    }

    [Fact]
    public void TryPlay_AfterInterval_Allowed()
    {
        var time = new FakeTime();
        var throttle = new SoundThrottle(TimeSpan.FromMilliseconds(150), time);

        Assert.True(throttle.TryPlay("beep"));
        time.Now = time.Now.AddMilliseconds(150);

        Assert.True(throttle.TryPlay("beep"));
    }

    [Fact]
    public void TryPlay_BlockedCallDoesNotExtendTheWindow()
    {
        var time = new FakeTime();
        var throttle = new SoundThrottle(TimeSpan.FromMilliseconds(150), time);

        Assert.True(throttle.TryPlay("beep"));

        // A bell loop hammering the gate must not push the next allowed play further out.
        time.Now = time.Now.AddMilliseconds(100);
        Assert.False(throttle.TryPlay("beep"));

        time.Now = time.Now.AddMilliseconds(50);
        Assert.True(throttle.TryPlay("beep"));
    }

    [Fact]
    public void TryPlay_DifferentSoundsAreIndependent()
    {
        var throttle = new SoundThrottle(TimeSpan.FromMilliseconds(150), new FakeTime());

        Assert.True(throttle.TryPlay("beep"));
        Assert.True(throttle.TryPlay("chime"));
        Assert.False(throttle.TryPlay("beep"));
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var throttle = new SoundThrottle(TimeSpan.FromMilliseconds(150), new FakeTime());

        Assert.True(throttle.TryPlay("beep"));
        throttle.Reset();

        Assert.True(throttle.TryPlay("beep"));
    }
}

/// <summary>The bundled themes must actually ship, and must stay interchangeable.</summary>
public class BundledSoundThemeTests
{
    private static string BundledRoot => SoundResolver.BundledSoundsFolder;

    /// <summary>
    /// The bundled themes alone, with no user folder and no Windows Media fallback: AppPaths
    /// is unconfigured in a test host, so the real user folder cannot be asked for.
    /// </summary>
    private static SoundResolver BundledOnly() =>
        new(Path.Combine(BundledRoot, "..", "no-user-sounds"), BundledRoot, []);

    [Fact]
    public void BundledThemes_AreCopiedToTheOutputFolder()
    {
        Assert.True(Directory.Exists(BundledRoot), $"No bundled sounds at {BundledRoot}");
        Assert.NotEmpty(BundledOnly().GetAvailableThemes());
    }

    [Fact]
    public void BundledThemes_AreFoundWithNoUserFolderAtAll()
    {
        // AppPaths is unconfigured until the app starts, so the user folder can legitimately
        // be absent. The bundled themes must still be findable, or the settings dropdowns
        // come back empty.
        var resolver = new SoundResolver(userRoot: "", themeRoot: BundledRoot, fallbackFolders: []);

        Assert.NotEmpty(resolver.GetAvailableThemes());
        Assert.NotEmpty(resolver.GetThemeEventNames(SoundResolver.DefaultTheme));
    }

    [Fact]
    public void PickerChoices_AreUsableWithoutAnyStartupConfiguration()
    {
        Assert.Contains(SoundResolver.DefaultTheme, SoundResolver.DefaultThemes);
        Assert.Contains("notification", SoundResolver.ForPicker().GetThemeEventNames());
    }

    [Fact]
    public void BundledThemes_IncludeTheDefaultTheme()
    {
        Assert.Contains(SoundResolver.DefaultTheme, BundledOnly().GetAvailableThemes());
    }

    [Fact]
    public void BundledThemes_AllCarryTheSameEventNames()
    {
        // Switching theme must never invalidate a saved AlertSound, which only holds while
        // every theme answers to the same names.
        var resolver = BundledOnly();
        var themes = resolver.GetAvailableThemes();
        var reference = resolver.GetThemeEventNames(themes[0]);

        Assert.NotEmpty(reference);
        foreach (var theme in themes)
            Assert.Equal(reference, resolver.GetThemeEventNames(theme));
    }
}

