using System.Windows;

namespace Raisin.StyleInspector;

internal static class CompareEngine
{
    public static List<ComparedProperty> Compare(DependencyObject a, DependencyObject b)
    {
        var propsA = PropertyEnumerator.Enumerate(a);
        var propsB = PropertyEnumerator.Enumerate(b);

        var dictA = new Dictionary<string, InspectedProperty>();
        foreach (var p in propsA)
            dictA.TryAdd(p.Name, p);

        var dictB = new Dictionary<string, InspectedProperty>();
        foreach (var p in propsB)
            dictB.TryAdd(p.Name, p);

        var allNames = new HashSet<string>(dictA.Keys);
        allNames.UnionWith(dictB.Keys);

        var results = new List<ComparedProperty>();
        foreach (var name in allNames)
        {
            dictA.TryGetValue(name, out var pa);
            dictB.TryGetValue(name, out var pb);

            results.Add(new ComparedProperty
            {
                Name = name,
                Category = pa?.Category ?? pb?.Category ?? "Other",
                Diff = ClassifyDiff(pa, pb),
                DisplayValueA = pa?.DisplayValue,
                SourceTagA = pa?.SourceTag,
                SourceTooltipA = pa?.SourceTooltip,
                SourceBrushA = pa?.SourceBrush,
                ColorPreviewA = pa?.ColorPreview,
                DisplayValueB = pb?.DisplayValue,
                SourceTagB = pb?.SourceTag,
                SourceTooltipB = pb?.SourceTooltip,
                SourceBrushB = pb?.SourceBrush,
                ColorPreviewB = pb?.ColorPreview,
            });
        }

        return results.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
    }

    private static DiffKind ClassifyDiff(InspectedProperty? a, InspectedProperty? b)
    {
        if (a == null) return DiffKind.OnlyB;
        if (b == null) return DiffKind.OnlyA;

        var sameValue = a.DisplayValue == b.DisplayValue;
        var sameSource = a.Source == b.Source;

        if (sameValue && sameSource) return DiffKind.Match;
        if (!sameValue) return DiffKind.ValueDiffers;
        return DiffKind.SourceDiffers;
    }
}
