using Xunit;
using EZJob.Store.SQLite;

namespace EZ.Job.Store.SQLite.Tests;

public sealed class SqliteJobStoreTests
{
    private const string ConnectionString = "Data Source=ez_jobs_test.db;Mode=Memory;Cache=Shared";

    [Fact(Skip = "Requires SQLite database setup")]
    public async Task AddAsync_should_store_job()
    {
        var store = new SqliteJobStore(ConnectionString);
        var job = new EZ.Job.Core.Job("test-id", "T", "M", [], [], EZ.Job.Core.JobStatus.Enqueued, System.DateTime.UtcNow, null);

        await store.AddAsync(job);
        var result = await store.GetAsync("test-id");

        Assert.NotNull(result);
        Assert.Equal("test-id", result!.Id);
    }
}
