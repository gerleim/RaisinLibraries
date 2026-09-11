using System.IO;

namespace Raisin.Audio;

/// <summary>Where a sound comes from. The picker asks for this first, then for a value.</summary>
public enum SoundSource
{
    /// <summary>No sound for this event.</summary>
    Silent,

    /// <summary>A named event in the active theme - the portable choice.</summary>
    Theme,

    /// <summary>One of the five Windows system sounds.</summary>
    System,

    /// <summary>A .wav Windows ships in %windir%\Media.</summary>
    Media,

    /// <summary>Any audio file, by absolute path.</summary>
    File,

    /// <summary>Words spoken by Windows rather than a sound played.</summary>
    Speech,
}

/// <summary>
/// Splits a stored sound spec into the (source, value) pair the picker edits, and puts it back
/// together again.
/// </summary>
/// <remarks>
/// The stored form stays a single string - what <see cref="SoundResolver"/> has always read, so
/// nothing about settings files or the resolver changes. This only exists because one text box
/// is a poor way to choose between a theme event, a Windows sound, and a file on disk.
/// </remarks>
public static class SoundSpec
{
    /// <summary>Which source a stored spec came from, so the picker opens on the right one.</summary>
    public static SoundSource Classify(string? spec, SoundResolver resolver)
    {
        if (string.IsNullOrWhiteSpace(spec)) return SoundSource.Silent;
        spec = spec.Trim();

        if (spec.StartsWith(SoundResolver.SpeechPrefix, StringComparison.OrdinalIgnoreCase))
            return SoundSource.Speech;

        if (spec.StartsWith(SoundResolver.SystemPrefix, StringComparison.OrdinalIgnoreCase))
            return SoundSource.System;

        if (IsRooted(spec)) return SoundSource.File;

        // A theme event and a Media .wav are both bare names, so ask what actually exists.
        // An extension says the spec is a file name, which is how a Media sound is stored;
        // without one it is an event name. That ordering keeps "Alarm01.wav" a Media sound
        // even in the unlikely case that a theme also answers to "Alarm01".
        bool named = HasAudioExtension(spec);

        if (!named && IsThemeEvent(resolver, spec)) return SoundSource.Theme;
        if (IsMediaSound(resolver, spec)) return SoundSource.Media;
        if (named && IsThemeEvent(resolver, spec)) return SoundSource.Theme;

        // An unresolved name is most likely a theme event whose file went missing; leaving it
        // under Theme shows the user what is stored rather than silently reinterpreting it.
        return SoundSource.Theme;
    }

    /// <summary>The part of a spec the value control edits, with any prefix removed.</summary>
    public static string ValueOf(string? spec, SoundSource source)
    {
        if (string.IsNullOrWhiteSpace(spec)) return "";
        spec = spec.Trim();

        return source switch
        {
            SoundSource.Silent => "",
            SoundSource.Speech => spec[SoundResolver.SpeechPrefix.Length..].Trim(),
            SoundSource.System => spec[SoundResolver.SystemPrefix.Length..],
            SoundSource.Theme => NameOf(spec),
            _ => spec,
        };
    }

    /// <summary>The spec to store for a (source, value) pair.</summary>
    public static string Compose(SoundSource source, string? value)
    {
        value = value?.Trim() ?? "";
        if (source == SoundSource.Silent || value.Length == 0) return "";

        return source switch
        {
            SoundSource.Speech => SoundResolver.SpeechPrefix + value,
            SoundSource.System => SoundResolver.SystemPrefix + value,
            _ => value,
        };
    }

    private static bool IsThemeEvent(SoundResolver resolver, string spec) =>
        resolver.GetThemeEventNames().Contains(NameOf(spec), StringComparer.OrdinalIgnoreCase);

    private static bool IsMediaSound(SoundResolver resolver, string spec) =>
        resolver.GetMediaFolderSounds().Contains(spec, StringComparer.OrdinalIgnoreCase);

    private static bool HasAudioExtension(string spec)
    {
        try
        {
            return SoundResolver.SupportedExtensions.Contains(
                Path.GetExtension(spec), StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>A theme event is stored without its extension; a Media file keeps one.</summary>
    private static string NameOf(string spec)
    {
        try { return Path.GetFileNameWithoutExtension(spec); }
        catch (ArgumentException) { return spec; }
    }

    private static bool IsRooted(string spec)
    {
        try { return Path.IsPathRooted(spec); }
        catch (ArgumentException) { return false; }
    }
}
