using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Domain.Entities;
using Prime.Domain.Entities.SwornStatements;
using Prime.Domain.Enums;

namespace Prime.Application.Features.SwornStatements;

public sealed record SaveSwornStatementRequest(
    string DeclarantName, Guid? DeclarantTaxpayerId, string? Citizenship, string? CivilStatus, string? PostalAddress, string? DeclarantTin,
    DeclarantCapacity Capacity, string? OwnerNames, Guid MunicipalityId, SwornStatementFilingBasis FilingBasis,
    DateOnly? SignedOn, string? SignedAt, bool Thumbmarked, string? Witness1, string? Witness2,
    DateOnly? SwornOn, string? SwornAt, string? AdministeringOfficer, string? OfficerTin,
    string? IdentityDocument, DateOnly? IdentityDocumentIssuedOn, string? IdentityDocumentIssuedAt,
    DateOnly? ReceivedOn, Guid? SupersedesId, string? Remarks);

public sealed record AddSwornStatementItemRequest(
    SwornStatementItemKind Kind, Guid? TaxDeclarationId, string? ExistingTdNumber, string? Location, decimal DeclaredMarketValue,
    string? LotNumber = null, string? BlockNumber = null, string? CadastralNumber = null, string? TitleNumber = null,
    decimal? Area = null, string? AreaUnit = null, Guid? ClassificationId = null,
    decimal? FloorArea = null, int? Storeys = null, string? Description = null, int? YearCompleted = null, Guid? ActualUseId = null, string? LotOwnerName = null,
    DateOnly? DateAcquired = null, DateOnly? DateOperationCommenced = null, decimal? AcquisitionCost = null, decimal? InstallationCost = null, decimal? Depreciation = null,
    Guid? ImprovementKindId = null, int? ProductiveCount = null, int? NonProductiveCount = null, string? AnnualProduct = null, string? Ages = null);

/// <summary>Filing: a typed index number, checked against the scheme in force; omitted, one is generated if a scheme is in force.</summary>
public sealed record FileSwornStatementRequest(string? Number);

public sealed record CancelSwornStatementRequest(string Reason);

public sealed record LinkSwornStatementItemRequest(Guid RpuId);

public sealed class SwornStatementSearchRequest : PagedRequest
{
    public Guid? MunicipalityId { get; set; }
    public string? Declarant { get; set; }
    public string? TdNumber { get; set; }
    public SwornStatementStatus? Status { get; set; }
    public DateOnly? ReceivedFrom { get; set; }
    public DateOnly? ReceivedTo { get; set; }
}

public sealed record SwornStatementItemDto(
    Guid Id, SwornStatementItemKind Kind, int Sequence, Guid? TaxDeclarationId, string? TdNumber, bool IsNew, Guid? PropertyId, Guid? RpuId, string? RpuNumber,
    string? Location, decimal DeclaredMarketValue,
    string? LotNumber, string? BlockNumber, string? CadastralNumber, string? TitleNumber, decimal? Area, string? AreaUnit, Guid? ClassificationId, string? ClassificationName,
    decimal? FloorArea, int? Storeys, string? Description, int? YearCompleted, Guid? ActualUseId, string? ActualUseName, string? LotOwnerName,
    DateOnly? DateAcquired, DateOnly? DateOperationCommenced, decimal? AcquisitionCost, decimal? InstallationCost, decimal? Depreciation,
    Guid? ImprovementKindId, string? ImprovementKindName, int? ProductiveCount, int? NonProductiveCount, string? AnnualProduct, string? Ages);

public sealed record SwornStatementDto(
    Guid Id, string? Number, SwornStatementStatus Status, string DeclarantName, Guid? DeclarantTaxpayerId, string? Citizenship, string? CivilStatus,
    string? PostalAddress, string? DeclarantTin, DeclarantCapacity Capacity, string? OwnerNames,
    Guid MunicipalityId, string MunicipalityName, string ProvinceName, SwornStatementFilingBasis FilingBasis,
    DateOnly? SignedOn, string? SignedAt, bool Thumbmarked, string? Witness1, string? Witness2,
    DateOnly? SwornOn, string? SwornAt, string? AdministeringOfficer, string? OfficerTin,
    string? IdentityDocument, DateOnly? IdentityDocumentIssuedOn, string? IdentityDocumentIssuedAt, DateOnly? ReceivedOn,
    Guid? SupersedesId, string? SupersedesNumber, Guid? SupersededById, DateTimeOffset? FiledAt, DateTimeOffset? CancelledAt, string? CancellationReason,
    string? Remarks, decimal TotalDeclaredValue, DateTimeOffset CreatedAt, IReadOnlyList<SwornStatementItemDto> Items);

