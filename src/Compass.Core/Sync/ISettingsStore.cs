namespace Compass.Core.Sync;

public interface ISettingsStore
{
    string? Get(string key);
    void Set(string key, string value);
    IReadOnlyDictionary<string, string> GetAll();
}
