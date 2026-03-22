namespace Raisin.EventSystem;

public static class EventSystemExtensions
{
    public static void Message(this EventSystem es, object sender, string message,
        string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new MessageArgs(message)
            { Category = category, Subcategory = subcategory });
    }

    public static void Message(this EventSystem es, object sender, string message,
        MessageSeverity severity, string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new MessageArgs(message)
            { Severity = severity, Category = category, Subcategory = subcategory });
    }

    public static void Log(this EventSystem es, object sender, string message,
        MessageSeverity severity = MessageSeverity.Info,
        string? category = null, string? subcategory = null)
    {
        es.InvokeOnThreadPool(sender, new LogArgs(message)
            { Severity = severity, Category = category, Subcategory = subcategory });
    }
}
