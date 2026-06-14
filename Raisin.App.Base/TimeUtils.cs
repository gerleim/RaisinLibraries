namespace Raisin.App.Base;

public static class TimeUtils
{
    public static readonly TimeZoneInfo EasternTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    public static DateTime ConvertLocalToEastern(DateTime localDateTime)
    {
        DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.Local);
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, EasternTimeZone);
    }

    public static DateTime ConvertUtcToEastern(DateTime utcDateTime)
        => TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, EasternTimeZone);

    public static DateTime ConvertEasternToUtc(DateTime easternDateTime)
    {
        var unspecified = DateTime.SpecifyKind(easternDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, EasternTimeZone);
    }

    public static DateTime AlignToBarBoundary(DateTime dt, int barSeconds)
    {
        var ticks = dt.Ticks;
        var barTicks = (long)barSeconds * TimeSpan.TicksPerSecond;
        var aligned = ticks - (ticks % barTicks);
        return new DateTime(aligned, dt.Kind);
    }

    public static DateTime AlignToBarBoundary(DateTime dt, TimeSpan barSpan)
    {
        var ticks = dt.Ticks;
        var barTicks = barSpan.Ticks;
        var aligned = ticks - (ticks % barTicks);
        return new DateTime(aligned, dt.Kind);
    }
}
