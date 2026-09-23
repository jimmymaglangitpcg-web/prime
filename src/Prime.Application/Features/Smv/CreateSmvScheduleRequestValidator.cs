using FluentValidation;

namespace Prime.Application.Features.Smv;

public sealed class CreateSmvScheduleRequestValidator : AbstractValidator<CreateSmvScheduleRequest>
{
    public CreateSmvScheduleRequestValidator()
    {
        RuleFor(x => x.ClassificationId).NotEmpty();
        RuleFor(x => x.ActualUseId).NotEmpty();
        RuleFor(x => x.PropertyTypeId).NotEmpty();
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.MarketValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumValue).GreaterThanOrEqualTo(0).When(x => x.MinimumValue is not null);
        RuleFor(x => x.MaximumValue).GreaterThanOrEqualTo(0).When(x => x.MaximumValue is not null);
        RuleFor(x => x)
            .Must(x => x.MinimumValue is null || x.MaximumValue is null || x.MinimumValue <= x.MaximumValue)
            .WithMessage("MinimumValue must not exceed MaximumValue.");
        RuleFor(x => x.EffectiveDate).NotEmpty();
    }
}
