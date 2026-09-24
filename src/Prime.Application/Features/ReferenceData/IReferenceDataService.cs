namespace Prime.Application.Features.ReferenceData;

/// <summary>
/// Read-only lookups for populating selection lists (dropdowns) in
/// registration forms. No validation/audit concerns — these tables are
/// maintained through administration (CLAUDE.md §78), not through this
/// read surface.
/// </summary>
public interface IReferenceDataService
{
    Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MunicipalityDto>> GetMunicipalitiesAsync(Guid? provinceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BarangayDto>> GetBarangaysAsync(Guid? municipalityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetZonesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetClassificationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetActualUsesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetSubClassificationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetOwnershipTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetPropertyTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetRoadTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetConditionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetBuildingTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetStructuralTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupDto>> GetMachineryTypesAsync(CancellationToken cancellationToken = default);
}
