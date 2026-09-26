using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Registers;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Registers;

public sealed record CreateRegisterRunRequest(
    RegisterKind Kind, DateOnly AsOf, Guid? BarangayId, Guid? ClassificationId, Guid? TaxpayerId, DateOnly? FromDate, string? Remarks);

public sealed record RegisterRunDto(
    Guid Id, RegisterKind Kind, string FormCode, DateOnly AsOf, DateOnly? FromDate, Guid? BarangayId, string? BarangayName,
    Guid? ClassificationId, string? ClassificationName, Guid? TaxpayerId, string? TaxpayerName, string? Remarks, DateTimeOffset CreatedAt);

public interface IRegisterService
{
    Task<Result<RegisterRunDto>> CreateRunAsync(CreateRegisterRunRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RegisterRunDto>>> ListRunsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Dated runs of the MRPAAO registers (docs/analysis/mrpaao-forms-model.md §15).
/// A run records only its kind, scope and date; the rows come from the form
/// data provider, and printing the run freezes them.
/// </summary>
public sealed class RegisterService(IApplicationDbContext db) : IRegisterService
{
    /// <summary>The form each register kind prints with (seeded MRPAAO layouts).</summary>
    public static string FormCode(RegisterKind kind) => kind switch
    {
        RegisterKind.TaxMapControlRoll => "TMCR",
        RegisterKind.AssessmentRollTaxable => "AR_TAXABLE",
        RegisterKind.AssessmentRollExempt => "AR_EXEMPT",
        RegisterKind.OwnershipRecordCard => "ORC",
        RegisterKind.RecordOfAssessment => "ROA",
        _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterKind)}: {kind}"),
    };

    public async Task<Result<RegisterRunDto>> CreateRunAsync(CreateRegisterRunRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.Kind) || r.AsOf == default || r.FromDate > r.AsOf || r.Remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A kind and an as-of date are required; the period cannot start after it ends; remarks max 1000.");
        }
        var needsOwner = r.Kind == RegisterKind.OwnershipRecordCard;
        if (needsOwner ? r.TaxpayerId is null : r.BarangayId is null)
        {
            return Fail("VALIDATION_FAILED", needsOwner ? "An Ownership Record Card is for one owner: name the taxpayer." : "This register is kept by barangay: name the barangay.");
        }
        if (r.Kind == RegisterKind.RecordOfAssessment && r.ClassificationId is null)
        {
            return Fail("VALIDATION_FAILED", "The Record of Assessment is kept by classification: name the classification.");
        }
        if (r.BarangayId is { } b && !await db.Barangays.AnyAsync(x => x.Id == b, cancellationToken))
        {
            return Fail("BARANGAY_NOT_FOUND", "The specified barangay does not exist.");
        }
        if (r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (r.TaxpayerId is { } t && !await db.Taxpayers.AnyAsync(x => x.Id == t, cancellationToken))
        {
            return Fail("TAXPAYER_NOT_FOUND", "The specified taxpayer does not exist.");
        }
        var run = new RegisterRun
        {
            Kind = r.Kind, AsOf = r.AsOf, FromDate = r.FromDate, Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim(),
            BarangayId = needsOwner ? null : r.BarangayId,
            ClassificationId = r.Kind == RegisterKind.RecordOfAssessment ? r.ClassificationId : null,
            TaxpayerId = needsOwner ? r.TaxpayerId : null,
        };
        db.RegisterRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await ListAsync(db.RegisterRuns.Where(x => x.Id == run.Id), cancellationToken)).Single());
    }

    public async Task<Result<IReadOnlyList<RegisterRunDto>>> ListRunsAsync(CancellationToken cancellationToken = default) =>
        Result.Success(await ListAsync(db.RegisterRuns.OrderByDescending(x => x.CreatedAt).Take(200), cancellationToken));

    private static async Task<IReadOnlyList<RegisterRunDto>> ListAsync(IQueryable<RegisterRun> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(x => x.Barangay).Include(x => x.Classification).Include(x => x.Taxpayer).ToListAsync(ct);
        return rows.Select(x => new RegisterRunDto(x.Id, x.Kind, FormCode(x.Kind), x.AsOf, x.FromDate, x.BarangayId, x.Barangay?.Name,
            x.ClassificationId, x.Classification?.Name, x.TaxpayerId,
            x.Taxpayer is { } tp ? TaxpayerNameFormatter.Format(tp.TaxpayerType, tp.LastName, tp.FirstName, tp.MiddleName, tp.Suffix, tp.CorporateName) : null,
            x.Remarks, x.CreatedAt)).ToList();
    }

    private static Result<RegisterRunDto> Fail(string code, string message) => Result.Failure<RegisterRunDto>(code, message);
}

