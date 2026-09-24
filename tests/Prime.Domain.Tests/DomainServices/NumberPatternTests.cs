using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Patterns here are DEMO formats — not the LAM's (docs/FORMS-REVISION-PLAN.md §4.4).</summary>
public class NumberPatternTests
{
    private static readonly NumberContext Ctx = new(2026, "0400", "0402", "040201");

    [Theory]
    [InlineData("TD-{YEAR}-{SEQ:5}", "TD-2026-00017")]
    [InlineData("{PROV}-{MUN}-{BRGY}-{SEQ:3}", "0400-0402-040201-017")]
    [InlineData("B{SEQ}", "B17")]
    [InlineData("{SEQ:2}/{YEAR}", "17/2026")]
    public void Format_ReplacesTokensAndPadsSequence(string pattern, string expected) =>
        NumberPattern.Format(pattern, Ctx, 17).ShouldBe(expected);

    [Fact]
    public void Format_SequenceLongerThanPadding_IsNotTruncated() =>
        NumberPattern.Format("X{SEQ:2}", Ctx, 12345).ShouldBe("X12345");

    [Fact]
    public void ScopeKey_LeavesOutSequence_SoYearRestartsNumbering()
    {
        NumberPattern.ScopeKey("TD-{YEAR}-{SEQ:5}", Ctx).ShouldBe("TD-2026-#");
        NumberPattern.ScopeKey("TD-{YEAR}-{SEQ:5}", Ctx with { Year = 2027 }).ShouldBe("TD-2027-#");
        NumberPattern.ScopeKey("TD-{SEQ:5}", Ctx).ShouldBe(NumberPattern.ScopeKey("TD-{SEQ:5}", Ctx with { Year = 2027 }));
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("TD-{YEAR}", "exactly one {SEQ}")]
    [InlineData("{SEQ}-{SEQ}", "exactly one {SEQ}")]
    [InlineData("{FOO}-{SEQ}", "unknown token")]
    [InlineData("{YEAR:2}-{SEQ}", "unknown token")]
    [InlineData("{SEQ:0}", "between 1 and 12")]
    [InlineData("{SEQ:13}", "between 1 and 12")]
    [InlineData("A{b}{SEQ}", "braces")]
    public void Validate_RejectsBadPatterns(string pattern, string expected) =>
        NumberPattern.Validate(pattern).ShouldNotBeNull().ShouldContain(expected);

    [Fact]
    public void Validate_AcceptsGoodPattern() => NumberPattern.Validate("TD-{PROV}{MUN}-{YEAR}-{SEQ:6}").ShouldBeNull();

    [Fact]
    public void MissingValues_ListsTokensWithoutContext()
    {
        var context = new NumberContext(2026, ProvinceCode: "0400");
        NumberPattern.MissingValues("{PROV}-{MUN}-{BRGY}-{SEQ}", context).ShouldBe(["{MUN}", "{BRGY}"]);
        Should.Throw<ArgumentException>(() => NumberPattern.Format("{MUN}-{SEQ}", context, 1));
    }

    [Fact]
    public void Format_RejectsSequenceBelowOne() =>
        Should.Throw<ArgumentOutOfRangeException>(() => NumberPattern.Format("{SEQ}", Ctx, 0));
}
