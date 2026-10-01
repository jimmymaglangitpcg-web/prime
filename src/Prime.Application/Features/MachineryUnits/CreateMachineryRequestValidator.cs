using FluentValidation;

namespace Prime.Application.Features.MachineryUnits;

public sealed class CreateMachineryRequestValidator : AbstractValidator<CreateMachineryRequest>
{
    public CreateMachineryRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.MachineryTypeId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).When(x => x.Capacity is not null);
        RuleFor(x => x.CapacityUnit).MaximumLength(20);
        RuleFor(x => x.AcquisitionCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InstallationCost).GreaterThanOrEqualTo(0).When(x => x.InstallationCost is not null);
        RuleFor(x => x.OtherCost).GreaterThanOrEqualTo(0).When(x => x.OtherCost is not null);
        RuleFor(x => x.ReplacementCost).GreaterThanOrEqualTo(0).When(x => x.ReplacementCost is not null);
        RuleFor(x => x.ReplacementCost).Null().When(x => x.IsBrandNew)
            .WithMessage("replacementCost applies only to machinery that is not brand-new; brand-new machinery is valued at acquisition cost (LGC §224(a)).");
        RuleFor(x => x.EconomicLifeYears).GreaterThanOrEqualTo(0).When(x => x.EconomicLifeYears is not null);
        RuleFor(x => x.RemainingLifeYears).GreaterThanOrEqualTo(0).When(x => x.RemainingLifeYears is not null);
        RuleFor(x => x.CostItems)
            .Must((x, _) => Problem(x) is null)
            .WithMessage(x => Problem(x));
    }

    private static string? Problem(CreateMachineryRequest x) =>
        MachineryInputs.Problem(x.IsImported, x.AcquisitionCurrency, x.ForeignAcquisitionCost, x.OriginCountry, x.PriceIndexSeries, x.CostItems ?? []);
}

/// <summary>The derived-method inputs' shape (valuation-foundation.md §4.6), shared by create and update.</summary>
public static class MachineryInputs
{
    public static string? Problem(bool imported, string? currency, decimal? foreignCost, string? origin, string? series,
        IReadOnlyList<MachineryCostItemRequest> items)
    {
        if (imported && (currency is null || currency.Trim().Length != 3 || !currency.Trim().All(char.IsAsciiLetterUpper)))
        {
            return "Imported machinery names the currency it was bought in (3-letter ISO 4217 code, e.g. USD).";
        }
        if (!imported && (currency is not null || foreignCost is not null))
        {
            return "acquisitionCurrency and foreignAcquisitionCost apply to imported machinery only.";
        }
        if (foreignCost < 0 || origin?.Length > 100 || series?.Trim().Length is 0 or > 30)
        {
            return "foreignAcquisitionCost cannot be negative; originCountry max 100; priceIndexSeries 1 to 30 characters.";
        }
        if (items.Any(i => !Enum.IsDefined(i.Kind) || i.Amount < 0 || i.Description?.Length > 200))
        {
            return "Each cost item has a known kind, an amount of 0 or more and a description of at most 200.";
        }
        return null;
    }
}
