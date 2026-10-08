using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Enums;

namespace Prime.Application.Features.MarketData;

public sealed record SaveMarketTransactionRequest(
    MarketDataSource Source, Guid? ConveyanceModeId, DateOnly TransactionDate, string? DocumentReference, string? DocumentFileNumber,
    string? GrantorNames, string? GranteeNames, string? GranteeAddress,
    Guid MunicipalityId, Guid? BarangayId, string? Location, Guid? PropertyId, string? Pin, string? TaxDeclarationNumber, string? LotNumber,
    string? PreviousTitleNumber, string? NewTitleNumber,
    bool ConveysLand, bool ConveysBuilding, Guid? ClassificationId, Guid? SubClassificationId, Guid? ActualUseId, Guid? BuildingTypeId, Guid? StructuralTypeId,
    decimal? LandArea, AreaMeasure LandAreaUnit, decimal? BuildingFloorArea, decimal Consideration, decimal? LandConsideration, string? Remarks);

/// <param name="Review">Accepted or Excluded; Excluded needs a reason.</param>
public sealed record ReviewMarketTransactionRequest(MarketDataReview Review, string? ExclusionReason, DateOnly? FieldValidatedOn, string? Note);

public sealed record CancelMarketDataRequest(string Reason);

public sealed class MarketTransactionSearchRequest : PagedRequest
{
    public Guid? MunicipalityId { get; set; }
    public Guid? BarangayId { get; set; }
    public Guid? ClassificationId { get; set; }
    public MarketDataReview? Review { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>Party, document, PIN, TD, lot or title.</summary>
    public string? Search { get; set; }
    public bool IncludeCancelled { get; set; }
}

public sealed record MarketTransactionDto(
    Guid Id, MarketDataSource Source, Guid? ConveyanceModeId, string? ConveyanceModeName, DateOnly TransactionDate, string? DocumentReference, string? DocumentFileNumber,
    string? GrantorNames, string? GranteeNames, string? GranteeAddress,
    Guid MunicipalityId, string MunicipalityName, Guid? BarangayId, string? BarangayName, string? Location, Guid? PropertyId, string? PropertyPin,
    string? Pin, string? TaxDeclarationNumber, string? LotNumber, string? PreviousTitleNumber, string? NewTitleNumber,
    bool ConveysLand, bool ConveysBuilding, Guid? ClassificationId, string? ClassificationName, Guid? SubClassificationId, string? SubClassificationName,
    Guid? ActualUseId, string? ActualUseName, Guid? BuildingTypeId, string? BuildingTypeName, Guid? StructuralTypeId, string? StructuralTypeName,
    decimal? LandArea, AreaMeasure LandAreaUnit, decimal? BuildingFloorArea, decimal Consideration, decimal? LandConsideration,
    decimal? LandUnitPrice, decimal? BuildingUnitPrice,
    MarketDataReview Review, string? ExclusionReason, DateOnly? FieldValidatedOn, string? ReviewNote, DateTimeOffset? ReviewedAt,
    Guid? PropertyTransactionId, string? ImportBatch, string? Remarks, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt, string? CancellationReason);

public interface IMarketTransactionService
{
    Task<Result<MarketTransactionDto>> CreateAsync(SaveMarketTransactionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Any change returns a reviewed record to Unreviewed: what was validated is no longer what is recorded.</summary>
    Task<Result<MarketTransactionDto>> UpdateAsync(Guid id, SaveMarketTransactionRequest request, CancellationToken cancellationToken = default);
    Task<Result<MarketTransactionDto>> ReviewAsync(Guid id, ReviewMarketTransactionRequest request, CancellationToken cancellationToken = default);
    Task<Result<MarketTransactionDto>> CancelAsync(Guid id, CancelMarketDataRequest request, CancellationToken cancellationToken = default);
    Task<Result<MarketTransactionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<MarketTransactionDto>>> SearchAsync(MarketTransactionSearchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Market transactions kept as evidence for the SMV (LAM 2025 Book I p.22; Book IV pp.104, 113;
/// docs/analysis/smv-preparation-general-revision.md §4.1). Recorded as stated; reviewed (accepted or excluded with a
/// reason) before a sales analysis may use one; cancelled, never deleted. Scoped to the office's jurisdiction.
/// </summary>
public sealed class MarketTransactionService(IApplicationDbContext db, IClock clock, ICurrentUserService currentUser, IJurisdiction jurisdiction)
    : IMarketTransactionService
{
    public async Task<Result<MarketTransactionDto>> CreateAsync(SaveMarketTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var t = new MarketTransaction();
        if (await MarketTransactionRules.ApplyAsync(db, clock, jurisdiction, t, request, cancellationToken) is { } error)
        {
            return Fail(error.Code, error.Message);
        }
        db.MarketTransactions.Add(t);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(t.Id, cancellationToken);
    }

    public async Task<Result<MarketTransactionDto>> UpdateAsync(Guid id, SaveMarketTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var t = await db.MarketTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null)
        {
            return NotFound();
        }
        if (t.CancelledAt is not null)
        {
            return Fail("MARKET_DATA_CANCELLED", "A cancelled record cannot be changed.");
        }
        if (await MarketTransactionRules.ApplyAsync(db, clock, jurisdiction, t, request, cancellationToken) is { } error)
        {
            return Fail(error.Code, error.Message);
        }
        (t.Review, t.ExclusionReason, t.ReviewedAt, t.ReviewedBy) = (MarketDataReview.Unreviewed, null, null, null);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(t.Id, cancellationToken);
    }

    public async Task<Result<MarketTransactionDto>> ReviewAsync(Guid id, ReviewMarketTransactionRequest r, CancellationToken cancellationToken = default)
    {
        if (r.Review == MarketDataReview.Unreviewed || !Enum.IsDefined(r.Review) || r.ExclusionReason?.Length > 500 || r.Note?.Length > 1000
            || r.FieldValidatedOn > clock.Today)
        {
            return Fail("VALIDATION_FAILED", "Accept or exclude; reason max 500, note max 1000; the field validation date cannot be in the future.");
        }
        var t = await db.MarketTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null)
        {
            return NotFound();
        }
        if (t.CancelledAt is not null)
        {
            return Fail("MARKET_DATA_CANCELLED", "A cancelled record cannot be reviewed.");
        }
        if (r.Review == MarketDataReview.Excluded && string.IsNullOrWhiteSpace(r.ExclusionReason))
        {
            return Fail("VALIDATION_FAILED", "State why the transaction is excluded from the analysis.");
        }
        if (r.Review == MarketDataReview.Accepted)
        {
            if (t.LandUnitPrice is null && t.BuildingUnitPrice is null)
            {
                return Fail("MARKET_DATA_NO_UNIT_PRICE",
                    "No unit price can be told from this record: give the area (and, when a building was conveyed with the land, the part of the consideration for the land).");
            }
            if (t.ClassificationId is null)
            {
                return Fail("MARKET_DATA_NO_CLASSIFICATION", "Give the classification before accepting: the analysis is by class and sub-class.");
            }
        }
        t.Review = r.Review;
        t.ExclusionReason = r.Review == MarketDataReview.Excluded ? r.ExclusionReason!.Trim() : null;
        t.FieldValidatedOn = r.FieldValidatedOn ?? t.FieldValidatedOn;
        t.ReviewNote = string.IsNullOrWhiteSpace(r.Note) ? t.ReviewNote : r.Note.Trim();
        t.ReviewedAt = clock.UtcNow;
        t.ReviewedBy = currentUser.AppUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(t.Id, cancellationToken);
    }

    public async Task<Result<MarketTransactionDto>> CancelAsync(Guid id, CancelMarketDataRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var t = await db.MarketTransactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null)
        {
            return NotFound();
        }
        if (t.CancelledAt is not null)
        {
            return Fail("MARKET_DATA_CANCELLED", "The record is already cancelled.");
        }
        (t.CancelledAt, t.CancelledBy, t.CancellationReason) = (clock.UtcNow, currentUser.AppUserId, r.Reason.Trim());
        currentUser.Reason = t.CancellationReason;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(t.Id, cancellationToken);
    }

    public async Task<Result<MarketTransactionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Project(db.MarketTransactions.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken) is { } dto
            ? Result.Success(dto)
            : NotFound();

    public async Task<Result<PagedResult<MarketTransactionDto>>> SearchAsync(MarketTransactionSearchRequest r, CancellationToken cancellationToken = default)
    {
        var query = db.MarketTransactions.AsNoTracking();
        if (!r.IncludeCancelled) query = query.Where(x => x.CancelledAt == null);
        if (r.MunicipalityId is { } m) query = query.Where(x => x.MunicipalityId == m);
        if (r.BarangayId is { } b) query = query.Where(x => x.BarangayId == b);
        if (r.ClassificationId is { } c) query = query.Where(x => x.ClassificationId == c);
        if (r.Review is { } review) query = query.Where(x => x.Review == review);
        if (r.From is { } from) query = query.Where(x => x.TransactionDate >= from);
        if (r.To is { } to) query = query.Where(x => x.TransactionDate <= to);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(x => (x.GrantorNames != null && x.GrantorNames.ToLower().Contains(term))
                || (x.GranteeNames != null && x.GranteeNames.ToLower().Contains(term))
                || (x.DocumentReference != null && x.DocumentReference.ToLower().Contains(term))
                || (x.Pin != null && x.Pin.ToLower().Contains(term)) || (x.TaxDeclarationNumber != null && x.TaxDeclarationNumber.ToLower().Contains(term))
                || (x.LotNumber != null && x.LotNumber.ToLower().Contains(term))
                || (x.NewTitleNumber != null && x.NewTitleNumber.ToLower().Contains(term))
                || (x.PreviousTitleNumber != null && x.PreviousTitleNumber.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt)
            .Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)).ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<MarketTransactionDto> { Items = items, TotalCount = total, Page = r.Page, PageSize = r.PageSize });
    }

