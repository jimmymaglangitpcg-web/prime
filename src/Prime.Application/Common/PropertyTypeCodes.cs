namespace Prime.Application.Common;

/// <summary>
/// The fixed <c>PropertyType.Code</c> values Land/Building/Machinery are
/// resolved by (CLAUDE.md §28/§29's `PropertyTypeId` key, which none of
/// those three entities carry directly — see
/// <see cref="Prime.Application.Features.Valuation.ValuationService"/> and
/// <see cref="Prime.Application.Features.Assessments.AssessmentService"/>,
/// both of which need the same codes). Shared here so the two services
/// can't silently drift on what "the Land property type" means.
/// </summary>
public static class PropertyTypeCodes
{
    public const string Land = "LAND";
    public const string Building = "BUILDING";
    public const string Machinery = "MACHINERY";
}
