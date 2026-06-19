using System.Text.RegularExpressions;

namespace Compass.Core.Sync;

public static partial class FeatureKey
{
    /// Build a namespaced feature key: "{category}:{slug}".
    /// Example: ("genre", "Role-playing (RPG)") → "genre:role-playing-rpg"
    public static string Build(string category, string name)
        => $"{category}:{Slug(name)}";

    /// Slug: lowercase, replace non-alphanumerics with '-', collapse dash runs, trim dashes.
    public static string Slug(string name)
    {
        var s = name.ToLowerInvariant();
        s = NonAlphaNum().Replace(s, "-");
        s = DashRun().Replace(s, "-").Trim('-');
        return s;
    }

    [GeneratedRegex(@"[^a-z0-9]+")] private static partial Regex NonAlphaNum();
    [GeneratedRegex(@"-+")] private static partial Regex DashRun();
}
