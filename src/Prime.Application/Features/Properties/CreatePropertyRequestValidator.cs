using FluentValidation;

namespace Prime.Application.Features.Properties;

public sealed class CreatePropertyRequestValidator : AbstractValidator<CreatePropertyRequest>
{
    public CreatePropertyRequestValidator()
    {
        RuleFor(x => x.PropertyIdentificationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProvinceId).NotEmpty();
        RuleFor(x => x.MunicipalityId).NotEmpty();
        RuleFor(x => x.BarangayId).NotEmpty();
        RuleFor(x => x.Street).MaximumLength(300);
        RuleFor(x => x.Sitio).MaximumLength(200);
        RuleFor(x => x.LotNumber).MaximumLength(50);
        RuleFor(x => x.BlockNumber).MaximumLength(50);
        RuleFor(x => x.SurveyNumber).MaximumLength(100);
        RuleFor(x => x.TitleNumber).MaximumLength(100);
        RuleFor(x => x.TaxMapNumber).MaximumLength(100);
    }
}
