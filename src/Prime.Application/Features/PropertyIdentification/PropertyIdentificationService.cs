using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Application.Features.PropertyIdentification;

// --- DTOs ---

public sealed record ProvinceIndexDto(Guid Id, string Name, string PsgcCode, string? PinIndexNumber);

public sealed record MunicipalityIndexDto(Guid Id, Guid ProvinceId, string Name, string PsgcCode, bool IsCity, string? PinIndexNumber, int DistrictCount);

public sealed record CityDistrictDto(Guid Id, Guid MunicipalityId, string IndexNumber, string Name, bool IsActive);

public sealed record BarangayIndexDto(
    Guid Id,
    Guid MunicipalityId,
    string Name,
    string PsgcCode,
    Guid? CityDistrictId,
    string? CityDistrictIndexNumber,
    string? PinIndexNumber,
    DateOnly? RetiredOn,
    string? RetirementReason,
    Guid? SplitFromBarangayId,
    int SectionCount);

public sealed record TaxMapSectionDto(Guid Id, Guid BarangayId, string IndexNumber, string? Remarks, Guid? SplitFromSectionId, DateOnly? RetiredOn, string? RetirementReason);

/// <summary>A new or changed index number. A reason is required when a number already set is changed (audited).</summary>
public sealed record SetIndexNumberRequest(string? IndexNumber, string? Reason);

public sealed record SetBarangayIndexRequest(string? IndexNumber, Guid? CityDistrictId, string? Reason);

/// <summary>Null <see cref="IndexNumber"/>: the next number after the highest ever used.</summary>
public sealed record CreateCityDistrictRequest(string? IndexNumber, string Name);

public sealed record NewBarangayRequest(string Name, string PsgcCode);

public sealed record SplitBarangayRequest(string Reason, DateOnly EffectiveDate, IReadOnlyList<NewBarangayRequest> Successors);

public sealed record CreateTaxMapSectionRequest(string? IndexNumber, string? Remarks, Guid? SplitFromSectionId);

public sealed record RetireRequest(string Reason, DateOnly EffectiveDate);

