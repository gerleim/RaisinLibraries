using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Raisin.WPF.Base.Behaviors;

/// <summary>
/// Attached behavior that restricts TextBox input to numeric values.
/// Set Mode="Int" for integer-only or Mode="Double" for decimal input.
/// </summary>
public static partial class NumericInputBehavior
{
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode", typeof(NumericMode), typeof(NumericInputBehavior),
            new PropertyMetadata(NumericMode.None, OnModeChanged));

    public static NumericMode GetMode(DependencyObject obj) => (NumericMode)obj.GetValue(ModeProperty);
    public static void SetMode(DependencyObject obj, NumericMode value) => obj.SetValue(ModeProperty, value);

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        textBox.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(textBox, OnPasting);

        if ((NumericMode)e.NewValue != NumericMode.None)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var mode = GetMode(textBox);

        if (mode == NumericMode.Int)
        {
            e.Handled = !IntCharRegex().IsMatch(e.Text);
        }
        else if (mode == NumericMode.Double)
        {
            // Allow digits and one decimal point
            if (e.Text == ".")
                e.Handled = textBox.Text.Contains('.');
            else
                e.Handled = !IntCharRegex().IsMatch(e.Text);
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (!e.DataObject.GetDataPresent(typeof(string))) { e.CancelCommand(); return; }

        var text = (string?)e.DataObject.GetData(typeof(string)) ?? "";
        var mode = GetMode(textBox);

        if (mode == NumericMode.Int && !int.TryParse(text, out _))
            e.CancelCommand();
        else if (mode == NumericMode.Double && !double.TryParse(text, out _))
            e.CancelCommand();
    }

    [GeneratedRegex(@"^[0-9]$")]
    private static partial Regex IntCharRegex();
}

public enum NumericMode
{
    None,
    Int,
    Double,
}
