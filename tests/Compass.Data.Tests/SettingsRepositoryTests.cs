using Compass.Core.Sync;
using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class SettingsRepositoryTests : IDisposable
{
    private readonly string _p; private readonly CompassDb _db;
    public SettingsRepositoryTests()
    { _p = Path.Combine(Path.GetTempPath(), $"compass-{Guid.NewGuid():N}.db"); _db = new(_p); _db.Initialize(); }
    public void Dispose()
    { SqliteConnection.ClearAllPools(); foreach (var f in new[]{_p,_p+"-wal",_p+"-shm"}) if (File.Exists(f)) File.Delete(f); }

    [Fact]
    public void Set_Get_Upsert_GetAll()
    {
        ISettingsStore s = new SettingsRepository(_db);
        s.Get("k").Should().BeNull();
        s.Set("k", "1"); s.Get("k").Should().Be("1");
        s.Set("k", "2"); s.Get("k").Should().Be("2");      // upsert
        s.Set("j", "x");
        s.GetAll().Should().Contain(new KeyValuePair<string,string>("k","2"));
        s.GetAll().Should().ContainKey("j");
    }
}
