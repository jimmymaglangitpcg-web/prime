using FluentValidation;

namespace Prime.Application.Features.Smv;

public sealed class CreateSmvRequestValidator : AbstractValidator<CreateSmvRequest>
{
    public CreateSmvRequestValidator()
    {
        RuleFor(x => x.OrdinanceNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OrdinanceDate).NotEmpty();
        RuleFor(x => x.EffectivityDate).NotEmpty();
        RuleFor(x => x.RevisionYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
