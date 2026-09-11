using System.Windows;
using Raisin.WPF.Base.Settings;

namespace Raisin.Audio;

/// <summary>
/// The shared selector plus the sound picker. Its SelectTemplate falls through to the base for
/// types it does not know, so an app can add an editor without changing Raisin.WPF.Base.
/// </summary>
public class SoundSettingTemplateSelector : SettingItemTemplateSelector
{
    public DataTemplate? SoundTemplate { get; set; }
    public DataTemplate? VolumeTemplate { get; set; }
    public DataTemplate? VoiceTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item switch
        {
            SoundSettingItem => SoundTemplate,
            VolumeSettingItem => VolumeTemplate,
            SpeechVoiceSettingItem => VoiceTemplate,
            _ => base.SelectTemplate(item, container),
        };
}
