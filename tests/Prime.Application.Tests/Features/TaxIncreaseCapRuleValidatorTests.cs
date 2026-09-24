using Prime.Application.Features.Billing.Rules;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Application.Tests.Features;

/// <summary>
/// docs/BILLING.md §3.7 — request-level rules for tax increase caps. The
/// percent values here are DEMO inputs; the validator never fixes a rate.
/// </summary>
public class TaxIncreaseCapRuleValidatorTests
{
    private static readonly CreateTaxIncreaseCapRuleRequestValidator Validator = new();

    private static CreateTaxIncreaseCapRuleRequest Request(
        TaxIncreaseCapBasis basis = TaxIncreaseCapBasis.StatutoryFirstYear,
        TaxIncreaseCapBaseline baseline = TaxIncreaseCapBaseline.TaxBeforeSmv,
        DateOnly? effective = null,
        DateOnly? end = null,
        decimal maxIncreasePercent = 6m) =>
        new("DEMO legal basis", "DEMO-ORD", null, effective ?? new DateOnly(2027, 1, 1), null,
            end ?? new DateOnly(2027, 12, 31), Guid.NewGuid(), null, basis, baseline, maxIncreasePercent);

    [Fact]
    public void StatutoryFirstYear_OneYearWindow_IsValid() =>
        Validator.Validate(Request()).IsValid.ShouldBeTrue();

    [Fact]
    public void StatutoryFirstYear_LongerThanOneYear_IsRejected()
    {
        var result = Validator.Validate(Request(end: new DateOnly(2028, 1, 1)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("at most one year"));
    }

    [Fact]
    public void StatutoryFirstYear_PreviousYearBaseline_IsRejected()
    {
        var result = Validator.Validate(Request(baseline: TaxIncreaseCapBaseline.PreviousTaxYear));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("TaxBeforeSmv"));
    }

    [Fact]
    public void LocalOrdinance_MultiYearWindowAndPreviousYearBaseline_IsValid() =>
        Validator.Validate(Request(TaxIncreaseCapBasis.LocalOrdinance, TaxIncreaseCapBaseline.PreviousTaxYear,
            new DateOnly(2028, 1, 1), new DateOnly(2030, 12, 31), 10m)).IsValid.ShouldBeTrue();

    [Fact]
    public void EndBeforeEffective_IsRejected() =>
        Validator.Validate(Request(TaxIncreaseCapBasis.LocalOrdinance, end: new DateOnly(2026, 12, 31))).IsValid.ShouldBeFalse();

    [Fact]
    public void SingleDayWindow_IsValid() =>
        Validator.Validate(Request(end: new DateOnly(2027, 1, 1))).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1000)]
    public void OutOfRangePercent_IsRejected(double percent) =>
        Validator.Validate(Request(maxIncreasePercent: (decimal)percent)).IsValid.ShouldBeFalse();

    [Fact]
    public void ZeroPercent_MeaningNoIncrease_IsValid() =>
        Validator.Validate(Request(maxIncreasePercent: 0m)).IsValid.ShouldBeTrue();

    [Fact]
    public void MissingLegalBasis_IsRejected() =>
        Validator.Validate(Request() with { LegalBasis = "" }).IsValid.ShouldBeFalse();

    [Fact]
    public void MissingSmv_IsRejected() =>
        Validator.Validate(Request() with { SmvId = Guid.Empty }).IsValid.ShouldBeFalse();
}
