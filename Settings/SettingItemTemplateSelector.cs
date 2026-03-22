using System.Windows;
using System.Windows.Controls;

namespace Raisin.WPF.Base.Settings;

public class SettingItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? CategoryHeaderTemplate { get; set; }
    public DataTemplate? BoolTemplate { get; set; }
    public DataTemplate? IntTemplate { get; set; }
    public DataTemplate? DoubleTemplate { get; set; }
    public DataTemplate? StringTemplate { get; set; }
    public DataTemplate? TimeOnlyTemplate { get; set; }
    public DataTemplate? ChoiceTemplate { get; set; }
    public DataTemplate? IntListTemplate { get; set; }
    public DataTemplate? OffsetTemplate { get; set; }
    public DataTemplate? TimePairTemplate { get; set; }
    public DataTemplate? IntPairTemplate { get; set; }
    public DataTemplate? DoublePairTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            CategoryHeaderItem => CategoryHeaderTemplate,
            BoolSettingItem => BoolTemplate,
            IntSettingItem => IntTemplate,
            DoubleSettingItem => DoubleTemplate,
            StringSettingItem => StringTemplate,
            TimeOnlySettingItem => TimeOnlyTemplate,
            ChoiceSettingItem => ChoiceTemplate,
            IntListSettingItem => IntListTemplate,
            OffsetSettingItem => OffsetTemplate,
            TimePairSettingItem => TimePairTemplate,
            IntPairSettingItem => IntPairTemplate,
            DoublePairSettingItem => DoublePairTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
