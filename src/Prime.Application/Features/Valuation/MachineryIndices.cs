using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Valuation;

public sealed record CreateExchangeRateRequest(string Currency, DateOnly RateDate, decimal PesosPerUnit, string Source, string? Remarks);

public sealed record ExchangeRateDto(Guid Id, string Currency, DateOnly RateDate, decimal PesosPerUnit, string Source, WorkflowStatus Status,
    Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

public sealed record CreatePriceIndexRequest(string Series, int Year, decimal Value, string Source, string? Remarks);

public sealed record PriceIndexDto(Guid Id, string Series, int Year, decimal Value, string Source, WorkflowStatus Status,
    Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

public interface IMachineryIndexService
{
    Task<Result<ExchangeRateDto>> CreateExchangeRateAsync(CreateExchangeRateRequest request, CancellationToken ct = default);
    Task<Result<ExchangeRateDto>> ApproveExchangeRateAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ExchangeRateDto>>> ListExchangeRatesAsync(string? currency, CancellationToken ct = default);
    Task<Result<PriceIndexDto>> CreatePriceIndexAsync(CreatePriceIndexRequest request, CancellationToken ct = default);
    Task<Result<PriceIndexDto>> ApprovePriceIndexAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PriceIndexDto>>> ListPriceIndicesAsync(string? series, CancellationToken ct = default);
}

/// <summary>
/// Exchange rates and price indices for machinery (docs/analysis/valuation-foundation.md §4.6): each
/// is a dated observation from its source, created as a Draft and approved by a second user
/// (CLAUDE.md §46). An approved observation is never edited; one approved value per currency and
/// date, per series and year. Every figure is source data, none is built in.
/// </summary>
public sealed class MachineryIndexService(IApplicationDbContext db, ICurrentUserService currentUser) : IMachineryIndexService
{
    public async Task<Result<ExchangeRateDto>> CreateExchangeRateAsync(CreateExchangeRateRequest request, CancellationToken ct = default)
    {
        var currency = request.Currency?.Trim() ?? "";
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper) || request.RateDate == default || request.PesosPerUnit <= 0m
            || string.IsNullOrWhiteSpace(request.Source) || request.Source.Length > 300 || request.Remarks?.Length > 1000)
        {
            return Result.Failure<ExchangeRateDto>("VALIDATION_FAILED",
                "currency (ISO 4217, e.g. USD), rateDate, a pesosPerUnit above 0 and the source (max 300) are required.");
        }
        var rate = new ExchangeRate
        {
            Currency = currency, RateDate = request.RateDate, PesosPerUnit = request.PesosPerUnit, Source = request.Source.Trim(), Remarks = request.Remarks,
        };
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync(ct);
        return Result.Success(ToDto(rate));
    }

    public async Task<Result<ExchangeRateDto>> ApproveExchangeRateAsync(Guid id, CancellationToken ct = default)
    {
        var rate = await db.ExchangeRates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (rate is null)
        {
            return Result.Failure<ExchangeRateDto>("EXCHANGE_RATE_NOT_FOUND", "No exchange rate was found with the given id.");
        }
        if (Problem(rate.Status, rate.CreatedBy, "EXCHANGE_RATE") is { } problem)
        {
            return Result.Failure<ExchangeRateDto>(problem.Code!, problem.Message!);
        }
        if (await db.ExchangeRates.AnyAsync(x => x.Currency == rate.Currency && x.RateDate == rate.RateDate && x.Status == WorkflowStatus.Approved, ct))
        {
            return Result.Failure<ExchangeRateDto>("EXCHANGE_RATE_EXISTS",
                $"An approved {rate.Currency} rate for {rate.RateDate:yyyy-MM-dd} already exists; an approved rate is never edited.");
        }
        Approve(rate);
        await db.SaveChangesAsync(ct);
        return Result.Success(ToDto(rate));
    }

    public async Task<Result<IReadOnlyList<ExchangeRateDto>>> ListExchangeRatesAsync(string? currency, CancellationToken ct = default)
    {
        var rows = await db.ExchangeRates.AsNoTracking().Where(x => currency == null || x.Currency == currency)
            .OrderBy(x => x.Currency).ThenByDescending(x => x.RateDate).ToListAsync(ct);
        return Result.Success<IReadOnlyList<ExchangeRateDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<Result<PriceIndexDto>> CreatePriceIndexAsync(CreatePriceIndexRequest request, CancellationToken ct = default)
    {
        var series = request.Series?.Trim() ?? "";
        if (series.Length is 0 or > 30 || request.Year is < 1900 or > 2200 || request.Value <= 0m
            || string.IsNullOrWhiteSpace(request.Source) || request.Source.Length > 300 || request.Remarks?.Length > 1000)
        {
            return Result.Failure<PriceIndexDto>("VALIDATION_FAILED", "series (max 30), year, a value above 0 and the source (max 300) are required.");
        }
        var index = new PriceIndex { Series = series, Year = request.Year, Value = request.Value, Source = request.Source.Trim(), Remarks = request.Remarks };
        db.PriceIndices.Add(index);
        await db.SaveChangesAsync(ct);
        return Result.Success(ToDto(index));
    }

    public async Task<Result<PriceIndexDto>> ApprovePriceIndexAsync(Guid id, CancellationToken ct = default)
    {
        var index = await db.PriceIndices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (index is null)
        {
            return Result.Failure<PriceIndexDto>("PRICE_INDEX_NOT_FOUND", "No price index was found with the given id.");
        }
        if (Problem(index.Status, index.CreatedBy, "PRICE_INDEX") is { } problem)
        {
            return Result.Failure<PriceIndexDto>(problem.Code!, problem.Message!);
        }
        if (await db.PriceIndices.AnyAsync(x => x.Series == index.Series && x.Year == index.Year && x.Status == WorkflowStatus.Approved, ct))
        {
            return Result.Failure<PriceIndexDto>("PRICE_INDEX_EXISTS",
                $"An approved {index.Series} index for {index.Year} already exists; an approved index is never edited.");
        }
        Approve(index);
        await db.SaveChangesAsync(ct);
        return Result.Success(ToDto(index));
    }

    public async Task<Result<IReadOnlyList<PriceIndexDto>>> ListPriceIndicesAsync(string? series, CancellationToken ct = default)
    {
        var rows = await db.PriceIndices.AsNoTracking().Where(x => series == null || x.Series == series)
            .OrderBy(x => x.Series).ThenByDescending(x => x.Year).ToListAsync(ct);
        return Result.Success<IReadOnlyList<PriceIndexDto>>(rows.Select(ToDto).ToList());
    }

    private Result? Problem(WorkflowStatus status, Guid? createdBy, string prefix) =>
        status != WorkflowStatus.Draft ? Result.Failure($"{prefix}_NOT_DRAFT", "Only a Draft can be approved.")
        : MakerChecker.Refusal(currentUser, createdBy, $"CANNOT_APPROVE_OWN_{prefix}",
            "The creator cannot also approve it (maker-checker, CLAUDE.md §46).") is { } refusal
            ? Result.Failure(refusal.Code, refusal.Message)
            : null;

    private void Approve(ExchangeRate x) => (x.Status, x.ApprovedBy, x.ApprovedAt) = (WorkflowStatus.Approved, currentUser.AppUserId, DateTimeOffset.UtcNow);
    private void Approve(PriceIndex x) => (x.Status, x.ApprovedBy, x.ApprovedAt) = (WorkflowStatus.Approved, currentUser.AppUserId, DateTimeOffset.UtcNow);

    private static ExchangeRateDto ToDto(ExchangeRate x) =>
        new(x.Id, x.Currency, x.RateDate, x.PesosPerUnit, x.Source, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static PriceIndexDto ToDto(PriceIndex x) =>
        new(x.Id, x.Series, x.Year, x.Value, x.Source, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);
}
