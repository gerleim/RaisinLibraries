using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Raisin.Audio.Tests;

/// <summary>
/// That the shared row templates actually load.
/// </summary>
/// <remarks>
/// Worth a test because the failure is invisible until someone opens an options window: a
/// StaticResource naming a key the consuming app does not define throws at load, and a merged
/// dictionary is never loaded by a build. This dictionary is merged into two apps that theme
/// themselves entirely differently, so it must name nothing but its own keys and the shared
/// editors' - which is what these assert.
/// </remarks>
public class SoundEditorTemplatesTests
{
    private const string Source =
        "pack://application:,,,/Raisin.Audio;component/Themes/SoundEditorTemplates.xaml";

    /// <summary>
    /// Loading a dictionary that instantiates WPF elements needs an STA thread, and the test
    /// runner does not give us one.
    /// </summary>
    private static T OnStaThread<T>(Func<T> work)
    {
        T? result = default;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                // The pack scheme is registered by WPF's own startup, which never runs here.
                // Touching this property is what registers it.
                _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
                if (Application.Current is null) _ = new Application();

                result = work();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
        return result!;
    }

    private static ResourceDictionary Load() =>
        new() { Source = new Uri(Source, UriKind.Absolute) };

    [Fact]
    public void TheDictionaryLoads()
    {
        var count = OnStaThread(() => Load().Count);

        Assert.True(count > 0, "the dictionary loaded but declared nothing");
    }

    [Theory]
    [InlineData("SoundSettingTemplate")]
    [InlineData("VolumeSettingTemplate")]
    [InlineData("VoiceSettingTemplate")]
    public void EveryRowTemplateIsThere(string key)
    {
        var template = OnStaThread(() => Load()[key] as DataTemplate);

        Assert.NotNull(template);
    }

    [Fact]
    public void TheSharedEditorsComeWithIt()
    {
        // Merged here rather than left to the consumer: the rows use SettingRowBorder and
        // SettingsResetButton, and a consumer who forgot would fail at load.
        var found = OnStaThread(() =>
        {
            var dictionary = Load();
            return dictionary.Contains("SettingRowBorder")
                && dictionary.Contains("SettingsResetButton");
        });

        Assert.True(found, "SettingsEditorTemplates is not merged, so the rows have no chrome");
    }

    [Fact]
    public void ARowCanActuallyBeBuilt()
    {
        // Loading the dictionary only parses it. A StaticResource inside a template is resolved
        // when the template is applied, which is the moment a missing palette key would throw -
        // so the row has to be instantiated for this to prove anything.
        var built = OnStaThread(() =>
        {
            var dictionary = Load();
            var template = (DataTemplate)dictionary["VolumeSettingTemplate"];
            return template.LoadContent() is not null;
        });

        Assert.True(built);
    }

    [Fact]
    public void TheGlyphsTakeTheirColourFromTheirSurroundings()
    {
        // Not from a named brush. Both speakers used to stroke themselves with a palette key
        // only RaisinTerminal2 defines, which would have thrown the moment StockRaisin2 opened
        // its options window.
        var stroke = OnStaThread(() =>
        {
            var style = (Style)Load()["SoundSpeakerGlyph"];
            var setter = style.Setters.OfType<Setter>()
                .First(s => s.Property == System.Windows.Shapes.Shape.StrokeProperty);
            return setter.Value;
        });

        Assert.IsType<System.Windows.Data.Binding>(stroke);
    }
}
