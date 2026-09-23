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
        RuleFor(x => x.TaxpayerId).NotEmpty();
        RuleFor(x => x.OwnershipTypeId).NotEmpty();
        RuleFor(x => x.OwnershipPercentage).GreaterThan(0).LessThanOrEqualTo(100);
    }
}
