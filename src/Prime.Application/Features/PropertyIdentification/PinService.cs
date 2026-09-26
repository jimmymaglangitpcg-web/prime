using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Application.Features.PropertyIdentification;

/// <summary>A property's PIN, its parts (for a permanent PIN) and every PIN it has carried.</summary>
public sealed record PropertyPinDto(
    Guid PropertyId,
    string Pin,
    PinKind? Kind,
    string? LguIndex,
    string? MunicipalityIndex,
    string? BarangayIndex,
    string? SectionIndex,
    int? ParcelNumber,
    Guid? ParcelId,
    IReadOnlyList<PinAssignmentDto> History);

public sealed record PinAssignmentDto(Guid Id, string Pin, PinKind Kind, Guid? ParcelId, string? SectionIndex, int? ParcelNumber,
    DateTimeOffset AssignedAt, Guid? AssignedByTransactionId, DateTimeOffset? RetiredAt, string? RetirementReason, Guid? PropertyTransactionId);

/// <summary>
/// Places the property's parcel in a tax map section, which gives the permanent PIN.
/// <see cref="ParcelNumber"/> is only for migrating an existing tax map (the PIN
/// scheme must allow manual entry); otherwise the next number in the section is used.
/// </summary>
public sealed record PlaceInSectionRequest(Guid ParcelId, Guid SectionId, int? ParcelNumber = null);

