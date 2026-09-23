using FluentValidation;

namespace Prime.Application.Features.AssessmentLevels;

public sealed class CreateAssessmentLevelRequestValidator : AbstractValidator<CreateAssessmentLevelRequest>
{
    public CreateAssessmentLevelRequestValidator()
    {
        RuleFor(x => x.OrdinanceNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ClassificationId).NotEmpty();
        RuleFor(x => x.ActualUseId).NotEmpty();
        RuleFor(x => x.PropertyTypeId).NotEmpty();
        RuleFor(x => x.LowerValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UpperValue).GreaterThanOrEqualTo(0).When(x => x.UpperValue is not null);
        RuleFor(x => x)
            .Must(x => x.UpperValue is null || x.LowerValue <= x.UpperValue)
            .WithMessage("LowerValue must not exceed UpperValue.");
        RuleFor(x => x.AssessmentPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.EffectiveDate).NotEmpty();
    }
}
