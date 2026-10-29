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