public interface IPinService
{
    Task<Result<PropertyPinDto>> GetAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Result<PropertyPinDto>> PlaceInSectionAsync(Guid propertyId, PlaceInSectionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The permanent PIN (docs/analysis/property-identification.md §3.3–§3.4; decisions
/// 3–6; MRPAAO Ch. II §1 C–D): built by the PropertyIdentificationNumber numbering
/// scheme from the assessor's index numbers and the section, with the scheme's
/// sequence as the parcel number. The sequence runs per section (its scope key) and
/// never goes back, so a parcel takes the next number after the highest ever used
/// and retired numbers are never given again. A section whose parcel-number width is
/// used up is refused (misc rule 2: open a new section).
/// </summary>
public sealed class PinService(
    IApplicationDbContext db,
    INumberSequenceAllocator allocator,
    ICurrentUserService currentUser,
    IClock clock) : IPinService
{
    public async Task<Result<PropertyPinDto>> GetAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Result.Failure<PropertyPinDto>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        var history = await db.PinAssignments.AsNoTracking().Include(x => x.Section)
            .Where(x => x.PropertyId == propertyId).OrderBy(x => x.AssignedAt).ToListAsync(cancellationToken);
        var current = history.SingleOrDefault(x => x.RetiredAt is null);
        NumberContext? parts = null;
        if (current is { Kind: PinKind.Permanent, BarangayId: { } barangayId })
        {
            parts = await PinContexts.ForBarangayAsync(db, barangayId, clock.Today.Year, current.Section!.IndexNumber, cancellationToken);
        }
        return Result.Success(new PropertyPinDto(propertyId, property.PropertyIdentificationNumber, current?.Kind,
            parts?.LguIndex, parts?.MunicipalityIndex, parts?.BarangayIndex, parts?.SectionIndex, current?.ParcelNumber, current?.ParcelId,
            history.Select(h => new PinAssignmentDto(h.Id, h.Pin, h.Kind, h.ParcelId, h.Section?.IndexNumber, h.ParcelNumber,
                h.AssignedAt, h.AssignedByTransactionId, h.RetiredAt, h.RetirementReason, h.PropertyTransactionId)).ToList()));
    }

    public async Task<Result<PropertyPinDto>> PlaceInSectionAsync(Guid propertyId, PlaceInSectionRequest request, CancellationToken cancellationToken = default)
    {
        var property = await db.Properties.FirstOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Fail("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        if (property.Status != RecordStatus.Active)
        {
            return Fail("PROPERTY_NOT_ACTIVE", "Only an active property can be given a PIN.");
        }
        var parcel = await db.Parcels.FirstOrDefaultAsync(x => x.Id == request.ParcelId && x.PropertyId == propertyId, cancellationToken);
        if (parcel is null)
        {
            return Fail("PARCEL_NOT_FOUND", "The parcel is not one of this property's.");
        }
        if (parcel.Status != RecordStatus.Active)
        {
            return Fail("PARCEL_NOT_ACTIVE", "Only an active parcel can be placed in a tax map section.");
        }
        var section = await db.TaxMapSections.Include(x => x.Barangay).FirstOrDefaultAsync(x => x.Id == request.SectionId, cancellationToken);
        if (section is null)
        {
            return Fail("TAX_MAP_SECTION_NOT_FOUND", "No tax map section was found with the given id.");
        }
        if (request.ParcelNumber is <= 0)
        {
            return Fail("VALIDATION_FAILED", "A parcel number is a positive whole number.");
        }

        var current = await db.PinAssignments.FirstOrDefaultAsync(x => x.PropertyId == propertyId && x.RetiredAt == null, cancellationToken);
        if (current?.Kind == PinKind.Permanent)
        {
            return Fail("PIN_ALREADY_PERMANENT",
                $"The property already has its permanent PIN {current.Pin}. A new PIN comes only with a subdivision, consolidation or tax map revision.");
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var assigned = await PermanentPins.AssignAsync(db, allocator, clock, property, parcel, section, request.ParcelNumber, current, null, cancellationToken);
            if (assigned.IsFailure)
            {
                return Fail(assigned.Code!, assigned.Message!);
            }
            currentUser.Reason = $"Permanent PIN {assigned.Value} (section {section.IndexNumber}, parcel {parcel.ParcelNumber}).";
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            return Fail("PIN_CONFLICT", "The PIN could not be saved because of a simultaneous change (e.g. the same parcel number). Nothing was changed; try again.");
        }
        return await GetAsync(propertyId, cancellationToken);
    }

    private static Result<PropertyPinDto> Fail(string code, string message) => Result.Failure<PropertyPinDto>(code, message);
}

/// <summary>
/// Giving and retiring PINs (docs/analysis/property-identification.md §3.3–§3.4), shared by
/// placing a parcel in a section and by subdivision and consolidation transactions (10a-3).
/// The caller owns the database transaction and saves; a failure may leave the sequence
/// advanced, so the caller must roll back.
/// </summary>
public static class PermanentPins
{
    /// <summary>
    /// The permanent PIN from the PIN scheme in force: the section's sequence is the parcel
    /// number (or <paramref name="typedNumber"/>, for migration, when the scheme allows manual
    /// entry). Retires <paramref name="current"/> (or records the pre-history PIN as retired),
    /// then sets the parcel's section and number and the property's PIN. Returns the PIN.
    /// </summary>
    public static async Task<Result<string>> AssignAsync(IApplicationDbContext db, INumberSequenceAllocator allocator, IClock clock,
        PropertyEntity property, Parcel parcel, TaxMapSection section, int? typedNumber, PinAssignment? current, Guid? transactionId,
        CancellationToken ct)
    {
        if (section.RetiredOn is not null || section.Barangay!.RetiredOn is not null)
        {
            return Fail("TAX_MAP_SECTION_RETIRED", $"Section {section.IndexNumber} or its barangay is retired; it takes no new parcels.");
        }
        if (section.BarangayId != parcel.BarangayId)
        {
            return Fail("VALIDATION_FAILED", $"Section {section.IndexNumber} is not in the parcel's barangay.");
        }

        var today = clock.Today;
        var scheme = await db.NumberingSchemes.InForce(today).FirstOrDefaultAsync(x => x.AppliesTo == NumberedDocumentKind.PropertyIdentificationNumber, ct);
        if (scheme is null || !NumberPattern.Tokens(scheme.Pattern).Contains("SECT"))
        {
            return Fail("PIN_SCHEME_NOT_SECTIONED",
                "No PIN numbering scheme with a section ({SECT}) is in force. Configure one under Forms & Numbering, e.g. {LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}.");
        }
        var context = await PinContexts.ForBarangayAsync(db, section.BarangayId, today.Year, section.IndexNumber, ct);
        if (NumberPattern.MissingValues(scheme.Pattern, context) is { Count: > 0 } missing)
        {
            return Fail("NUMBER_CONTEXT_MISSING",
                $"The PIN needs {string.Join(", ", missing)}; set the index numbers under Property Identification first.");
        }
        if (typedNumber is not null && !scheme.AllowManualEntry)
        {
            return Fail("NUMBER_MANUAL_ENTRY_NOT_ALLOWED", $"Parcel numbers are generated by the '{scheme.Name}' scheme; leave the number empty.");
        }

        var scope = NumberPattern.ScopeKey(scheme.Pattern, context);
        long number;
        if (typedNumber is { } typed)
        {
            number = typed;
            await allocator.ReserveAsync(scheme.Id, scope, typed, ct);
        }
        else
        {
            number = await allocator.NextAsync(scheme.Id, scope, ct);
        }
        if (!NumberPattern.Fits(scheme.Pattern, number))
        {
            return Fail("TAX_MAP_SECTION_FULL",
                $"Section {section.IndexNumber} has used every parcel number its PIN can hold. Draw the parcels on a new section (MRPAAO Ch. II §2 E.2).");
        }
        var pin = NumberPattern.Format(scheme.Pattern, context, number);
        if (await db.PinAssignments.AnyAsync(x => x.Pin == pin, ct)
            || await db.Properties.AnyAsync(x => x.Id != property.Id && x.PropertyIdentificationNumber == pin, ct)
            || await db.Parcels.AnyAsync(x => x.SectionId == section.Id && x.ParcelNumber == (int)number, ct))
        {
            return Fail("PIN_DUPLICATE", $"PIN {pin} has already been given; PINs are never reused.");
        }

        var now = clock.UtcNow;
        Retire(db, property, current, now, $"Superseded by the permanent PIN {pin}.", transactionId);
        // Retire first: one current assignment per property, and the old row may share the new PIN's number space.
        await db.SaveChangesAsync(ct);

        parcel.SectionId = section.Id;
        parcel.ParcelNumber = (int)number;
        property.PropertyIdentificationNumber = pin;
        db.PinAssignments.Add(new PinAssignment
        {
            PropertyId = property.Id, Pin = pin, Kind = PinKind.Permanent, ParcelId = parcel.Id, BarangayId = section.BarangayId,
            SectionId = section.Id, ParcelNumber = (int)number, AssignedAt = now, AssignedByTransactionId = transactionId,
        });
        return Result.Success(pin);
    }

    /// <summary>
    /// Retires the property's current PIN. A property from before PIN history was kept has no
    /// row yet: the PIN it had is recorded, already retired. Nothing is saved here.
    /// </summary>
    public static void Retire(IApplicationDbContext db, PropertyEntity property, PinAssignment? current, DateTimeOffset now, string reason, Guid? transactionId)
    {
        if (current is null)
        {
            db.PinAssignments.Add(new PinAssignment
            {
                PropertyId = property.Id, Pin = property.PropertyIdentificationNumber, Kind = PinKind.Registered,
                AssignedAt = property.CreatedAt, RetiredAt = now, RetirementReason = reason, PropertyTransactionId = transactionId,
            });
            return;
        }
        current.RetiredAt = now;
        current.RetirementReason = reason;
        current.PropertyTransactionId = transactionId;
    }

    private static Result<string> Fail(string code, string message) => Result.Failure<string>(code, message);
}

/// <summary>The assessor's index numbers of a barangay's location, for PIN patterns (docs/analysis/property-identification.md §3.3).</summary>
public static class PinContexts
{
    /// <summary>
    /// LGU index: the city's or Metro Manila municipality's own 3-digit number, else the
    /// province's. Municipality index: the city district's, else the municipality's
    /// 2-digit number. Values not yet set are null (the pattern then reports them missing).
    /// </summary>
    public static async Task<NumberContext> ForBarangayAsync(IApplicationDbContext db, Guid barangayId, int year, string? sectionIndex, CancellationToken ct)
    {
        var b = await db.Barangays.AsNoTracking().Where(x => x.Id == barangayId).Select(x => new
        {
            x.PsgcCode, x.PinIndexNumber, District = x.CityDistrict!.IndexNumber,
            MunicipalityCode = x.Municipality!.PsgcCode, MunicipalityIndex = x.Municipality.PinIndexNumber,
            ProvinceCode = x.Municipality.Province!.PsgcCode, ProvinceIndex = x.Municipality.Province.PinIndexNumber,
        }).SingleAsync(ct);
        var ownLguIndex = b.MunicipalityIndex is { Length: 3 };
        return new NumberContext(year, b.ProvinceCode, b.MunicipalityCode, b.PsgcCode,
            LguIndex: ownLguIndex ? b.MunicipalityIndex : b.ProvinceIndex,
            MunicipalityIndex: b.District ?? (ownLguIndex ? null : b.MunicipalityIndex),
            BarangayIndex: b.PinIndexNumber,
            SectionIndex: sectionIndex);
    }
}
