using FluentValidation;

namespace Prime.Application.Features.TaxDeclarations;

public sealed class CreateTaxDeclarationRequestValidator : AbstractValidator<CreateTaxDeclarationRequest>
{
    public CreateTaxDeclarationRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.TaxDeclarationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EffectivityDate).NotEmpty();
        RuleFor(x => x.Taxability).IsInEnum();
        RuleFor(x => x.ClassificationId).NotEmpty();
        RuleFor(x => x.ActualUseId).NotEmpty();
        RuleFor(x => x.AssessmentYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.Remarks).MaximumLength(2000);
    }
}
