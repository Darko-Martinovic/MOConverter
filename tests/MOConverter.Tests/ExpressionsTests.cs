using MOConverter.Rules;

namespace MOConverter.Tests;

public sealed class ExpressionsTests
{
    [Theory]
    [InlineData("([UpperLimit]>(0))")]
    [InlineData("([Likes]>=(0) AND [Charindex] IS NOT NULL)")]
    [InlineData("([Price]>=(0) AND round([Price],(0))<(1000000))")]
    [InlineData("(iif([a]>(0),(1),(0))=(1) AND coalesce([a],(0))>=nullif([b],(0)))")]
    [InlineData("(CONVERT([varchar](10),[a])<>'' AND CAST([a] AS bigint)>(0))")]
    [InlineData("([d]<=getdate() AND [d]<=CURRENT_TIMESTAMP)")]
    [InlineData("([a] IN ((1),(2)) AND [b] BETWEEN (1) AND (10) AND [c] IS NOT NULL)")]
    public void Supported_conditions_pass(string definition)
    {
        // Version 1 dropped any check whose text contained "upper", "like" or "charindex"
        Assert.Empty(Expressions.UnsupportedInCondition(definition));
    }

    [Theory]
    [InlineData("([Code] like 'A%')", "LIKE")]
    [InlineData("(upper([Name])=[Name])", "UPPER")]
    [InlineData("(charindex('x',[s])=(0) AND left([s],(1))<>'x')", "CHARINDEX", "LEFT")]
    [InlineData("(try_cast([s] AS int) IS NULL)", "TRY_CAST")]
    [InlineData("([a]>[dbo].[Limit]())", "LIMIT (a user-defined function or a method)")]
    [InlineData("([a]>@@SPID)", "@@SPID")]
    [InlineData("([d] AT TIME ZONE 'UTC'>getdate())", "AT TIME ZONE")]
    public void Unsupported_conditions_are_named(string definition, params string[] expected)
    {
        Assert.Equal(expected, Expressions.UnsupportedInCondition(definition));
    }

    [Theory]
    [InlineData("(NEXT VALUE FOR [dbo].[Numbers])", "NEXT VALUE FOR")]
    [InlineData("(host_name())", "HOST_NAME")]
    [InlineData("(session_user)", "SESSIONUSER")]
    public void Unsupported_values_are_named(string definition, string expected)
    {
        Assert.Equal([expected], Expressions.UnsupportedInValue(definition));
    }

    [Theory]
    [InlineData("(sysutcdatetime())")]
    [InlineData("(newsequentialid())")]
    [InlineData("((0))")]
    [InlineData("(N'x')")]
    [InlineData("([Price]*(2))")]
    public void Supported_values_pass(string definition)
    {
        Assert.Empty(Expressions.UnsupportedInValue(definition));
    }

    [Fact]
    public void An_expression_that_doesnt_parse_is_unsupported()
    {
        Assert.Equal(["an expression this tool can't parse"], Expressions.UnsupportedInCondition("(this is not sql"));
    }

    [Fact]
    public void Known_and_unknown_are_told_apart()
    {
        Assert.Equal(
            "LIKE, which memory-optimized tables don't accept; and SOUNDEX, which isn't known to work in memory-optimized tables",
            Expressions.Describe(["LIKE", "SOUNDEX"]));
    }
}