    private static IQueryable<MarketTransactionDto> Project(IQueryable<MarketTransaction> query) => query.Select(x => new MarketTransactionDto(
        x.Id, x.Source, x.ConveyanceModeId, x.ConveyanceMode != null ? x.ConveyanceMode.Name : null, x.TransactionDate, x.DocumentReference, x.DocumentFileNumber,
        x.GrantorNames, x.GranteeNames, x.GranteeAddress,
        x.MunicipalityId, x.Municipality!.Name, x.BarangayId, x.Barangay != null ? x.Barangay.Name : null, x.Location, x.PropertyId,
        x.Property != null ? x.Property.PropertyIdentificationNumber : null,
        x.Pin, x.TaxDeclarationNumber, x.LotNumber, x.PreviousTitleNumber, x.NewTitleNumber,
        x.ConveysLand, x.ConveysBuilding, x.ClassificationId, x.Classification != null ? x.Classification.Name : null,
        x.SubClassificationId, x.SubClassification != null ? x.SubClassification.Name : null,
        x.ActualUseId, x.ActualUse != null ? x.ActualUse.Name : null, x.BuildingTypeId, x.BuildingType != null ? x.BuildingType.Name : null,
        x.StructuralTypeId, x.StructuralType != null ? x.StructuralType.Name : null,
        x.LandArea, x.LandAreaUnit, x.BuildingFloorArea, x.Consideration, x.LandConsideration, x.LandUnitPrice, x.BuildingUnitPrice,
        x.Review, x.ExclusionReason, x.FieldValidatedOn, x.ReviewNote, x.ReviewedAt,
        x.PropertyTransactionId, x.ImportBatch, x.Remarks, x.CreatedAt, x.CancelledAt, x.CancellationReason));

