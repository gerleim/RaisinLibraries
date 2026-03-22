namespace Raisin.WPF.Base.Settings;

public static class SettingItemFactory
{
    /// <summary>
    /// Creates a SettingItemViewModel for the given definition, or null if the editor type is not
    /// a universal type (allowing apps to handle app-specific types).
    /// </summary>
    public static SettingItemViewModel? TryCreate(SettingDefinition def, Func<object> defaultFactory)
    {
        return def.EditorType switch
        {
            SettingEditorType.Bool => new BoolSettingItem(def, defaultFactory),
            SettingEditorType.UnsignedInt => new IntSettingItem(def, defaultFactory),
            SettingEditorType.Double => new DoubleSettingItem(def, defaultFactory),
            SettingEditorType.String => new StringSettingItem(def, defaultFactory),
            SettingEditorType.TimeOnly => new TimeOnlySettingItem(def, defaultFactory),
            SettingEditorType.Choice => new ChoiceSettingItem(def, defaultFactory),
            SettingEditorType.IntList => new IntListSettingItem(def, defaultFactory),
            _ => null,
        };
    }

    public static OffsetSettingItem CreateOffset(SettingDefinition def, string modePropertyName, Func<object> defaultFactory)
        => new(def, modePropertyName, defaultFactory);

    public static TimePairSettingItem CreateTimePair(SettingDefinition left, SettingDefinition right, Func<object> defaultFactory)
        => new(left, right, defaultFactory);

    public static IntPairSettingItem CreateIntPair(SettingDefinition left, SettingDefinition right, Func<object> defaultFactory)
        => new(left, right, defaultFactory);

    public static DoublePairSettingItem CreateDoublePair(SettingDefinition left, SettingDefinition right, Func<object> defaultFactory)
        => new(left, right, defaultFactory);
}
