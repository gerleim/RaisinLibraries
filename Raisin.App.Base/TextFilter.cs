using System.Text;
using System.Text.RegularExpressions;

namespace Raisin.App.Base;

public class TextFilter
{
    private string _searchTerm = "";
    private bool _negate;
    private Regex? _regex;

    public void Update(string text)
    {
        _negate = text.StartsWith('!');
        _searchTerm = _negate ? text[1..] : text;
        _regex = BuildRegex(_searchTerm);
    }

    public bool Matches(string text)
    {
        if (_searchTerm.Length == 0) return true;
        bool matches = _regex is { } regex
            ? regex.IsMatch(text)
            : text.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase);
        return _negate ? !matches : matches;
    }

    private static Regex? BuildRegex(string pattern)
    {
        if (pattern.Length == 0) return null;
        if (!pattern.AsSpan().ContainsAny('%', '_', '?')) return null;

        var sb = new StringBuilder("(?si)^");
        foreach (var ch in pattern)
        {
            switch (ch)
            {
                case '%': sb.Append(".*"); break;
                case '_': sb.Append('.'); break;
                case '?': sb.Append(".?"); break;
                default: sb.Append(Regex.Escape(ch.ToString())); break;
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString());
    }
}
