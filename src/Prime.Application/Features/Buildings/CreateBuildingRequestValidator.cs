using FluentValidation;

namespace Prime.Application.Features.Buildings;

public sealed class CreateBuildingRequestValidator : AbstractValidator<CreateBuildingRequest>
{
    public CreateBuildingRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.BuildingTypeId).NotEmpty();
        RuleFor(x => x.StructuralTypeId).NotEmpty();
        RuleFor(x => x.ActualUseId).NotEmpty();
        RuleFor(x => x.NumberOfStoreys).GreaterThanOrEqualTo(1).When(x => x.NumberOfStoreys is not null);
        RuleFor(x => x.FloorArea).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalFloorArea).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConditionId).NotEmpty();
        RuleFor(x => x.CompletionPercentage).InclusiveBetween(0, 100).When(x => x.CompletionPercentage is not null);
    }
}
