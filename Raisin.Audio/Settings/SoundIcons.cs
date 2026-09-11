using System.Windows.Media;

namespace Raisin.Audio;

/// <summary>
/// Icons for the Sounds tab, drawn on the same 16-unit grid as the toolbar icons in
/// <see cref="Controls.TerminalToolbar"/> and frozen for sharing across every row.
/// </summary>
/// <remarks>
/// Play is a filled triangle and carries no stroke; the speakers are stroked outlines, matching
/// how the rest of the app draws its glyphs.
/// </remarks>
public static class SoundIcons
{
    private static readonly Geometry Play = Freeze(Geometry.Parse("M5,3 L12.5,8 L5,13 Z"));

    private static readonly Geometry SpeakerQuiet = Freeze(Geometry.Parse(
        "M3,6.5 L5.5,6.5 L9,3.5 L9,12.5 L5.5,9.5 L3,9.5 Z"));

    private static readonly Geometry SpeakerLoud = Freeze(Geometry.Parse(
        "M3,6.5 L5.5,6.5 L9,3.5 L9,12.5 L5.5,9.5 L3,9.5 Z "
        + "M11,6 A2.5,2.5 0 0 1 11,10 M12.8,4 A5.5,5.5 0 0 1 12.8,12"));

    public static Geometry PlayData => Play;
    public static Geometry SpeakerQuietData => SpeakerQuiet;
    public static Geometry SpeakerLoudData => SpeakerLoud;

    private static Geometry Freeze(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
}
