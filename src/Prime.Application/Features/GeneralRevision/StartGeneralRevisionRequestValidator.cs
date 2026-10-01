using FluentValidation;

namespace Prime.Application.Features.GeneralRevision;

public sealed class StartGeneralRevisionRequestValidator : AbstractValidator<StartGeneralRevisionRequest>
{
    public StartGeneralRevisionRequestValidator()
    {
        RuleFor(x => x.RpuIds).NotEmpty();
        RuleFor(x => x.RevisionYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate (the revision's effectivity) is required.");
    }
}
