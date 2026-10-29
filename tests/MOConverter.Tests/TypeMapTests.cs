using MOConverter.Catalog;
using MOConverter.Rules;

namespace MOConverter.Tests;

public sealed class TypeMapTests
{
    private static ColumnInfo System(string type, int maxLength = 0, int precision = 0, int scale = 0) =>
        new("c", type, type, "sys", IsAliasType: false, IsClrType: false, maxLength, precision, scale, IsNullable: true);

    private static ColumnInfo Alias(string name, string baseType, int maxLength = 0, int precision = 0, int scale = 0) =>
        new("c", name, baseType, "dbo", IsAliasType: true, IsClrType: false, maxLength, precision, scale, IsNullable: true);

    [Theory]
    [InlineData("bigint", 8, 19, 0, "bigint")]
    [InlineData("smallint", 2, 5, 0, "smallint")]
    [InlineData("tinyint", 1, 3, 0, "tinyint")]
    [InlineData("varchar", 50, 0, 0, "varchar(50)")]
    [InlineData("varbinary", -1, 0, 0, "varbinary(max)")]
    [InlineData("nvarchar", 100, 0, 0, "nvarchar(50)")]
    [InlineData("datetime2", 7, 23, 3, "datetime2(3)")]
    [InlineData("smalldatetime", 4, 16, 0, "smalldatetime")]
    [InlineData("decimal", 9, 18, 4, "decimal(18,4)")]
    public void An_alias_gets_its_exact_base_type(string baseType, int maxLength, int precision, int scale, string expected)
    {
        // Version 1 turned these into int, varchar into char, datetime2 into datetime, varbinary into binary
        var mapping = TypeMap.Map(Alias("Custom", baseType, maxLength, precision, scale));

        Assert.Equal(RuleCatalogue.AliasType, mapping.Rule);
        Assert.Equal("dbo.Custom", mapping.Source);
        Assert.Equal(expected, mapping.Target);
    }

    [Fact]
    public void An_alias_of_a_lossy_type_is_lossy()
    {
        var mapping = TypeMap.Map(Alias("Moment", "datetimeoffset", 10, 34, 7));

        Assert.Equal(Outcome.Lossy, mapping.Outcome);
        Assert.Equal("dbo.Moment (datetimeoffset(7))", mapping.Source);
        Assert.Equal("datetime2(7)", mapping.Target);
    }

    [Theory]
    [InlineData("int", "int")]
    [InlineData("nchar", "nchar(5)")]
    [InlineData("sysname", "sysname")]
    [InlineData("uniqueidentifier", "uniqueidentifier")]
    public void Supported_types_stay(string type, string expected)
    {
        var mapping = TypeMap.Map(System(type, type == "nchar" ? 10 : 4));

        Assert.Null(mapping.Rule);
        Assert.Equal(expected, mapping.Target);
    }

    [Theory]
    [InlineData("text", Outcome.Fixed, "varchar(max)")]
    [InlineData("ntext", Outcome.Fixed, "nvarchar(max)")]
    [InlineData("image", Outcome.Fixed, "varbinary(max)")]
    [InlineData("xml", Outcome.Lossy, "nvarchar(max)")]
    [InlineData("timestamp", Outcome.Lossy, "binary(8)")]
    [InlineData("json", Outcome.Lossy, "nvarchar(max)")]
    [InlineData("sql_variant", Outcome.Blocked, "")]
    [InlineData("something_new", Outcome.Blocked, "")]
    public void Other_types_are_mapped(string type, Outcome outcome, string target)
    {
        var mapping = TypeMap.Map(System(type, -1));

        Assert.Equal(outcome, mapping.Outcome);
        Assert.Equal(target, mapping.Target);
    }

    [Fact]
    public void Spatial_types_become_varbinary_whole()
    {
        // Version 1 cast them to nvarchar(1000), cutting long values
        var column = new ColumnInfo("c", "geography", null, "sys", IsAliasType: false, IsClrType: true, -1, 0, 0, IsNullable: true);

        Assert.Equal("varbinary(max)", TypeMap.Map(column).Target);
    }

    [Fact]
    public void A_clr_type_of_your_own_is_blocked()
    {
        var column = new ColumnInfo("c", "Money", null, "app", IsAliasType: false, IsClrType: true, -1, 0, 0, IsNullable: true);

        Assert.Equal(RuleCatalogue.TypeUnsupported, TypeMap.Map(column).Rule);
    }

    [Fact]
    public void A_vector_says_its_dimensions()
    {
        var mapping = TypeMap.Map(System("vector", 8 + 4 * 1536));

        Assert.Equal("vector(1536)", mapping.Source);
        Assert.Contains("vector(1536)", mapping.Reason);
    }
}