/// <summary>
/// The rows of a register run (MRPAAO Att. 5–9), from the FAAS in force on
/// the run's date: a TD approved and effective by then, and not cancelled
/// before it, with the assessment it declares (else the unit's latest posted
/// assessment by then). Nothing is computed that PRIME has not recorded.
/// DOMAIN VERIFICATION REQUIRED: page numbering (barangay index + sheet) and
/// the manual's location index numbers — PSGC codes are shown instead.
/// </summary>
public sealed class RegisterFormDataProvider(IApplicationDbContext db, IClock clock, IOptions<FaasOptions> faas) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.Register;

    private sealed record Faas(TaxDeclaration Td, Assessment? Assessment, DateOnly EnteredOn);

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var run = await db.RegisterRuns.AsNoTracking().Include(x => x.Barangay).ThenInclude(b => b!.Municipality).ThenInclude(m => m!.Province)
            .Include(x => x.Classification).Include(x => x.Taxpayer).FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (run is null)
        {
            return null;
        }
        var ct = cancellationToken;
        object rows = run.Kind switch
        {
            RegisterKind.TaxMapControlRoll => await TaxMapRowsAsync(run, ct),
            RegisterKind.AssessmentRollTaxable => await RollRowsAsync(run, Taxability.Taxable, ct),
            RegisterKind.AssessmentRollExempt => await RollRowsAsync(run, Taxability.Exempt, ct),
            RegisterKind.OwnershipRecordCard => await OwnershipRowsAsync(run, ct),
            RegisterKind.RecordOfAssessment => await RecordRowsAsync(run, ct),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterKind)}: {run.Kind}"),
        };
        var owner = run.Taxpayer;
        var data = FormData.ToJson(new
        {
            register = new
            {
                kind = run.Kind.ToString(), formCode = RegisterService.FormCode(run.Kind), asOf = run.AsOf, fromDate = run.FromDate, remarks = run.Remarks,
                barangay = run.Barangay?.Name, barangayCode = run.Barangay?.PsgcCode,
                municipality = run.Barangay?.Municipality?.Name, municipalityCode = run.Barangay?.Municipality?.PsgcCode,
                province = run.Barangay?.Municipality?.Province?.Name, provinceCode = run.Barangay?.Municipality?.Province?.PsgcCode,
                classification = run.Classification?.Name, classificationCode = run.Classification?.Code,
                // The general revision year: the revision year of the SMV the listed assessments used.
                revisionYear = await RevisionYearAsync(run, ct),
                owner = owner is null ? null : new
                {
                    name = TaxpayerNameFormatter.Format(owner.TaxpayerType, owner.LastName, owner.FirstName, owner.MiddleName, owner.Suffix, owner.CorporateName),
                    address = owner.Address, tin = owner.Tin, contactNumber = owner.ContactNumber, since = owner.CreatedAt,
                },
            },
            rows,
        });
        return new FormSubjectData(null, data, null);
    }

    // ---------------- FAAS in force ----------------

    private async Task<List<Faas>> FaasInForceAsync(IQueryable<TaxDeclaration> scope, DateOnly asOf, CancellationToken ct)
    {
        var tds = await scope.AsNoTracking().Include(x => x.Rpu).Include(x => x.Classification).Include(x => x.Assessment)
            .Include(x => x.PreviousTaxDeclaration).ThenInclude(x => x!.Assessment)
            .Where(x => x.EffectivityDate <= asOf && (x.Status == WorkflowStatus.Approved || x.Status == WorkflowStatus.Cancelled))
            .ToListAsync(ct);
        var inForce = tds
            .Where(x => x.Status == WorkflowStatus.Approved || (x.CancelledAt is { } c && clock.LocalDate(c) > asOf))
            .Where(x => x.ApprovedAt is not { } a || clock.LocalDate(a) <= asOf)
            .GroupBy(x => x.RpuId)
            .Select(g => g.OrderByDescending(x => x.EffectivityDate).ThenByDescending(x => x.RevisionNumber).First())
            .ToList();
        var result = new List<Faas>();
        foreach (var td in inForce)
        {
            result.Add(new Faas(td, td.Assessment ?? await LatestPostedAsync(td.RpuId, asOf, ct), Entered(td)));
        }
        return result;
    }

    private Task<Assessment?> LatestPostedAsync(Guid rpuId, DateOnly asOf, CancellationToken ct) =>
        db.Assessments.AsNoTracking().Where(x => x.RpuId == rpuId && x.Status == WorkflowStatus.Posted && x.EffectiveDate <= asOf)
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

    private DateOnly Entered(TaxDeclaration td) => clock.LocalDate(td.ApprovedAt ?? td.CreatedAt);

    private async Task<int?> RevisionYearAsync(RegisterRun run, CancellationToken ct) =>
        run.BarangayId is not { } b ? null
            : await db.Assessments.Where(a => a.Status == WorkflowStatus.Posted && a.Property!.BarangayId == b && a.Valuation!.Smv != null)
                .OrderByDescending(a => a.EffectiveDate).Select(a => (int?)a.Valuation!.Smv!.RevisionYear).FirstOrDefaultAsync(ct);

    // ---------------- Shared row fields ----------------

    private async Task<(string Names, string? Address)> OwnersAsync(Guid propertyId, Guid rpuId, DateOnly asOf, CancellationToken ct)
    {
        var rows = await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, propertyId, rpuId,
            x => x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf)
                && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner), ct), ct);
        return (string.Join("; ", rows.Select(o => o.TaxpayerDisplayName)), rows.Select(o => o.Address).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)));
    }

    private async Task<PropertyEntity> PropertyAsync(Guid id, CancellationToken ct) =>
        await db.Properties.AsNoTracking().Include(p => p.Barangay).Include(p => p.TitleType).FirstAsync(p => p.Id == id, ct);

    /// <summary>The unit's full PIN (MRPAAO p.42): the parcel number is parenthesised when the unit has owners of its own.</summary>
    private async Task<string> UnitPinAsync(PropertyEntity p, RealPropertyUnit rpu, DateOnly asOf, CancellationToken ct) =>
        UnitPin.Compose(p.PropertyIdentificationNumber, rpu.PinSuffix, rpu.PinSuffix is not null && await db.PropertyTaxpayers.AnyAsync(
            x => x.RpuId == rpu.Id && x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf), ct));

    private string? Arp(Faas f) => faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? f.Td.TaxDeclarationNumber : f.Assessment?.FaasNumber;

    private static string KindCode(RpuType type) => type switch
    {
        RpuType.Land => "L",
        RpuType.Building => "B",
        RpuType.Machinery => "M",
        _ => "O",
    };

    private static string ParcelNumber(string pin) => pin.LastIndexOf('-') is var cut and >= 0 ? pin[(cut + 1)..] : pin;

    private static object Effectivity(DateOnly d) => new { quarter = (d.Month - 1) / 3 + 1, year = d.Year };

    private async Task<(decimal? Area, string? Unit)> AreaAsync(TaxDeclaration td, CancellationToken ct) => td.Rpu!.RpuType switch
    {
        RpuType.Land => await db.Lands.Where(x => x.RpuId == td.RpuId).Select(x => new ValueTuple<decimal?, string?>(x.Area, x.AreaUnit)).FirstOrDefaultAsync(ct),
        RpuType.Building => (await db.Buildings.Where(x => x.RpuId == td.RpuId).Select(x => (decimal?)x.TotalFloorArea).FirstOrDefaultAsync(ct), "sqm floor"),
        _ => (null, null),
    };

    // ---------------- Registers ----------------

    /// <summary>MRPAAO Att. 5 (p.158–159): one row per land parcel of the barangay.</summary>
    private async Task<List<object>> TaxMapRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => x.Property!.BarangayId == run.BarangayId), run.AsOf, ct);
        var rows = new List<object>();
        foreach (var f in all.Where(x => x.Td.Rpu!.RpuType == RpuType.Land).OrderBy(x => x.Td.Rpu!.RpuNumber))
        {
            var p = await PropertyAsync(f.Td.PropertyId, ct);
            var units = all.Where(x => x.Td.PropertyId == f.Td.PropertyId).Select(x => x.Td.Rpu!.RpuType).ToList();
            var improvements = await db.LandImprovements.Where(i => db.Lands.Any(l => l.Id == i.LandId && l.RpuId == f.Td.RpuId))
                .Select(i => i.ImprovementKind!.Name).Distinct().ToListAsync(ct);
            var (area, unit) = await AreaAsync(f.Td, ct);
            var (owners, _) = await OwnersAsync(f.Td.PropertyId, f.Td.RpuId, run.AsOf, ct);
            rows.Add(new
            {
                assessorLotNumber = ParcelNumber(p.PropertyIdentificationNumber), pin = p.PropertyIdentificationNumber,
                surveyNumber = p.SurveyNumber, lotNumber = p.LotNumber, blockNumber = p.BlockNumber,
                titleNumber = p.TitleNumber, area, areaUnit = unit, classCode = f.Td.Classification!.Code, owner = owners,
                arpNumber = Arp(f), tdNumber = f.Td.TaxDeclarationNumber,
                buildings = units.Count(t => t == RpuType.Building), machinery = units.Any(t => t == RpuType.Machinery),
                others = string.Join(", ", improvements.Concat(units.Where(t => t == RpuType.OtherImprovement).Select(_ => "other improvement")).Distinct()),
                remarks = f.Td.TransactionCode,
            });
        }
        return rows;
    }

    /// <summary>MRPAAO Att. 6–7 (p.160–163): the FAAS of the barangay, taxable or exempt; a supplement lists only those entered since the from-date.</summary>
    private async Task<List<object>> RollRowsAsync(RegisterRun run, Taxability taxability, CancellationToken ct)
    {
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => x.Property!.BarangayId == run.BarangayId), run.AsOf, ct);
        var rows = new List<object>();
        foreach (var f in all.Where(x => taxability == Taxability.Taxable ? x.Td.Taxability == Taxability.Taxable : x.Td.Taxability != Taxability.Taxable)
                     .Where(x => run.FromDate is not { } from || x.EnteredOn >= from)
                     .OrderBy(x => x.Td.Rpu!.RpuNumber))
        {
            var p = await PropertyAsync(f.Td.PropertyId, ct);
            var (owners, address) = await OwnersAsync(f.Td.PropertyId, f.Td.RpuId, run.AsOf, ct);
            rows.Add(new
            {
                arpNumber = Arp(f), tdNumber = f.Td.TaxDeclarationNumber,
                pin = await UnitPinAsync(p, f.Td.Rpu!, run.AsOf, ct),
                lotBlock = $"{p.LotNumber ?? "—"} / {p.BlockNumber ?? "—"}", owner = owners, ownerAddress = address,
                kind = KindCode(f.Td.Rpu.RpuType), classCode = f.Td.Classification!.Code, assessedValue = f.Assessment?.AssessedValue,
                previousArpNumber = f.Td.PreviousTaxDeclaration is { } prev
                    ? faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? prev.TaxDeclarationNumber : prev.Assessment?.FaasNumber : null, previousTdNumber = f.Td.PreviousTaxDeclaration?.TaxDeclarationNumber,
                legalBasis = (string?)null, // exemptions are not yet modelled (CLAUDE.md §43)
                effectivity = Effectivity(f.Td.EffectivityDate), remarks = f.Td.TransactionCode, enteredOn = f.EnteredOn,
            });
        }
        return rows;
    }

    /// <summary>MRPAAO Att. 8 (p.164–166): the owner's properties in force, the owner's own units and the properties they own whole.</summary>
    private async Task<List<object>> OwnershipRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var asOf = run.AsOf;
        var holdings = await db.PropertyTaxpayers.AsNoTracking()
            .Where(x => x.TaxpayerId == run.TaxpayerId && x.Role == PropertyPartyRole.Owner && x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf))
            .Select(x => new { x.PropertyId, x.RpuId }).ToListAsync(ct);
        var propertyIds = holdings.Select(h => h.PropertyId).Distinct().ToList();
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => propertyIds.Contains(x.PropertyId)), asOf, ct);
        var rows = new List<object>();
        foreach (var f in all.OrderBy(x => x.Td.PropertyId).ThenBy(x => x.Td.Rpu!.RpuNumber))
        {
            // The unit is the owner's when the owner holds it, or holds the property and the unit has no owners of its own.
            var scoped = await PropertyParties.ScopeAsync(db, f.Td.PropertyId, f.Td.RpuId,
                x => x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf), ct);
            if (!await scoped.AnyAsync(x => x.TaxpayerId == run.TaxpayerId && x.Role == PropertyPartyRole.Owner, ct))
            {
                continue;
            }
            var p = await PropertyAsync(f.Td.PropertyId, ct);
            var (area, unit) = await AreaAsync(f.Td, ct);
            var previousOwner = f.Td.PreviousTaxDeclaration is { } prev
                ? (await OwnersAsync(prev.PropertyId, prev.RpuId, Entered(prev), ct)).Names : null;
            rows.Add(new
            {
                enteredOn = f.EnteredOn, kind = KindCode(f.Td.Rpu!.RpuType), classCode = f.Td.Classification!.Code,
                pin = await UnitPinAsync(p, f.Td.Rpu, asOf, ct),
                titleNumber = p.TitleNumber, lotBlock = $"{p.LotNumber ?? "—"} / {p.BlockNumber ?? "—"}",
                arpNumber = Arp(f), tdNumber = f.Td.TaxDeclarationNumber, previousOwner,
                location = string.Join(", ", new[] { p.Street, p.Barangay!.Name }.Where(x => !string.IsNullOrWhiteSpace(x))),
                area, areaUnit = unit, marketValue = f.Assessment?.MarketValue, assessedValue = f.Assessment?.AssessedValue,
                remarks = f.Td.TransactionCode,
            });
        }
        return rows;
    }

    /// <summary>
    /// MRPAAO Att. 9 (p.166–167): the assessment transactions of the barangay and
    /// classification recorded in the period — each FAAS entered (TD approved),
    /// with its values split by kind (land / building / machinery), taxable or exempt.
    /// </summary>
    private async Task<List<object>> RecordRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var tds = await db.TaxDeclarations.AsNoTracking().Include(x => x.Rpu).Include(x => x.Assessment)
            .Where(x => x.Property!.BarangayId == run.BarangayId && x.ClassificationId == run.ClassificationId
                && (x.Status == WorkflowStatus.Approved || x.Status == WorkflowStatus.Cancelled))
            .ToListAsync(ct);
        var rows = new List<object>();
        foreach (var td in tds.Where(x => Entered(x) <= run.AsOf && (run.FromDate is not { } from || Entered(x) >= from))
                     .OrderBy(Entered).ThenBy(x => x.TaxDeclarationNumber))
        {
            var assessment = td.Assessment ?? await LatestPostedAsync(td.RpuId, td.EffectivityDate, ct);
            var p = await PropertyAsync(td.PropertyId, ct);
            // The declared owner when the FAAS was entered.
            var (owners, _) = await OwnersAsync(td.PropertyId, td.RpuId, Entered(td), ct);
            var (area, _) = td.Rpu!.RpuType == RpuType.Land ? await AreaAsync(td, ct) : (null, null);
            var kind = KindCode(td.Rpu.RpuType);
            var taxable = td.Taxability == Taxability.Taxable;
            var mv = assessment?.MarketValue;
            var av = assessment?.AssessedValue;
            rows.Add(new
            {
                date = Entered(td), arpNumber = faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? td.TaxDeclarationNumber : assessment?.FaasNumber,
                tdNumber = td.TaxDeclarationNumber, owner = owners, pin = await UnitPinAsync(p, td.Rpu, Entered(td), ct),
                location = string.Join(", ", new[] { p.Street, p.Barangay!.Name }.Where(x => !string.IsNullOrWhiteSpace(x))),
                taxable, landArea = area, kind,
                marketValueL = kind == "L" ? mv : null, marketValueB = kind == "B" ? mv : null, marketValueM = kind == "M" ? mv : null,
                assessedValueL = kind == "L" ? av : null, assessedValueB = kind == "B" ? av : null, assessedValueM = kind == "M" ? av : null,
                yearTaxesBegin = taxable ? td.EffectivityDate.Year : (int?)null, transactionCode = td.TransactionCode,
            });
        }
        return rows;
    }
}
