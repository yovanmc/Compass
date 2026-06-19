using System.Text.RegularExpressions;

namespace Compass.Data.Match;

public static partial class NameNormalizer
{
    private static readonly string[] EditionSuffixes =
    {
        "definitive edition", "deluxe edition", "deluxe", "complete edition",
        "game of the year edition", "goty edition", "goty", "remastered",
        "enhanced edition", "ultimate edition", "anniversary edition"
    };

    public static string Normalize(string raw)
    {
        var s = raw.ToLowerInvariant();
        s = s.Replace("™", " ").Replace("®", " ").Replace("©", " ");
        // Strip edition suffixes that appear after a dash, colon, parenthesis, or trailing
        foreach (var suf in EditionSuffixes)
            s = Regex.Replace(s, $@"[\-\(\:]?\s*{Regex.Escape(suf)}\s*\)?", " ");
        // Drop remaining punctuation (non-alphanumeric, non-space)
        s = NonAlphaNum().Replace(s, " ");
        // Collapse whitespace
        s = WhitespaceRun().Replace(s, " ").Trim();
        // Strip leading articles (helps match "The Witcher" → "witcher")
        s = LeadingArticle().Replace(s, "");
        return s;
    }

    [GeneratedRegex(@"[^a-z0-9 ]")] private static partial Regex NonAlphaNum();
    [GeneratedRegex(@"\s+")] private static partial Regex WhitespaceRun();
    [GeneratedRegex(@"^(?:the|a|an) ")] private static partial Regex LeadingArticle();
}
