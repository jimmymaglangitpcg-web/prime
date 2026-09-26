using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Features.Collection;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Collection;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 9 step 9b (docs/analysis/collection.md §4, §7): pay posted bills,
/// allocate, receipt numbers, balances, refusals, idempotency and concurrent
/// posting. The LGU clock is pinned (payments are always dated today), and
/// every rate, account code, mode and number format is DEMO test data
/// (CLAUDE.md §81). The seeded bill: AV 100,000; DEMO basic 1% and SEF 1%
/// (1,000 each), one installment due 31 March 2026; DEMO 10% prompt discount
/// and 2%/month interest (a started month counts).
/// </summary>
public class CollectionFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset March15 = new(2026, 3, 15, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset June15 = new(2026, 6, 15, 2, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private readonly Dictionary<DateTimeOffset, WebApplicationFactory<Program>> hosts = [];

    /// <summary>A host whose LGU clock reads <paramref name="utc"/>.</summary>
    private WebApplicationFactory<Program> At(DateTimeOffset utc)
    {
        if (!hosts.TryGetValue(utc, out var host))
        {
            host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTime(utc))));
            hosts[utc] = host;
        }
        return host;
    }

    internal sealed record PaySeed(BillingFlowTests.Seed Seed, TaxType Basic, TaxType Sef, TaxBillDto Bill, PaymentMode Cash, PaymentMode Check);

    private sealed class ScopedTransaction(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    /// <summary>A rolled-back scope with every approved billing rule, receipt/transaction numbering scheme and revenue mapping retired.</summary>
    private async Task<(PrimeDbContext Db, IServiceProvider Services, IAsyncDisposable Transaction)> BeginAsync(DateTimeOffset at)
    {
        var scope = At(at).Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await BillingFlowTests.RetireExistingRulesAsync(db);
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved
                && (x.AppliesTo == NumberedDocumentKind.OfficialReceipt || x.AppliesTo == NumberedDocumentKind.PaymentTransaction))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.RevenueAccountMappings.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        return (db, scope.ServiceProvider, new ScopedTransaction(transaction, scope));
    }

    private static T ApprovedConfig<T>(T item) where T : Prime.Domain.Common.EffectiveDatedConfiguration
    {
        item.LegalBasis = "DEMO — not an LGU/COA source";
        item.EffectiveDate = new DateOnly(2020, 1, 1);
        item.Status = WorkflowStatus.Approved;
        item.ApprovedAt = DateTimeOffset.UtcNow;
        return item;
    }

    private static IEnumerable<RevenueAccountMapping> DemoMappings(params TaxType[] taxTypes) =>
        from taxType in taxTypes
        from component in Enum.GetValues<BillingComponent>()
        from category in Enum.GetValues<CollectionYearCategory>()
        select ApprovedConfig(new RevenueAccountMapping
        {
            TaxType = taxType, Component = component, YearCategory = category,
            AccountCode = $"DEMO-{taxType.Name}-{component}-{category}", AccountName = $"DEMO {taxType.Name} {component} {category}", Fund = $"DEMO {taxType.Name}",
        });

    private static void AddNumbering(PrimeDbContext db, string tag, bool receipt = true, bool transaction = true)
    {
        if (receipt)
        {
            db.Add(ApprovedConfig(new NumberingScheme { AppliesTo = NumberedDocumentKind.OfficialReceipt, Name = "DEMO OR", Pattern = $"DEMO-OR-{tag}-{{SEQ:5}}", AllowManualEntry = true }));
        }
        if (transaction)
        {
            db.Add(ApprovedConfig(new NumberingScheme { AppliesTo = NumberedDocumentKind.PaymentTransaction, Name = "DEMO TXN", Pattern = $"DEMO-TXN-{tag}-{{SEQ:6}}" }));
        }
    }

    /// <summary>A posted 2026 bill (as of <paramref name="billAsOf"/>), DEMO modes, and optionally numbering and account mappings.</summary>
    private static async Task<PaySeed> SeedBillAsync(IServiceProvider services, PrimeDbContext db, DateOnly billAsOf,
        bool numbering = true, bool mappings = true)
    {
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        var (basic, sef) = await BillingFlowTests.SeedRulesAsync(db, new DateOnly(2026, 1, 1));
        var tag = Guid.NewGuid().ToString("N")[..8];
        var cash = new PaymentMode { Code = $"DC{tag}", Name = "DEMO Cash", AllowsChange = true };
        var check = new PaymentMode { Code = $"DK{tag}", Name = "DEMO Check", RequiresReference = true };
        db.AddRange(cash, check);
        if (numbering)
        {
            AddNumbering(db, tag);
        }
        if (mappings)
        {
            db.AddRange(DemoMappings(basic, sef));
        }
        await db.SaveChangesAsync();

        var bills = services.GetRequiredService<IBillService>();
        var bill = await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, billAsOf));
        bill.IsSuccess.ShouldBeTrue(bill.IsSuccess ? null : bill.Message);
        var posted = await bills.PostAsync(bill.Value.Id);
        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        return new PaySeed(seed, basic, sef, posted.Value, cash, check);
    }

    private static PaymentItemRequest Whole(PaySeed s) => new(s.Seed.RpuId, 2026, 1);

    private static PostPaymentRequest Pay(PaySeed s, decimal expected, IReadOnlyList<PaymentTenderRequest>? tenders = null,
        IReadOnlyList<PaymentItemRequest>? items = null, string? key = null, string? receiptNumber = null) =>
        new(key ?? Guid.NewGuid().ToString(), null, "DEMO PAYOR", "DEMO Address", items ?? [Whole(s)],
            tenders ?? [new PaymentTenderRequest(s.Cash.Id, expected)], expected, receiptNumber);

    // --- Full payment, receipt, balance ---

    [Fact]
    public async Task Pay_OnTime_WholeInstallment_GetsDiscount_ReceiptNumbered_BalanceZero()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();

        var quote = await payments.QuoteAsync(new QuotePaymentRequest([Whole(s)]));
        quote.IsSuccess.ShouldBeTrue(quote.IsSuccess ? null : quote.Message);
        quote.Value.PaymentDate.ShouldBe(new DateOnly(2026, 3, 15));
        quote.Value.Total.ShouldBe(1_800m);
        quote.Value.Allocations.Count(a => a.Component == BillingComponent.Discount).ShouldBe(2);

        var posted = await payments.PostAsync(Pay(s, 1_800m, [new PaymentTenderRequest(s.Cash.Id, 2_000m)]));

        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        var p = posted.Value;
        p.Status.ShouldBe(PaymentStatus.Posted);
        p.AmountDue.ShouldBe(1_800m);
        p.AmountTendered.ShouldBe(2_000m);
        p.Change.ShouldBe(200m);
        p.OfficialReceiptNumber.ShouldStartWith("DEMO-OR-");
        p.TransactionNumber.ShouldStartWith("DEMO-TXN-");
        p.OfficialReceiptNumber.ShouldNotBe(p.TransactionNumber);
        p.PaymentDate.ShouldBe(new DateOnly(2026, 3, 15));
        p.Allocations.Sum(a => a.Amount).ShouldBe(1_800m);
        p.Allocations.ShouldAllBe(a => a.AccountCode.StartsWith("DEMO-") && a.BillId == s.Bill.Id && a.YearCategory == CollectionYearCategory.Current);
        p.Allocations.Single(a => a.TaxTypeId == s.Basic.Id && a.Component == BillingComponent.Tax).AccountCode.ShouldBe("DEMO-DEMO_BASIC-Tax-Current");

        var outstanding = (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value;
        outstanding.TotalOutstandingPrincipal.ShouldBe(0m);
        outstanding.TotalDueAsOf.ShouldBe(0m);
        var installment = outstanding.Bills.Single().Installments.Single();
        installment.PrincipalPaid.ShouldBe(2_000m);
        installment.DueIfPaidAsOf.ShouldBeNull();

        (await payments.QuoteAsync(new QuotePaymentRequest([Whole(s)]))).Code.ShouldBe("PAYMENT_ALREADY_SETTLED");
        (await payments.ListByPropertyAsync(s.Seed.PropertyId)).Value.Single().Id.ShouldBe(p.Id);
        (await payments.ListAsync(new DateOnly(2026, 3, 15), null, null)).Value.ShouldContain(x => x.Id == p.Id);
        (await payments.GetByIdAsync(p.Id)).Value.Tenders.Single().ModeCode.ShouldBe(s.Cash.Code);
    }

    [Fact]
    public async Task Pay_Late_ChargesInterestUpToThePaymentDate()
    {
        var (db, services, transaction) = await BeginAsync(June15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 1, 15));
        var payments = services.GetRequiredService<IPaymentService>();

        var quote = (await payments.QuoteAsync(new QuotePaymentRequest([Whole(s)]))).Value;

        // Bill as of 15 Jan showed a discount; paid 15 June: 2,000 × 2% × 3 started months.
        quote.Allocations.ShouldNotContain(a => a.Component == BillingComponent.Discount);
        quote.Allocations.Where(a => a.Component == BillingComponent.Interest).Sum(a => a.Amount).ShouldBe(120m);
        quote.Total.ShouldBe(2_120m);
        (await payments.PostAsync(Pay(s, 2_120m))).Value.AmountDue.ShouldBe(2_120m);
    }

    [Fact]
    public async Task PartialPayment_ThenTheRest_NoDiscountOnParts_BalanceZero()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();

        var part = await payments.PostAsync(Pay(s, 500m, items: [new PaymentItemRequest(s.Seed.RpuId, 2026, 1, 500m)]));
        part.IsSuccess.ShouldBeTrue(part.IsSuccess ? null : part.Message);
        part.Value.Allocations.Select(a => a.Amount).ShouldBe([250m, 250m]);

        var outstanding = (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value;
        outstanding.TotalOutstandingPrincipal.ShouldBe(1_500m);
        outstanding.TotalDueAsOf.ShouldBe(1_500m);

        (await payments.PostAsync(Pay(s, 1_500m))).IsSuccess.ShouldBeTrue();
        (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value.TotalOutstandingPrincipal.ShouldBe(0m);
    }

    // --- Idempotency and stale quotes ---

    [Fact]
    public async Task SameSubmissionKey_ReturnsTheFirstPayment_AndAChangedAmountIsRefused()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var request = Pay(s, 1_800m);

        var first = (await payments.PostAsync(request)).Value;
        var again = await payments.PostAsync(request);

        again.Value.Id.ShouldBe(first.Id);
        (await db.Payments.CountAsync(x => x.IdempotencyKey == request.IdempotencyKey)).ShouldBe(1);
        (await payments.PostAsync(request with { ExpectedTotal = 999m })).Code.ShouldBe("PAYMENT_IDEMPOTENCY_CONFLICT");
    }

    [Fact]
    public async Task ExpectedTotalDiffers_IsRefused_AndNothingIsPosted()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();

        (await payments.PostAsync(Pay(s, 2_000m))).Code.ShouldBe("PAYMENT_QUOTE_CHANGED");
        (await payments.ListByPropertyAsync(s.Seed.PropertyId)).Value.ShouldBeEmpty();
    }

    // --- Refusals ---

    [Fact]
    public async Task MissingRevenueAccount_IsRefused()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15), mappings: false);

        var quote = await services.GetRequiredService<IPaymentService>().QuoteAsync(new QuotePaymentRequest([Whole(s)]));

        quote.Code.ShouldBe("PAYMENT_ACCOUNT_NOT_MAPPED");
        quote.Message!.ShouldContain(s.Basic.Code);
    }

    [Fact]
    public async Task MissingTransactionNumbering_IsRefused()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15), numbering: false);

        (await services.GetRequiredService<IPaymentService>().PostAsync(Pay(s, 1_800m, receiptNumber: "DEMO-PAPER-1")))
            .Code.ShouldBe("PAYMENT_TRANSACTION_NUMBERING_NOT_CONFIGURED");
    }

    [Fact]
    public async Task Tenders_MustCoverTheAmount_CarryReferences_AndGiveChangeOnlyFromCash()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();

        (await payments.PostAsync(Pay(s, 1_800m, [new PaymentTenderRequest(s.Cash.Id, 1_000m)]))).Code.ShouldBe("PAYMENT_TENDER_INVALID");
        (await payments.PostAsync(Pay(s, 1_800m, [new PaymentTenderRequest(s.Check.Id, 1_800m)]))).Code.ShouldBe("PAYMENT_TENDER_INVALID");
        (await payments.PostAsync(Pay(s, 1_800m, [new PaymentTenderRequest(s.Check.Id, 2_000m, "DEMO-CHK-1")]))).Code.ShouldBe("PAYMENT_TENDER_INVALID");

        var mixed = await payments.PostAsync(Pay(s, 1_800m,
            [new PaymentTenderRequest(s.Check.Id, 1_000m, "DEMO-CHK-2", "DEMO Bank"), new PaymentTenderRequest(s.Cash.Id, 1_000m)]));
        mixed.IsSuccess.ShouldBeTrue(mixed.IsSuccess ? null : mixed.Message);
        mixed.Value.Change.ShouldBe(200m);
        mixed.Value.Tenders.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TypedReceiptNumber_IsUsed_AndCannotBeIssuedTwice()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var paper = $"DEMO-PAPER-{Guid.NewGuid():N}"[..24];

        var first = await payments.PostAsync(Pay(s, 500m, items: [new PaymentItemRequest(s.Seed.RpuId, 2026, 1, 500m)], receiptNumber: paper));
        first.Value.OfficialReceiptNumber.ShouldBe(paper);

        (await payments.PostAsync(Pay(s, 500m, items: [new PaymentItemRequest(s.Seed.RpuId, 2026, 1, 500m)], receiptNumber: paper)))
            .Code.ShouldBe("PAYMENT_OR_NUMBER_DUPLICATE");
    }

    // --- Bills with payments ---

    [Fact]
    public async Task BillWithPayments_CannotBeCancelled_ButASupersedingBillKeepsThem()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var bills = services.GetRequiredService<IBillService>();
        (await payments.PostAsync(Pay(s, 500m, items: [new PaymentItemRequest(s.Seed.RpuId, 2026, 1, 500m)]))).IsSuccess.ShouldBeTrue();

        (await bills.CancelAsync(s.Bill.Id, "DEMO")).Code.ShouldBe("BILL_HAS_PAYMENTS");

        var recomputed = (await bills.GenerateAsync(new GenerateBillRequest(s.Seed.RpuId, 2026, new DateOnly(2026, 3, 20)))).Value;
        (await bills.PostAsync(recomputed.Id)).IsSuccess.ShouldBeTrue();

        var outstanding = (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value;
        outstanding.Bills.Single().BillId.ShouldBe(recomputed.Id);
        outstanding.Bills.Single().Installments.Single().PrincipalPaid.ShouldBe(500m);
        outstanding.TotalOutstandingPrincipal.ShouldBe(1_500m);
    }

    // --- Void, reversal, correction (step 9c) ---

    private static (Prime.Infrastructure.Identity.CurrentUserService User, Guid Maker, Guid Checker) Users(IServiceProvider services)
    {
        var user = services.GetRequiredService<Prime.Infrastructure.Identity.CurrentUserService>();
        var maker = Guid.NewGuid();
        user.AppUserId = maker;
        return (user, maker, Guid.NewGuid());
    }

    [Fact]
    public async Task Void_SameDay_NeedsAnotherUser_GetsItsOwnTransactionNumber_AndRestoresTheBalance()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var (user, _, checker) = Users(services);
        var paid = (await payments.PostAsync(Pay(s, 1_800m))).Value;

        var request = await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("DEMO wrong property"));
        request.IsSuccess.ShouldBeTrue(request.IsSuccess ? null : request.Message);
        (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("again"))).Code.ShouldBe("PAYMENT_CANCELLATION_DUPLICATE");
        (await payments.ApproveCancellationAsync(request.Value.Id, new DecidePaymentCancellationRequest(null))).Code.ShouldBe("CANNOT_APPROVE_OWN_PAYMENT_CANCELLATION");

        user.AppUserId = checker;
        var approved = await payments.ApproveCancellationAsync(request.Value.Id, new DecidePaymentCancellationRequest("DEMO ok"));

        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        approved.Value.Kind.ShouldBe(PaymentCancellationKind.Void);
        approved.Value.Status.ShouldBe(PaymentCancellationStatus.Approved);
        approved.Value.DecidedBy.ShouldBe(checker);
        approved.Value.TransactionNumber.ShouldStartWith("DEMO-TXN-");
        approved.Value.TransactionNumber.ShouldNotBe(paid.TransactionNumber);

        var voided = (await payments.GetByIdAsync(paid.Id)).Value;
        voided.Status.ShouldBe(PaymentStatus.Voided);
        voided.CancelledAt.ShouldNotBeNull();
        voided.Allocations.Count.ShouldBe(paid.Allocations.Count);   // kept on record
        voided.Cancellations.Single().Id.ShouldBe(request.Value.Id);
        (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value.TotalOutstandingPrincipal.ShouldBe(2_000m);
        (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("DEMO"))).Code.ShouldBe("PAYMENT_NOT_POSTED");
    }

    [Fact]
    public async Task ApprovedOnALaterDay_IsAReversal()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var (user, _, checker) = Users(services);
        var paid = (await payments.PostAsync(Pay(s, 1_800m))).Value;
        // As if received the day before (the clock is pinned to 15 March).
        await db.Payments.Where(x => x.Id == paid.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.PaymentDate, new DateOnly(2026, 3, 14)));

        var request = (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("DEMO dishonoured check"))).Value;
        user.AppUserId = checker;
        var approved = (await payments.ApproveCancellationAsync(request.Id, new DecidePaymentCancellationRequest(null))).Value;

        approved.Kind.ShouldBe(PaymentCancellationKind.Reversal);
        (await payments.GetByIdAsync(paid.Id)).Value.Status.ShouldBe(PaymentStatus.Reversed);
    }

    [Fact]
    public async Task Reject_NeedsReasons_KeepsThePayment_AndAllowsANewRequest()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var (user, _, checker) = Users(services);
        var paid = (await payments.PostAsync(Pay(s, 1_800m))).Value;
        (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest(" "))).Code.ShouldBe("VALIDATION_FAILED");
        var request = (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("DEMO"))).Value;

        user.AppUserId = checker;
        (await payments.RejectCancellationAsync(request.Id, new DecidePaymentCancellationRequest(null))).Code.ShouldBe("VALIDATION_FAILED");
        var rejected = (await payments.RejectCancellationAsync(request.Id, new DecidePaymentCancellationRequest("DEMO receipt is correct"))).Value;

        rejected.Status.ShouldBe(PaymentCancellationStatus.Rejected);
        rejected.TransactionNumber.ShouldBeNull();
        (await payments.GetByIdAsync(paid.Id)).Value.Status.ShouldBe(PaymentStatus.Posted);
        (await payments.ApproveCancellationAsync(request.Id, new DecidePaymentCancellationRequest(null))).Code.ShouldBe("PAYMENT_CANCELLATION_NOT_PENDING");
        (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("DEMO second thought"))).IsSuccess.ShouldBeTrue();
        (await payments.ListCancellationsAsync(PaymentCancellationStatus.Pending)).Value.ShouldContain(c => c.PaymentId == paid.Id);
    }

    [Fact]
    public async Task Correction_VoidsAndReissues_InOneApprovedStep_DatedLikeTheOriginal()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var (user, _, checker) = Users(services);
        var paid = (await payments.PostAsync(Pay(s, 1_800m) with { PayorName = "DEMO WRONG NAME" })).Value;
        var replacement = new PaymentReplacementRequest(null, "DEMO RIGHT NAME", "DEMO Address", [Whole(s)], [new PaymentTenderRequest(s.Cash.Id, 1_800m)], 1_800m);

        (await payments.RequestCorrectionAsync(paid.Id, new RequestPaymentCorrectionRequest("DEMO", replacement with { ExpectedTotal = 2_000m })))
            .Code.ShouldBe("PAYMENT_QUOTE_CHANGED");
        var request = await payments.RequestCorrectionAsync(paid.Id, new RequestPaymentCorrectionRequest("DEMO payor misspelled", replacement));
        request.IsSuccess.ShouldBeTrue(request.IsSuccess ? null : request.Message);
        request.Value.Replacement!.PayorName.ShouldBe("DEMO RIGHT NAME");

        user.AppUserId = checker;
        var approved = await payments.ApproveCancellationAsync(request.Value.Id, new DecidePaymentCancellationRequest(null));

        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        var reissued = (await payments.GetByIdAsync(approved.Value.ReplacementPaymentId!.Value)).Value;
        reissued.PayorName.ShouldBe("DEMO RIGHT NAME");
        reissued.ReplacesPaymentId.ShouldBe(paid.Id);
        reissued.PaymentDate.ShouldBe(paid.PaymentDate);
        reissued.OfficialReceiptNumber.ShouldNotBe(paid.OfficialReceiptNumber);
        reissued.AmountDue.ShouldBe(1_800m);
        var original = (await payments.GetByIdAsync(paid.Id)).Value;
        original.Status.ShouldBe(PaymentStatus.Voided);
        original.ReplacedByPaymentId.ShouldBe(reissued.Id);
        (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value.TotalOutstandingPrincipal.ShouldBe(0m);
    }

    [Fact]
    public async Task Correction_ThatCannotBePostedAtApproval_ChangesNothing()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var (user, _, checker) = Users(services);
        var paid = (await payments.PostAsync(Pay(s, 1_800m))).Value;
        var replacement = new PaymentReplacementRequest(null, "DEMO", null, [Whole(s)], [new PaymentTenderRequest(s.Cash.Id, 1_800m)], 1_800m);
        var request = (await payments.RequestCorrectionAsync(paid.Id, new RequestPaymentCorrectionRequest("DEMO", replacement))).Value;
        // The revenue accounts are withdrawn before the checker approves.
        await db.RevenueAccountMappings.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));

        user.AppUserId = checker;
        (await payments.ApproveCancellationAsync(request.Id, new DecidePaymentCancellationRequest(null))).Code.ShouldBe("PAYMENT_ACCOUNT_NOT_MAPPED");

        (await payments.GetByIdAsync(paid.Id)).Value.Status.ShouldBe(PaymentStatus.Posted);
        (await payments.ListCancellationsAsync(PaymentCancellationStatus.Pending)).Value.ShouldContain(c => c.Id == request.Id);
        (await payments.GetOutstandingAsync(s.Seed.PropertyId, null)).Value.TotalOutstandingPrincipal.ShouldBe(0m);
    }

    // --- Receipt and statement of account (step 9d) ---

    [Fact]
    public async Task Receipt_IsIssuedOnce_WithTheEorContent_AndAVoidedPaymentCannotBeIssued()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var forms = services.GetRequiredService<Prime.Application.Features.Forms.IFormService>();
        var paid = (await payments.PostAsync(Pay(s, 1_800m, [new PaymentTenderRequest(s.Cash.Id, 2_000m)]))).Value;

        var issued = await forms.IssueAsync(new Prime.Application.Features.Forms.IssueFormRequest("OFFICIAL_RECEIPT", paid.Id));

        issued.IsSuccess.ShouldBeTrue(issued.IsSuccess ? null : issued.Message);
        var html = issued.Value.Html!;
        html.ShouldContain("PROVISIONAL");
        html.ShouldContain(paid.OfficialReceiptNumber);
        html.ShouldContain(paid.TransactionNumber);
        html.ShouldContain("15 March 2026");                     // date and time of receipt
        html.ShouldContain("DEMO-DEMO_BASIC-Tax-Current");       // coded to the revenue account
        html.ShouldContain(s.Bill.TaxDeclarationNumber);        // bill / TD reference
        html.ShouldContain("DEMO Cash");
        html.ShouldContain("1,800.00");
        html.ShouldContain("200.00");                            // change
        (await forms.IssueAsync(new Prime.Application.Features.Forms.IssueFormRequest("OFFICIAL_RECEIPT", paid.Id))).Value.Id.ShouldBe(issued.Value.Id);

        var (user, _, checker) = Users(services);
        var other = (await payments.PostAsync(Pay(s, 1_800m))).Code;   // already settled: the first payment stands
        other.ShouldBe("PAYMENT_ALREADY_SETTLED");
        var request = (await payments.RequestCancellationAsync(paid.Id, new RequestPaymentCancellationRequest("DEMO"))).Value;
        user.AppUserId = checker;
        (await payments.ApproveCancellationAsync(request.Id, new DecidePaymentCancellationRequest(null))).IsSuccess.ShouldBeTrue();
        var again = (await payments.PostAsync(Pay(s, 1_800m))).Value;
        var preview = await forms.PreviewAsync("OFFICIAL_RECEIPT", paid.Id);
        preview.Value.Html.ShouldContain("VOIDED");
        (await forms.IssueAsync(new Prime.Application.Features.Forms.IssueFormRequest("OFFICIAL_RECEIPT", again.Id))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task StatementOfAccount_ShowsPaidOutstandingAndReceipts()
    {
        var (db, services, transaction) = await BeginAsync(March15);
        await using var _ = transaction;
        var s = await SeedBillAsync(services, db, new DateOnly(2026, 3, 15));
        var payments = services.GetRequiredService<IPaymentService>();
        var paid = (await payments.PostAsync(Pay(s, 500m, items: [new PaymentItemRequest(s.Seed.RpuId, 2026, 1, 500m)]))).Value;

        var statement = (await services.GetRequiredService<IBillService>().GetStatementOfAccountAsync(s.Seed.PropertyId)).Value;

        statement.AsOfDate.ShouldBe(new DateOnly(2026, 3, 15));
        var line = statement.Bills.Single();
        line.PrincipalOwed.ShouldBe(2_000m);
        line.PrincipalPaid.ShouldBe(500m);
        line.OutstandingPrincipal.ShouldBe(1_500m);
        line.DueAsOf.ShouldBe(1_500m);                           // no discount on the rest of a part-paid installment
        statement.TotalDueAsOf.ShouldBe(1_500m);
        statement.Payments.Single().OfficialReceiptNumber.ShouldBe(paid.OfficialReceiptNumber);
        statement.Payments.Single().Amount.ShouldBe(500m);
    }

    // --- HTTP surface ---

    [Fact]
    public async Task Api_RefusesAnEmptySelection_AndListsPaymentModes()
    {
        var client = factory.CreateClient();

        var quote = await client.PostAsJsonAsync("/api/payments/quote", new QuotePaymentRequest([]));
        quote.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await quote.Content.ReadAsStringAsync()).ShouldContain("VALIDATION_FAILED");

        (await client.GetAsync("/api/collection/payment-modes")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/collection/account-mappings")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // --- Concurrency (committed data: two connections must see each other's writes) ---

    /// <summary>
    /// Two cashiers post the same installment at the same moment: exactly one
    /// payment is saved; the other is told it is already settled. The same
    /// submission sent twice at once yields one payment. This test commits DEMO
    /// rows to the dev database (a fresh DEMO property and bill each run, plus one
    /// shared DEMO tax type, mode and account mappings) because uncommitted rows are
    /// invisible to a second connection; it reuses any receipt/transaction numbering
    /// scheme already in force.
    /// </summary>
    [Fact]
    public async Task ConcurrentPosting_OfOneInstallment_SavesExactlyOnePayment()
    {
        var host = At(March15);
        PaySeed s;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
            var tag = Guid.NewGuid().ToString("N")[..8];
            // One shared DEMO tax type, mode and set of account mappings, reused by every run so
            // repeated runs do not fill the dev database's pick-lists.
            var basic = await db.TaxTypes.FirstOrDefaultAsync(x => x.Code == "DEMO-CONCURRENCY")
                ?? db.TaxTypes.Add(new TaxType { Code = "DEMO-CONCURRENCY", Name = "DEMO_CONCURRENCY_TEST" }).Entity;
            var cash = await db.PaymentModes.FirstOrDefaultAsync(x => x.Code == "DEMO-CONCURRENCY-CASH")
                ?? db.PaymentModes.Add(new PaymentMode { Code = "DEMO-CONCURRENCY-CASH", Name = "DEMO Cash (concurrency test)", AllowsChange = true }).Entity;
            if (!await db.RevenueAccountMappings.AnyAsync(x => x.TaxTypeId == basic.Id && x.Status == WorkflowStatus.Approved))
            {
                db.AddRange(DemoMappings(basic));
            }
            AddNumbering(db, tag,
                receipt: !await db.NumberingSchemes.InForceAsync(NumberedDocumentKind.OfficialReceipt, new DateOnly(2026, 3, 15)),
                transaction: !await db.NumberingSchemes.InForceAsync(NumberedDocumentKind.PaymentTransaction, new DateOnly(2026, 3, 15)));
            // A posted bill written directly: this test is about posting payments, not about billing rules.
            var bill = new TaxBill
            {
                PropertyId = seed.PropertyId, RpuId = seed.RpuId, TaxDeclarationId = seed.TaxDeclaration.Id, AssessmentId = seed.AssessmentId,
                TaxYear = 2026, AsOfDate = new DateOnly(2026, 3, 15), RulesAsOfDate = new DateOnly(2026, 1, 1), AssessedValue = 100_000m,
                ClassificationId = seed.ClassificationId, Status = WorkflowStatus.Posted, PostedAt = DateTimeOffset.UtcNow,
                Notes = "DEMO bill written by CollectionFlowTests.ConcurrentPosting",
                Details =
                [
                    new TaxBillDetail
                    {
                        LineNumber = 1, InstallmentSequence = 1, DueDate = new DateOnly(2090, 12, 31), TaxTypeId = basic.Id,
                        Component = BillingComponent.Tax, RuleId = Guid.NewGuid(), BaseAmount = 1_000m, Amount = 1_000m, Explanation = "DEMO",
                    },
                ],
            };
            db.TaxBills.Add(bill);
            await db.SaveChangesAsync();
            s = new PaySeed(seed, basic, basic, null!, cash, cash);
        }

        async Task<(bool Ok, string? Code, Guid? Id)> PostInOwnScope(string key, decimal expected)
        {
            using var scope = host.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IPaymentService>().PostAsync(Pay(s, expected, key: key));
            return (result.IsSuccess, result.Code, result.IsSuccess ? result.Value.Id : null);
        }

        decimal due;
        using (var scope = host.Services.CreateScope())
        {
            var quote = await scope.ServiceProvider.GetRequiredService<IPaymentService>().QuoteAsync(new QuotePaymentRequest([Whole(s)]));
            quote.IsSuccess.ShouldBeTrue(quote.IsSuccess ? null : quote.Message);
            due = quote.Value.Total;
        }

        var sharedKey = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(
            PostInOwnScope(sharedKey, due), PostInOwnScope(sharedKey, due),
            PostInOwnScope(Guid.NewGuid().ToString(), due), PostInOwnScope(Guid.NewGuid().ToString(), due));

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            var saved = await db.Payments.Where(p => p.Allocations.Any(a => a.RpuId == s.Seed.RpuId)).ToListAsync();
            saved.Count.ShouldBe(1);
            results.Where(r => r.Ok).Select(r => r.Id).Distinct().ShouldBe([saved[0].Id]);
            results.Where(r => !r.Ok).ShouldAllBe(r => r.Code == "PAYMENT_ALREADY_SETTLED");
            // Both submissions with the shared key succeeded only if that key won; either way there is one payment.
            (await db.PaymentAllocations.Where(a => a.RpuId == s.Seed.RpuId && a.Component == BillingComponent.Tax).SumAsync(a => a.Amount)).ShouldBe(1_000m);
        }
    }
}

internal static class NumberingSchemeQueries
{
    public static Task<bool> InForceAsync(this IQueryable<NumberingScheme> schemes, NumberedDocumentKind kind, DateOnly date) =>
        schemes.AnyAsync(x => x.AppliesTo == kind && x.Status == WorkflowStatus.Approved && x.EffectiveDate <= date && (x.EndDate == null || x.EndDate >= date));
}
