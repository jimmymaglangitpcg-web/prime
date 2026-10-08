using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Enums;

namespace Prime.Application.Features.MarketData;

public sealed record SaveBuildingPermitRequest(
    string PermitNumber, DateOnly IssuedOn, DateOnly? ProposedConstructionDate, DateOnly? ExpectedCompletionDate, string PermitteeName,
    string? PermitteeAddress, string? TaxDeclarationNumber, Guid MunicipalityId, Guid? BarangayId, string? BlockLotNumber, string? Street,
    BuildingPermitScope Scope, Guid? BuildingTypeId, Guid? StructuralTypeId, int? Storeys, decimal? TotalFloorArea, decimal? EstimatedCost,
    Guid? ClassificationId, DateOnly? ReceivedOn, string? Remarks);

public sealed record SaveMachineryRegistrationRequest(
    string CertificateNumber, DateOnly IssuedOn, string OwnerName, string? OwnerAddress, string? TaxDeclarationNumber, Guid MunicipalityId,
    Guid? BarangayId, string? Location, Guid? MachineryTypeId, string? Description, string? BrandModel, int? YearAcquired, string? Manufacturer,
    decimal? Cost, string? CurrentCondition, DateOnly? InstallationDate, DateOnly? ReceivedOn, string? Remarks);

/// <summary>The declared building or machinery the abstract refers to; null unlinks it.</summary>
public sealed record LinkAbstractRequest(Guid? TargetId);

public sealed class AbstractSearchRequest : PagedRequest
{
    public Guid? MunicipalityId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>Only those not linked to a declared building or machinery (discovery leads).</summary>
    public bool UnlinkedOnly { get; set; }
    public string? Search { get; set; }
    public bool IncludeCancelled { get; set; }
}

/// <param name="SuggestedBuildingId">A building on record with the same permit number in the same municipality, not yet linked.</param>
public sealed record BuildingPermitDto(
    Guid Id, string PermitNumber, DateOnly IssuedOn, DateOnly? ProposedConstructionDate, DateOnly? ExpectedCompletionDate, string PermitteeName,
    string? PermitteeAddress, string? TaxDeclarationNumber, Guid MunicipalityId, string MunicipalityName, Guid? BarangayId, string? BarangayName,
    string? BlockLotNumber, string? Street, BuildingPermitScope Scope, Guid? BuildingTypeId, string? BuildingTypeName, Guid? StructuralTypeId,
    string? StructuralTypeName, int? Storeys, decimal? TotalFloorArea, decimal? EstimatedCost, Guid? ClassificationId, string? ClassificationName,
    DateOnly? ReceivedOn, Guid? BuildingId, Guid? BuildingPropertyId, string? BuildingPin, Guid? SuggestedBuildingId, string? Remarks,
    DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt, string? CancellationReason);

/// <param name="SuggestedMachineryId">Machinery on record with the same registration number in the same municipality, not yet linked.</param>
public sealed record MachineryRegistrationDto(
    Guid Id, string CertificateNumber, DateOnly IssuedOn, string OwnerName, string? OwnerAddress, string? TaxDeclarationNumber,
    Guid MunicipalityId, string MunicipalityName, Guid? BarangayId, string? BarangayName, string? Location, Guid? MachineryTypeId, string? MachineryTypeName,
    string? Description, string? BrandModel, int? YearAcquired, string? Manufacturer, decimal? Cost, string? CurrentCondition, DateOnly? InstallationDate,
    DateOnly? ReceivedOn, Guid? MachineryId, Guid? MachineryPropertyId, string? MachineryPin, Guid? SuggestedMachineryId, string? Remarks,
    DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt, string? CancellationReason);

