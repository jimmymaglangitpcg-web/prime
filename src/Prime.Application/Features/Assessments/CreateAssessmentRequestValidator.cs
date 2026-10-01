using FluentValidation;

namespace Prime.Application.Features.Assessments;

public sealed class CreateAssessmentRequestValidator : AbstractValidator<CreateAssessmentRequest>
{
    public CreateAssessmentRequestValidator()
    {
        RuleFor(x => x.ValuationId).NotEmpty();
        RuleFor(x => x.AssessmentYear).InclusiveBetween(1900, 2200);
        // Whether the effective date is needed depends on the transaction type's rule (AssessmentService).
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).When(x => x.EffectiveDate is not null);
        RuleFor(x => x.CauseDate).NotEqual(default(DateOnly)).When(x => x.CauseDate is not null);
        RuleFor(x => x.Remarks).MaximumLength(2000);
        RuleFor(x => x.EffectivityOverrideReason).MaximumLength(1000);
    }
}
