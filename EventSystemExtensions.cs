namespace Raisin.EventSystem;

#pragma warning disable CS0618 // Obsolete usage in backwards-compatibility methods
public static class EventSystemExtensions
{
    public static void Log(this EventSystem es, object sender, string message,
        LogTarget target, LogSeverity severity = LogSeverity.Info,
        string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new LogArgs(message)
            { Target = target, LogSeverity = severity, Category = category, Subcategory = subcategory });
    }

    [Obsolete("Use Log(sender, message, LogTarget.UI) instead")]
    public static void Message(this EventSystem es, object sender, string message,
        string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new MessageArgs(message)
            { Category = category, Subcategory = subcategory });
    }

    [Obsolete("Use Log(sender, message, LogTarget.UI) instead")]
    public static void Message(this EventSystem es, object sender, string message,
        MessageSeverity severity, string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new MessageArgs(message)
            { Severity = severity, Category = category, Subcategory = subcategory });
    }

    [Obsolete("Use Log(sender, message, LogTarget.File) instead")]
    public static void Log(this EventSystem es, object sender, string message,
        MessageSeverity severity = MessageSeverity.Info,
        string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new LogArgs(message)
            { Severity = severity, Category = category, Subcategory = subcategory });
    }
}
#pragma warning restore CS0618
