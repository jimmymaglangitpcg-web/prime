using FluentValidation;

namespace Prime.Application.Features.TaxDeclarations;

public sealed class CreateTaxDeclarationRequestValidator : AbstractValidator<CreateTaxDeclarationRequest>
{
    public CreateTaxDeclarationRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        // Optional: generated when a numbering scheme is in force (docs/FORMS-REVISION-PLAN.md section 4.4).
        RuleFor(x => x.TaxDeclarationNumber).MaximumLength(50);
        RuleFor(x => x.EffectivityDate).NotEmpty();
        RuleFor(x => x.Taxability).IsInEnum();
        RuleFor(x => x.ClassificationId).NotEmpty();
        RuleFor(x => x.ActualUseId).NotEmpty();
        RuleFor(x => x.AssessmentYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.Remarks).MaximumLength(2000);
    }
}
