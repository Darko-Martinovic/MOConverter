# MOConverter

A .NET tool that tells you which tables of a SQL Server database can become memory-optimized (In-Memory OLTP), and what stops the others. It then copies the ones you choose into a new database as memory-optimized tables. The source database is only read.

```
dotnet tool install --global MOConverter
moconverter assess --source "Server=.;Database=Sales;Integrated Security=true;TrustServerCertificate=true"
```

## Commands

- `assess` reports, table by table, what converting would do:
  - **ready**: converts as it is;
  - **fixed**: something changes, but the data and query results stay the same;
  - **lossy**: converts with a change in meaning, which needs `--accept-lossy`;
  - **blocked**: can't be converted until something changes.
- `script` writes the T-SQL that creates memory-optimized copies of the tables in another database, with each change marked in a comment.
- `convert` makes that database, copies the data, checks the row counts, and adds the foreign keys.
- `rules` lists every rule the assessment applies.

It needs SQL Server 2019 or later, and the .NET 10 runtime. The limits behind the rules are tested against SQL Server 2019, 2022 and 2025.

The README on GitHub has every option, examples, and what changed from version 1, which backs the Simple Talk article *Converting a Database to In-Memory OLTP*: https://github.com/Darko-Martinovic/MOConverter
