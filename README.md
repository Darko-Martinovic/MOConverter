# MOConverter

MOConverter tells you which tables of a SQL Server database can become memory-optimized (In-Memory OLTP), and what stops the others. It then copies the ones you choose into a new database as memory-optimized tables.

The Red Gate Simple Talk article [Converting a Database to In-Memory OLTP](https://www.red-gate.com/simple-talk/sql/t-sql-programming/converting-database-memory-oltp/) explains the problem. The code as the article shows it is the [`v1-article`](https://github.com/Darko-Martinovic/MOConverter/tree/v1-article) tag: a library with WinForms and WPF front ends, for SQL Server 2016 and 2017. The `main` branch is version 2.

## Version 2

Version 2 is a command-line tool for SQL Server 2019 and later. It looks before it converts:

- `assess` reports, table by table, what converting would do. It only reads.
- `script` writes the T-SQL that creates memory-optimized copies of the tables you choose in another database, for you to review and run. It only reads.
- `convert` makes a new database with those tables and copies the data into them. The source is only read.
- `rules` lists the rules the assessment applies.

Version 1 converted every table and logged what it dropped on the way. Version 2 refuses a change in meaning unless you accept it with `--accept-lossy`. It also fixes version 1's mistakes:

- **Alias types** get their exact base type. Version 1 turned `bigint` into `int`, `varchar` into `char`, `datetime2` into `datetime` and `varbinary` into `binary`.
- **Spatial types** become `varbinary(max)` whole. Version 1 cut them to `nvarchar(1000)`.
- **`datetimeoffset`** is copied as its UTC time, so the instant survives. Version 1 dropped the offset and kept the local time.
- **Check constraints** are parsed. Version 1 dropped any check whose text contained `upper`, `like` or `charindex`, even when it was only part of a column name such as `UpperLimit`.
- **Computed columns, more than 8 indexes and clustered columnstore indexes** are kept, since SQL Server 2017 supports them.
- **The data** is copied once, in batches. Version 1 copied each table twice, in one transaction, and left a staging table behind.
- **A table linked by a foreign key** to one that can't be converted is reported before anything starts. Version 1 failed partway through.

## Requirements

- .NET 10 SDK, to build and run it.
- SQL Server 2019 or later, on Windows or Linux, or Azure SQL Managed Instance. The tests run on SQL Server 2019, 2022 and 2025. Azure SQL Database supports In-Memory OLTP on the Premium and Business Critical tiers only, and the tool hasn't been tested there.
- To read the source, a login that can see the database's definitions (VIEW DEFINITION). With VIEW SERVER STATE too, the tool compares the size of the tables with the server's memory; without it, that check is skipped.
- For `convert`, or to run the script: permission to create a database on the target server.

## Running it

Install it from nuget.org as a .NET tool:

```
dotnet tool install --global MOConverter
moconverter assess --source "Server=.;Database=Sales;Integrated Security=true;TrustServerCertificate=true"
moconverter script --source "..." --tables sales.Orders,sales.OrderLines --target-db SalesInMemory --out convert.sql
moconverter convert --source "..." --tables sales.Orders,sales.OrderLines --target-db SalesInMemory
moconverter rules
```

or run it from the repository:

```
dotnet run --project src/MOConverter.Cli -- assess --source "..."
```

A tag `vX.Y.Z` whose GitHub release is published goes to nuget.org through the `release` workflow, which nuget.org trusts by Trusted Publishing: no API key is stored in the repository.

Options of `assess`, `script` and `convert`:

| Option | Meaning |
|---|---|
| `-s`, `--source` | The database, as a SqlClient connection string. Without it, the `MOCONVERTER_SOURCE` environment variable, which keeps a password off the command line. |
| `--trust-server-certificate` | Accept a self-signed certificate, such as a container's. |
| `-t`, `--tables` | Comma-separated tables: `schema.table`, or `table` in any schema. Names with commas, dots or spaces go in brackets: `[Sales].[Order Details]`. A name matches regardless of case, unless a table matches it exactly. |
| `--schemas` | Comma-separated schemas. With neither `--tables` nor `--schemas`, every table. |

`assess` only:

| Option | Meaning |
|---|---|
| `-f`, `--format` | `table` (the default), `json` or `markdown`. |

`script` and `convert`:

| Option | Meaning |
|---|---|
| `-d`, `--target-db` | The database to create the tables in. It gets the source's collation. It can't be the source, and for `convert` it mustn't exist yet. |
| `--accept-lossy` | Convert lossy tables too. Each change is marked `LOSSY` in the script. Without it, nothing is done while a selected table is lossy. |
| `--hash-pk` | Tables whose primary key becomes a hash index, with twice as many buckets as rows, rounded up to a power of two. Version 1's `MOIndexEp` extended property set to `HASH` asks for the same. Other primary keys get a range (nonclustered) index, unless they are too wide for one. |
| `--schema-only` | Tables made `DURABILITY = SCHEMA_ONLY`: their rows don't survive a restart. A durable table can't reference one. |

`script` only:

| Option | Meaning |
|---|---|
| `-o`, `--out` | The file to write. |

`convert` only:

| Option | Meaning |
|---|---|
| `--target` | The server to create the new database on, as a connection string. Without it, the source's server. |
| `--batch-size` | Rows copied in each batch, each in a transaction of its own. 10,000 by default. |

The exit code is:
- 0 when nothing selected is blocked;
- 2 when something is, or when a lossy table isn't accepted;
- 1 when a database can't be read, an option is wrong, or `convert` fails partway.

So `assess` can run in a pipeline.

## The script

`script` and `convert` do nothing while a selected table is blocked. A foreign key to a table that isn't selected blocks the table, so the selection must take both ends of each key. The script then:

1. creates the target database if it doesn't exist, turns off `AUTO_CLOSE` (Express turns it on, and a memory-optimized filegroup refuses it) and turns on `MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT`;
2. adds a memory-optimized filegroup and its container, in the server's default data folder, if the database lacks them;
3. creates the schemas and the tables. Each change the assessment describes is marked in a comment: `FIXED` when the data and query results stay the same, `LOSSY` when they don't;
4. adds the foreign keys once every table exists.

Part of the script for the test tables, with some columns left out here:

```sql
-- [cases].[LossyTypes]: 1 rows
CREATE TABLE [cases].[LossyTypes]
(
    [Id] int NOT NULL,
    -- LOSSY: xml → nvarchar(max): XML methods, XML indexes and schema validation are lost
    [Doc] nvarchar(max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
    -- LOSSY: datetimeoffset(3) → datetime2(3): it is stored in UTC, and the offset is lost
    [Moment] datetime2(3) NULL,
    CONSTRAINT [PK__LossyTyp__3214EC07A1B2C3D4] PRIMARY KEY NONCLUSTERED ([Id])
)
WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
```

The tables the script makes are empty. T-SQL can't copy the data: a statement that touches a memory-optimized table can't touch another database (Msg 41317). Copying is `convert`'s job.

Run the script with `sqlcmd -b -x -i convert.sql`. The script sets `QUOTED_IDENTIFIER` itself. `-x` stops sqlcmd from taking a `$(` in a name for one of its variables.

## Converting

`convert` makes the new database the same way, and then copies the data:

1. It runs the script up to the empty tables.
2. It copies each table with `SqlBulkCopy`, reading the mapped columns cast to their new types. Identity values and NULLs are kept, and constraints are checked.
3. It compares the row counts.
4. It adds the foreign keys last, so the order of the tables doesn't matter, and the keys check the copied data.

The rows go through the machine that runs the tool, so for a big database, run it near the servers. If a step fails, `convert` says which one, and leaves the new database as it is for a look. Drop the database before trying again.

For five of the test tables:

```
Converting 5 tables (2 ready, 1 fixed, 2 lossy) into [Sales InMemory].
  [cases].[AliasTypes]: 1 rows in 0.3 s
  [cases].[Child]: 2 rows in 0.0 s
  [cases].[LossyTypes]: 1 rows in 0.0 s
  [cases].[Parent]: 2 rows in 0.0 s
  [cases].[Ready]: 1,001 rows in 0.1 s
Done: 1,007 rows in 5 memory-optimized tables, and their foreign keys.
```

## What it says

Each table gets the worst outcome among its findings:

| Outcome | Meaning |
|---|---|
| ready | Converts as it is. |
| fixed | The tool changes something, and the data and query results stay the same. For example, an alias type becomes its base type, `text` becomes `varchar(max)`, a UTF-8 `varchar` becomes `nvarchar`, or a clustered index becomes nonclustered. A filter or INCLUDE is dropped from an index. An index or columnstore index memory-optimized tables can't hold is left out. A primary key too wide for a range index becomes a hash index. |
| lossy | Converts with a change in meaning, which `--accept-lossy` has to allow. For example, `datetimeoffset` loses its offset, `xml` and the spatial types lose their methods, or a CASCADE foreign key becomes NO ACTION. A check or default SQL Server refuses, a trigger, or a unique key too wide to keep is left out. A computed column reads a column whose type changes. |
| blocked | Can't be converted until something changes. For example, the table has no primary key (or a disabled one), uses `sql_variant`, or has a unique filtered index. It is a temporal, ledger or graph table. A foreign key references a unique key rather than the primary key, or links the table to one that won't be in the new database. A key or check was made `WITH NOCHECK` or `NOT FOR REPLICATION`, so rows may break it. What must stay in a row passes 8060 bytes. A definition is hidden from the login, or there is more data than memory. |

For example (a few of the test tables, cut to fit):

```
╭──────────────────────┬───────┬──────────┬─────────┬──────────────────────────────────────────────────────╮
│ Table                │  Rows │   Memory │ Outcome │ Why                                                  │
├──────────────────────┼───────┼──────────┼─────────┼──────────────────────────────────────────────────────┤
│ [cases].[AliasTypes] │     1 │  32.1 KB │ fixed   │ fixed: [Id] dbo.BigId → bigint: the alias type's     │
│                      │       │          │         │ base type is bigint                                  │
│ [cases].[Child]      │     2 │  32.1 KB │ blocked │ blocked: foreign key [FK_Child_Parent] links it to   │
│                      │       │          │         │ [cases].[Parent], which isn't selected               │
│                      │       │          │         │ lossy: foreign key [FK_Child_Parent] ON DELETE       │
│                      │       │          │         │ CASCADE ON UPDATE SET NULL becomes NO ACTION         │
│ [cases].[Ready]      │ 1,001 │ 270.2 KB │ ready   │                                                      │
╰──────────────────────┴───────┴──────────┴─────────┴──────────────────────────────────────────────────────╯
```

The memory column is a guide. For each row it counts the table's data, a 24-byte header and an 8-byte pointer per index, and then doubles that for row versions and growth, as Microsoft's sizing advice suggests.

The limits behind these rules were checked on SQL Server 2019, 2022 and 2025. The tests ask the server again on every run (`tests/MOConverter.Tests/ServerAgreesTests.cs`):
- each function the tool accepts or refuses in a check or default;
- each type it keeps or maps, and each CAST it refuses;
- the 2500-byte key limit, at its boundary;
- the columnstore limits;
- each feature it works around.

## Rules

Every rule the assessment applies, worst outcome first. `moconverter rules` prints the same list. The rules marked ready are notes about the server or the selection, not about a table.

<!-- rules: written by moconverter rules --format markdown, and a test checks it is current -->
| Rule | Outcome | What it means |
|---|---|---|
| `server-version` | blocked | SQL Server 2019 or later is needed. |
| `server-xtp` | blocked | The server or service tier doesn't support In-Memory OLTP. |
| `selection-unmatched` | blocked | A table or schema named in the selection doesn't exist. |
| `memory-total` | blocked | The selected tables need more memory than the server has. |
| `no-primary-key` | blocked | A memory-optimized table needs a primary key. |
| `memory` | blocked | The table alone needs more memory than the server has. |
| `temporal` | blocked | System-versioned (temporal) tables and their history tables are converted by hand. |
| `ledger` | blocked | Ledger tables can't be memory-optimized. |
| `graph` | blocked | Graph (node and edge) tables can't be memory-optimized. |
| `filetable` | blocked | FileTables can't be memory-optimized. |
| `replicated` | blocked | Tables published for replication are converted by hand. |
| `column-set` | blocked | Column sets (and the sparse columns behind them) aren't supported. |
| `type-unsupported` | blocked | A column's type has no memory-optimized equivalent: sql_variant, or a CLR type of your own. |
| `computed-unsupported` | blocked | A computed column uses something memory-optimized tables don't accept. |
| `unique-filtered-index` | blocked | A unique filtered index: without its filter, rows it allowed could clash. |
| `foreign-key-boundary` | blocked | A foreign key links the table to one that won't be in the new database: not selected, blocked, or memory-optimized already. |
| `foreign-key-target` | blocked | A foreign key references a unique key: between memory-optimized tables, it must reference the primary key. |
| `max-key` | blocked | A primary key is too wide even for a hash index: a column becomes max, as a UTF-8 varchar over 4000 does, or the key passes 8060 bytes. |
| `row-too-long` | blocked | What must stay in a row (fixed-size columns and index keys) passes 8060 bytes. |
| `untrusted` | blocked | A foreign key or check made WITH NOCHECK or NOT FOR REPLICATION may have rows that break it, and the memory-optimized copy checks every row. |
| `definition-hidden` | blocked | A definition isn't visible to this login: it needs VIEW DEFINITION. |
| `type-lossy` | lossy | A column's type is stored as another type, and loses its methods or part of its value. |
| `check-unsupported` | lossy | A check constraint uses something memory-optimized tables don't accept, and is left out. |
| `default-unsupported` | lossy | A default uses something memory-optimized tables don't accept, and is left out. |
| `identity-seed` | lossy | IDENTITY must be (1,1): existing values are kept, and new ones count up by 1. |
| `foreign-key-action` | lossy | CASCADE, SET NULL and SET DEFAULT aren't supported: the key becomes NO ACTION. |
| `trigger` | lossy | Triggers aren't carried over: rewrite them as natively compiled AFTER triggers. |
| `change-tracking` | lossy | Change tracking isn't supported on memory-optimized tables. |
| `change-data-capture` | lossy | Change data capture isn't supported on memory-optimized tables. |
| `full-text` | lossy | Full-text indexes aren't supported on memory-optimized tables. |
| `computed-over-lossy` | lossy | A computed column uses a column whose type changes, so its values can change. |
| `wide-unique-key` | lossy | A unique index wider than a range index's 2500 bytes is left out, and with it the uniqueness it enforced. |
| `alias-type` | fixed | Alias types aren't supported: the column gets the alias's base type. |
| `old-lob-type` | fixed | text, ntext and image become varchar(max), nvarchar(max) and varbinary(max). |
| `sparse` | fixed | SPARSE isn't supported: the column is stored in full. |
| `utf8-collation` | fixed | char and varchar can't have a UTF-8 collation: they become nchar and nvarchar, with the same collation. |
| `wide-key` | fixed | A range index's key can't pass 2500 bytes: a primary key becomes a hash index, another index is left out. |
| `columnstore-limits` | fixed | A clustered columnstore index is left out when a column is max, a column is computed, or a row is too long for one. |
| `computed-index-persisted` | fixed | An index on a computed column needs it PERSISTED. |
| `clustered-index` | fixed | Memory-optimized tables have no clustered rowstore index: it becomes nonclustered. |
| `included-columns` | fixed | Every index on a memory-optimized table covers all columns: INCLUDE is dropped. |
| `filtered-index` | fixed | Filtered indexes aren't supported: the index covers every row. |
| `nonclustered-columnstore` | fixed | Only a clustered columnstore index is supported: the nonclustered one is left out. |
| `xml-spatial-index` | fixed | XML and spatial indexes are left out, with the types they index. |
| `disabled` | fixed | Disabled indexes and constraints aren't carried over. |
| `partitioned` | fixed | Memory-optimized tables aren't partitioned: the table becomes one partition. |
| `compression` | fixed | Row and page compression aren't supported: the data is stored uncompressed. |
| `memory-note` | ready | How the memory check was done, or why it wasn't. |
| `edition-quota` | ready | Express, Web and Standard editions cap memory-optimized data per database. |
| `azure-sql-database` | ready | Azure SQL Database: only Premium and Business Critical tiers support In-Memory OLTP. |
<!-- /rules -->

## Building and testing

```
dotnet build MOConverter.slnx
dotnet test --solution MOConverter.slnx
```

The tests start SQL Server 2025 in a container with Testcontainers, so they need Docker or Podman. Set `MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2022-latest` to test on 2022, as CI does. With Podman on Windows, set `TESTCONTAINERS_RYUK_DISABLED=true`.

## License

MIT. See [LICENSE](LICENSE).
