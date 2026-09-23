using FluentValidation;

namespace Prime.Application.Features.Lands;

public sealed class CreateLandRequestValidator : AbstractValidator<CreateLandRequest>
{
    public CreateLandRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.Area).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AreaUnit).MaximumLength(10);
        RuleFor(x => x.ClassificationId).NotEmpty();
        RuleFor(x => x.ActualUseId).NotEmpty();
        RuleFor(x => x.LocationFactor).GreaterThanOrEqualTo(0).When(x => x.LocationFactor is not null);
        RuleFor(x => x.RoadFrontage).GreaterThanOrEqualTo(0).When(x => x.RoadFrontage is not null);
        RuleFor(x => x.Zoning).MaximumLength(50);
    }
}
