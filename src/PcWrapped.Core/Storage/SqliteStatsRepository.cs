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
);
CREATE TABLE IF NOT EXISTS app_paths (
    process TEXT PRIMARY KEY,
    path    TEXT NOT NULL
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

    public async Task RollupOlderThanAsync(DateOnly cutoffDay)
    {
        long cutoff = new DateTimeOffset(
            cutoffDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

        await using var tx = (Microsoft.Data.Sqlite.SqliteTransaction)
            await _conn.BeginTransactionAsync();

        // Bucket each old sample to the start of its hour, sum seconds per (process, hour).
        await using (var agg = _conn.CreateCommand())
        {
            agg.Transaction = tx;
            agg.CommandText = @"
CREATE TEMP TABLE _rollup AS
SELECT (start_unix / 3600) * 3600 AS hour_unix, process,
       MIN(title) AS title, SUM(seconds) AS seconds
FROM samples WHERE start_unix < $cutoff
GROUP BY hour_unix, process;";
            agg.Parameters.AddWithValue("$cutoff", cutoff);
            await agg.ExecuteNonQueryAsync();
        }
        await using (var del = _conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM samples WHERE start_unix < $cutoff;";
            del.Parameters.AddWithValue("$cutoff", cutoff);
            await del.ExecuteNonQueryAsync();
        }
        await using (var ins = _conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"
INSERT INTO samples (start_unix, process, title, seconds)
SELECT hour_unix, process, title, seconds FROM _rollup;
DROP TABLE _rollup;";
            await ins.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task UpsertAppPathAsync(string process, string path)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO app_paths (process, path) VALUES ($p, $path) " +
            "ON CONFLICT(process) DO UPDATE SET path = $path;";
        cmd.Parameters.AddWithValue("$p", process);
        cmd.Parameters.AddWithValue("$path", path);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAppPathsAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT process, path FROM app_paths";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    public void Dispose() => _conn.Dispose();
}
