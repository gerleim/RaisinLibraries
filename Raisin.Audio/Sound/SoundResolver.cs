using System.IO;
using Raisin.Core;

namespace Raisin.Audio;

/// <summary>How a resolved sound gets produced.</summary>
public enum SoundKind
{
    /// <summary>Nothing to play: the spec was empty, or named a file we could not find.</summary>
    None,

    /// <summary>A Windows system sound, played through <see cref="System.Media.SystemSounds"/>.</summary>
    System,

    /// <summary>An audio file on disk, played through MediaPlayer.</summary>
    File,

    /// <summary>Words to speak. <see cref="ResolvedSound.Value"/> is the phrase, still unfilled.</summary>
    Speech,
}

/// <summary>A sound spec after resolution. <see cref="Value"/> is the system sound name, or the full file path.</summary>
public readonly record struct ResolvedSound(SoundKind Kind, string Value)
{
    public static ResolvedSound None { get; } = new(SoundKind.None, "");

    public bool IsSilent => Kind == SoundKind.None;
}

/// <summary>
/// Turns a sound spec from settings into something playable.
/// </summary>
/// <remarks>
/// Sounds are organised into themes: <c>Sounds\arcade</c>, <c>Sounds\zen</c> and so on, each
/// carrying the same set of event names. A spec therefore names an event - <c>notification</c>,
/// <c>error</c> - and the active theme decides what that sounds like. Because the names are
/// identical across themes, switching theme never invalidates a saved setting.
/// <para>
/// A spec is one of:
/// <list type="bullet">
/// <item>empty - silent;</item>
/// <item><c>system:Beep</c> (also Asterisk, Exclamation, Hand, Question) - a Windows system sound;</item>
/// <item>a rooted path - that file;</item>
/// <item><c>arcade/success</c> - that event from a named theme, whatever the active theme is;</item>
/// <item>a bare name - the event in the active theme.</item>
/// </list>
/// The extension may be left off, in which case every supported extension is probed.
/// </para>
/// <para>
/// Search order puts the user ahead of the app: a file in the user's sounds folder, or in a
/// theme subfolder of it, shadows the bundled one, which in turn shadows <c>%windir%\Media</c>.
/// That is how someone replaces one sound of a theme without forking the theme.
/// </para>
/// <para>
/// Resolution only finds files; it does not check that a codec exists for them. A file
/// Media Foundation cannot decode resolves fine and fails at playback, where MediaFailed logs it.
/// </para>
/// </remarks>
public sealed class SoundResolver
{
    public const string SystemPrefix = "system:";

    /// <summary>Marks a spec as words to speak rather than a sound to play.</summary>
    public const string SpeechPrefix = "say:";

    /// <summary>The theme assumed when nothing is configured. Quiet, short, unobtrusive next to a terminal.</summary>
    public const string DefaultTheme = "minimal";

    /// <summary>
    /// Container extensions Media Foundation decodes out of the box on Windows 10 and later.
    /// This is what a picker offers and what an extensionless spec probes - it is not a
    /// restriction on <see cref="Resolve"/>, which will hand MediaPlayer any file it is pointed at.
    /// </summary>
    public static readonly string[] SupportedExtensions =
        [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".mp4"];

    public static readonly string[] SystemSoundNames =
        ["Asterisk", "Beep", "Exclamation", "Hand", "Question"];

    /// <summary>
    /// Where a user drops their own sounds, flat or in theme subfolders. Empty until
    /// <see cref="AppPaths"/> has been configured, which the app does at startup - the bundled
    /// themes must still be findable before that, or a picker built during static
    /// initialisation would come back empty.
    /// </summary>
    public static string UserSoundsFolder =>
        string.IsNullOrEmpty(AppPaths.AppDataDir) ? "" : Path.Combine(AppPaths.AppDataDir, "sounds");

    /// <summary>The bundled themes, laid down next to the executable by the build.</summary>
    public static string BundledSoundsFolder => Path.Combine(AppContext.BaseDirectory, "Sounds");

    public static string WindowsMediaFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

    private readonly string _userRoot;
    private readonly string _themeRoot;
    private readonly IReadOnlyList<string> _fallbackFolders;

    public SoundResolver(
        string? userRoot = null,
        string? themeRoot = null,
        IReadOnlyList<string>? fallbackFolders = null)
    {
        _userRoot = userRoot ?? UserSoundsFolder;
        _themeRoot = themeRoot ?? BundledSoundsFolder;
        _fallbackFolders = fallbackFolders ?? [WindowsMediaFolder];
    }

    /// <summary>The active theme. Settable, because a user can change it without restarting.</summary>
    public string Theme { get; set; } = DefaultTheme;

    /// <summary>Where a bare name is looked up, most specific first.</summary>
    public IReadOnlyList<string> SearchFolders
    {
        get
        {
            var folders = new List<string>(5);
            if (HasUserRoot)
            {
                if (!string.IsNullOrWhiteSpace(Theme))
                    folders.Add(Path.Combine(_userRoot, Theme));
                folders.Add(_userRoot);
            }
            if (!string.IsNullOrWhiteSpace(Theme))
                folders.Add(Path.Combine(_themeRoot, Theme));
            folders.Add(_themeRoot);          // lets "arcade/success" pin a theme
            folders.AddRange(_fallbackFolders);
            return folders;
        }
    }

