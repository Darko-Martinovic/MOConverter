# MOConverter

MOConverter tells you which tables of a SQL Server database can become memory-optimized (In-Memory OLTP), and what stops the others.

The Red Gate Simple Talk article [Converting a Database to In-Memory OLTP](https://www.red-gate.com/simple-talk/sql/t-sql-programming/converting-database-memory-oltp/) explains the problem. The code as the article shows it, a library with WinForms and WPF front ends for SQL Server 2016 and 2017, is the [`v1-article`](https://github.com/Darko-Martinovic/MOConverter/tree/v1-article) tag. The `main` branch is version 2.

## Version 2

Version 2 is a command-line tool for SQL Server 2017 and later. It looks before it converts:

- `assess` reports, table by table, what converting would do. It only reads. **Available now.**
- `script` will write the T-SQL for the conversion, for you to review. **Coming next.**
- `convert` will copy the tables you choose into a new database, as memory-optimized tables. The source isn't changed. **Coming after `script`.**

Version 1 converted every table and logged what it dropped on the way. Version 2 refuses a change in meaning unless you accept it with `--accept-lossy`. It also fixes version 1's mistakes:

- Alias types now get their exact base type. Version 1 turned `bigint` into `int`, `varchar` into `char`, `datetime2` into `datetime` and `varbinary` into `binary`.
- Spatial types become `varbinary(max)` whole. Version 1 cut them to `nvarchar(1000)`.
- Check constraints are parsed. Version 1 dropped any check whose text contained `upper`, `like` or `charindex`, even in a column name such as `UpperLimit`.
- Computed columns, more than 8 indexes and clustered columnstore indexes are kept, since SQL Server 2017 supports them.
- A table linked by a foreign key to one that can't be converted is reported, instead of failing halfway.

## Requirements

- .NET 10 SDK, to build and run it.
- SQL Server 2017 or later, on Windows or Linux, or Azure SQL Managed Instance. Azure SQL Database works on the Premium and Business Critical tiers only, and hasn't been tested. The tests run on SQL Server 2022 and 2025.
- For `assess`: a login that can see the database's definitions (VIEW DEFINITION). VIEW SERVER STATE lets it compare the size of the tables with the server's memory; without it, the memory check is skipped.

## Running it

```
dotnet run --project src/MOConverter.Cli -- assess --source "Server=.;Database=Sales;Integrated Security=true;TrustServerCertificate=true"
```

or install it as a .NET tool from the repository:

```
dotnet pack src/MOConverter.Cli -o artifacts
dotnet tool install --global MOConverter --add-source artifacts
moconverter assess --source "..."
```

Options:

| Option | Meaning |
|---|---|
| `-s`, `--source` | The database, as a SqlClient connection string. Without it, the `MOCONVERTER_SOURCE` environment variable, which keeps a password off the command line. |
| `--trust-server-certificate` | Accept a self-signed certificate, such as a container's. |
| `-t`, `--tables` | Comma-separated tables: `schema.table`, or `table` in any schema. Names with commas, dots or spaces go in brackets: `[Sales].[Order Details]`. |
| `--schemas` | Comma-separated schemas. With neither `--tables` nor `--schemas`, every table. |
| `-f`, `--format` | `table` (the default), `json` or `markdown`. |

The exit code is 0 when nothing selected is blocked, 2 when something is, and 1 when the database couldn't be read. So `assess` can run in a pipeline.

## What it says

Each table gets the worst outcome among its findings:

| Outcome | Meaning |
|---|---|
| ready | Converts as it is. |
| fixed | The tool changes something, and the data and query results stay the same: an alias type becomes its base type, `text` becomes `varchar(max)`, a clustered index becomes nonclustered, a filter or INCLUDE is dropped from an index. |
| lossy | Converts with a change in meaning, which `--accept-lossy` has to allow: `datetimeoffset` loses its offset, `xml` and the spatial types lose their methods, a CASCADE foreign key becomes NO ACTION, a check or default SQL Server refuses is left out, a trigger isn't carried over. |
| blocked | Can't be converted until something changes: no primary key (or a disabled one), `sql_variant`, a unique filtered index, a temporal, ledger or graph table, a foreign key to a unique key rather than the primary key, a foreign key to a table that won't be converted, a definition the login can't see, or more data than memory. |

For example (a few of the test tables, cut to fit):

```
╭──────────────────────┬───────┬──────────┬─────────┬──────────────────────────────────────────────────────╮
│ Table                │  Rows │   Memory │ Outcome │ Why                                                  │
├──────────────────────┼───────┼──────────┼─────────┼──────────────────────────────────────────────────────┤
│ [cases].[AliasTypes] │     0 │  0 bytes │ fixed   │ fixed: [Id] dbo.BigId → bigint: the alias type's     │
│                      │       │          │         │ base type is bigint                                  │
│ [cases].[Child]      │     0 │  0 bytes │ blocked │ blocked: foreign key [FK_Child_Parent] links it to   │
│                      │       │          │         │ [cases].[Parent], which isn't selected               │
│                      │       │          │         │ lossy: foreign key [FK_Child_Parent] ON DELETE       │
│                      │       │          │         │ CASCADE ON UPDATE SET NULL becomes NO ACTION         │
│ [cases].[Ready]      │ 1,000 │ 270.1 KB │ ready   │                                                      │
╰──────────────────────┴───────┴──────────┴─────────┴──────────────────────────────────────────────────────╯
```

The memory column is a guide: the table's data plus a 24-byte header and an 8-byte pointer per index for each row, doubled for row versions and growth, as Microsoft's sizing advice suggests.

The limits behind these rules were checked on SQL Server 2022 and 2025. The tests ask the server again on every run: each function the tool accepts in a check or default, each type it maps, and each feature it works around (`tests/MOConverter.Tests/ServerAgreesTests.cs`).

## Building and testing

```
dotnet build MOConverter.slnx
dotnet test --solution MOConverter.slnx
```

The tests start SQL Server 2025 in a container with Testcontainers, so they need Docker or Podman. Set `MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2022-latest` to test on 2022, as CI does. With Podman on Windows, set `TESTCONTAINERS_RYUK_DISABLED=true`.

## License

MIT. See [LICENSE](LICENSE).
