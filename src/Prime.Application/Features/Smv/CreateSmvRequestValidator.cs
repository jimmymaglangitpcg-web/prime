using FluentValidation;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

public sealed class CreateSmvRequestValidator : AbstractValidator<CreateSmvRequest>
{
    public CreateSmvRequestValidator()
    {
        RuleFor(x => x.Basis).IsInEnum();
        // An ordinance SMV names its ordinance; a certified SMV (RA 12001) its certification
        // (docs/analysis/valuation-foundation.md §4.3).
        RuleFor(x => x.OrdinanceNumber).NotEmpty().When(x => x.Basis == SmvBasis.Ordinance)
            .WithMessage("ordinanceNumber is required for an SMV enacted by ordinance.");
        RuleFor(x => x.OrdinanceDate).NotNull().When(x => x.Basis == SmvBasis.Ordinance)
            .WithMessage("ordinanceDate is required for an SMV enacted by ordinance.");
        RuleFor(x => x.CertificationReference).NotEmpty().When(x => x.Basis == SmvBasis.Certified)
            .WithMessage("certificationReference is required for a certified SMV.");
        RuleFor(x => x.OrdinanceNumber).MaximumLength(50);
        RuleFor(x => x.CertificationReference).MaximumLength(100);
        RuleFor(x => x.PublicationReference).MaximumLength(200);
        RuleFor(x => x.EffectivityDate).NotEmpty();
        RuleFor(x => x.RevisionYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.MunicipalityIds)
            .Must(ids => ids!.Distinct().Count() == ids!.Count).When(x => x.MunicipalityIds is { Count: > 0 })
            .WithMessage("A municipality is listed more than once in the coverage.");
        RuleFor(x => x).Must(x => x.CertifiedOn is null || x.CertifiedOn <= x.EffectivityDate)
            .WithMessage("The SMV cannot take effect before it was certified.");
    }
}