    public ResolvedSound Resolve(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return ResolvedSound.None;
        spec = spec.Trim();

        // Speech carries its own text, so there is nothing on disk to look for.
        if (spec.StartsWith(SpeechPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var phrase = spec[SpeechPrefix.Length..].Trim();
            return phrase.Length == 0
                ? ResolvedSound.None
                : new ResolvedSound(SoundKind.Speech, phrase);
        }

        if (spec.StartsWith(SystemPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = spec[SystemPrefix.Length..];
            var match = SystemSoundNames.FirstOrDefault(
                n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            // An unrecognised system name still beeps rather than going silent: the user
            // asked for a system sound, and a wrong one is a better clue than nothing.
            return new ResolvedSound(SoundKind.System, match ?? "Beep");
        }

        try
        {
            if (Path.IsPathRooted(spec))
                return File.Exists(spec)
                    ? new ResolvedSound(SoundKind.File, Path.GetFullPath(spec))
                    : ResolvedSound.None;

            foreach (var folder in SearchFolders)
                foreach (var candidate in Candidates(folder, spec))
                    if (File.Exists(candidate))
                        return new ResolvedSound(SoundKind.File, Path.GetFullPath(candidate));
        }
        catch (ArgumentException) { }      // invalid characters in the spec
        catch (NotSupportedException) { }
        catch (PathTooLongException) { }

        return ResolvedSound.None;
    }

    /// <summary>
    /// The themes on offer: every subfolder of the bundled root that holds sounds, plus any
    /// the user added under their own sounds folder.
    /// </summary>
    public IReadOnlyList<string> GetAvailableThemes()
    {
        var themes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in Roots())
            foreach (var folder in EnumerateDirectories(root))
            {
                var name = Path.GetFileName(folder);
                if (name.Length == 0 || !seen.Add(name)) continue;
                if (EnumerateFiles(folder).Any(IsSupported))
                    themes.Add(name);
            }

        themes.Sort(StringComparer.OrdinalIgnoreCase);
        return themes;
    }

    /// <summary>
    /// The sounds Windows ships, by file name - the contents of <c>%windir%\Media</c>. Its own
    /// list rather than part of the theme events: 70-odd wavs in one dropdown with the event
    /// names would bury them, which is why the picker asks for a source first.
    /// </summary>
    public IReadOnlyList<string> GetMediaFolderSounds()
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in _fallbackFolders)
            foreach (var file in EnumerateFiles(folder).Where(IsSupported))
            {
                var name = Path.GetFileName(file);
                if (name.Length > 0 && seen.Add(name)) names.Add(name);
            }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>The event names a theme provides, without extensions. Defaults to the active theme.</summary>
    public IReadOnlyList<string> GetThemeEventNames(string? theme = null)
    {
        theme ??= Theme;
        if (string.IsNullOrWhiteSpace(theme)) return [];

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The user's copy of a theme can add names the bundled one does not have.
        foreach (var root in Roots(userFirst: true))
            foreach (var file in EnumerateFiles(Path.Combine(root, theme)).Where(IsSupported))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length > 0 && seen.Add(name)) names.Add(name);
            }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>
    /// Themes for a picker that is built before any service exists - the settings registry is a
    /// static list, so it cannot ask the running <see cref="SoundService"/> what is installed.
    /// </summary>
    public static string[] DefaultThemes => ForPicker(r => r.GetAvailableThemes());

    /// <summary>
    /// A resolver for the options window, opened on a theme that is actually installed. Event
    /// names are identical in every theme, so which one answers does not matter - only that one
    /// of them is present.
    /// </summary>
    public static SoundResolver ForPicker()
    {
        var resolver = new SoundResolver();
        try
        {
            var themes = resolver.GetAvailableThemes();
            if (themes.Count > 0 && !themes.Contains(resolver.Theme, StringComparer.OrdinalIgnoreCase))
                resolver.Theme = themes[0];
        }
        catch (Exception)
        {
            // A picker with nothing in it beats a settings page that will not open.
        }
        return resolver;
    }

    private static string[] ForPicker(Func<SoundResolver, IReadOnlyList<string>> read)
    {
        try
        {
            var resolver = new SoundResolver();
            var themes = resolver.GetAvailableThemes();

            // The default theme may not be installed; any present theme lists the same names.
            if (themes.Count > 0 && !themes.Contains(resolver.Theme, StringComparer.OrdinalIgnoreCase))
                resolver.Theme = themes[0];

            return read(resolver).ToArray();
        }
        catch (Exception)
        {
            // A picker with nothing in it beats a settings page that will not open. This runs
            // during static initialisation, before there is anywhere useful to log.
            return [];
        }
    }

    private static IEnumerable<string> Candidates(string folder, string spec)
    {
        yield return Path.Combine(folder, spec);

        // "success" and "my.sound" both get the supported extensions appended; "success.mp3" does not.
        if (IsSupported(spec)) yield break;

        foreach (var ext in SupportedExtensions)
            yield return Path.Combine(folder, spec + ext);
    }

    private bool HasUserRoot => !string.IsNullOrEmpty(_userRoot);

    /// <summary>The roots worth walking. The user root drops out when there is not one yet.</summary>
    private IEnumerable<string> Roots(bool userFirst = false)
    {
        if (userFirst && HasUserRoot) yield return _userRoot;
        yield return _themeRoot;
        if (!userFirst && HasUserRoot) yield return _userRoot;
    }

    private static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string[] EnumerateFiles(string folder)
    {
        try { return Directory.Exists(folder) ? Directory.GetFiles(folder) : []; }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static string[] EnumerateDirectories(string root)
    {
        try { return Directory.Exists(root) ? Directory.GetDirectories(root) : []; }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
