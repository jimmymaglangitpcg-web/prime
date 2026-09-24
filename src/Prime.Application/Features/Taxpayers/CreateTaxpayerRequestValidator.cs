using FluentValidation;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Taxpayers;

public sealed class CreateTaxpayerRequestValidator : AbstractValidator<CreateTaxpayerRequest>
{
    public CreateTaxpayerRequestValidator()
    {
        RuleFor(x => x.TaxpayerType).IsInEnum();

        When(x => x.TaxpayerType == TaxpayerType.Individual, () =>
        {
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(150);
        }).Otherwise(() =>
        {
            RuleFor(x => x.CorporateName).NotEmpty().MaximumLength(300);
        });

        RuleFor(x => x.Tin).MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ContactNumber).MaximumLength(30);
    }
}

public sealed class AddPropertyOwnerRequestValidator : AbstractValidator<AddPropertyOwnerRequest>
{
    public AddPropertyOwnerRequestValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("startDate is required.");
        // LGC §§204–205: an unknown owner has no taxpayer; every other capacity names one.
        RuleFor(x => x.TaxpayerId).Null().When(x => x.Role == PropertyPartyRole.UnknownOwner)
            .WithMessage("An unknown-owner declaration has no taxpayer.");
        RuleFor(x => x.TaxpayerId).NotEmpty().When(x => x.Role != PropertyPartyRole.UnknownOwner)
            .WithMessage("taxpayerId is required.");
        // Only owners hold an ownership share of a type.
        RuleFor(x => x.OwnershipTypeId).NotEmpty().When(x => x.Role == PropertyPartyRole.Owner)
            .WithMessage("ownershipTypeId is required for an owner.");
        RuleFor(x => x.OwnershipPercentage).GreaterThan(0).LessThanOrEqualTo(100).When(x => x.Role == PropertyPartyRole.Owner);
        RuleFor(x => x.OwnershipPercentage).InclusiveBetween(0, 100).When(x => x.Role != PropertyPartyRole.Owner);
        RuleFor(x => x.OwnershipPercentage).Equal(0).When(x => x.Role == PropertyPartyRole.UnknownOwner)
            .WithMessage("An unknown-owner declaration has no share.");
    }
}
