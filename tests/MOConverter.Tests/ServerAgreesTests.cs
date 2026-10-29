using MOConverter.Catalog;
using MOConverter.Rules;

namespace MOConverter.Tests;

/// <summary>
/// The rules say what memory-optimized tables accept and refuse. These tests ask the server,
/// so a rule that stops being true on a new version of SQL Server fails here.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ServerAgreesTests(SqlServer server)
{
    private const string Columns = "i int NULL, f float NULL, s nvarchar(100) NULL, d datetime2 NULL";

    /// <summary>An expression using each function: a condition for a check, or a value for a default.</summary>
    private static readonly Dictionary<string, (bool IsDefault, string Expression)> Samples = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ABS"] = (false, "ABS(i) >= 0"),
        ["CEILING"] = (false, "CEILING(f) > 0"),
        ["FLOOR"] = (false, "FLOOR(f) > 0"),
        ["ROUND"] = (false, "ROUND(f, 1) > 0"),
        ["POWER"] = (false, "POWER(i, 2) >= 0"),
        ["SQRT"] = (false, "SQRT(f) >= 0"),
        ["SIGN"] = (false, "SIGN(i) >= -1"),
        ["LEN"] = (false, "LEN(s) > 0"),
        ["LTRIM"] = (false, "LTRIM(s) <> N''"),
        ["RTRIM"] = (false, "RTRIM(s) <> N''"),
        ["TRIM"] = (false, "TRIM(s) <> N''"),
        ["SUBSTRING"] = (false, "SUBSTRING(s, 1, 1) <> N'x'"),
        ["CONCAT_WS"] = (false, "CONCAT_WS(N',', s, s) <> N''"),
        ["TRANSLATE"] = (false, "TRANSLATE(s, N'ab', N'cd') <> N''"),
        ["ISNULL"] = (false, "ISNULL(i, 0) >= 0"),
        ["DATEADD"] = (false, "DATEADD(day, 1, d) > d"),
        ["DATEDIFF"] = (false, "DATEDIFF(day, d, d) = 0"),
        ["DATEPART"] = (false, "DATEPART(year, d) > 2000"),
        ["YEAR"] = (false, "YEAR(d) > 2000"),
        ["MONTH"] = (false, "MONTH(d) > 0"),
        ["DAY"] = (false, "DAY(d) > 0"),
        ["EOMONTH"] = (false, "EOMONTH(d) >= d"),
        ["DATEFROMPARTS"] = (false, "DATEFROMPARTS(2020, 1, 1) <= d"),
        ["GETDATE"] = (false, "d <= GETDATE()"),
        ["GETUTCDATE"] = (false, "d <= GETUTCDATE()"),
        ["SYSDATETIME"] = (false, "d <= SYSDATETIME()"),
        ["SYSUTCDATETIME"] = (false, "d <= SYSUTCDATETIME()"),
        ["ISJSON"] = (false, "ISJSON(s) = 1"),
        ["JSON_VALUE"] = (false, "JSON_VALUE(s, N'$.a') IS NULL"),
        ["NEWID"] = (true, "NEWID()"),
        ["NEWSEQUENTIALID"] = (true, "NEWSEQUENTIALID()"),
        ["SUSER_SNAME"] = (true, "SUSER_SNAME()"),
        ["LIKE"] = (false, "s LIKE N'A%'"),
        ["NEXT VALUE FOR"] = (true, "NEXT VALUE FOR dbo.Numbers"),
        ["TRY_CAST"] = (false, "TRY_CAST(s AS int) IS NULL"),
        ["TRY_CONVERT"] = (false, "TRY_CONVERT(int, s) IS NULL"),
        ["DATALENGTH"] = (false, "DATALENGTH(s) > 0"),
        ["LEFT"] = (false, "LEFT(s, 1) <> N'x'"),
        ["RIGHT"] = (false, "RIGHT(s, 1) <> N'x'"),
        ["REPLACE"] = (false, "REPLACE(s, N'a', N'b') <> N''"),
        ["REPLICATE"] = (false, "REPLICATE(s, 2) <> N''"),
        ["REVERSE"] = (false, "REVERSE(s) <> N''"),
        ["CONCAT"] = (false, "CONCAT(s, s) <> N''"),
        ["LOWER"] = (false, "LOWER(s) = s"),
        ["UPPER"] = (false, "UPPER(s) = s"),
        ["CHARINDEX"] = (false, "CHARINDEX(N'x', s) = 0"),
        ["PATINDEX"] = (false, "PATINDEX(N'%x%', s) = 0"),
        ["STUFF"] = (false, "STUFF(s, 1, 1, N'x') <> N''"),
        ["FORMAT"] = (false, "FORMAT(i, 'N') <> N''"),
        ["ISNUMERIC"] = (false, "ISNUMERIC(s) >= 0"),
        ["HASHBYTES"] = (false, "HASHBYTES('SHA2_256', s) IS NOT NULL"),
        ["DB_NAME"] = (true, "DB_NAME()"),
        ["HOST_NAME"] = (true, "HOST_NAME()"),
        ["APP_NAME"] = (true, "APP_NAME()"),
    };

    public static TheoryData<string> SupportedFunctions => [.. Expressions.SupportedFunctions.Order()];

    public static TheoryData<string> UnsupportedFunctions => [.. Expressions.KnownUnsupported.Order()];

    public static TheoryData<string> SupportedTypes => [.. TypeMap.Supported.Order()];

    [Fact]
    public void Every_function_has_a_sample()
    {
        Assert.All(Expressions.SupportedFunctions.Concat(Expressions.KnownUnsupported), f => Assert.Contains(f, Samples.Keys));
    }

    [Theory]
    [MemberData(nameof(SupportedFunctions))]
    public async Task A_supported_function_works(string function)
    {
        Assert.Null(await TryAsync(Sampled(function)));
        Assert.Empty(Unsupported(function));
    }

    [Theory]
    [MemberData(nameof(UnsupportedFunctions))]
    public async Task An_unsupported_function_is_refused(string function)
    {
        Refused(await TryAsync(Sampled(function)));
        Assert.Equal([function], Unsupported(function));
    }

    [Theory]
    [MemberData(nameof(SupportedTypes))]
    public async Task A_supported_type_works(string type)
    {
        Assert.Null(await TryAsync($"c {Declared(type)} NULL"));
    }

    [Theory]
    [InlineData("text", "varchar(max)")]
    [InlineData("ntext", "nvarchar(max)")]
    [InlineData("image", "varbinary(max)")]
    [InlineData("xml", "nvarchar(max)")]
    [InlineData("geography", "varbinary(max)")]
    [InlineData("geometry", "varbinary(max)")]
    [InlineData("hierarchyid", "varbinary(892)")]
    [InlineData("datetimeoffset(3)", "datetime2(3)")]
    [InlineData("rowversion", "binary(8)")]
    [InlineData("sql_variant", null)]
    [InlineData("dbo.Amount", "decimal(10,2)")]
    public async Task A_mapped_type_is_refused_and_its_target_works(string type, string? target)
    {
        await server.ExecuteAsync(SqlServer.Probe, "IF TYPE_ID('dbo.Amount') IS NULL CREATE TYPE dbo.Amount FROM decimal(10, 2);");

        Refused(await TryAsync($"c {type} NULL"));
        if (target is not null)
            Assert.Null(await TryAsync($"c {target} NULL"));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("vector(3)")]
    public async Task The_2025_types_are_refused_and_their_target_works(string type)
    {
        Assert.SkipUnless(server.MajorVersion >= 17, "json and vector need SQL Server 2025");

        Refused(await TryAsync($"c {type} NULL"));
        Assert.Null(await TryAsync("c nvarchar(max) NULL"));
    }

    [Theory]
    [InlineData("identity-seed", "c int IDENTITY(100, 5) NOT NULL")]
    [InlineData("sparse", "c int SPARSE NULL")]
    [InlineData("clustered-index", "c int NULL, INDEX ix CLUSTERED (c)")]
    [InlineData("included-columns", "c int NULL, e int NULL, INDEX ix (c) INCLUDE (e)")]
    [InlineData("nonclustered-columnstore", "c int NULL, INDEX ix NONCLUSTERED COLUMNSTORE (c)")]
    [InlineData("computed-index-persisted", "c int NULL, e AS c * 2, INDEX ix (e)")]
    [InlineData("filtered-index", "c int NULL, INDEX ix (c) WHERE c IS NOT NULL")]
    public async Task What_a_rule_changes_is_refused(string rule, string definition)
    {
        Assert.Contains(RuleCatalogue.All, r => r.Id == rule);
        Refused(await TryAsync(definition));
    }

    [Theory]
    [InlineData("computed column", "c int NULL, e AS c * 2")]
    [InlineData("persisted computed column in an index", "c int NULL, e AS c * 2 PERSISTED, INDEX ix (e)")]
    [InlineData("more than 8 indexes", "a int, b int, c int, e int, g int, h int, j int, k int, l int, INDEX ia (a), INDEX ib (b), INDEX ic (c), INDEX ie (e), INDEX ig (g), INDEX ih (h), INDEX ij (j), INDEX ik (k), INDEX il (l)")]
    [InlineData("clustered columnstore", "c int NULL, INDEX cci CLUSTERED COLUMNSTORE")]
    [InlineData("descending index", "c int NULL, INDEX ix (c DESC)")]
    [InlineData("unique nullable column", "c int NULL, CONSTRAINT uq UNIQUE NONCLUSTERED (c)")]
    [InlineData("bigint identity", "c bigint IDENTITY(1, 1) NOT NULL")]
    public async Task What_version_1_worked_around_is_accepted(string what, string definition)
    {
        Assert.NotNull(what);
        Assert.Null(await TryAsync(definition));
    }

    [Theory]
    [InlineData("ON DELETE CASCADE")]
    [InlineData("ON DELETE SET NULL")]
    [InlineData("ON UPDATE CASCADE")]
    [InlineData("ON DELETE SET DEFAULT")]
    public async Task Cascading_foreign_keys_are_refused(string action)
    {
        var parent = await ParentAsync();

        Refused(await TryAsync($"p int NULL DEFAULT 1 REFERENCES {parent} (id) {action}"));
        Assert.Null(await TryAsync($"p int NULL REFERENCES {parent} (id)"));
    }

    [Theory]
    [InlineData("CHECK ({0})", "CAST(i AS xml) IS NOT NULL", "CAST to xml")]
    [InlineData("CHECK ({0})", "d > CAST('2000-01-01' AS datetimeoffset)", "CAST to datetimeoffset")]
    [InlineData("z int NULL DEFAULT {0}", "CONVERT(int, CONVERT(sql_variant, 1))", "CONVERT to sql_variant")]
    public async Task A_cast_to_a_refused_type_is_refused(string shape, string expression, string found)
    {
        Refused(await TryAsync($"{Columns}, {string.Format(shape, expression)}"));

        var unsupported = shape.Contains("CHECK") ? Expressions.UnsupportedInCondition(expression) : Expressions.UnsupportedInValue(expression);
        Assert.Equal([found], unsupported);
    }

    /// <summary>
    /// Index keys: columns separated by "; ", each a type and NULL or NOT NULL. A type without a
    /// length (varchar, char, binary, varbinary, nvarchar) is the one whose width is found.
    /// </summary>
    public static TheoryData<string> KeyShapes =>
    [
        "varchar NOT NULL",
        "varchar NULL",
        "char NOT NULL",
        "binary NULL",
        "int NOT NULL; varchar NOT NULL",
        "int NULL; varchar NOT NULL",
        "varchar(100) NOT NULL; varchar NOT NULL",
        "varchar(100) NULL; varchar NULL",
        "varchar(1200) NOT NULL; varchar NOT NULL",
        "char(100) NOT NULL; char NOT NULL",
        "varchar(100) NOT NULL; varchar(100) NOT NULL; varchar NOT NULL",
        "int NOT NULL; varchar(100) NOT NULL; varchar NOT NULL",
        "binary(8) NOT NULL; varchar NOT NULL",
        "nvarchar(50) NOT NULL; varchar NOT NULL",
        "nchar(10) NOT NULL; varchar NOT NULL",
        "sysname NOT NULL; varchar NOT NULL",
        "bigint NOT NULL; varchar NOT NULL",
        "bigint NULL; varchar NOT NULL",
        "bigint NOT NULL; bigint NOT NULL; varchar NOT NULL",
        "datetime2 NOT NULL; varbinary NOT NULL",
        "datetime2(2) NOT NULL; varchar NOT NULL",
        "time NOT NULL; varchar NOT NULL",
        "time(0) NOT NULL; varchar NOT NULL",
        "date NOT NULL; varchar NOT NULL",
        "datetime NOT NULL; varchar NOT NULL",
        "smalldatetime NOT NULL; varchar NOT NULL",
        "money NOT NULL; varchar NOT NULL",
        "float NOT NULL; varchar NOT NULL",
        "real NOT NULL; varchar NOT NULL",
        "decimal(18,2) NOT NULL; varchar NOT NULL",
        "numeric(9,2) NOT NULL; varchar NOT NULL",
        "numeric(19,4) NOT NULL; varchar NOT NULL",
        "numeric(38,0) NOT NULL; varchar NOT NULL",
        "uniqueidentifier NOT NULL; varchar NOT NULL",
        "uniqueidentifier NOT NULL; bigint NOT NULL; varchar NOT NULL",
        "smallint NOT NULL; int NOT NULL; varchar NOT NULL",
        "tinyint NOT NULL; int NOT NULL; varchar NOT NULL",
        "tinyint NOT NULL; tinyint NOT NULL; tinyint NOT NULL; varchar NOT NULL",
        "bit NOT NULL; varchar NOT NULL",
        "time NOT NULL; time NOT NULL; time NOT NULL; time NOT NULL; time NOT NULL; time NOT NULL; time NOT NULL; time NOT NULL; varchar NOT NULL",
        "numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; numeric(9,0) NOT NULL; varchar NOT NULL",
        "int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; varchar NOT NULL",
    ];

    [Theory]
    [MemberData(nameof(KeyShapes))]
    public async Task The_server_takes_every_range_key_the_tool_allows(string shape)
    {
        var columns = ShapeColumns(shape.Split("; "));
        int Count(int width) => Limits.KeyBytes(Keyed(columns, width, out var index), index);

        var widest = Enumerable.Range(1, 8000).Last(w => Count(w) <= Limits.MaxKeyBytes);
        Assert.Null(await TryAsync(ShapeSql(columns, widest, index: true)));
        // Not much stricter than the server either
        Refused(await TryAsync(ShapeSql(columns, widest + 48, index: true)));
    }

    [Fact]
    public async Task The_server_takes_every_hash_key_the_tool_allows()
    {
        var columns = ShapeColumns(["varchar(8000) NOT NULL", "varchar NOT NULL"]);
        int Count(int width) => Limits.KeyBytes(Keyed(columns, width, out var index), index);

        var widest = Enumerable.Range(1, 8000).Last(w => Count(w) <= Limits.MaxHashKeyBytes);
        var sql = ShapeSql(columns, widest, index: false);
        Assert.Null(await server.TryAsync(SqlServer.Probe,
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} ({sql}, PRIMARY KEY NONCLUSTERED HASH (c0, c1) WITH (BUCKET_COUNT = 64)) WITH (MEMORY_OPTIMIZED = ON);"));
        Assert.NotNull(await server.TryAsync(SqlServer.Probe,
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} ({ShapeSql(columns, widest + 48, index: false)}, PRIMARY KEY NONCLUSTERED HASH (c0, c1) WITH (BUCKET_COUNT = 64)) WITH (MEMORY_OPTIMIZED = ON);"));
    }

    /// <summary>A columnstore table's columns: a count of one type, then a filler whose width is found.</summary>
    [Theory]
    [InlineData(1, "varchar(8000) NULL")]
    [InlineData(1, "varchar(4000) NULL")]
    [InlineData(1, "binary(100) NULL")]
    [InlineData(100, "int NULL")]
    [InlineData(100, "int NOT NULL")]
    [InlineData(100, "time NULL")]
    [InlineData(100, "numeric(9,0) NULL")]
    [InlineData(100, "datetime2(0) NULL")]
    [InlineData(100, "varchar(10) NULL")]
    [InlineData(100, "char(10) NOT NULL")]
    [InlineData(100, "date NULL")]
    [InlineData(200, "bit NULL")]
    [InlineData(50, "uniqueidentifier NULL")]
    [InlineData(50, "numeric(38,2) NULL")]
    public async Task The_server_takes_every_columnstore_row_the_tool_allows(int count, string type)
    {
        var columns = ShapeColumns([.. Enumerable.Repeat(type, count), "varchar NULL"]);
        string Table(int width) =>
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} (id int NOT NULL PRIMARY KEY NONCLUSTERED, {ShapeSql(columns, width, index: false)}, INDEX cci CLUSTERED COLUMNSTORE) WITH (MEMORY_OPTIMIZED = ON);";
        int Row(int width) => Limits.RowBytes(new TableInfo(new ObjectName("dbo", "t"), 0, 0,
            [new ColumnInfo("id", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: false), .. Keyed(columns, width, out _).Columns], [], [], []));

        var widest = Enumerable.Range(1, 8000).Last(w => Row(w) <= Limits.MaxColumnstoreRowBytes);
        Assert.Null(await server.TryAsync(SqlServer.Probe, Table(widest)));
        Assert.NotNull(await server.TryAsync(SqlServer.Probe, Table(widest + 64)));
    }

    /// <summary>
    /// A table's columns besides an int primary key and a filler of bigint columns, whose count is
    /// found: what must stay in the row against 8060 bytes. "indexed" puts a range index on the
    /// columns.
    /// </summary>
    [Theory]
    [InlineData(0, "int NOT NULL", false)]
    [InlineData(100, "varchar(1) NULL", false)]
    [InlineData(100, "varchar(10) NULL", false)]
    [InlineData(100, "varchar(24) NULL", false)]
    [InlineData(100, "varchar(25) NULL", false)]
    [InlineData(100, "varchar(31) NULL", false)]
    [InlineData(100, "varchar(4) NULL", false)]
    [InlineData(100, "varchar(32) NULL", false)]
    [InlineData(100, "varchar(100) NULL", false)]
    [InlineData(100, "varchar(32) NOT NULL", false)]
    [InlineData(100, "nvarchar(12) NULL", false)]
    [InlineData(100, "nvarchar(16) NULL", false)]
    [InlineData(10, "varchar(1000) NULL", false)]
    [InlineData(1, "varchar(7000) NULL", false)]
    [InlineData(2, "nvarchar(max) NULL", false)]
    [InlineData(1, "varchar(2490) NOT NULL", true)]
    [InlineData(3, "varchar(2000) NOT NULL", true)]
    [InlineData(200, "time(0) NOT NULL", false)]
    [InlineData(300, "datetime2(0) NULL", false)]
    public async Task The_server_takes_every_row_the_tool_allows(int count, string type, bool indexed)
    {
        var columns = ShapeColumns([.. Enumerable.Repeat(type, count)]);
        TableInfo Table(int bigints)
        {
            var infos = new List<ColumnInfo> { new("id", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: false) };
            infos.AddRange(columns.Select((c, i) => new ColumnInfo($"c{i}", c.Type, c.Type, "sys", false, false, c.MaxLength, c.Precision, c.Scale, c.Nullable)));
            infos.AddRange(Enumerable.Range(0, bigints).Select(i => new ColumnInfo($"b{i}", "bigint", "bigint", "sys", false, false, 8, 19, 0, IsNullable: false)));
            List<IndexInfo> indexes = [new("pk", "NONCLUSTERED", true, true, false, false, null, false, [new IndexColumn("id", false)], [])];
            if (indexed)
                indexes.AddRange(columns.Select((_, i) => new IndexInfo($"ix{i}", "NONCLUSTERED", false, false, false, false, null, false, [new IndexColumn($"c{i}", false)], [])));
            return new TableInfo(new ObjectName("dbo", "t"), 0, 0, infos, indexes, [], []);
        }

        string Sql(int bigints) =>
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} (id int NOT NULL PRIMARY KEY NONCLUSTERED"
            + string.Concat(columns.Select((c, i) => $", c{i} {c.Sql} {(c.Nullable ? "NULL" : "NOT NULL")}"))
            + string.Concat(Enumerable.Range(0, bigints).Select(i => $", b{i} bigint NOT NULL"))
            + (indexed ? string.Concat(columns.Select((_, i) => $", INDEX ix{i} (c{i})")) : "")
            + ") WITH (MEMORY_OPTIMIZED = ON);";

        var most = Enumerable.Range(0, 1100).Last(b => Limits.InRowBytes(Table(b)) <= Limits.MaxInRowBytes);
        Assert.Null(await server.TryAsync(SqlServer.Probe, Sql(most)));
        // 128 bytes more: not much stricter than the server either. Tiny varchar columns are where
        // it is strictest, a byte or so each, since counting less would risk counting too little
        Assert.NotNull(await server.TryAsync(SqlServer.Probe, Sql(most + 16)));
    }

    [Theory]
    // Four range keys that each fit, but not all in one row; and a key that fits a hash index alone,
    // but not with one more column
    [InlineData("a varchar(2490) NOT NULL, b varchar(2490) NOT NULL, c varchar(2490) NOT NULL, d varchar(2490) NOT NULL, PRIMARY KEY NONCLUSTERED (a), INDEX ib (b), INDEX ic (c), INDEX id2 (d)")]
    [InlineData("a varchar(4000) NOT NULL, b varchar(4050) NOT NULL, x int NOT NULL, PRIMARY KEY NONCLUSTERED HASH (a, b) WITH (BUCKET_COUNT = 64)")]
    public async Task A_row_too_long_is_refused(string columns)
    {
        Assert.NotNull(await server.TryAsync(SqlServer.Probe, $"CREATE TABLE dbo.t_{Guid.NewGuid():N} ({columns}) WITH (MEMORY_OPTIMIZED = ON);"));
    }

    private sealed record ShapeColumn(string Sql, string Type, int MaxLength, int Precision, int Scale, bool Nullable, bool Grows);

    /// <summary>Columns from their declarations, with what sys.columns would say of each.</summary>
    private static List<ShapeColumn> ShapeColumns(string[] specs) => specs.Select(spec =>
    {
        var parts = spec.Split(' ', 2);
        var nullable = parts[1] == "NULL";
        var declared = parts[0];
        var open = declared.IndexOf('(');
        var name = open > 0 ? declared[..open] : declared;
        var arguments = open > 0 ? declared[(open + 1)..^1].Split(',').Select(a => a == "max" ? -1 : int.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray() : [];
        return name switch
        {
            "varchar" or "char" or "binary" or "varbinary" when arguments.Length == 0 => new ShapeColumn(declared, name, 0, 0, 0, nullable, Grows: true),
            "varchar" or "char" or "binary" or "varbinary" => new ShapeColumn(declared, name, arguments[0], 0, 0, nullable, false),
            "nvarchar" or "nchar" => new ShapeColumn(declared, name, arguments[0] < 0 ? -1 : 2 * arguments[0], 0, 0, nullable, false),
            "sysname" => new ShapeColumn(declared, "sysname", 256, 0, 0, nullable, false),
            "decimal" or "numeric" => new ShapeColumn(declared, name, arguments[0] switch { <= 9 => 5, <= 19 => 9, <= 28 => 13, _ => 17 }, arguments[0], arguments.ElementAtOrDefault(1), nullable, false),
            "datetime2" => new ShapeColumn(declared, name, 8, 27, arguments.Length > 0 ? arguments[0] : 7, nullable, false),
            "time" => new ShapeColumn(declared, name, 5, 16, arguments.Length > 0 ? arguments[0] : 7, nullable, false),
            "int" or "real" or "smallmoney" or "smalldatetime" => new ShapeColumn(declared, name, 4, 0, 0, nullable, false),
            "bigint" or "money" or "datetime" or "float" => new ShapeColumn(declared, name, 8, 0, 0, nullable, false),
            "smallint" => new ShapeColumn(declared, name, 2, 0, 0, nullable, false),
            "tinyint" or "bit" => new ShapeColumn(declared, name, 1, 0, 0, nullable, false),
            "date" => new ShapeColumn(declared, name, 3, 0, 0, nullable, false),
            "uniqueidentifier" => new ShapeColumn(declared, name, 16, 0, 0, nullable, false),
            _ => throw new ArgumentException(spec),
        };
    }).ToList();

    /// <summary>The columns as the catalog would describe them, the growing one at this width, and an index on all of them.</summary>
    private static TableInfo Keyed(List<ShapeColumn> columns, int width, out IndexInfo index)
    {
        var infos = columns.Select((c, i) => new ColumnInfo($"c{i}", c.Type, c.Type, "sys", false, false,
            c.Grows ? width : c.MaxLength, c.Precision, c.Scale, c.Nullable)).ToList();
        index = new IndexInfo("ix", "NONCLUSTERED", false, false, false, false, null, false, infos.Select(c => new IndexColumn(c.Name, false)).ToList(), []);
        return new TableInfo(new ObjectName("dbo", "t"), 0, 0, infos, [index], [], []);
    }

    private static string ShapeSql(List<ShapeColumn> columns, int width, bool index) =>
        string.Join(", ", columns.Select((c, i) => $"c{i} {(c.Grows ? $"{c.Sql}({width})" : c.Sql)} {(c.Nullable ? "NULL" : "NOT NULL")}"))
        + (index ? $", INDEX ix ({string.Join(", ", columns.Select((_, i) => $"c{i}"))})" : "");

    [Fact]
    public async Task A_hash_primary_key_can_be_wider()
    {
        Assert.Null(await server.TryAsync(SqlServer.Probe,
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} (id varchar(8000) NOT NULL PRIMARY KEY NONCLUSTERED HASH WITH (BUCKET_COUNT = 64)) WITH (MEMORY_OPTIMIZED = ON);"));
    }

    [Fact]
    public async Task Utf8_is_refused_for_varchar_and_accepted_for_nvarchar()
    {
        Refused(await TryAsync("a varchar(20) COLLATE Latin1_General_100_CI_AS_SC_UTF8 NULL"));
        Assert.Null(await TryAsync("a nvarchar(20) COLLATE Latin1_General_100_CI_AS_SC_UTF8 NULL, INDEX ix (a)"));
    }

    [Theory]
    [InlineData("a nvarchar(max) NULL, INDEX cci CLUSTERED COLUMNSTORE", "SCHEMA_AND_DATA")]
    [InlineData("a int NULL, b AS a * 2, INDEX cci CLUSTERED COLUMNSTORE", "SCHEMA_AND_DATA")]
    [InlineData("a varchar(8000) NULL, b varchar(8000) NULL, INDEX cci CLUSTERED COLUMNSTORE", "SCHEMA_AND_DATA")]
    [InlineData("a int NULL, INDEX cci CLUSTERED COLUMNSTORE", "SCHEMA_ONLY")]
    public async Task A_columnstore_index_has_limits(string columns, string durability)
    {
        var error = await server.TryAsync(SqlServer.Probe,
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} (id int NOT NULL PRIMARY KEY NONCLUSTERED, {columns}) WITH (MEMORY_OPTIMIZED = ON, DURABILITY = {durability});");

        Assert.NotNull(error);
    }

    [Fact]
    public async Task A_durable_table_cant_reference_a_schema_only_one()
    {
        var parent = $"dbo.parent_{Guid.NewGuid():N}";
        await server.ExecuteAsync(SqlServer.Probe, $"CREATE TABLE {parent} (id int NOT NULL PRIMARY KEY NONCLUSTERED) WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);");

        Refused(await TryAsync($"p int NULL REFERENCES {parent} (id)"));
    }

    [Fact]
    public async Task A_foreign_key_to_a_unique_key_is_refused()
    {
        var name = $"dbo.parent_{Guid.NewGuid():N}";
        await server.ExecuteAsync(SqlServer.Probe,
            $"CREATE TABLE {name} (id int NOT NULL PRIMARY KEY NONCLUSTERED, code int NOT NULL, CONSTRAINT uq_{Guid.NewGuid():N} UNIQUE NONCLUSTERED (code)) WITH (MEMORY_OPTIMIZED = ON);");

        Refused(await TryAsync($"p int NULL REFERENCES {name} (code)"));
    }

    [Fact]
    public async Task A_foreign_key_to_a_disk_table_is_refused()
    {
        var name = $"dbo.disk_{Guid.NewGuid():N}";
        await server.ExecuteAsync(SqlServer.Probe, $"CREATE TABLE {name} (id int PRIMARY KEY);");

        Refused(await TryAsync($"p int NULL REFERENCES {name} (id)"));
    }

    /// <summary>The statement failed, and for being memory-optimized rather than for a mistake in the test.</summary>
    private static void Refused(Microsoft.Data.SqlClient.SqlException? error)
    {
        Assert.NotNull(error);
        var messages = error.Errors.Cast<Microsoft.Data.SqlClient.SqlError>().Select(e => e.Message).ToList();
        Assert.True(messages.Any(m => m.Replace('-', ' ').Contains("memory optimized", StringComparison.OrdinalIgnoreCase)),
            $"Refused for another reason: {string.Join(" | ", messages)}");
    }

    private static IReadOnlyList<string> Unsupported(string function)
    {
        var (isDefault, expression) = Samples[function];
        return isDefault ? Expressions.UnsupportedInValue(expression) : Expressions.UnsupportedInCondition(expression);
    }

    private static string Sampled(string function)
    {
        var (isDefault, expression) = Samples[function];
        var type = expression.Contains("ID()") ? "uniqueidentifier" : expression.StartsWith("NEXT") ? "bigint" : "nvarchar(128)";
        return isDefault ? $"{Columns}, z {type} NULL DEFAULT {expression}" : $"{Columns}, CHECK ({expression})";
    }

    private static string Declared(string type) => type switch
    {
        "decimal" or "numeric" => $"{type}(10, 2)",
        "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary" => $"{type}(10)",
        _ => type,
    };

    private async Task<string> ParentAsync()
    {
        var name = $"dbo.parent_{Guid.NewGuid():N}";
        await server.ExecuteAsync(SqlServer.Probe, $"CREATE TABLE {name} (id int NOT NULL PRIMARY KEY NONCLUSTERED) WITH (MEMORY_OPTIMIZED = ON);");
        return name;
    }

    /// <summary>Tries a memory-optimized table with these columns, after an id primary key.</summary>
    private async Task<Microsoft.Data.SqlClient.SqlException?> TryAsync(string columns)
    {
        await server.ExecuteAsync(SqlServer.Probe, "IF OBJECT_ID('dbo.Numbers') IS NULL CREATE SEQUENCE dbo.Numbers;");
        var name = $"dbo.t_{Guid.NewGuid():N}";
        return await server.TryAsync(SqlServer.Probe,
            $"CREATE TABLE {name} (id int NOT NULL PRIMARY KEY NONCLUSTERED, {columns}) WITH (MEMORY_OPTIMIZED = ON);");
    }
}
