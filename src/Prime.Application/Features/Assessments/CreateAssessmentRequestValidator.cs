using FluentValidation;

namespace Prime.Application.Features.Assessments;

public sealed class CreateAssessmentRequestValidator : AbstractValidator<CreateAssessmentRequest>
{
    public CreateAssessmentRequestValidator()
    {
        RuleFor(x => x.ValuationId).NotEmpty();
        RuleFor(x => x.AssessmentYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.Remarks).MaximumLength(2000);
    }
}
