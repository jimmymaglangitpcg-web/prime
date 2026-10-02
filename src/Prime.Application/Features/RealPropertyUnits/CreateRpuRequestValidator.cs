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
        RuleFor(x => x.IsLeasingProperty).Equal(false).When(x => x.RpuType != Domain.Enums.RpuType.Building)
            .WithMessage("Only a building is a leasing property (condominium).");
        RuleFor(x => x.FloorNumber).InclusiveBetween(1, 99).When(x => x.FloorNumber is not null);
        RuleFor(x => x.UnitNumber).InclusiveBetween(1, 999).When(x => x.UnitNumber is not null);
        RuleFor(x => x.FloorNumber).NotNull().When(x => x.UnitNumber is not null || x.FloorPrefix is not null)
            .WithMessage("A unit of a leasing property gives its floor number.");
    }
}
