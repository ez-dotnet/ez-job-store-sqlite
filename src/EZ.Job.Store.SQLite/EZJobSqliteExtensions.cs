using EZ.Job.Core;
using EZJob.Store.SQLite;

namespace Microsoft.Extensions.DependencyInjection;

public static class EZJobSqliteExtensions
{
    public static EZJobBuilder AddSqliteStore(this EZJobBuilder builder, string connectionString)
    {
        return AddSqliteStore(builder, o => o.ConnectionString = connectionString);
    }

    public static EZJobBuilder AddSqliteStore(this EZJobBuilder builder, Action<SqliteStoreOptions> configure)
    {
        var options = new SqliteStoreOptions();
        configure(options);

        builder.Services.AddSingleton<IJobStore>(_ => new SqliteJobStore(options.ConnectionString));

        return builder;
    }
}
