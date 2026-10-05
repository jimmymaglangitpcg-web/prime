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
        // An amendment names the SMV it amends and why (LAM 2025 Book IV Ch. III §4; smv-preparation-general-revision.md §4.7).
        RuleFor(x => x.AmendsSmvId).NotEmpty().When(x => x.Basis == SmvBasis.Amendment)
            .WithMessage("amendsSmvId is required for an amendment.");
        RuleFor(x => x.AmendmentGround).NotNull().IsInEnum().When(x => x.Basis == SmvBasis.Amendment)
            .WithMessage("amendmentGround is required for an amendment.");
        RuleFor(x => x).Must(x => x.AmendsSmvId is null && x.AmendmentGround is null).When(x => x.Basis != SmvBasis.Amendment)
            .WithMessage("Only an amendment names an amended SMV and a ground.");
        RuleFor(x => x.OrdinanceNumber).Empty().When(x => x.Basis == SmvBasis.Amendment)
            .WithMessage("An amendment is certified, not enacted by ordinance.");
        RuleFor(x => x.OrdinanceNumber).MaximumLength(50);
        RuleFor(x => x.CertificationReference).MaximumLength(100);
        RuleFor(x => x.PublicationReference).MaximumLength(200);
        RuleFor(x => x.EffectivityDate).NotEmpty();
        // An amendment carries the amended SMV's revision year.
        RuleFor(x => x.RevisionYear).InclusiveBetween(1900, 2200).When(x => x.Basis != SmvBasis.Amendment);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.MunicipalityIds)
            .Must(ids => ids!.Distinct().Count() == ids!.Count).When(x => x.MunicipalityIds is { Count: > 0 })
            .WithMessage("A municipality is listed more than once in the coverage.");
        RuleFor(x => x).Must(x => x.CertifiedOn is null || x.CertifiedOn <= x.EffectivityDate)
            .WithMessage("The SMV cannot take effect before it was certified.");
    }
}