public interface IPropertyIdentificationService
{
    Task<Result<IReadOnlyList<ProvinceIndexDto>>> ListProvincesAsync(CancellationToken cancellationToken = default);
    Task<Result<ProvinceIndexDto>> SetProvinceIndexAsync(Guid id, SetIndexNumberRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MunicipalityIndexDto>>> ListMunicipalitiesAsync(Guid provinceId, CancellationToken cancellationToken = default);
    Task<Result<MunicipalityIndexDto>> SetMunicipalityIndexAsync(Guid id, SetIndexNumberRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CityDistrictDto>>> ListDistrictsAsync(Guid municipalityId, CancellationToken cancellationToken = default);
    Task<Result<CityDistrictDto>> CreateDistrictAsync(Guid municipalityId, CreateCityDistrictRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BarangayIndexDto>>> ListBarangaysAsync(Guid municipalityId, CancellationToken cancellationToken = default);
    Task<Result<BarangayIndexDto>> SetBarangayIndexAsync(Guid id, SetBarangayIndexRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BarangayIndexDto>>> SplitBarangayAsync(Guid id, SplitBarangayRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxMapSectionDto>>> ListSectionsAsync(Guid barangayId, CancellationToken cancellationToken = default);
    Task<Result<TaxMapSectionDto>> CreateSectionAsync(Guid barangayId, CreateTaxMapSectionRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxMapSectionDto>> RetireSectionAsync(Guid id, RetireRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The Real Property Identification System's configuration (MRPAAO Ch. II §1;
/// docs/analysis/property-identification.md §3.1–§3.2, step 10a-1): the
/// assessor's index numbers of the LGU, its municipalities or city districts
/// and barangays, and the tax map sections of each barangay. Every number is
/// entered by the LGU (decision 1); PRIME ships none. Numbers are unique and
/// never reused: a divided barangay and a retired section keep theirs.
/// </summary>
public sealed partial class PropertyIdentificationService(IApplicationDbContext db, ICurrentUserService currentUser,
    Microsoft.Extensions.Options.IOptions<PinOptions> pin) : IPropertyIdentificationService
{
    [GeneratedRegex("^[0-9]{3}$")] private static partial Regex ThreeDigits();
    [GeneratedRegex("^[0-9]{2,3}$")] private static partial Regex TwoOrThreeDigits();
    [GeneratedRegex("^[0-9]{2}$")] private static partial Regex TwoDigits();
    [GeneratedRegex("^[0-9]{4}$")] private static partial Regex FourDigits();

    // --- LGU (province, city, Metro Manila municipality) and municipality numbers ---

    public async Task<Result<IReadOnlyList<ProvinceIndexDto>>> ListProvincesAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<ProvinceIndexDto>>(await db.Provinces.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ProvinceIndexDto(x.Id, x.Name, x.PsgcCode, x.PinIndexNumber)).ToListAsync(cancellationToken));

    public async Task<Result<ProvinceIndexDto>> SetProvinceIndexAsync(Guid id, SetIndexNumberRequest request, CancellationToken cancellationToken = default)
    {
        var province = await db.Provinces.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (province is null)
        {
            return Result.Failure<ProvinceIndexDto>("PROVINCE_NOT_FOUND", "No province was found with the given id.");
        }
        var number = Clean(request.IndexNumber);
        var problem = Check(number, ThreeDigits(), "A province index number has 3 digits.")
            ?? ReasonNeeded(province.PinIndexNumber, number, request.Reason);
        if (problem is not null)
        {
            return Result.Failure<ProvinceIndexDto>("VALIDATION_FAILED", problem);
        }
        if (province.PinIndexNumber is not null && province.PinIndexNumber != number
            && await db.PinAssignments.IgnoreQueryFilters().AnyAsync(a => a.Kind == PinKind.Permanent && a.Barangay!.Municipality!.ProvinceId == id, cancellationToken))
        {
            return Result.Failure<ProvinceIndexDto>("PIN_INDEX_LOCKED", LockedMessage(province.PinIndexNumber));
        }
        if (number is not null && await LguIndexTakenAsync(number, provinceId: id, municipalityId: null, cancellationToken))
        {
            return Result.Failure<ProvinceIndexDto>("PIN_INDEX_DUPLICATE", $"Index number {number} is already used by another province, city or Metro Manila municipality.");
        }
        province.PinIndexNumber = number;
        currentUser.Reason = Clean(request.Reason);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new ProvinceIndexDto(province.Id, province.Name, province.PsgcCode, province.PinIndexNumber));
    }

    public async Task<Result<IReadOnlyList<MunicipalityIndexDto>>> ListMunicipalitiesAsync(Guid provinceId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<MunicipalityIndexDto>>(await db.Municipalities.AsNoTracking().Where(x => x.ProvinceId == provinceId)
            .OrderBy(x => x.Name)
            .Select(x => new MunicipalityIndexDto(x.Id, x.ProvinceId, x.Name, x.PsgcCode, x.IsCity, x.PinIndexNumber,
                db.CityDistricts.Count(d => d.MunicipalityId == x.Id)))
            .ToListAsync(cancellationToken));

    public async Task<Result<MunicipalityIndexDto>> SetMunicipalityIndexAsync(Guid id, SetIndexNumberRequest request, CancellationToken cancellationToken = default)
    {
        var municipality = await db.Municipalities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (municipality is null)
        {
            return Result.Failure<MunicipalityIndexDto>("MUNICIPALITY_NOT_FOUND", "No city or municipality was found with the given id.");
        }
        var number = Clean(request.IndexNumber);
        var problem = Check(number, TwoOrThreeDigits(),
                "Enter 3 digits for a city or Metro Manila municipality with its own index number, or 2 digits for a municipality within its province.")
            ?? ReasonNeeded(municipality.PinIndexNumber, number, request.Reason);
        if (problem is not null)
        {
            return Result.Failure<MunicipalityIndexDto>("VALIDATION_FAILED", problem);
        }
        if (municipality.PinIndexNumber is not null && municipality.PinIndexNumber != number
            && await db.PinAssignments.IgnoreQueryFilters().AnyAsync(a => a.Kind == PinKind.Permanent && a.Barangay!.MunicipalityId == id, cancellationToken))
        {
            return Result.Failure<MunicipalityIndexDto>("PIN_INDEX_LOCKED", LockedMessage(municipality.PinIndexNumber));
        }
        if (number is { Length: 3 } && await LguIndexTakenAsync(number, provinceId: null, municipalityId: id, cancellationToken))
        {
            return Result.Failure<MunicipalityIndexDto>("PIN_INDEX_DUPLICATE", $"Index number {number} is already used by a province, city or Metro Manila municipality.");
        }
        if (number is { Length: 2 } && await db.Municipalities.AnyAsync(x => x.Id != id && x.ProvinceId == municipality.ProvinceId && x.PinIndexNumber == number, cancellationToken))
        {
            return Result.Failure<MunicipalityIndexDto>("PIN_INDEX_DUPLICATE", $"Index number {number} is already used by another municipality of this province.");
        }
        municipality.PinIndexNumber = number;
        currentUser.Reason = Clean(request.Reason);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await ListMunicipalitiesAsync(municipality.ProvinceId, cancellationToken)).Value.Single(x => x.Id == id));
    }

    /// <summary>The 1st–3rd digits identify a province, a city or a Metro Manila municipality, so the three share one number space.</summary>
    private async Task<bool> LguIndexTakenAsync(string number, Guid? provinceId, Guid? municipalityId, CancellationToken ct) =>
        await db.Provinces.AnyAsync(x => x.Id != provinceId && x.PinIndexNumber == number, ct)
        || await db.Municipalities.AnyAsync(x => x.Id != municipalityId && x.PinIndexNumber == number, ct);

    // --- City districts ---

    public async Task<Result<IReadOnlyList<CityDistrictDto>>> ListDistrictsAsync(Guid municipalityId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<CityDistrictDto>>(await db.CityDistricts.AsNoTracking().Where(x => x.MunicipalityId == municipalityId)
            .OrderBy(x => x.IndexNumber).Select(x => new CityDistrictDto(x.Id, x.MunicipalityId, x.IndexNumber, x.Name, x.IsActive))
            .ToListAsync(cancellationToken));

    public async Task<Result<CityDistrictDto>> CreateDistrictAsync(Guid municipalityId, CreateCityDistrictRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Municipalities.AnyAsync(x => x.Id == municipalityId, cancellationToken))
        {
            return Result.Failure<CityDistrictDto>("MUNICIPALITY_NOT_FOUND", "No city or municipality was found with the given id.");
        }
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            return Result.Failure<CityDistrictDto>("VALIDATION_FAILED", "A district name is required (max 200).");
        }
        var used = await db.CityDistricts.Where(x => x.MunicipalityId == municipalityId).Select(x => x.IndexNumber).ToListAsync(cancellationToken);
        var number = Clean(request.IndexNumber) ?? Next(used, 2);
        var problem = number is null ? "All 2-digit district numbers are used." : Check(number, TwoDigits(), "A district index number has 2 digits.");
        if (problem is not null)
        {
            return Result.Failure<CityDistrictDto>("VALIDATION_FAILED", problem);
        }
        if (used.Contains(number!))
        {
            return Result.Failure<CityDistrictDto>("PIN_INDEX_DUPLICATE", $"District {number} already exists in this city or municipality.");
        }
        var district = new CityDistrict { MunicipalityId = municipalityId, IndexNumber = number!, Name = request.Name.Trim() };
        db.CityDistricts.Add(district);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new CityDistrictDto(district.Id, municipalityId, district.IndexNumber, district.Name, district.IsActive));
    }

    // --- Barangays ---

    public async Task<Result<IReadOnlyList<BarangayIndexDto>>> ListBarangaysAsync(Guid municipalityId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<BarangayIndexDto>>(await db.Barangays.AsNoTracking().Where(x => x.MunicipalityId == municipalityId)
            .OrderBy(x => x.CityDistrict!.IndexNumber).ThenBy(x => x.PinIndexNumber == null).ThenBy(x => x.PinIndexNumber).ThenBy(x => x.Name)
            .Select(x => new BarangayIndexDto(x.Id, x.MunicipalityId, x.Name, x.PsgcCode, x.CityDistrictId, x.CityDistrict!.IndexNumber, x.PinIndexNumber,
                x.RetiredOn, x.RetirementReason, x.SplitFromBarangayId, db.TaxMapSections.Count(s => s.BarangayId == x.Id)))
            .ToListAsync(cancellationToken));

    public async Task<Result<BarangayIndexDto>> SetBarangayIndexAsync(Guid id, SetBarangayIndexRequest request, CancellationToken cancellationToken = default)
    {
        var barangay = await db.Barangays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (barangay is null)
        {
            return Result.Failure<BarangayIndexDto>("BARANGAY_NOT_FOUND", "No barangay was found with the given id.");
        }
        if (barangay.RetiredOn is not null)
        {
            return Result.Failure<BarangayIndexDto>("BARANGAY_RETIRED", "A retired barangay keeps its index number; it cannot be changed.");
        }
        var number = Clean(request.IndexNumber);
        var digits = pin.Value.BarangayIndexDigits;
        var problem = Check(number, new Regex($"^[0-9]{{{digits}}}$"), $"A barangay index number has {digits} digits.")
            ?? ReasonNeeded(barangay.PinIndexNumber, number, request.Reason)
            ?? (barangay.CityDistrictId != request.CityDistrictId && barangay.CityDistrictId is not null && string.IsNullOrWhiteSpace(request.Reason)
                ? "Give the reason for moving the barangay to another district." : null);
        if (problem is not null)
        {
            return Result.Failure<BarangayIndexDto>("VALIDATION_FAILED", problem);
        }
        if ((barangay.PinIndexNumber is not null && barangay.PinIndexNumber != number || barangay.CityDistrictId != request.CityDistrictId)
            && await db.PinAssignments.IgnoreQueryFilters().AnyAsync(a => a.Kind == PinKind.Permanent && a.BarangayId == id, cancellationToken))
        {
            return Result.Failure<BarangayIndexDto>("PIN_INDEX_LOCKED", LockedMessage(barangay.PinIndexNumber));
        }
        if (request.CityDistrictId is { } districtId && !await db.CityDistricts.AnyAsync(x => x.Id == districtId && x.MunicipalityId == barangay.MunicipalityId, cancellationToken))
        {
            return Result.Failure<BarangayIndexDto>("CITY_DISTRICT_NOT_FOUND", "The district is not one of this barangay's city or municipality.");
        }
        if (number is not null && await db.Barangays.AnyAsync(x => x.Id != id && x.MunicipalityId == barangay.MunicipalityId
                && x.CityDistrictId == request.CityDistrictId && x.PinIndexNumber == number, cancellationToken))
        {
            return Result.Failure<BarangayIndexDto>("PIN_INDEX_DUPLICATE",
                $"Barangay index number {number} is already used (or retired) in this {(request.CityDistrictId is null ? "municipality" : "district")}; numbers are never reused.");
        }
        barangay.PinIndexNumber = number;
        barangay.CityDistrictId = request.CityDistrictId;
        currentUser.Reason = Clean(request.Reason);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await BarangayAsync(id, cancellationToken));
    }

    /// <summary>
    /// A barangay divided into new ones (MRPAAO Ch. II §1 D.5): the mother's index number
    /// is retired, and the new barangays take the numbers after the highest ever used in
    /// the municipality or district, in the order given.
    /// </summary>
    public async Task<Result<IReadOnlyList<BarangayIndexDto>>> SplitBarangayAsync(Guid id, SplitBarangayRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Result.Failure<IReadOnlyList<BarangayIndexDto>>("VALIDATION_FAILED", "A reason is required (max 1000), e.g. the ordinance creating the new barangays.");
        }
        if (request.Successors.Count < 2 || request.Successors.Any(s => string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.PsgcCode)))
        {
            return Result.Failure<IReadOnlyList<BarangayIndexDto>>("VALIDATION_FAILED", "Name two or more new barangays, each with its name and PSGC code.");
        }
        var mother = await db.Barangays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (mother is null)
        {
            return Result.Failure<IReadOnlyList<BarangayIndexDto>>("BARANGAY_NOT_FOUND", "No barangay was found with the given id.");
        }
        if (mother.RetiredOn is not null)
        {
            return Result.Failure<IReadOnlyList<BarangayIndexDto>>("BARANGAY_RETIRED", "This barangay has already been divided.");
        }
        if (mother.PinIndexNumber is null)
        {
            return Result.Failure<IReadOnlyList<BarangayIndexDto>>("VALIDATION_FAILED", "Give the barangay its index number before dividing it, so the number can be retired.");
        }
        var codes = request.Successors.Select(s => s.PsgcCode.Trim()).ToList();
        if (codes.Distinct().Count() != codes.Count || await db.Barangays.AnyAsync(x => codes.Contains(x.PsgcCode), cancellationToken))
        {
            return Result.Failure<IReadOnlyList<BarangayIndexDto>>("BARANGAY_DUPLICATE", "A new barangay's PSGC code is already in use.");
        }

        var used = await db.Barangays.Where(x => x.MunicipalityId == mother.MunicipalityId && x.CityDistrictId == mother.CityDistrictId && x.PinIndexNumber != null)
            .Select(x => x.PinIndexNumber!).ToListAsync(cancellationToken);
        var created = new List<Barangay>();
        foreach (var successor in request.Successors)
        {
            var number = Next(used, pin.Value.BarangayIndexDigits);
            if (number is null)
            {
                return Result.Failure<IReadOnlyList<BarangayIndexDto>>("VALIDATION_FAILED", $"All {pin.Value.BarangayIndexDigits}-digit barangay numbers are used.");
            }
            used.Add(number);
            created.Add(new Barangay
            {
                MunicipalityId = mother.MunicipalityId, CityDistrictId = mother.CityDistrictId, Name = successor.Name.Trim(),
                PsgcCode = successor.PsgcCode.Trim(), PinIndexNumber = number, SplitFromBarangayId = mother.Id,
            });
        }
        mother.RetiredOn = request.EffectiveDate;
        mother.RetirementReason = request.Reason.Trim();
        mother.IsActive = false;
        db.Barangays.AddRange(created);
        currentUser.Reason = mother.RetirementReason;
        await db.SaveChangesAsync(cancellationToken);

        var ids = created.Select(c => c.Id).Prepend(mother.Id).ToList();
        return Result.Success<IReadOnlyList<BarangayIndexDto>>((await ListBarangaysAsync(mother.MunicipalityId, cancellationToken)).Value
            .Where(b => ids.Contains(b.Id)).OrderBy(b => b.PinIndexNumber).ToList());
    }

    private async Task<BarangayIndexDto> BarangayAsync(Guid id, CancellationToken ct)
    {
        var municipalityId = await db.Barangays.Where(x => x.Id == id).Select(x => x.MunicipalityId).SingleAsync(ct);
        return (await ListBarangaysAsync(municipalityId, ct)).Value.Single(x => x.Id == id);
    }

    // --- Tax map sections ---

    public async Task<Result<IReadOnlyList<TaxMapSectionDto>>> ListSectionsAsync(Guid barangayId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<TaxMapSectionDto>>(await db.TaxMapSections.AsNoTracking().Where(x => x.BarangayId == barangayId)
            .OrderBy(x => x.IndexNumber).Select(x => ToDto(x)).ToListAsync(cancellationToken));

    public async Task<Result<TaxMapSectionDto>> CreateSectionAsync(Guid barangayId, CreateTaxMapSectionRequest request, CancellationToken cancellationToken = default)
    {
        var barangay = await db.Barangays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == barangayId, cancellationToken);
        if (barangay is null)
        {
            return Result.Failure<TaxMapSectionDto>("BARANGAY_NOT_FOUND", "No barangay was found with the given id.");
        }
        if (barangay.RetiredOn is not null)
        {
            return Result.Failure<TaxMapSectionDto>("BARANGAY_RETIRED", "A retired barangay takes no new sections.");
        }
        if (request.Remarks is { Length: > 1000 })
        {
            return Result.Failure<TaxMapSectionDto>("VALIDATION_FAILED", "Remarks may not exceed 1000 characters.");
        }
        if (request.SplitFromSectionId is { } from && !await db.TaxMapSections.AnyAsync(x => x.Id == from && x.BarangayId == barangayId, cancellationToken))
        {
            return Result.Failure<TaxMapSectionDto>("TAX_MAP_SECTION_NOT_FOUND", "The section it comes from is not one of this barangay's.");
        }
        var used = await db.TaxMapSections.Where(x => x.BarangayId == barangayId).Select(x => x.IndexNumber).ToListAsync(cancellationToken);
        var number = Clean(request.IndexNumber) ?? Next(used, 3);
        if (number is null)
        {
            return Result.Failure<TaxMapSectionDto>("VALIDATION_FAILED", "All 3-digit section numbers of this barangay are used.");
        }
        if (Check(number, ThreeDigits(), "A section index number has 3 digits.") is { } invalid)
        {
            return Result.Failure<TaxMapSectionDto>("VALIDATION_FAILED", invalid);
        }
        if (used.Contains(number))
        {
            return Result.Failure<TaxMapSectionDto>("PIN_INDEX_DUPLICATE", $"Section {number} already exists (or existed) in this barangay; numbers are never reused.");
        }
        var section = new TaxMapSection
        {
            BarangayId = barangayId, IndexNumber = number, Remarks = Clean(request.Remarks), SplitFromSectionId = request.SplitFromSectionId,
        };
        db.TaxMapSections.Add(section);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(section));
    }

