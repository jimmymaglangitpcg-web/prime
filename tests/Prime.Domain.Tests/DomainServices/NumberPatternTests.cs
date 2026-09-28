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

    // --- The MRPAAO PIN (docs/analysis/property-identification.md §3.3; DEMO index numbers) ---

    private const string MrpaaoPin = "{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}";

    [Fact]
    public void Pin_IsBuiltFromTheIndexNumbers_WithTheParcelAsSequence()
    {
        var province = new NumberContext(2026, LguIndex: "020", MunicipalityIndex: "15", BarangayIndex: "0005", SectionIndex: "002");
        var cityDistrict = new NumberContext(2026, LguIndex: "132", MunicipalityIndex: "06", BarangayIndex: "0012", SectionIndex: "001");

        NumberPattern.Format(MrpaaoPin, province, 5).ShouldBe("020-15-0005-002-05");
        NumberPattern.Format(MrpaaoPin, cityDistrict, 35).ShouldBe("132-06-0012-001-35");
    }

    [Fact]
    public void Pin_SequenceRunsPerSection()
    {
        var section2 = new NumberContext(2026, LguIndex: "020", MunicipalityIndex: "15", BarangayIndex: "0005", SectionIndex: "002");
        var section3 = section2 with { SectionIndex = "003" };

        NumberPattern.ScopeKey(MrpaaoPin, section2).ShouldBe("020-15-0005-002-#");
        NumberPattern.ScopeKey(MrpaaoPin, section3).ShouldNotBe(NumberPattern.ScopeKey(MrpaaoPin, section2));
        NumberPattern.Tokens(MrpaaoPin).ShouldBe(["LGUIDX", "MUNIDX", "BRGYIDX", "SECT"]);
        NumberPattern.MissingValues(MrpaaoPin, section2 with { SectionIndex = null }).ShouldBe(["{SECT}"]);
    }

    [Fact]
    public void Fits_ChecksTheSequenceWidth()
    {
        NumberPattern.Fits(MrpaaoPin, 99).ShouldBeTrue();
        NumberPattern.Fits(MrpaaoPin, 100).ShouldBeFalse();
        NumberPattern.Fits("{SEQ}", 123_456).ShouldBeTrue();
    }

    [Fact]
    public void Arpn_RevisionScopesTheSequence_ButIsNotPrinted()
    {
        // MRPAAO Ch. II §2 E.14: MM-BBBB-NNNNN, restarting at 00001 with each general revision.
        const string arpn = "{MUNIDX}-{BRGYIDX}-{REV}{SEQ:5}";
        var gr2026 = new NumberContext(2026, MunicipalityIndex: "01", BarangayIndex: "0001", RevisionYear: 2026);
        var gr2029 = gr2026 with { RevisionYear = 2029 };

        NumberPattern.Validate(arpn).ShouldBeNull();
        NumberPattern.Format(arpn, gr2026, 1).ShouldBe("01-0001-00001");
        NumberPattern.ScopeKey(arpn, gr2029).ShouldNotBe(NumberPattern.ScopeKey(arpn, gr2026));
        NumberPattern.MissingValues(arpn, gr2026 with { RevisionYear = null }).ShouldBe(["{REV}"]);
    }
}
