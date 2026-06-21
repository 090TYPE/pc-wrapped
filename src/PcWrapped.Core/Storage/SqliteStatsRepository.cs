using Microsoft.Data.Sqlite;
using PcWrapped.Core.Models;

namespace PcWrapped.Core.Storage;

public sealed class SqliteStatsRepository : IStatsRepository, IDisposable
{
    private readonly SqliteConnection _conn;

    public SqliteStatsRepository(string connectionString)
    {
        _conn = new SqliteConnection(connectionString);
        _conn.Open(); // keep open so in-memory shared DB survives
    }

    public async Task InitializeAsync()
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS samples (
    start_unix INTEGER NOT NULL,
    process    TEXT    NOT NULL,
    title      TEXT    NOT NULL,
    seconds    INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_samples_start ON samples(start_unix);
CREATE TABLE IF NOT EXISTS input_counters (
    day        TEXT    PRIMARY KEY,
    keystrokes INTEGER NOT NULL,
    clicks     INTEGER NOT NULL,
    pixels     REAL    NOT NULL
);";
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddSampleAsync(UsageSample sample)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO samples (start_unix, process, title, seconds) VALUES ($s,$p,$t,$d)";
        cmd.Parameters.AddWithValue("$s", sample.Start.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$p", sample.ProcessName);
        cmd.Parameters.AddWithValue("$t", sample.WindowTitle);
        cmd.Parameters.AddWithValue("$d", sample.DurationSeconds);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<UsageSample>> GetSamplesAsync(
        DateTimeOffset fromInclusive, DateTimeOffset toExclusive)
    {
        var list = new List<UsageSample>();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT start_unix, process, title, seconds FROM samples " +
            "WHERE start_unix >= $from AND start_unix < $to";
        cmd.Parameters.AddWithValue("$from", fromInclusive.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$to", toExclusive.ToUnixTimeSeconds());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new UsageSample(
                DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(0)),
                r.GetString(1), r.GetString(2), r.GetInt32(3)));
        }
        return list;
    }

    public async Task AddInputCountersAsync(DateOnly day, InputCounters delta)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO input_counters (day, keystrokes, clicks, pixels)
VALUES ($day, $k, $c, $p)
ON CONFLICT(day) DO UPDATE SET
    keystrokes = keystrokes + $k,
    clicks     = clicks + $c,
    pixels     = pixels + $p;";
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$k", delta.Keystrokes);
        cmd.Parameters.AddWithValue("$c", delta.Clicks);
        cmd.Parameters.AddWithValue("$p", delta.MousePixels);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<InputCounters> GetInputCountersAsync(
        DateOnly fromInclusive, DateOnly toInclusive)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT COALESCE(SUM(keystrokes),0), COALESCE(SUM(clicks),0), COALESCE(SUM(pixels),0) " +
            "FROM input_counters WHERE day >= $from AND day <= $to";
        cmd.Parameters.AddWithValue("$from", fromInclusive.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", toInclusive.ToString("yyyy-MM-dd"));
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new InputCounters(r.GetInt64(0), r.GetInt64(1), r.GetDouble(2));
    }

    public async Task<IReadOnlyList<DateOnly>> GetActiveDaysAsync()
    {
        var days = new List<DateOnly>();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT date(start_unix, 'unixepoch') FROM samples ORDER BY 1";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            days.Add(DateOnly.Parse(r.GetString(0)));
        return days;
    }

    public void Dispose() => _conn.Dispose();
}
