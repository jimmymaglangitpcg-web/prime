using FluentValidation;

namespace Prime.Application.Features.RealPropertyUnits;

public sealed class CreateRpuRequestValidator : AbstractValidator<CreateRpuRequest>
{
    public CreateRpuRequestValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.RpuNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RpuType).IsInEnum();
        RuleFor(x => x.EffectivityDate).NotEmpty();
    }
}
