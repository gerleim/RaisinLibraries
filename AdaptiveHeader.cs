using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Raisin.WPF.Base;

/// <summary>
/// Attached property that switches DataGrid column headers between full and short text
/// based on available width. When the full header text doesn't fit, the short text is shown
/// and a tooltip displays the full text.
/// Usage: base:AdaptiveHeader.ShortText="R P&amp;L" on any DataGridColumn.
/// </summary>
public static class AdaptiveHeader
{
    public static readonly DependencyProperty ShortTextProperty =
        DependencyProperty.RegisterAttached(
            "ShortText",
            typeof(string),
            typeof(AdaptiveHeader),
            new PropertyMetadata(null, OnShortTextChanged));

    public static string? GetShortText(DependencyObject obj) => (string?)obj.GetValue(ShortTextProperty);
    public static void SetShortText(DependencyObject obj, string? value) => obj.SetValue(ShortTextProperty, value);

    private static void OnShortTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGridColumn column || e.NewValue is not string shortText || string.IsNullOrEmpty(shortText))
            return;

        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(AdaptiveHeaderControl));
        factory.SetValue(AdaptiveHeaderControl.ShortTextProperty, shortText);
        template.VisualTree = factory;
        column.HeaderTemplate = template;
    }
}

/// <summary>
/// Control placed inside a DataGrid column HeaderTemplate.
/// Measures the full header text and switches to short text + tooltip when it doesn't fit.
/// </summary>
internal sealed class AdaptiveHeaderControl : Decorator
{
    public static readonly DependencyProperty ShortTextProperty =
        DependencyProperty.Register(
            nameof(ShortText),
            typeof(string),
            typeof(AdaptiveHeaderControl),
            new PropertyMetadata(null));

    public string? ShortText
    {
        get => (string?)GetValue(ShortTextProperty);
        set => SetValue(ShortTextProperty, value);
    }

    private readonly TextBlock _textBlock;
    private DataGridColumnHeader? _header;
    private string? _fullText;

    public AdaptiveHeaderControl()
    {
        _textBlock = new TextBlock();
        Child = _textBlock;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        // Defer setup until the header is in the visual tree
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _header = FindAncestor<DataGridColumnHeader>(this);
        if (_header is null)
            return;

        // Ensure the ContentPresenter offers full width to this control
        _header.HorizontalContentAlignment = HorizontalAlignment.Stretch;

        // The column's Header property holds the full text
        _fullText = _header.Column?.Header?.ToString();
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        UpdateText();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        _header = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateText();
    }

    private void UpdateText()
    {
        if (_fullText is null)
            return;

        var availableWidth = ActualWidth;
        var fullWidth = MeasureText(_fullText);

        if (fullWidth <= availableWidth)
        {
            _textBlock.Text = _fullText;
            _textBlock.ToolTip = null;
        }
        else
        {
            _textBlock.Text = ShortText ?? _fullText;
            _textBlock.ToolTip = _fullText;
        }
    }

    private double MeasureText(string text)
    {
        var typeface = new Typeface(
            _textBlock.FontFamily,
            _textBlock.FontStyle,
            _textBlock.FontWeight,
            _textBlock.FontStretch);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            _textBlock.FontSize > 0 ? _textBlock.FontSize : 12,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        return formatted.Width;
    }

    private static T? FindAncestor<T>(DependencyObject obj) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(obj);
        while (current is not null)
        {
            if (current is T result)
                return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
