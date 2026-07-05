using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Raisin.StyleInspector;

internal static class XamlExporter
{
    public static string ExportSetter(InspectedProperty prop)
    {
        var value = FormatXamlValue(prop);
        return $"<Setter Property=\"{prop.Name}\" Value=\"{value}\"/>";
    }

    public static string ExportStyle(FrameworkElement element, IEnumerable<InspectedProperty> properties, bool editedOnly)
    {
        var targetType = element.GetType().Name;
        var sb = new StringBuilder();
        sb.AppendLine($"<Style TargetType=\"{targetType}\">");

        foreach (var prop in properties)
        {
            if (editedOnly && !prop.IsEdited) continue;
            if (!editedOnly && prop.IsDefault) continue;

            var value = FormatXamlValue(prop);
            sb.AppendLine($"    <Setter Property=\"{prop.Name}\" Value=\"{value}\"/>");
        }

        sb.Append("</Style>");
        return sb.ToString();
    }

    public static string ExportDiff(FrameworkElement elementA, FrameworkElement elementB,
        IEnumerable<ComparedProperty> compared)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<!-- A: {elementA.GetType().Name} → B: {elementB.GetType().Name} -->");
        sb.AppendLine($"<Style TargetType=\"{elementB.GetType().Name}\">");

        foreach (var cp in compared)
        {
            if (cp.IsMatch) continue;
            if (cp.Diff == DiffKind.OnlyA) continue;
            if (cp.DisplayValueB == null) continue;

            sb.AppendLine($"    <!-- Was: {cp.DisplayValueA ?? "(absent)"} -->");
            sb.AppendLine($"    <Setter Property=\"{cp.Name}\" Value=\"{cp.DisplayValueB}\"/>");
        }

        sb.Append("</Style>");
        return sb.ToString();
    }

    private static string FormatXamlValue(InspectedProperty prop)
    {
        if (prop.ResourceKey != null)
            return $"{{DynamicResource {prop.ResourceKey}}}";

        return prop.Value switch
        {
            null => "{x:Null}",
            SolidColorBrush brush => brush.Color.ToString(),
            Color color => color.ToString(),
            Thickness t => FormatThickness(t),
            CornerRadius cr => FormatCornerRadius(cr),
            GridLength gl => FormatGridLength(gl),
            double d when double.IsNaN(d) => "Auto",
            double d => d.ToString("G"),
            bool b => b.ToString(),
            Enum e => e.ToString(),
            FontFamily ff => ff.Source,
            string s => s,
            _ => prop.DisplayValue,
        };
    }

    private static string FormatThickness(Thickness t)
    {
        if (t.Left == t.Right && t.Top == t.Bottom && t.Left == t.Top)
            return t.Left.ToString("G");
        if (t.Left == t.Right && t.Top == t.Bottom)
            return $"{t.Left:G},{t.Top:G}";
        return $"{t.Left:G},{t.Top:G},{t.Right:G},{t.Bottom:G}";
    }

    private static string FormatCornerRadius(CornerRadius cr)
    {
        if (cr.TopLeft == cr.TopRight && cr.TopLeft == cr.BottomRight && cr.TopLeft == cr.BottomLeft)
            return cr.TopLeft.ToString("G");
        return $"{cr.TopLeft:G},{cr.TopRight:G},{cr.BottomRight:G},{cr.BottomLeft:G}";
    }

    private static string FormatGridLength(GridLength gl)
    {
        if (gl.IsAuto) return "Auto";
        if (gl.IsStar) return gl.Value == 1.0 ? "*" : $"{gl.Value:G}*";
        return gl.Value.ToString("G");
    }
}
