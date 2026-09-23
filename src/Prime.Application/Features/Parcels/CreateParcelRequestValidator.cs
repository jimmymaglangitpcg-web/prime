using FluentValidation;

namespace Prime.Application.Features.Parcels;

public sealed class CreateParcelRequestValidator : AbstractValidator<CreateParcelRequest>
{
    public CreateParcelRequestValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.BarangayId).NotEmpty();
        RuleFor(x => x.Area).GreaterThan(0).When(x => x.Area.HasValue);
        RuleFor(x => x.SurveyNumber).MaximumLength(100);
        RuleFor(x => x.LotNumber).MaximumLength(50);
        RuleFor(x => x.BlockNumber).MaximumLength(50);
    }
}
