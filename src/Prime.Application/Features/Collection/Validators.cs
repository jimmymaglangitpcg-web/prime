using FluentValidation;

namespace Prime.Application.Features.Collection;

public sealed class PaymentItemRequestValidator : AbstractValidator<PaymentItemRequest>
{
    public PaymentItemRequestValidator()
    {
        RuleFor(x => x.RpuId).NotEmpty();
        RuleFor(x => x.TaxYear).InclusiveBetween(1900, 2200);
        RuleFor(x => x.InstallmentSequence).GreaterThan(0);
        RuleFor(x => x.PrincipalAmount).GreaterThan(0).PrecisionScale(18, 2, true).When(x => x.PrincipalAmount is not null);
    }
}

public sealed class QuotePaymentRequestValidator : AbstractValidator<QuotePaymentRequest>
{
    public QuotePaymentRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Select at least one installment.");
        RuleForEach(x => x.Items).SetValidator(new PaymentItemRequestValidator());
    }
}

public sealed class PostPaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    public PostPaymentRequestValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PayorName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.PayorAddress).MaximumLength(500);
        RuleFor(x => x.OfficialReceiptNumber).MaximumLength(100);
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.ExpectedTotal).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Select at least one installment.");
        RuleForEach(x => x.Items).SetValidator(new PaymentItemRequestValidator());
        RuleFor(x => x.Tenders).NotEmpty().WithMessage("Enter at least one mode of payment.");
        RuleForEach(x => x.Tenders).ChildRules(t =>
        {
            t.RuleFor(x => x.PaymentModeId).NotEmpty();
            t.RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, true);
            t.RuleFor(x => x.Reference).MaximumLength(200);
            t.RuleFor(x => x.Bank).MaximumLength(200);
        });
    }
}

public sealed class CreatePaymentModeRequestValidator : AbstractValidator<CreatePaymentModeRequest>
{
    public CreatePaymentModeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateRevenueAccountMappingRequestValidator : AbstractValidator<CreateRevenueAccountMappingRequest>
{
    public CreateRevenueAccountMappingRequestValidator()
    {
        RuleFor(x => x.TaxTypeId).NotEmpty();
        RuleFor(x => x.Component).IsInEnum();
        RuleFor(x => x.YearCategory).IsInEnum();
        RuleFor(x => x.AccountCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Fund).MaximumLength(100);
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}
