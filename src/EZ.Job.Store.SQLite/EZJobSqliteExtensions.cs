using EZ.Job.Core;
using EZJob.Store.SQLite;

namespace Microsoft.Extensions.DependencyInjection;

public static class EZJobSqliteExtensions
{
    public static IEZJobBuilder AddSqliteStore(this IEZJobBuilder builder, string connectionString)
    {
        return AddSqliteStore(builder, o => o.ConnectionString = connectionString);
    }

    public static IEZJobBuilder AddSqliteStore(this IEZJobBuilder builder, Action<SqliteStoreOptions> configure)
    {
        var options = new SqliteStoreOptions();
        configure(options);

        builder.Services.AddSingleton<IJobStore>(_ => new SqliteJobStore(options.ConnectionString));
        builder.Services.AddSingleton<IRecurringStore>(_ => new SqliteRecurringStore(options.ConnectionString));

        return builder;
    }
}