    public async Task<Result<TaxMapSectionDto>> RetireSectionAsync(Guid id, RetireRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
        {
            return Result.Failure<TaxMapSectionDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var section = await db.TaxMapSections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (section is null)
        {
            return Result.Failure<TaxMapSectionDto>("TAX_MAP_SECTION_NOT_FOUND", "No tax map section was found with the given id.");
        }
        if (section.RetiredOn is not null)
        {
            return Result.Failure<TaxMapSectionDto>("TAX_MAP_SECTION_RETIRED", "The section is already retired.");
        }
        section.RetiredOn = request.EffectiveDate;
        section.RetirementReason = request.Reason.Trim();
        currentUser.Reason = section.RetirementReason;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(section));
    }

    // --- Helpers ---

    private static TaxMapSectionDto ToDto(TaxMapSection x) =>
        new(x.Id, x.BarangayId, x.IndexNumber, x.Remarks, x.SplitFromSectionId, x.RetiredOn, x.RetirementReason);

    /// <summary>
    /// Permanent PINs were built from this number (docs/analysis/property-identification.md §3.1):
    /// changing it would make new PINs disagree with the ones issued. PINs change only through
    /// retirement and renumbering, never by editing an index number.
    /// </summary>
    private static string LockedMessage(string? current) =>
        $"Index number {current} is part of permanent PINs already issued and can no longer be changed.";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Check(string? number, Regex format, string message) => number is not null && !format.IsMatch(number) ? message : null;

    /// <summary>Changing (or clearing) a number already set is audited with a reason.</summary>
    private static string? ReasonNeeded(string? current, string? next, string? reason) =>
        current is not null && current != next && string.IsNullOrWhiteSpace(reason)
            ? $"Index number {current} is already set; give the reason for changing it."
            : reason is { Length: > 1000 } ? "The reason may not exceed 1000 characters." : null;

    /// <summary>The number after the highest ever used, zero-padded to <paramref name="width"/>; null when the width is exhausted.</summary>
    private static string? Next(IEnumerable<string> used, int width)
    {
        var highest = used.Select(u => int.TryParse(u, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        var next = highest + 1;
        return next.ToString(CultureInfo.InvariantCulture).Length > width ? null : next.ToString(new string('0', width), CultureInfo.InvariantCulture);
    }
}
