namespace Compass.Core.Config;

public static class SecretsGuard
{
    public const string Placeholder = "REPLACE_VIA_USER_SECRETS";

    public static IReadOnlyList<string> FindMissing(CompassOptions o)
    {
        var missing = new List<string>();
        void Check(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == Placeholder)
                missing.Add(name);
        }
        Check("Steam:ApiKey", o.Steam.ApiKey);
        Check("Igdb:ClientId", o.Igdb.ClientId);
        Check("Igdb:ClientSecret", o.Igdb.ClientSecret);
        return missing;
    }
}
