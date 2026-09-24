using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Reference;

namespace Prime.Application.Features.ReferenceData;

public sealed class ReferenceDataService(IApplicationDbContext db) : IReferenceDataService
{
    public async Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync(CancellationToken cancellationToken = default) =>
        await db.Provinces.Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ProvinceDto(x.Id, x.PsgcCode, x.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MunicipalityDto>> GetMunicipalitiesAsync(Guid? provinceId, CancellationToken cancellationToken = default)
    {
        var query = db.Municipalities.Where(x => x.IsActive);
        if (provinceId is not null)
        {
            query = query.Where(x => x.ProvinceId == provinceId);
        }
        return await query.OrderBy(x => x.Name)
            .Select(x => new MunicipalityDto(x.Id, x.PsgcCode, x.Name, x.ProvinceId, x.IsCity))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BarangayDto>> GetBarangaysAsync(Guid? municipalityId, CancellationToken cancellationToken = default)
    {
        var query = db.Barangays.Where(x => x.IsActive);
        if (municipalityId is not null)
        {
            query = query.Where(x => x.MunicipalityId == municipalityId);
        }
        return await query.OrderBy(x => x.Name)
            .Select(x => new BarangayDto(x.Id, x.PsgcCode, x.Name, x.MunicipalityId))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<LookupDto>> GetZonesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.Zones, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetClassificationsAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.Classifications, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetActualUsesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.ActualUses, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetSubClassificationsAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.SubClassifications, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetOwnershipTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.OwnershipTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetPropertyTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.PropertyTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetRoadTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.RoadTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetConditionsAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.Conditions, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetBuildingTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.BuildingTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetStructuralTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.StructuralTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetMachineryTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.MachineryTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetTaxTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.TaxTypes, cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetAnnotationTypesAsync(CancellationToken cancellationToken = default) =>
        GetLookupAsync(db.AnnotationTypes, cancellationToken);

    private static async Task<IReadOnlyList<LookupDto>> GetLookupAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        where T : LookupEntity
    {
        return await query.Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new LookupDto(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);
    }
}
