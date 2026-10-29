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
    /// Index keys of other columns, then a last column whose width is found: "varchar NULL",
    /// "int NOT NULL" and so on.
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
        "binary(8) NOT NULL; varchar NOT NULL",
        "nvarchar(50) NOT NULL; varchar NOT NULL",
        "bigint NOT NULL; varchar NOT NULL",
        "bigint NULL; varchar NOT NULL",
        "bigint NOT NULL; bigint NOT NULL; varchar NOT NULL",
        "datetime2 NOT NULL; varbinary NOT NULL",
        "decimal NOT NULL; varchar NOT NULL",
        "uniqueidentifier NOT NULL; varchar NOT NULL",
        "tinyint NOT NULL; tinyint NOT NULL; tinyint NOT NULL; varchar NOT NULL",
        "bit NOT NULL; varchar NOT NULL",
        "int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; int NULL; varchar NOT NULL",
    ];

    [Theory]
    [MemberData(nameof(KeyShapes))]
    public async Task The_key_size_never_counts_less_than_the_server_and_at_most_4_bytes_more(string shape)
    {
        var specs = shape.Split("; ");
        int Count(int width) => Limits.KeyBytes(KeyTable(specs, width, out var index), index);

        // The widest last column the tool says fits
        var widest = Enumerable.Range(1, 8000).Last(w => Count(w) <= Limits.MaxKeyBytes);
        Assert.Null(await TryAsync(KeySql(specs, widest)));
        Refused(await TryAsync(KeySql(specs, widest + 5)));
    }

    private static TableInfo KeyTable(string[] specs, int width, out IndexInfo index)
    {
        var columns = specs.Select((spec, i) =>
        {
            var parts = spec.Split(' ', 2);
            var (type, length) = KeyType(parts[0], i == specs.Length - 1 ? width : null);
            return new ColumnInfo($"c{i}", type.Split('(')[0], type.Split('(')[0], "sys", false, false, length,
                type.StartsWith("decimal") ? 18 : 0, type.StartsWith("decimal") ? 2 : type.StartsWith("datetime2") ? 7 : 0,
                IsNullable: parts[1] == "NULL");
        }).ToList();
        index = new IndexInfo("ix", "NONCLUSTERED", false, false, false, false, null, false, columns.Select(c => new IndexColumn(c.Name, false)).ToList(), []);
        return new TableInfo(new ObjectName("dbo", "t"), 0, 0, columns, [index], [], []);
    }

    private static string KeySql(string[] specs, int width) =>
        string.Join(", ", specs.Select((spec, i) =>
        {
            var parts = spec.Split(' ', 2);
            var type = parts[0] switch
            {
                "varchar" or "char" or "binary" or "varbinary" or "nvarchar" => $"{parts[0]}({width})",
                "decimal" => "decimal(18,2)",
                _ => parts[0],
            };
            return $"c{i} {type} {parts[1]}";
        }))
        + $", INDEX ix ({string.Join(", ", specs.Select((_, i) => $"c{i}"))})";

    private static (string Type, int Length) KeyType(string type, int? width)
    {
        var open = type.IndexOf('(');
        if (open > 0)
        {
            var length = int.Parse(type[(open + 1)..^1], System.Globalization.CultureInfo.InvariantCulture);
            return (type, type.StartsWith('n') ? 2 * length : length);
        }

        return type switch
        {
            "varchar" or "char" or "binary" or "varbinary" => (type, width ?? 1),
            "nvarchar" => (type, 2 * (width ?? 1)),
            "int" => (type, 4),
            "bigint" => (type, 8),
            "tinyint" or "bit" => (type, 1),
            "uniqueidentifier" => (type, 16),
            "datetime2" => (type, 8),
            "decimal" => (type, 9),
            _ => throw new ArgumentException(type),
        };
    }

    [Theory]
    // 8040 bytes of columns: 4 for the id, and the rest
    [InlineData("a varchar(8000) NULL, b varchar({0}) NULL", 36)]
    [InlineData("a varchar(4000) NULL, b varchar({0}) NULL", 4036)]
    [InlineData("a binary(100) NULL, b varchar({0}) NULL", 7936)]
    public async Task A_columnstore_row_fits_8040_bytes_of_columns(string shape, int widest)
    {
        string Table(int width) =>
            $"CREATE TABLE dbo.t_{Guid.NewGuid():N} (id int NOT NULL PRIMARY KEY NONCLUSTERED, {string.Format(shape, width)}, INDEX cci CLUSTERED COLUMNSTORE) WITH (MEMORY_OPTIMIZED = ON);";

        Assert.Null(await server.TryAsync(SqlServer.Probe, Table(widest)));
        Assert.NotNull(await server.TryAsync(SqlServer.Probe, Table(widest + 1)));

        var first = int.Parse(shape[(shape.IndexOf('(') + 1)..shape.IndexOf(')')], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Limits.MaxColumnstoreRowBytes, 4 + first + widest);
    }

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

    private static ColumnInfo Column(string name, string type, int length, bool nullable) =>
        new(name, type, type, "sys", false, false, length, 0, 0, nullable);

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
