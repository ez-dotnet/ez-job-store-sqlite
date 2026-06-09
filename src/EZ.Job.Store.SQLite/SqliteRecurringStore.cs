using System.Text.Json;
using EZ.Job.Core;
using Microsoft.Data.Sqlite;

namespace EZJob.Store.SQLite;

public sealed class SqliteRecurringStore : IRecurringStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _connectionString;

    public SqliteRecurringStore(string connectionString)
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
            CREATE TABLE IF NOT EXISTS ez_recurring_definitions (
                id                  TEXT PRIMARY KEY,
                type_name           TEXT NOT NULL,
                method_name         TEXT NOT NULL,
                argument_types      TEXT NOT NULL,
                arguments           TEXT NOT NULL,
                cron_expression     TEXT NOT NULL,
                is_active           INTEGER NOT NULL DEFAULT 1,
                created_at_utc      TEXT NOT NULL,
                last_execution_utc  TEXT
            )
            """;

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async ValueTask AddOrUpdateAsync(RecurringDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ez_recurring_definitions (id, type_name, method_name, argument_types, arguments, cron_expression, is_active, created_at_utc, last_execution_utc)
            VALUES ($id, $type_name, $method_name, $argument_types, $arguments, $cron_expression, $is_active, $created_at_utc, $last_execution_utc)
            ON CONFLICT(id) DO UPDATE SET
                type_name = excluded.type_name,
                method_name = excluded.method_name,
                argument_types = excluded.argument_types,
                arguments = excluded.arguments,
                cron_expression = excluded.cron_expression,
                is_active = excluded.is_active,
                last_execution_utc = excluded.last_execution_utc
            """;

        cmd.Parameters.AddWithValue("$id", definition.Id.ToString());
        cmd.Parameters.AddWithValue("$type_name", definition.TypeName);
        cmd.Parameters.AddWithValue("$method_name", definition.MethodName);
        cmd.Parameters.AddWithValue("$argument_types", JsonSerializer.Serialize(definition.ArgumentTypes, JsonOptions));
        cmd.Parameters.AddWithValue("$arguments", JsonSerializer.Serialize(definition.Arguments, JsonOptions));
        cmd.Parameters.AddWithValue("$cron_expression", definition.CronExpression);
        cmd.Parameters.AddWithValue("$is_active", definition.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$created_at_utc", definition.CreatedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$last_execution_utc", definition.LastExecutionUtc?.ToString("O") ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ez_recurring_definitions WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<RecurringDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ez_recurring_definitions WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadDefinition(reader);
        }

        return null;
    }

    public async ValueTask<IEnumerable<RecurringDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var definitions = new List<RecurringDefinition>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ez_recurring_definitions ORDER BY created_at_utc";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            definitions.Add(ReadDefinition(reader));
        }

        return definitions;
    }

    public async ValueTask SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE ez_recurring_definitions SET is_active = $is_active WHERE id = $id";
        cmd.Parameters.AddWithValue("$is_active", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static RecurringDefinition ReadDefinition(SqliteDataReader reader)
    {
        return new RecurringDefinition(
            Id: Guid.Parse(reader.GetString(0)),
            TypeName: reader.GetString(1),
            MethodName: reader.GetString(2),
            ArgumentTypes: JsonSerializer.Deserialize<string[]>(reader.GetString(3), JsonOptions) ?? [],
            Arguments: JsonSerializer.Deserialize<object?[]>(reader.GetString(4), JsonOptions) ?? [],
            CronExpression: reader.GetString(5),
            IsActive: reader.GetInt32(6) == 1,
            CreatedAtUtc: DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
            LastExecutionUtc: reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}
