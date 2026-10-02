using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;

namespace Prime.Application.Features.PropertyIdentification;

public sealed record BarangayPartRequest(Guid BarangayId, decimal Area, decimal AssessedValueShare);

/// <param name="Parts">Empty clears them; otherwise at least two barangays of the property's municipality.</param>
public sealed record SetBarangayPartsRequest(IReadOnlyList<BarangayPartRequest> Parts, string Reason);

public sealed record BarangayPartDto(int Sequence, Guid BarangayId, string BarangayName, string? BarangayIndex, decimal Area, decimal AssessedValueShare);

public interface IBarangayPartService
{
    Task<Result<IReadOnlyList<BarangayPartDto>>> ListAsync(Guid propertyId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BarangayPartDto>>> SetAsync(Guid propertyId, SetBarangayPartsRequest request, CancellationToken ct = default);
}

/// <summary>
/// A parcel crossed by a barangay line (LAM Bk II p.59 §9.4; docs/analysis/identification-numbering.md §4.3, Q10):
/// the part in each barangay with its area and share of the assessed value, printed as the annotation. The property
/// carries the barangay (and PIN) of its larger part. A municipal or city line makes separate parcels instead, so
/// the parts stay within one municipality.
/// </summary>
public sealed class BarangayPartService(IApplicationDbContext db, ICurrentUserService currentUser) : IBarangayPartService
{
    public async Task<Result<IReadOnlyList<BarangayPartDto>>> ListAsync(Guid propertyId, CancellationToken ct = default) =>
        Result.Success<IReadOnlyList<BarangayPartDto>>(await db.PropertyBarangayParts.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderBy(x => x.Sequence)
            .Select(x => new BarangayPartDto(x.Sequence, x.BarangayId, x.Barangay!.Name, x.Barangay.PinIndexNumber, x.Area, x.AssessedValueShare))
            .ToListAsync(ct));

    public async Task<Result<IReadOnlyList<BarangayPartDto>>> SetAsync(Guid propertyId, SetBarangayPartsRequest request, CancellationToken ct = default)
    {
        var property = await db.Properties.FirstOrDefaultAsync(x => x.Id == propertyId, ct);
        if (property is null)
        {
            return Fail("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        var parts = request.Parts ?? [];
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
        {
            return Fail("VALIDATION_FAILED", "A reason (max 500) is required.");
        }
        if (parts.Count == 1 || parts.Any(p => p.Area <= 0m || p.AssessedValueShare is < 0m or > 100m)
            || parts.Select(p => p.BarangayId).Distinct().Count() != parts.Count)
        {
            return Fail("VALIDATION_FAILED", "A crossed parcel has at least two parts, each in a different barangay, with an area above 0 and a share from 0 to 100.");
        }
        if (parts.Count > 0 && parts.Sum(p => p.AssessedValueShare) != 100m)
        {
            return Fail("VALIDATION_FAILED", $"The assessed-value shares total {parts.Sum(p => p.AssessedValueShare)}%, not 100%.");
        }
        var ids = parts.Select(p => p.BarangayId).ToList();
        var barangays = await db.Barangays.Where(b => ids.Contains(b.Id)).Select(b => new { b.Id, b.MunicipalityId, b.Name }).ToListAsync(ct);
        if (barangays.Count != parts.Count || barangays.Any(b => b.MunicipalityId != property.MunicipalityId))
        {
            return Fail("BARANGAY_PART_INVALID", "Every part is in a barangay of the property's municipality; a municipal or city line makes separate parcels.");
        }
        if (parts.Count > 0)
        {
            var largest = parts.Max(p => p.Area);
            if (parts.All(p => p.BarangayId != property.BarangayId) || parts.Single(p => p.BarangayId == property.BarangayId).Area < largest)
            {
                var name = barangays.First(b => b.Id == parts.First(p => p.Area == largest).BarangayId).Name;
                return Fail("BARANGAY_PART_NOT_LARGEST",
                    $"The property carries the barangay of its larger part ({name}); record the property under that barangay, or correct the areas.");
            }
        }
        db.PropertyBarangayParts.RemoveRange(await db.PropertyBarangayParts.Where(x => x.PropertyId == propertyId).ToListAsync(ct));
        foreach (var (p, i) in parts.Select((p, i) => (p, i)))
        {
            db.PropertyBarangayParts.Add(new PropertyBarangayPart
            {
                PropertyId = propertyId, Sequence = i + 1, BarangayId = p.BarangayId, Area = p.Area, AssessedValueShare = p.AssessedValueShare,
            });
        }
        currentUser.Reason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return await ListAsync(propertyId, ct);
    }

    private static Result<IReadOnlyList<BarangayPartDto>> Fail(string code, string message) => Result.Failure<IReadOnlyList<BarangayPartDto>>(code, message);
}
