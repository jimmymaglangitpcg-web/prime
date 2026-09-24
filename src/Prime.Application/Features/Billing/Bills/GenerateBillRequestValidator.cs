using FluentValidation;

namespace Prime.Application.Features.Billing.Bills;

public sealed class GenerateBillRequestValidator : AbstractValidator<GenerateBillRequest>
{
    public GenerateBillRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.TaxYear).InclusiveBetween(1900, 9999);
        RuleFor(x => x.AsOfDate).NotEqual(default(DateOnly)).WithMessage("asOfDate is required.");
    }
}