public interface IMarketDataAbstractService
{
    Task<Result<BuildingPermitDto>> CreatePermitAsync(SaveBuildingPermitRequest request, CancellationToken cancellationToken = default);
    Task<Result<BuildingPermitDto>> UpdatePermitAsync(Guid id, SaveBuildingPermitRequest request, CancellationToken cancellationToken = default);
    Task<Result<BuildingPermitDto>> LinkPermitAsync(Guid id, LinkAbstractRequest request, CancellationToken cancellationToken = default);
    Task<Result<BuildingPermitDto>> CancelPermitAsync(Guid id, CancelMarketDataRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<BuildingPermitDto>>> SearchPermitsAsync(AbstractSearchRequest request, CancellationToken cancellationToken = default);

    Task<Result<MachineryRegistrationDto>> CreateRegistrationAsync(SaveMachineryRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<Result<MachineryRegistrationDto>> UpdateRegistrationAsync(Guid id, SaveMachineryRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<Result<MachineryRegistrationDto>> LinkRegistrationAsync(Guid id, LinkAbstractRequest request, CancellationToken cancellationToken = default);
    Task<Result<MachineryRegistrationDto>> CancelRegistrationAsync(Guid id, CancelMarketDataRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<MachineryRegistrationDto>>> SearchRegistrationsAsync(AbstractSearchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstracts of building permits (LGC §290) and of certificates of machinery installation (LGC §210) received by the
/// assessor (LAM 2025 Book I pp.22–23; docs/analysis/smv-preparation-general-revision.md §4.1). One not yet linked to a
/// declared building or machinery is a discovery lead; a building or machinery on record with the same number is
/// suggested for the link, never linked automatically.
/// </summary>
public sealed class MarketDataAbstractService(IApplicationDbContext db, IClock clock, ICurrentUserService currentUser, IJurisdiction jurisdiction)
    : IMarketDataAbstractService
{
    // --- Building permits ---

    public async Task<Result<BuildingPermitDto>> CreatePermitAsync(SaveBuildingPermitRequest request, CancellationToken cancellationToken = default)
    {
        var p = new BuildingPermitAbstract();
        if (await ApplyAsync(p, request, cancellationToken) is { } error)
        {
            return Result.Failure<BuildingPermitDto>(error.Code, error.Message);
        }
        db.BuildingPermitAbstracts.Add(p);
        await db.SaveChangesAsync(cancellationToken);
        return await GetPermitAsync(p.Id, cancellationToken);
    }

    public async Task<Result<BuildingPermitDto>> UpdatePermitAsync(Guid id, SaveBuildingPermitRequest request, CancellationToken cancellationToken = default)
    {
        var p = await db.BuildingPermitAbstracts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p is null || p.CancelledAt is not null)
        {
            return p is null ? PermitNotFound() : Result.Failure<BuildingPermitDto>("MARKET_DATA_CANCELLED", "A cancelled record cannot be changed.");
        }
        if (p.MunicipalityId != request.MunicipalityId && p.BuildingId is not null)
        {
            return Result.Failure<BuildingPermitDto>("VALIDATION_FAILED", "Unlink the building before moving the permit to another city/municipality.");
        }
        if (await ApplyAsync(p, request, cancellationToken) is { } error)
        {
            return Result.Failure<BuildingPermitDto>(error.Code, error.Message);
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetPermitAsync(p.Id, cancellationToken);
    }

    public async Task<Result<BuildingPermitDto>> LinkPermitAsync(Guid id, LinkAbstractRequest request, CancellationToken cancellationToken = default)
    {
        var p = await db.BuildingPermitAbstracts.FirstOrDefaultAsync(x => x.Id == id && x.CancelledAt == null, cancellationToken);
        if (p is null)
        {
            return PermitNotFound();
        }
        if (request.TargetId is { } buildingId)
        {
            // The query filter hides buildings outside the jurisdiction, so they read as not found.
            var municipality = await db.Buildings.Where(x => x.Id == buildingId).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(cancellationToken);
            if (municipality is null)
            {
                return Result.Failure<BuildingPermitDto>("BUILDING_NOT_FOUND", "The specified building does not exist.");
            }
            if (municipality != p.MunicipalityId)
            {
                return Result.Failure<BuildingPermitDto>("VALIDATION_FAILED", "The building is in another city/municipality than the permit.");
            }
        }
        p.BuildingId = request.TargetId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetPermitAsync(p.Id, cancellationToken);
    }

    public async Task<Result<BuildingPermitDto>> CancelPermitAsync(Guid id, CancelMarketDataRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000)
        {
            return Result.Failure<BuildingPermitDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var p = await db.BuildingPermitAbstracts.FirstOrDefaultAsync(x => x.Id == id && x.CancelledAt == null, cancellationToken);
        if (p is null)
        {
            return PermitNotFound();
        }
        (p.CancelledAt, p.CancelledBy, p.CancellationReason) = (clock.UtcNow, currentUser.AppUserId, r.Reason.Trim());
        currentUser.Reason = p.CancellationReason;
        await db.SaveChangesAsync(cancellationToken);
        return await GetPermitAsync(p.Id, cancellationToken);
    }

    public async Task<Result<PagedResult<BuildingPermitDto>>> SearchPermitsAsync(AbstractSearchRequest r, CancellationToken cancellationToken = default)
    {
        var query = db.BuildingPermitAbstracts.AsNoTracking();
        if (!r.IncludeCancelled) query = query.Where(x => x.CancelledAt == null);
        if (r.MunicipalityId is { } m) query = query.Where(x => x.MunicipalityId == m);
        if (r.From is { } from) query = query.Where(x => x.IssuedOn >= from);
        if (r.To is { } to) query = query.Where(x => x.IssuedOn <= to);
        // A demolition permit leads to nothing to declare.
        if (r.UnlinkedOnly) query = query.Where(x => x.BuildingId == null && x.Scope != BuildingPermitScope.Demolition);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(x => x.PermitNumber.ToLower().Contains(term) || x.PermitteeName.ToLower().Contains(term)
                || (x.TaxDeclarationNumber != null && x.TaxDeclarationNumber.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectPermits(query.OrderByDescending(x => x.IssuedOn).ThenByDescending(x => x.CreatedAt).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize))
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<BuildingPermitDto> { Items = items, TotalCount = total, Page = r.Page, PageSize = r.PageSize });
    }

    private async Task<Result<BuildingPermitDto>> GetPermitAsync(Guid id, CancellationToken ct) =>
        await ProjectPermits(db.BuildingPermitAbstracts.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Result.Success(dto) : PermitNotFound();

    private IQueryable<BuildingPermitDto> ProjectPermits(IQueryable<BuildingPermitAbstract> query) => query.Select(x => new BuildingPermitDto(
        x.Id, x.PermitNumber, x.IssuedOn, x.ProposedConstructionDate, x.ExpectedCompletionDate, x.PermitteeName, x.PermitteeAddress, x.TaxDeclarationNumber,
        x.MunicipalityId, x.Municipality!.Name, x.BarangayId, x.Barangay != null ? x.Barangay.Name : null, x.BlockLotNumber, x.Street, x.Scope,
        x.BuildingTypeId, x.BuildingType != null ? x.BuildingType.Name : null, x.StructuralTypeId, x.StructuralType != null ? x.StructuralType.Name : null,
        x.Storeys, x.TotalFloorArea, x.EstimatedCost, x.ClassificationId, x.Classification != null ? x.Classification.Name : null, x.ReceivedOn,
        x.BuildingId, x.Building != null ? x.Building.PropertyId : null, x.Building != null ? x.Building.Property!.PropertyIdentificationNumber : null,
        x.BuildingId != null ? null : db.Buildings
            .Where(b => b.BuildingPermitNumber == x.PermitNumber && b.Property!.MunicipalityId == x.MunicipalityId
                && !db.BuildingPermitAbstracts.Any(o => o.BuildingId == b.Id && o.CancelledAt == null))
            .Select(b => (Guid?)b.Id).FirstOrDefault(),
        x.Remarks, x.CreatedAt, x.CancelledAt, x.CancellationReason));

    private async Task<MarketTransactionRules.Error?> ApplyAsync(BuildingPermitAbstract p, SaveBuildingPermitRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.PermitNumber) || r.PermitNumber.Length > 100 || string.IsNullOrWhiteSpace(r.PermitteeName) || r.PermitteeName.Length > 300
            || r.IssuedOn == default || !Enum.IsDefined(r.Scope) || r.PermitteeAddress?.Length > 1000 || r.TaxDeclarationNumber?.Length > 100
            || r.BlockLotNumber?.Length > 100 || r.Street?.Length > 300 || r.Remarks?.Length > 1000)
        {
            return new("VALIDATION_FAILED", "The permit number (max 100), date issued, permittee (max 300) and scope of work are required; a text field is too long.");
        }
        if (r.IssuedOn > clock.Today || r.ReceivedOn > clock.Today || r.ReceivedOn < r.IssuedOn || r.ExpectedCompletionDate < r.ProposedConstructionDate)
        {
            return new("VALIDATION_FAILED", "The permit is issued and received by today, received on or after issue; completion follows construction.");
        }
        if (r.Storeys < 1 || r.TotalFloorArea < 0 || r.EstimatedCost < 0)
        {
            return new("VALIDATION_FAILED", "Storeys start at 1; area and cost cannot be negative.");
        }
        if (await PlaceAsync(r.MunicipalityId, r.BarangayId, ct) is { } placeError)
        {
            return placeError;
        }
        if (r.BuildingTypeId is { } bt && !await db.BuildingTypes.AnyAsync(x => x.Id == bt, ct)
            || r.StructuralTypeId is { } st && !await db.StructuralTypes.AnyAsync(x => x.Id == st, ct)
            || r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, ct))
        {
            return new("LOOKUP_NOT_FOUND", "A kind of building, structural type or classification does not exist.");
        }
        var number = r.PermitNumber.Trim();
        if (await db.BuildingPermitAbstracts.AnyAsync(x => x.Id != p.Id && x.MunicipalityId == r.MunicipalityId && x.PermitNumber == number && x.CancelledAt == null, ct))
        {
            return new("BUILDING_PERMIT_DUPLICATE", $"Permit {number} is already recorded for this city/municipality.");
        }
        p.PermitNumber = number;
        p.IssuedOn = r.IssuedOn;
        p.ProposedConstructionDate = r.ProposedConstructionDate;
        p.ExpectedCompletionDate = r.ExpectedCompletionDate;
        p.PermitteeName = r.PermitteeName.Trim();
        p.PermitteeAddress = Clean(r.PermitteeAddress);
        p.TaxDeclarationNumber = Clean(r.TaxDeclarationNumber);
        p.MunicipalityId = r.MunicipalityId;
        p.BarangayId = r.BarangayId;
        p.BlockLotNumber = Clean(r.BlockLotNumber);
        p.Street = Clean(r.Street);
        p.Scope = r.Scope;
        p.BuildingTypeId = r.BuildingTypeId;
        p.StructuralTypeId = r.StructuralTypeId;
        p.Storeys = r.Storeys;
        p.TotalFloorArea = r.TotalFloorArea;
        p.EstimatedCost = r.EstimatedCost;
        p.ClassificationId = r.ClassificationId;
        p.ReceivedOn = r.ReceivedOn;
        p.Remarks = Clean(r.Remarks);
        return null;
    }

    // --- Machinery registrations ---

    public async Task<Result<MachineryRegistrationDto>> CreateRegistrationAsync(SaveMachineryRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var m = new MachineryRegistrationAbstract();
        if (await ApplyAsync(m, request, cancellationToken) is { } error)
        {
            return Result.Failure<MachineryRegistrationDto>(error.Code, error.Message);
        }
        db.MachineryRegistrationAbstracts.Add(m);
        await db.SaveChangesAsync(cancellationToken);
        return await GetRegistrationAsync(m.Id, cancellationToken);
    }

    public async Task<Result<MachineryRegistrationDto>> UpdateRegistrationAsync(Guid id, SaveMachineryRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var m = await db.MachineryRegistrationAbstracts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m is null || m.CancelledAt is not null)
        {
            return m is null ? RegistrationNotFound() : Result.Failure<MachineryRegistrationDto>("MARKET_DATA_CANCELLED", "A cancelled record cannot be changed.");
        }
        if (m.MunicipalityId != request.MunicipalityId && m.MachineryId is not null)
        {
            return Result.Failure<MachineryRegistrationDto>("VALIDATION_FAILED", "Unlink the machinery before moving the certificate to another city/municipality.");
        }
        if (await ApplyAsync(m, request, cancellationToken) is { } error)
        {
            return Result.Failure<MachineryRegistrationDto>(error.Code, error.Message);
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetRegistrationAsync(m.Id, cancellationToken);
    }

    public async Task<Result<MachineryRegistrationDto>> LinkRegistrationAsync(Guid id, LinkAbstractRequest request, CancellationToken cancellationToken = default)
    {
        var m = await db.MachineryRegistrationAbstracts.FirstOrDefaultAsync(x => x.Id == id && x.CancelledAt == null, cancellationToken);
        if (m is null)
        {
            return RegistrationNotFound();
        }
        if (request.TargetId is { } machineryId)
        {
            var municipality = await db.MachineryUnits.Where(x => x.Id == machineryId).Select(x => (Guid?)x.Property!.MunicipalityId).FirstOrDefaultAsync(cancellationToken);
            if (municipality is null)
            {
                return Result.Failure<MachineryRegistrationDto>("MACHINERY_NOT_FOUND", "The specified machinery does not exist.");
            }
            if (municipality != m.MunicipalityId)
            {
                return Result.Failure<MachineryRegistrationDto>("VALIDATION_FAILED", "The machinery is in another city/municipality than the certificate.");
            }
        }
        m.MachineryId = request.TargetId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetRegistrationAsync(m.Id, cancellationToken);
    }

    public async Task<Result<MachineryRegistrationDto>> CancelRegistrationAsync(Guid id, CancelMarketDataRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000)
        {
            return Result.Failure<MachineryRegistrationDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var m = await db.MachineryRegistrationAbstracts.FirstOrDefaultAsync(x => x.Id == id && x.CancelledAt == null, cancellationToken);
        if (m is null)
        {
            return RegistrationNotFound();
        }
        (m.CancelledAt, m.CancelledBy, m.CancellationReason) = (clock.UtcNow, currentUser.AppUserId, r.Reason.Trim());
        currentUser.Reason = m.CancellationReason;
        await db.SaveChangesAsync(cancellationToken);
        return await GetRegistrationAsync(m.Id, cancellationToken);
    }

    public async Task<Result<PagedResult<MachineryRegistrationDto>>> SearchRegistrationsAsync(AbstractSearchRequest r, CancellationToken cancellationToken = default)
    {
        var query = db.MachineryRegistrationAbstracts.AsNoTracking();
        if (!r.IncludeCancelled) query = query.Where(x => x.CancelledAt == null);
        if (r.MunicipalityId is { } mun) query = query.Where(x => x.MunicipalityId == mun);
        if (r.From is { } from) query = query.Where(x => x.IssuedOn >= from);
        if (r.To is { } to) query = query.Where(x => x.IssuedOn <= to);
        if (r.UnlinkedOnly) query = query.Where(x => x.MachineryId == null);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(x => x.CertificateNumber.ToLower().Contains(term) || x.OwnerName.ToLower().Contains(term)
                || (x.TaxDeclarationNumber != null && x.TaxDeclarationNumber.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectRegistrations(query.OrderByDescending(x => x.IssuedOn).ThenByDescending(x => x.CreatedAt).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize))
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<MachineryRegistrationDto> { Items = items, TotalCount = total, Page = r.Page, PageSize = r.PageSize });
    }

    private async Task<Result<MachineryRegistrationDto>> GetRegistrationAsync(Guid id, CancellationToken ct) =>
        await ProjectRegistrations(db.MachineryRegistrationAbstracts.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(ct) is { } dto
            ? Result.Success(dto)
            : RegistrationNotFound();

    private IQueryable<MachineryRegistrationDto> ProjectRegistrations(IQueryable<MachineryRegistrationAbstract> query) => query.Select(x => new MachineryRegistrationDto(
        x.Id, x.CertificateNumber, x.IssuedOn, x.OwnerName, x.OwnerAddress, x.TaxDeclarationNumber, x.MunicipalityId, x.Municipality!.Name,
        x.BarangayId, x.Barangay != null ? x.Barangay.Name : null, x.Location, x.MachineryTypeId, x.MachineryType != null ? x.MachineryType.Name : null,
        x.Description, x.BrandModel, x.YearAcquired, x.Manufacturer, x.Cost, x.CurrentCondition, x.InstallationDate, x.ReceivedOn,
        x.MachineryId, x.Machinery != null ? x.Machinery.PropertyId : null, x.Machinery != null ? x.Machinery.Property!.PropertyIdentificationNumber : null,
        x.MachineryId != null ? null : db.MachineryUnits
            .Where(u => u.EngineeringRegistrationNumber == x.CertificateNumber && u.Property!.MunicipalityId == x.MunicipalityId
                && !db.MachineryRegistrationAbstracts.Any(o => o.MachineryId == u.Id && o.CancelledAt == null))
            .Select(u => (Guid?)u.Id).FirstOrDefault(),
        x.Remarks, x.CreatedAt, x.CancelledAt, x.CancellationReason));

    private async Task<MarketTransactionRules.Error?> ApplyAsync(MachineryRegistrationAbstract m, SaveMachineryRegistrationRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.CertificateNumber) || r.CertificateNumber.Length > 100 || string.IsNullOrWhiteSpace(r.OwnerName) || r.OwnerName.Length > 300
            || r.IssuedOn == default || r.OwnerAddress?.Length > 1000 || r.TaxDeclarationNumber?.Length > 100 || r.Location?.Length > 300
            || r.Description?.Length > 500 || r.BrandModel?.Length > 200 || r.Manufacturer?.Length > 200 || r.CurrentCondition?.Length > 200 || r.Remarks?.Length > 1000)
        {
            return new("VALIDATION_FAILED", "The certificate number (max 100), date issued and owner (max 300) are required; a text field is too long.");
        }
        if (r.IssuedOn > clock.Today || r.ReceivedOn > clock.Today || r.ReceivedOn < r.IssuedOn || r.InstallationDate > clock.Today
            || r.YearAcquired is < 1800 || r.YearAcquired > clock.Today.Year)
        {
            return new("VALIDATION_FAILED", "Dates and the year acquired cannot be in the future; the certificate is received on or after issue.");
        }
        if (r.Cost < 0)
        {
            return new("VALIDATION_FAILED", "The cost cannot be negative.");
        }
        if (await PlaceAsync(r.MunicipalityId, r.BarangayId, ct) is { } placeError)
        {
            return placeError;
        }
        if (r.MachineryTypeId is { } mt && !await db.MachineryTypes.AnyAsync(x => x.Id == mt, ct))
        {
            return new("LOOKUP_NOT_FOUND", "The type of machinery does not exist.");
        }
        var number = r.CertificateNumber.Trim();
        if (await db.MachineryRegistrationAbstracts.AnyAsync(x => x.Id != m.Id && x.MunicipalityId == r.MunicipalityId && x.CertificateNumber == number && x.CancelledAt == null, ct))
        {
            return new("MACHINERY_REGISTRATION_DUPLICATE", $"Certificate {number} is already recorded for this city/municipality.");
        }
        m.CertificateNumber = number;
        m.IssuedOn = r.IssuedOn;
        m.OwnerName = r.OwnerName.Trim();
        m.OwnerAddress = Clean(r.OwnerAddress);
        m.TaxDeclarationNumber = Clean(r.TaxDeclarationNumber);
        m.MunicipalityId = r.MunicipalityId;
        m.BarangayId = r.BarangayId;
        m.Location = Clean(r.Location);
        m.MachineryTypeId = r.MachineryTypeId;
        m.Description = Clean(r.Description);
        m.BrandModel = Clean(r.BrandModel);
        m.YearAcquired = r.YearAcquired;
        m.Manufacturer = Clean(r.Manufacturer);
        m.Cost = r.Cost;
        m.CurrentCondition = Clean(r.CurrentCondition);
        m.InstallationDate = r.InstallationDate;
        m.ReceivedOn = r.ReceivedOn;
        m.Remarks = Clean(r.Remarks);
        return null;
    }

    private async Task<MarketTransactionRules.Error?> PlaceAsync(Guid municipalityId, Guid? barangayId, CancellationToken ct)
    {
        if (!await db.Municipalities.AnyAsync(x => x.Id == municipalityId, ct))
        {
            return new("MUNICIPALITY_NOT_FOUND", "The specified city/municipality does not exist.");
        }
        if (!jurisdiction.Allows(municipalityId))
        {
            return new(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        if (barangayId is { } b && !await db.Barangays.AnyAsync(x => x.Id == b && x.MunicipalityId == municipalityId, ct))
        {
            return new("BARANGAY_NOT_FOUND", "The barangay is not one of the named city/municipality.");
        }
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<BuildingPermitDto> PermitNotFound() =>
        Result.Failure<BuildingPermitDto>("BUILDING_PERMIT_NOT_FOUND", "No building permit abstract was found with the given id.");

    private static Result<MachineryRegistrationDto> RegistrationNotFound() =>
        Result.Failure<MachineryRegistrationDto>("MACHINERY_REGISTRATION_NOT_FOUND", "No machinery registration abstract was found with the given id.");
}
