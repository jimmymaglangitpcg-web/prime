using FluentValidation;

namespace Prime.Application.Features.MachineryUnits;

public sealed class CreateMachineryRequestValidator : AbstractValidator<CreateMachineryRequest>
{
    public CreateMachineryRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.MachineryTypeId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).When(x => x.Capacity is not null);
        RuleFor(x => x.CapacityUnit).MaximumLength(20);
        RuleFor(x => x.AcquisitionCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InstallationCost).GreaterThanOrEqualTo(0).When(x => x.InstallationCost is not null);
        RuleFor(x => x.OtherCost).GreaterThanOrEqualTo(0).When(x => x.OtherCost is not null);
        RuleFor(x => x.EconomicLifeYears).GreaterThanOrEqualTo(0).When(x => x.EconomicLifeYears is not null);
        RuleFor(x => x.RemainingLifeYears).GreaterThanOrEqualTo(0).When(x => x.RemainingLifeYears is not null);
    }
}
