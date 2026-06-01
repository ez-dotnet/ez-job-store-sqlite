# Resultados — Benchmark EZ.Job.Store.SQLite

## Ambiente

| Item           | Valor                         |
|----------------|-------------------------------|
| Hardware       | Intel i7-12700K, 64GB DDR5   |
| SO             | Ubuntu 24.04                  |
| .NET           | 10.0                          |
| Driver         | Microsoft.Data.Sqlite 9.0.3  |

## Resultados

| Jobs | Workers | EZ.Job (ms) | Hangfire (ms) | Vezes mais rápido |
|------|---------|-------------|---------------|-------------------|
| 100  | 1       | 59.45       | 145.06        | 2.44×             |
| 1000 | 4       | 372.50      | 865.33        | 2.32×             |

SQLite tem a maior latência absoluta devido ao locking de arquivo único, mas ainda é 2.3–2.4× mais rápido que Hangfire.

## Eficiência de Memória

| Métrica                | EZ.Job | Hangfire |
|------------------------|--------|----------|
| Alocações por job      | ~2.4 KB| ~4.1 KB  |
| Objetos por job        | ~18    | ~31      |
| Pressão Gen 0/1/2      | Baixa  | Moderada |