    private static Result<MarketTransactionDto> Fail(string code, string message) => Result.Failure<MarketTransactionDto>(code, message);

    private static Result<MarketTransactionDto> NotFound() => Fail("MARKET_TRANSACTION_NOT_FOUND", "No market transaction was found with the given id.");
}

/// <summary>The checks and normalisation shared by entry, import and the transfer prefill.</summary>
public static class MarketTransactionRules
{
    public sealed record Error(string Code, string Message);

    public static async Task<Error?> ApplyAsync(IApplicationDbContext db, IClock clock, IJurisdiction jurisdiction, MarketTransaction t,
        SaveMarketTransactionRequest r, CancellationToken ct)
    {
        if (!Enum.IsDefined(r.Source) || !Enum.IsDefined(r.LandAreaUnit) || r.TransactionDate == default
            || TooLong(r.DocumentReference, 200) || TooLong(r.DocumentFileNumber, 100) || TooLong(r.GrantorNames, 1000) || TooLong(r.GranteeNames, 1000)
            || TooLong(r.GranteeAddress, 1000) || TooLong(r.Location, 300) || TooLong(r.Pin, 100) || TooLong(r.TaxDeclarationNumber, 100)
            || TooLong(r.LotNumber, 100) || TooLong(r.PreviousTitleNumber, 100) || TooLong(r.NewTitleNumber, 100) || TooLong(r.Remarks, 1000))
        {
            return new("VALIDATION_FAILED", "A source and the transaction date are required; a text field exceeds its maximum length.");
        }
        if (r.TransactionDate > clock.Today)
        {
            return new("VALIDATION_FAILED", "The transaction date cannot be in the future.");
        }
        if (!r.ConveysLand && !r.ConveysBuilding)
        {
            return new("VALIDATION_FAILED", "Say what was conveyed: the land, a building, or both.");
        }
        if (r.Consideration < 0 || r.LandConsideration < 0 || r.LandArea < 0 || r.BuildingFloorArea < 0)
        {
            return new("VALIDATION_FAILED", "Amounts and areas cannot be negative.");
        }
        if (r.LandConsideration is not null && !(r.ConveysLand && r.ConveysBuilding))
        {
            return new("VALIDATION_FAILED", "The part of the consideration for the land is given only when a building was conveyed with it.");
        }
        if (r.LandConsideration > r.Consideration)
        {
            return new("VALIDATION_FAILED", "The part for the land cannot exceed the whole consideration.");
        }
        if (!await db.Municipalities.AnyAsync(x => x.Id == r.MunicipalityId, ct))
        {
            return new("MUNICIPALITY_NOT_FOUND", "The specified city/municipality does not exist.");
        }
        if (!jurisdiction.Allows(r.MunicipalityId))
        {
            return new(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        if (r.BarangayId is { } b && !await db.Barangays.AnyAsync(x => x.Id == b && x.MunicipalityId == r.MunicipalityId, ct))
        {
            return new("BARANGAY_NOT_FOUND", "The barangay is not one of the named city/municipality.");
        }
        if (r.PropertyId is { } p && !await db.Properties.AnyAsync(x => x.Id == p && x.MunicipalityId == r.MunicipalityId, ct))
        {
            return new("PROPERTY_NOT_FOUND", "The property does not exist in the named city/municipality.");
        }
        if (r.ConveyanceModeId is { } mode && !await db.ConveyanceModes.AnyAsync(x => x.Id == mode && x.IsActive, ct))
        {
            return new("CONVEYANCE_MODE_NOT_FOUND", "The mode of conveyance does not exist.");
        }
        if (r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, ct))
        {
            return new("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (r.SubClassificationId is { } sc && !await db.SubClassifications.AnyAsync(x => x.Id == sc, ct))
        {
            return new("SUB_CLASSIFICATION_NOT_FOUND", "The specified sub-class does not exist.");
        }
        if (r.ActualUseId is { } u && !await db.ActualUses.AnyAsync(x => x.Id == u, ct))
        {
            return new("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (r.BuildingTypeId is { } bt && !await db.BuildingTypes.AnyAsync(x => x.Id == bt, ct))
        {
            return new("BUILDING_TYPE_NOT_FOUND", "The specified kind of building does not exist.");
        }
        if (r.StructuralTypeId is { } st && !await db.StructuralTypes.AnyAsync(x => x.Id == st, ct))
        {
            return new("STRUCTURAL_TYPE_NOT_FOUND", "The specified structural type does not exist.");
        }

        t.Source = r.Source;
        t.ConveyanceModeId = r.ConveyanceModeId;
        t.TransactionDate = r.TransactionDate;
        t.DocumentReference = Clean(r.DocumentReference);
        t.DocumentFileNumber = Clean(r.DocumentFileNumber);
        t.GrantorNames = Clean(r.GrantorNames);
        t.GranteeNames = Clean(r.GranteeNames);
        t.GranteeAddress = Clean(r.GranteeAddress);
        t.MunicipalityId = r.MunicipalityId;
        t.BarangayId = r.BarangayId;
        t.Location = Clean(r.Location);
        t.PropertyId = r.PropertyId;
        t.Pin = Clean(r.Pin);
        t.TaxDeclarationNumber = Clean(r.TaxDeclarationNumber);
        t.LotNumber = Clean(r.LotNumber);
        t.PreviousTitleNumber = Clean(r.PreviousTitleNumber);
        t.NewTitleNumber = Clean(r.NewTitleNumber);
        t.ConveysLand = r.ConveysLand;
        t.ConveysBuilding = r.ConveysBuilding;
        t.ClassificationId = r.ClassificationId;
        t.SubClassificationId = r.SubClassificationId;
        t.ActualUseId = r.ActualUseId;
        t.BuildingTypeId = r.ConveysBuilding ? r.BuildingTypeId : null;
        t.StructuralTypeId = r.ConveysBuilding ? r.StructuralTypeId : null;
        t.LandArea = r.ConveysLand ? r.LandArea : null;
        t.LandAreaUnit = r.LandAreaUnit;
        t.BuildingFloorArea = r.ConveysBuilding ? r.BuildingFloorArea : null;
        t.Consideration = r.Consideration;
        t.LandConsideration = r.LandConsideration;
        t.Remarks = Clean(r.Remarks);
        (t.LandUnitPrice, t.BuildingUnitPrice) = MarketPrices.Compute(t.ConveysLand, t.ConveysBuilding, t.Consideration, t.LandConsideration,
            t.LandArea, t.BuildingFloorArea);
        return null;
    }

    private static bool TooLong(string? value, int max) => value?.Length > max;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
