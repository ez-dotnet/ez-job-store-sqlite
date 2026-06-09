# EZ.Job.Store.SQLite

Store **SQLite** para [EZ.Job.Core](https://github.com/ez-dotnet/ez-job-core).

## Performance

| Store  | Jobs | Workers | EZ.Job (ms) | Hangfire (ms) | Vezes mais rápido |
|--------|------|---------|-------------|---------------|-------------------|
| SQLite | 100  | 1       | 59.45       | 145.06        | 2.44×             |
| SQLite | 1000 | 4       | 372.50      | 865.33        | 2.32×             |

**Eficiência de memória:** EZ.Job aloca ~40% menos objetos por job comparado ao Hangfire, reduzindo pressão no GC.

## Instalação

```bash
dotnet add package EZ.Job.Core
dotnet add package EZ.Job.Store.SQLite
```

## Uso

```csharp
builder.Services.AddEZJob()
    .AddSqliteStore("Data Source=ez_jobs.db");
```

## Projetos relacionados

- [EZ.DotNet](https://github.com/ez-dotnet)
- [EZ.Job.Core](https://github.com/ez-dotnet/ez-job-core)
- [EZ.Job.Recurring](https://github.com/ez-dotnet/ez-job-recurring)