public sealed record SwornStatementSummaryDto(
    Guid Id, string? Number, SwornStatementStatus Status, string DeclarantName, DeclarantCapacity Capacity, string MunicipalityName,
    DateOnly? ReceivedOn, int ItemCount, decimal TotalDeclaredValue, DateTimeOffset CreatedAt);

public interface ISwornStatementService
{
    Task<Result<SwornStatementDto>> CreateAsync(SaveSwornStatementRequest request, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> UpdateAsync(Guid id, SaveSwornStatementRequest request, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> AddItemAsync(Guid id, AddSwornStatementItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> RemoveItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> FileAsync(Guid id, FileSwornStatementRequest request, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> CancelAsync(Guid id, CancelSwornStatementRequest request, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> LinkItemAsync(Guid id, Guid itemId, LinkSwornStatementItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<SwornStatementDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SwornStatementSummaryDto>>> SearchAsync(SwornStatementSearchRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SwornStatementDto>>> ForPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The owner's sworn statement of market value (MRPAAO Att. 11;
/// docs/analysis/mrpaao-forms-model.md §16): intake as a draft, filing once
/// sworn and received, cancellation, correction by a superseding statement,
/// and linking a NEW item to its unit once registered. Declared values are
/// information only — nothing here touches valuation.
/// </summary>
public sealed class SwornStatementService(IApplicationDbContext db, INumberingService numbering, IClock clock, ICurrentUserService currentUser)
    : ISwornStatementService
{
    public async Task<Result<SwornStatementDto>> CreateAsync(SaveSwornStatementRequest request, CancellationToken cancellationToken = default)
    {
        var statement = new SwornStatement();
        if (await ApplyHeaderAsync(statement, request, cancellationToken) is { } error)
        {
            return error;
        }
        db.SwornStatements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(statement.Id, cancellationToken);
    }

    public async Task<Result<SwornStatementDto>> UpdateAsync(Guid id, SaveSwornStatementRequest request, CancellationToken cancellationToken = default)
    {
        var statement = await db.SwornStatements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }
        if (statement.Status != SwornStatementStatus.Draft)
        {
            return NotDraft();
        }
        if (await ApplyHeaderAsync(statement, request, cancellationToken) is { } error)
        {
            return error;
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(statement.Id, cancellationToken);
    }

    private async Task<Result<SwornStatementDto>?> ApplyHeaderAsync(SwornStatement s, SaveSwornStatementRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.DeclarantName) || r.DeclarantName.Length > 300 || !Enum.IsDefined(r.Capacity) || !Enum.IsDefined(r.FilingBasis)
            || TooLong(r.Citizenship, 100) || TooLong(r.CivilStatus, 50) || TooLong(r.PostalAddress, 1000) || TooLong(r.DeclarantTin, 50)
            || TooLong(r.OwnerNames, 2000) || TooLong(r.SignedAt, 300) || TooLong(r.Witness1, 300) || TooLong(r.Witness2, 300)
            || TooLong(r.SwornAt, 300) || TooLong(r.AdministeringOfficer, 300) || TooLong(r.OfficerTin, 50) || TooLong(r.IdentityDocument, 300)
            || TooLong(r.IdentityDocumentIssuedAt, 300) || TooLong(r.Remarks, 1000))
        {
            return Fail("VALIDATION_FAILED", "The declarant's name (max 300), capacity and filing basis are required; a text field exceeds its maximum length.");
        }
        if (r.Capacity != DeclarantCapacity.Owner && string.IsNullOrWhiteSpace(r.OwnerNames))
        {
            return Fail("VALIDATION_FAILED", "An administrator or authorized representative must state the owners' names (Att. 11, item 1).");
        }
        var today = clock.Today;
        if (r.SignedOn > today || r.SwornOn > today || r.ReceivedOn > today || (r.SwornOn is { } sworn && r.SignedOn is { } signed && sworn < signed)
            || (r.ReceivedOn is { } received && r.SwornOn is { } swornOn && received < swornOn))
        {
            return Fail("VALIDATION_FAILED", "Dates cannot be in the future; the statement is sworn on or after signing and received on or after swearing.");
        }
        if (!await db.Municipalities.AnyAsync(x => x.Id == r.MunicipalityId, ct))
        {
            return Fail("MUNICIPALITY_NOT_FOUND", "The specified city/municipality does not exist.");
        }
        if (r.DeclarantTaxpayerId is { } taxpayerId && !await db.Taxpayers.AnyAsync(x => x.Id == taxpayerId, ct))
        {
            return Fail("TAXPAYER_NOT_FOUND", "The specified taxpayer does not exist.");
        }
        if (r.SupersedesId is { } supersedes)
        {
            var old = await db.SwornStatements.AsNoTracking().Where(x => x.Id == supersedes).Select(x => new { x.Status, x.MunicipalityId }).FirstOrDefaultAsync(ct);
            if (old is null || old.Status != SwornStatementStatus.Filed || supersedes == s.Id)
            {
                return Fail("SWORN_STATEMENT_NOT_SUPERSEDABLE", "Only another filed statement (not already superseded or cancelled) can be corrected.");
            }
            if (old.MunicipalityId != r.MunicipalityId)
            {
                return Fail("SWORN_STATEMENT_OTHER_LGU", "A correction covers the same city/municipality as the statement it replaces.");
            }
            if (await db.SwornStatements.AnyAsync(x => x.SupersedesId == supersedes && x.Id != s.Id && x.Status != SwornStatementStatus.Cancelled, ct))
            {
                return Fail("SWORN_STATEMENT_ALREADY_CORRECTED", "Another statement already corrects that statement.");
            }
        }
        if (s.MunicipalityId != Guid.Empty && s.MunicipalityId != r.MunicipalityId
            && await db.SwornStatementItems.AnyAsync(x => x.SwornStatementId == s.Id && x.PropertyId != null, ct))
        {
            return Fail("SWORN_STATEMENT_OTHER_LGU", "Remove the items linked to declared properties before changing the city/municipality.");
        }

        s.DeclarantName = r.DeclarantName.Trim();
        s.DeclarantTaxpayerId = r.DeclarantTaxpayerId;
        s.Citizenship = Clean(r.Citizenship);
        s.CivilStatus = Clean(r.CivilStatus);
        s.PostalAddress = Clean(r.PostalAddress);
        s.DeclarantTin = Clean(r.DeclarantTin);
        s.Capacity = r.Capacity;
        s.OwnerNames = r.Capacity == DeclarantCapacity.Owner ? Clean(r.OwnerNames) : r.OwnerNames!.Trim();
        s.MunicipalityId = r.MunicipalityId;
        s.FilingBasis = r.FilingBasis;
        s.SignedOn = r.SignedOn;
        s.SignedAt = Clean(r.SignedAt);
        s.Thumbmarked = r.Thumbmarked;
        s.Witness1 = Clean(r.Witness1);
        s.Witness2 = Clean(r.Witness2);
        s.SwornOn = r.SwornOn;
        s.SwornAt = Clean(r.SwornAt);
        s.AdministeringOfficer = Clean(r.AdministeringOfficer);
        s.OfficerTin = Clean(r.OfficerTin);
        s.IdentityDocument = Clean(r.IdentityDocument);
        s.IdentityDocumentIssuedOn = r.IdentityDocumentIssuedOn;
        s.IdentityDocumentIssuedAt = Clean(r.IdentityDocumentIssuedAt);
        s.ReceivedOn = r.ReceivedOn;
        s.SupersedesId = r.SupersedesId;
        s.Remarks = Clean(r.Remarks);
        return null;
    }

    public async Task<Result<SwornStatementDto>> AddItemAsync(Guid id, AddSwornStatementItemRequest r, CancellationToken cancellationToken = default)
    {
        var statement = await db.SwornStatements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }
        if (statement.Status != SwornStatementStatus.Draft)
        {
            return NotDraft();
        }
        if (ValidateItem(r) is { } invalid)
        {
            return Fail("VALIDATION_FAILED", invalid);
        }
        var item = new SwornStatementItem
        {
            SwornStatementId = id, Kind = r.Kind, ExistingTdNumber = Clean(r.ExistingTdNumber), Location = Clean(r.Location),
            DeclaredMarketValue = r.DeclaredMarketValue,
        };
        if (r.TaxDeclarationId is { } tdId)
        {
            var td = await db.TaxDeclarations.AsNoTracking().Include(x => x.Rpu).Include(x => x.Property).ThenInclude(p => p!.Barangay)
                .FirstOrDefaultAsync(x => x.Id == tdId, cancellationToken);
            if (td is null)
            {
                return Fail("TAX_DECLARATION_NOT_FOUND", "The specified Tax Declaration does not exist.");
            }
            if (td.Status != WorkflowStatus.Approved)
            {
                return Fail("TAX_DECLARATION_NOT_IN_FORCE", "An existing declaration must be an approved (current) Tax Declaration.");
            }
            if (!Fits(r.Kind, td.Rpu!.RpuType))
            {
                return Fail("SWORN_STATEMENT_KIND_MISMATCH", $"A {r.Kind} item cannot declare a {td.Rpu.RpuType} unit's Tax Declaration.");
            }
            if (td.Property!.MunicipalityId != statement.MunicipalityId)
            {
                return OtherLgu();
            }
            item.TaxDeclarationId = td.Id;
            item.PropertyId = td.PropertyId;
            item.RpuId = td.RpuId;
            item.Location ??= string.Join(", ", new[] { td.Property.Street, td.Property.Barangay?.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        if (r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken)
            || r.ActualUseId is { } u && !await db.ActualUses.AnyAsync(x => x.Id == u, cancellationToken)
            || r.ImprovementKindId is { } k && !await db.ImprovementKinds.AnyAsync(x => x.Id == k, cancellationToken))
        {
            return Fail("REFERENCE_NOT_FOUND", "The specified classification, actual use or improvement kind does not exist.");
        }
        CopyKindColumns(item, r);
        item.Sequence = (await db.SwornStatementItems.Where(x => x.SwornStatementId == id).MaxAsync(x => (int?)x.Sequence, cancellationToken) ?? 0) + 1;
        db.SwornStatementItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private static string? ValidateItem(AddSwornStatementItemRequest r)
    {
        if (!Enum.IsDefined(r.Kind) || r.DeclaredMarketValue < 0 || TooLong(r.Location, 500) || TooLong(r.ExistingTdNumber, 100))
        {
            return "A kind and a declared value (not negative) are required; location max 500; TD number max 100.";
        }
        if (r.TaxDeclarationId is not null && !string.IsNullOrWhiteSpace(r.ExistingTdNumber))
        {
            return "Name the existing declaration either as a Tax Declaration in PRIME or as a number, not both.";
        }
        if (r.AcquisitionCost < 0 || r.InstallationCost < 0 || r.Depreciation < 0 || r.ProductiveCount < 0 || r.NonProductiveCount < 0 || r.Storeys < 1
            || r.YearCompleted is < 1800 or > 2200 || (r.DateOperationCommenced is { } op && r.DateAcquired is { } acq && op < acq)
            || TooLong(r.LotNumber, 100) || TooLong(r.BlockNumber, 100) || TooLong(r.CadastralNumber, 100) || TooLong(r.TitleNumber, 100)
            || TooLong(r.AreaUnit, 20) || TooLong(r.Description, 500) || TooLong(r.LotOwnerName, 300) || TooLong(r.AnnualProduct, 200) || TooLong(r.Ages, 200))
        {
            return "Amounts and counts cannot be negative; storeys at least 1; operation cannot start before acquisition; a text field exceeds its maximum length.";
        }
        return r.Kind switch
        {
            SwornStatementItemKind.Land when r.Area is not > 0 || string.IsNullOrWhiteSpace(r.AreaUnit) => "Land needs its area and unit (ha or sqm).",
            SwornStatementItemKind.Building when r.FloorArea is not > 0 => "A building needs its total floor area.",
            SwornStatementItemKind.Machinery when string.IsNullOrWhiteSpace(r.Description) => "A machine needs its description.",
            SwornStatementItemKind.OtherImprovement when r.ImprovementKindId is null => "Trees and plants need their kind.",
            _ => null,
        };
    }

    /// <summary>Only the columns of the item's kind are kept (Att. 11 parts A–C and Other Improvements).</summary>
    private static void CopyKindColumns(SwornStatementItem item, AddSwornStatementItemRequest r)
    {
        switch (r.Kind)
        {
            case SwornStatementItemKind.Land:
                (item.LotNumber, item.BlockNumber, item.CadastralNumber, item.TitleNumber) = (Clean(r.LotNumber), Clean(r.BlockNumber), Clean(r.CadastralNumber), Clean(r.TitleNumber));
                (item.Area, item.AreaUnit, item.ClassificationId) = (r.Area, r.AreaUnit!.Trim(), r.ClassificationId);
                break;
            case SwornStatementItemKind.Building:
                (item.FloorArea, item.Storeys, item.Description, item.YearCompleted) = (r.FloorArea, r.Storeys, Clean(r.Description), r.YearCompleted);
                (item.ActualUseId, item.LotOwnerName) = (r.ActualUseId, Clean(r.LotOwnerName));
                break;
            case SwornStatementItemKind.Machinery:
                (item.Description, item.DateAcquired, item.DateOperationCommenced) = (r.Description!.Trim(), r.DateAcquired, r.DateOperationCommenced);
                (item.AcquisitionCost, item.InstallationCost, item.Depreciation) = (r.AcquisitionCost, r.InstallationCost, r.Depreciation);
                break;
            case SwornStatementItemKind.OtherImprovement:
                (item.ImprovementKindId, item.ProductiveCount, item.NonProductiveCount) = (r.ImprovementKindId, r.ProductiveCount, r.NonProductiveCount);
                (item.AnnualProduct, item.Ages) = (Clean(r.AnnualProduct), Clean(r.Ages));
                break;
        }
    }

    /// <summary>Trees and plants are declared on the land's FAAS (MRPAAO Att. 1) or as their own unit.</summary>
    private static bool Fits(SwornStatementItemKind kind, RpuType type) => kind switch
    {
        SwornStatementItemKind.Land => type == RpuType.Land,
        SwornStatementItemKind.Building => type is RpuType.Building or RpuType.OtherImprovement,
        SwornStatementItemKind.Machinery => type == RpuType.Machinery,
        SwornStatementItemKind.OtherImprovement => type is RpuType.Land or RpuType.OtherImprovement,
        _ => false,
    };

    public async Task<Result<SwornStatementDto>> RemoveItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default)
    {
        var statement = await db.SwornStatements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }
        if (statement.Status != SwornStatementStatus.Draft)
        {
            return NotDraft();
        }
        var item = await db.SwornStatementItems.FirstOrDefaultAsync(x => x.Id == itemId && x.SwornStatementId == id, cancellationToken);
        if (item is null)
        {
            return Fail("SWORN_STATEMENT_ITEM_NOT_FOUND", "The item does not exist on this statement.");
        }
        // A draft is not yet a record: its items may be removed.
        db.SwornStatementItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SwornStatementDto>> FileAsync(Guid id, FileSwornStatementRequest request, CancellationToken cancellationToken = default)
    {
        var statement = await db.SwornStatements.Include(x => x.Items).Include(x => x.Municipality).ThenInclude(m => m!.Province)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }
        if (statement.Status != SwornStatementStatus.Draft)
        {
            return NotDraft();
        }
        var missing = new List<string>();
        if (statement.Items.Count == 0) missing.Add("at least one property");
        if (statement.SignedOn is null) missing.Add("the signing date");
        if (statement.SwornOn is null || statement.AdministeringOfficer is null) missing.Add("the jurat (sworn on, administering officer)");
        if (statement.Thumbmarked && (statement.Witness1 is null || statement.Witness2 is null)) missing.Add("two witnesses (thumbmarked)");
        if (statement.ReceivedOn is null) missing.Add("the date received");
        if (missing.Count > 0)
        {
            return Fail("SWORN_STATEMENT_INCOMPLETE", $"Before filing, record {string.Join(", ", missing)}.");
        }
        var propertyIds = statement.Items.Where(x => x.PropertyId != null).Select(x => x.PropertyId!.Value).Distinct().ToList();
        if (await db.Properties.AnyAsync(p => propertyIds.Contains(p.Id) && p.MunicipalityId != statement.MunicipalityId, cancellationToken))
        {
            return OtherLgu();
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var m = statement.Municipality!;
        var context = new Domain.DomainServices.NumberContext(statement.ReceivedOn!.Value.Year, m.Province?.PsgcCode, m.PsgcCode);
        var typed = request.Number?.Trim();
        if (string.IsNullOrEmpty(typed))
        {
            var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.SwornStatement, context, statement.ReceivedOn.Value, cancellationToken);
            if (number.IsFailure)
            {
                return Fail(number.Code!, number.Message!);
            }
            statement.Number = number.Value;
        }
        else
        {
            var number = await numbering.AssignAsync(NumberedDocumentKind.SwornStatement, context, typed, statement.ReceivedOn.Value, cancellationToken);
            if (number.IsFailure)
            {
                return Fail(number.Code!, number.Message!);
            }
            if (await db.SwornStatements.AnyAsync(x => x.Number == number.Value, cancellationToken))
            {
                return Fail("SWORN_STATEMENT_NUMBER_TAKEN", "Another statement already has this index number.");
            }
            statement.Number = number.Value;
        }
        if (statement.SupersedesId is { } supersedes)
        {
            var old = await db.SwornStatements.FirstAsync(x => x.Id == supersedes, cancellationToken);
            if (old.Status != SwornStatementStatus.Filed)
            {
                return Fail("SWORN_STATEMENT_NOT_SUPERSEDABLE", "The statement this one corrects is no longer filed.");
            }
            old.Status = SwornStatementStatus.Superseded;
        }
        statement.Status = SwornStatementStatus.Filed;
        statement.FiledAt = clock.UtcNow;
        statement.FiledBy = currentUser.AppUserId;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SwornStatementDto>> CancelAsync(Guid id, CancelSwornStatementRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason (max 1000) is required.");
        }
        var statement = await db.SwornStatements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }
        if (statement.Status is not (SwornStatementStatus.Draft or SwornStatementStatus.Filed))
        {
            return Fail("SWORN_STATEMENT_NOT_CANCELLABLE", "Only a draft or a filed statement can be cancelled.");
        }
        if (statement.Status == SwornStatementStatus.Filed && statement.SupersedesId is { } supersedes)
        {
            // Cancelling a filed correction puts the statement it replaced back in force.
            var old = await db.SwornStatements.FirstAsync(x => x.Id == supersedes, cancellationToken);
            if (old.Status == SwornStatementStatus.Superseded)
            {
                old.Status = SwornStatementStatus.Filed;
            }
        }
        currentUser.Reason = request.Reason.Trim();
        statement.Status = SwornStatementStatus.Cancelled;
        statement.CancelledAt = clock.UtcNow;
        statement.CancelledBy = currentUser.AppUserId;
        statement.CancellationReason = request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SwornStatementDto>> LinkItemAsync(Guid id, Guid itemId, LinkSwornStatementItemRequest request, CancellationToken cancellationToken = default)
    {
        var statement = await db.SwornStatements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (statement is null)
        {
            return NotFound();
        }
        if (statement.Status is SwornStatementStatus.Cancelled)
        {
            return Fail("SWORN_STATEMENT_CANCELLED", "A cancelled statement cannot be changed.");
        }
        var item = await db.SwornStatementItems.FirstOrDefaultAsync(x => x.Id == itemId && x.SwornStatementId == id, cancellationToken);
        if (item is null)
        {
            return Fail("SWORN_STATEMENT_ITEM_NOT_FOUND", "The item does not exist on this statement.");
        }
        if (item.RpuId is not null)
        {
            return Fail("SWORN_STATEMENT_ITEM_LINKED", "The item already refers to a unit; only a NEW item is linked after registration.");
        }
        var rpu = await db.RealPropertyUnits.AsNoTracking().Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == request.RpuId, cancellationToken);
        if (rpu is null)
        {
            return Fail("RPU_NOT_FOUND", "The specified RPU does not exist.");
        }
        if (!Fits(item.Kind, rpu.RpuType))
        {
            return Fail("SWORN_STATEMENT_KIND_MISMATCH", $"A {item.Kind} item cannot be linked to a {rpu.RpuType} unit.");
        }
        if (rpu.Property!.MunicipalityId != statement.MunicipalityId)
        {
            return OtherLgu();
        }
        item.PropertyId = rpu.PropertyId;
        item.RpuId = rpu.Id;
        currentUser.Reason = "NEW item linked to its registered unit";
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SwornStatementDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(db.SwornStatements.Where(x => x.Id == id), cancellationToken);
        return list.Count == 0 ? NotFound() : Result.Success(list[0]);
    }

    public async Task<Result<IReadOnlyList<SwornStatementDto>>> ForPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<SwornStatementDto>>(await LoadAsync(
            db.SwornStatements.Where(s => s.Status != SwornStatementStatus.Draft && s.Items.Any(i => i.PropertyId == propertyId))
                .OrderByDescending(s => s.ReceivedOn).ThenByDescending(s => s.CreatedAt), cancellationToken));

    public async Task<Result<PagedResult<SwornStatementSummaryDto>>> SearchAsync(SwornStatementSearchRequest r, CancellationToken cancellationToken = default)
    {
        var query = db.SwornStatements.AsNoTracking();
        if (r.MunicipalityId is { } m) query = query.Where(x => x.MunicipalityId == m);
        if (r.Status is { } status) query = query.Where(x => x.Status == status);
        if (r.ReceivedFrom is { } from) query = query.Where(x => x.ReceivedOn >= from);
        if (r.ReceivedTo is { } to) query = query.Where(x => x.ReceivedOn <= to);
        if (!string.IsNullOrWhiteSpace(r.Declarant))
        {
            // .ToLower().Contains() as in PropertyService (Application does not reference Npgsql).
            var term = r.Declarant.Trim().ToLower();
            query = query.Where(x => x.DeclarantName.ToLower().Contains(term) || (x.OwnerNames != null && x.OwnerNames.ToLower().Contains(term))
                || (x.Number != null && x.Number.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(r.TdNumber))
        {
            var td = r.TdNumber.Trim();
            query = query.Where(x => x.Items.Any(i => i.ExistingTdNumber == td || (i.TaxDeclaration != null && i.TaxDeclaration.TaxDeclarationNumber == td)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(x => new SwornStatementSummaryDto(x.Id, x.Number, x.Status, x.DeclarantName, x.Capacity, x.Municipality!.Name, x.ReceivedOn,
                x.Items.Count, x.Items.Sum(i => (decimal?)i.DeclaredMarketValue) ?? 0m, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<SwornStatementSummaryDto> { Items = items, TotalCount = total, Page = r.Page, PageSize = r.PageSize });
    }

    private async Task<List<SwornStatementDto>> LoadAsync(IQueryable<SwornStatement> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(x => x.Municipality).ThenInclude(m => m!.Province).Include(x => x.Supersedes)
            .Include(x => x.Items).ThenInclude(i => i.TaxDeclaration)
            .Include(x => x.Items).ThenInclude(i => i.Rpu)
            .Include(x => x.Items).ThenInclude(i => i.Classification)
            .Include(x => x.Items).ThenInclude(i => i.ActualUse)
            .Include(x => x.Items).ThenInclude(i => i.ImprovementKind)
            .ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToList();
        var correctedBy = await db.SwornStatements.AsNoTracking()
            .Where(x => x.SupersedesId != null && ids.Contains(x.SupersedesId.Value) && x.Status != SwornStatementStatus.Cancelled)
            .Select(x => new { Old = x.SupersedesId!.Value, x.Id }).ToListAsync(ct);
        return rows.Select(s => new SwornStatementDto(
            s.Id, s.Number, s.Status, s.DeclarantName, s.DeclarantTaxpayerId, s.Citizenship, s.CivilStatus, s.PostalAddress, s.DeclarantTin,
            s.Capacity, s.OwnerNames, s.MunicipalityId, s.Municipality!.Name, s.Municipality.Province?.Name ?? "", s.FilingBasis,
            s.SignedOn, s.SignedAt, s.Thumbmarked, s.Witness1, s.Witness2, s.SwornOn, s.SwornAt, s.AdministeringOfficer, s.OfficerTin,
            s.IdentityDocument, s.IdentityDocumentIssuedOn, s.IdentityDocumentIssuedAt, s.ReceivedOn,
            s.SupersedesId, s.Supersedes?.Number, correctedBy.FirstOrDefault(c => c.Old == s.Id)?.Id, s.FiledAt, s.CancelledAt, s.CancellationReason,
            s.Remarks, s.Items.Sum(i => i.DeclaredMarketValue), s.CreatedAt,
            s.Items.OrderBy(i => i.Sequence).Select(ToDto).ToList())).ToList();
    }

    private static SwornStatementItemDto ToDto(SwornStatementItem i) => new(
        i.Id, i.Kind, i.Sequence, i.TaxDeclarationId, i.TaxDeclaration?.TaxDeclarationNumber ?? i.ExistingTdNumber,
        i.TaxDeclarationId is null && i.ExistingTdNumber is null, i.PropertyId, i.RpuId, i.Rpu?.RpuNumber, i.Location, i.DeclaredMarketValue,
        i.LotNumber, i.BlockNumber, i.CadastralNumber, i.TitleNumber, i.Area, i.AreaUnit, i.ClassificationId, i.Classification?.Name,
        i.FloorArea, i.Storeys, i.Description, i.YearCompleted, i.ActualUseId, i.ActualUse?.Name, i.LotOwnerName,
        i.DateAcquired, i.DateOperationCommenced, i.AcquisitionCost, i.InstallationCost, i.Depreciation,
        i.ImprovementKindId, i.ImprovementKind?.Name, i.ProductiveCount, i.NonProductiveCount, i.AnnualProduct, i.Ages);

    private static bool TooLong(string? value, int max) => value?.Length > max;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Result<SwornStatementDto> Fail(string code, string message) => Result.Failure<SwornStatementDto>(code, message);
    private static Result<SwornStatementDto> NotFound() => Fail("SWORN_STATEMENT_NOT_FOUND", "The sworn statement does not exist.");
    private static Result<SwornStatementDto> NotDraft() => Fail("SWORN_STATEMENT_NOT_DRAFT", "Only a draft statement can be changed; correct a filed one with a new statement.");
    private static Result<SwornStatementDto> OtherLgu() =>
        Fail("SWORN_STATEMENT_OTHER_LGU", "A sworn statement covers properties in one city/municipality only (Att. 11, Note 1).");
}

/// <summary>
/// The Att. 11 form of a statement (FormSubjectType.SwornStatement). A draft
/// previews — printed pre-filled for the affiant to sign and swear — and a
/// filed statement can be issued, freezing it as received.
/// </summary>
public sealed class SwornStatementFormDataProvider(ISwornStatementService statements) : Forms.IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.SwornStatement;

    public async Task<Forms.FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await statements.GetAsync(subjectId, cancellationToken);
        if (result.IsFailure)
        {
            return null;
        }
        var s = result.Value;
        var blocker = s.Status switch
        {
            SwornStatementStatus.Draft => "Only a filed sworn statement can be issued; preview it to print for signing.",
            SwornStatementStatus.Cancelled => "A cancelled sworn statement cannot be issued.",
            _ => null,
        };
        var items = s.Items;
        var data = Forms.FormData.ToJson(new
        {
            statement = s,
            land = items.Where(i => i.Kind == SwornStatementItemKind.Land),
            buildings = items.Where(i => i.Kind == SwornStatementItemKind.Building),
            machinery = items.Where(i => i.Kind == SwornStatementItemKind.Machinery),
            trees = items.Where(i => i.Kind == SwornStatementItemKind.OtherImprovement),
        });
        return new Forms.FormSubjectData(s.Number, data, blocker);
    }
}
