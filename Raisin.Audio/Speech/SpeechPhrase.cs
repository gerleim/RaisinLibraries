using System.Text;
using System.Text.RegularExpressions;

namespace Raisin.Audio;

/// <summary>
/// What a spoken alert says: one wording for a single session, another for several.
/// </summary>
/// <remarks>
/// Stored as one string - <c>one session||several sessions</c> - because a sound setting is a
/// single spec, and keeping it that way means the resolver, the save path and the round-trip
/// tests all stay as they are. The picker shows the two halves as separate boxes, so the
/// separator is an implementation detail rather than something anyone types.
/// </remarks>
public sealed record SpeechPhrase(string Singular, string Plural)
{
    public const string Separator = "||";

    public static SpeechPhrase Empty { get; } = new("", "");

    public static SpeechPhrase Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Empty;

        var split = text.Split(Separator, 2);
        return new SpeechPhrase(
            split[0].Trim(),
            split.Length > 1 ? split[1].Trim() : "");
    }

    /// <summary>Back to the stored form. The plural half is dropped when it is blank.</summary>
    public string Compose() =>
        string.IsNullOrWhiteSpace(Plural) ? Singular.Trim()
                                          : $"{Singular.Trim()}{Separator}{Plural.Trim()}";

    public bool IsEmpty => string.IsNullOrWhiteSpace(Singular);
}

/// <summary>One subject's reason to speak, waiting to be folded into the next batch.</summary>
/// <param name="Subject">
/// What this is about, as one name. The batch groups and counts by it, and a withdrawal matches
/// on it. Also readable from a phrase as <c>{subject}</c>.
/// </param>
/// <param name="Tokens">
/// The other placeholders this request can fill, by name and without the braces.
/// </param>
/// <param name="Count">
/// How many things this request already speaks for. Normally one - the batch does the
/// counting - but a caller that has counted for itself says so here, and the phrase is filled
/// with that number rather than with the number of requests.
/// </param>
public sealed record SpeechRequest(
    SpeechPhrase Phrase,
    string? Subject = null,
    IReadOnlyDictionary<string, string>? Tokens = null,
    int Count = 1);

/// <summary>
/// Turns everything that happened during a batching window into one thing to say.
/// </summary>
/// <remarks>
/// Speech occupies the channel for seconds, so the rate limit that suits a chime does not
/// transfer: dropping a chime costs nothing, dropping an utterance loses what it was going to
/// tell you. Collecting instead means five sessions finishing at once becomes one sentence
/// rather than five overlapping ones or four discarded.
/// </remarks>
public static class SpeechBatch
{
    /// <summary>The sentence for a batch, or empty when there is nothing worth saying.</summary>
    public static string Compose(IEnumerable<SpeechRequest> requests)
    {
        // The same session reporting the same thing twice inside one window is one event.
        var distinct = requests
            .Where(r => !r.Phrase.IsEmpty)
            .GroupBy(r => (r.Phrase.Singular, r.Phrase.Plural, Subject: r.Subject ?? ""))
            .Select(g => g.First())
            .ToList();

        var sentences = new List<string>();

        // Grouped by wording, in the order each was first heard from.
        foreach (var group in distinct.GroupBy(r => r.Phrase))
        {
            var items = group.ToList();
            var phrase = group.Key;

            // Requests usually stand for one session each, so this is the number of them; a
            // request that counted for itself contributes its own number instead.
            var count = items.Sum(i => i.Count);

            if (count == 1)
            {
                sentences.Add(Fill(phrase.Singular, items[0].Subject, items[0].Tokens, 1));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(phrase.Plural))
            {
                // The subject becomes the list of them, and the tokens of the first stand for
                // the group: a plural wording that names one of several is worse than one that
                // names none, so a phrase meant for a batch should be written not to.
                sentences.Add(Fill(phrase.Plural, NameList(items), items[0].Tokens, count));
                continue;
            }

            // No plural wording given: say the singular for each rather than lose the detail.
            foreach (var item in items)
                sentences.Add(Fill(phrase.Singular, item.Subject, item.Tokens, item.Count));
        }

        return string.Join(" ", sentences.Where(s => s.Length > 0));
    }

    /// <summary>
    /// Substitutes the placeholders and leaves the sentence ending in a full stop, which is what
    /// gives a synthesiser its falling intonation instead of running two clauses together.
    /// </summary>
    /// <remarks>
    /// A placeholder nobody supplied is replaced with nothing rather than left in place: a
    /// phrase carried over from another app, or written before a token existed, should read
    /// short rather than read the word "symbol" in braces aloud. Which is also why the doubled
    /// spaces a dropped placeholder leaves behind are squeezed out afterwards.
    /// </remarks>
    public static string Fill(
        string template,
        string? subject,
        IReadOnlyDictionary<string, string>? tokens,
        int count)
    {
        if (string.IsNullOrWhiteSpace(template)) return "";

        var text = new StringBuilder(template)
            .Replace("{subject}", SpokenName(subject))
            .Replace("{count}", count.ToString());

        if (tokens is not null)
            foreach (var (name, value) in tokens)
                text.Replace("{" + name + "}", SpokenName(value));

        var filled = DropUnfilled(text.ToString()).Trim();

        // A blank placeholder can leave doubled spaces behind.
        while (filled.Contains("  ")) filled = filled.Replace("  ", " ");

        if (filled.Length == 0) return "";
        return filled.EndsWith('.') || filled.EndsWith('!') || filled.EndsWith('?') ? filled : filled + ".";
    }

    /// <summary>
    /// Removes any <c>{placeholder}</c> nothing answered to. Deliberately narrow - only a brace
    /// pair around a bare identifier - so that braces meaning something else in a sentence
    /// survive.
    /// </summary>
    private static string DropUnfilled(string text) =>
        text.Contains('{') ? UnfilledPlaceholder.Replace(text, "") : text;

    private static readonly Regex UnfilledPlaceholder = new(@"\{[A-Za-z0-9_-]+\}", RegexOptions.Compiled);

    /// <summary>
    /// A name as it should be said. Claude prefixes its session titles with U+2733, the eight
    /// spoked asterisk, which a synthesiser reads out as the word "asterisk" - so decoration at
    /// either end of a name is dropped. Punctuation inside a name is left alone.
    /// </summary>
    public static string SpokenName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        int start = 0;
        while (start < name.Length && !char.IsLetterOrDigit(name[start])) start++;

        int end = name.Length - 1;
        while (end >= start && !char.IsLetterOrDigit(name[end])) end--;

        return start > end ? "" : name[start..(end + 1)];
    }

    private static string NameList(IReadOnlyList<SpeechRequest> items)
    {
        var names = items.Select(i => SpokenName(i.Subject))
                         .Where(n => n.Length > 0)
                         .ToList();

        return names.Count switch
        {
            0 => "",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
        };
    }
}
