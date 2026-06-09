using System.Text.Json;
using EZ.Job.Core;
using Microsoft.Data.Sqlite;

namespace EZJob.Store.SQLite;

public sealed class SqliteJobStore : IJobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _connectionString;

    public SqliteJobStore(string connectionString)
    {
        _connectionString = connectionString;
        EnsureTableAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureTableAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ez_jobs (
                id              TEXT PRIMARY KEY,
                type_name       TEXT NOT NULL,
                method_name     TEXT NOT NULL,
                argument_types  TEXT NOT NULL,
                arguments       TEXT NOT NULL,
                status          INTEGER NOT NULL DEFAULT 0,
                created_at      TEXT NOT NULL,
                error           TEXT,
                recurring_job_id TEXT,
                started_at      TEXT,
                completed_at     TEXT
            )
            """;

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async ValueTask AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ez_jobs (id, type_name, method_name, argument_types, arguments, status, created_at, error, recurring_job_id, started_at, completed_at)
            VALUES ($id, $type_name, $method_name, $argument_types, $arguments, $status, $created_at, $error, $recurring_job_id, $started_at, $completed_at)
            """;

        cmd.Parameters.AddWithValue("$id", job.Id);
        cmd.Parameters.AddWithValue("$type_name", job.TypeName);
        cmd.Parameters.AddWithValue("$method_name", job.MethodName);
        cmd.Parameters.AddWithValue("$argument_types", JsonSerializer.Serialize(job.ArgumentTypes, JsonOptions));
        cmd.Parameters.AddWithValue("$arguments", SerializeArgs(job.Arguments));
        cmd.Parameters.AddWithValue("$status", (int)job.Status);
        cmd.Parameters.AddWithValue("$created_at", job.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$error", job.Error ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$recurring_job_id", job.RecurringJobId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$started_at", job.StartedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$completed_at", job.CompletedAt?.ToString("O") ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Job?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ez_jobs WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadJob(reader);
        }

        return null;
    }

    public async ValueTask<IEnumerable<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = new List<Job>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ez_jobs ORDER BY created_at";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            jobs.Add(ReadJob(reader));
        }

        return jobs;
    }

    public async ValueTask UpdateStatusAsync(string id, JobStatus status, string? error = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        var now = DateTime.UtcNow;

        cmd.CommandText = """
            UPDATE ez_jobs
            SET status = $status,
                error = $error,
                started_at = CASE WHEN $status = 1 THEN COALESCE(started_at, $now) ELSE started_at END,
                completed_at = CASE WHEN $status IN (2, 3) THEN $now ELSE NULL END
            WHERE id = $id
            """;

        cmd.Parameters.AddWithValue("$status", (int)status);
        cmd.Parameters.AddWithValue("$error", error ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$now", now.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IEnumerable<Job>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var jobs = new List<Job>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ez_jobs WHERE status = 0 ORDER BY created_at";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            jobs.Add(ReadJob(reader));
        }

        return jobs;
    }

    private static Job ReadJob(SqliteDataReader reader)
    {
        return new Job(
            Id: reader.GetString(0),
            TypeName: reader.GetString(1),
            MethodName: reader.GetString(2),
            ArgumentTypes: JsonSerializer.Deserialize<string[]>(reader.GetString(3), JsonOptions) ?? [],
            Arguments: DeserializeArgs(reader.GetString(4)),
            Status: (JobStatus)reader.GetInt32(5),
            CreatedAt: DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
            Error: reader.IsDBNull(7) ? null : reader.GetString(7),
            StartedAt: reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
            CompletedAt: reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
            RecurringJobId: reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    private static string SerializeArgs(object?[] args)
    {
        return JsonSerializer.Serialize(args, JsonOptions);
    }

    private static object?[] DeserializeArgs(string json)
    {
        return JsonSerializer.Deserialize<object?[]>(json, JsonOptions) ?? [];
    }
}
