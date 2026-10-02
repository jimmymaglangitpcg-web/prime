using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Properties;
using Prime.Application.Features.PropertyIdentification;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Registers;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Registers;

/// <param name="SectionId">Tax Map Control Roll only: the tax map section (its barangay is taken from it).</param>
/// <param name="IncludePastOwners">Ownership Record Form only: also list properties the owner no longer holds (records-and-forms.md §4.5).</param>
public sealed record CreateRegisterRunRequest(
    RegisterKind Kind, DateOnly AsOf, Guid? BarangayId, Guid? ClassificationId, Guid? TaxpayerId, DateOnly? FromDate, string? Remarks, Guid? SectionId = null,
    bool IncludePastOwners = false);

public sealed record RegisterRunDto(
    Guid Id, RegisterKind Kind, string FormCode, DateOnly AsOf, DateOnly? FromDate, Guid? BarangayId, string? BarangayName,
    Guid? ClassificationId, string? ClassificationName, Guid? TaxpayerId, string? TaxpayerName, string? Remarks, DateTimeOffset CreatedAt,
    Guid? SectionId = null, string? SectionIndexNumber = null, bool IncludePastOwners = false);

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
public sealed class RegisterService(IApplicationDbContext db, IJurisdiction jurisdiction) : IRegisterService
{
    /// <summary>The form each register kind prints with (seeded MRPAAO layouts).</summary>
    public static string FormCode(RegisterKind kind) => kind switch
    {
        RegisterKind.TaxMapControlRoll => "TMCR",
        RegisterKind.AssessmentRollTaxable => "AR_TAXABLE",
        RegisterKind.AssessmentRollExempt => "AR_EXEMPT",
        RegisterKind.OwnershipRecordCard => "ORC",
        RegisterKind.RecordOfAssessment => "ROA",
        RegisterKind.PreTaxMapControlRoll => "PRE_TMCR",
        _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterKind)}: {kind}"),
    };

    public async Task<Result<RegisterRunDto>> CreateRunAsync(CreateRegisterRunRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.Kind) || r.AsOf == default || r.FromDate > r.AsOf || r.Remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A kind and an as-of date are required; the period cannot start after it ends; remarks max 1000.");
        }
        var needsOwner = r.Kind == RegisterKind.OwnershipRecordCard;
        var barangayId = r.BarangayId;
        if (r.SectionId is { } sectionId)
        {
            if (r.Kind != RegisterKind.TaxMapControlRoll)
            {
                return Fail("VALIDATION_FAILED", "Only the Tax Map Control Roll is kept per tax map section.");
            }
            var sectionBarangay = await db.TaxMapSections.Where(x => x.Id == sectionId).Select(x => (Guid?)x.BarangayId).SingleOrDefaultAsync(cancellationToken);
            if (sectionBarangay is null)
            {
                return Fail("TAX_MAP_SECTION_NOT_FOUND", "The specified tax map section does not exist.");
            }
            if (barangayId is { } named && named != sectionBarangay)
            {
                return Fail("VALIDATION_FAILED", "The tax map section is not in the named barangay.");
            }
            barangayId = sectionBarangay;
        }
        if (needsOwner ? r.TaxpayerId is null : barangayId is null)
        {
            return Fail("VALIDATION_FAILED", needsOwner ? "An Ownership Record Form is for one owner: name the taxpayer." : "This register is kept by barangay: name the barangay.");
        }
        if (r.IncludePastOwners && !needsOwner)
        {
            return Fail("VALIDATION_FAILED", "Only the Ownership Record Form lists past owners.");
        }
        if (r.Kind == RegisterKind.RecordOfAssessment && r.ClassificationId is null)
        {
            return Fail("VALIDATION_FAILED", "The Record of Assessment is kept by classification: name the classification.");
        }
        if (barangayId is { } b)
        {
            var municipalityId = await db.Barangays.Where(x => x.Id == b).Select(x => (Guid?)x.MunicipalityId).FirstOrDefaultAsync(cancellationToken);
            if (municipalityId is null)
            {
                return Fail("BARANGAY_NOT_FOUND", "The specified barangay does not exist.");
            }
            if (!jurisdiction.Allows(municipalityId.Value))
            {
                return Fail(JurisdictionErrors.Code, JurisdictionErrors.Message);
            }
        }
        if (r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Fail("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (r.TaxpayerId is { } t && !await db.Taxpayers.AnyAsync(x => x.Id == t, cancellationToken))
        {
            return Fail("TAXPAYER_NOT_FOUND", "The specified taxpayer does not exist.");
        }
        // A past owner's form is set apart (LAM Book I p.21): printed only when the run asks for past owners (Q12).
        if (needsOwner && !r.IncludePastOwners && !await db.PropertyTaxpayers.AnyAsync(x => x.TaxpayerId == r.TaxpayerId && x.Role == PropertyPartyRole.Owner
                && x.StartDate <= r.AsOf && (x.EndDate == null || x.EndDate > r.AsOf), cancellationToken))
        {
            return Fail("OWNER_HOLDS_NO_PROPERTY",
                $"The taxpayer owns no property in your jurisdiction on {r.AsOf:yyyy-MM-dd}. A past owner's form is set apart: tick \"include past owners\" to print it.");
        }
        var run = new RegisterRun
        {
            Kind = r.Kind, AsOf = r.AsOf, FromDate = r.FromDate, Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim(),
            BarangayId = needsOwner ? null : barangayId, SectionId = r.SectionId,
            ClassificationId = r.Kind == RegisterKind.RecordOfAssessment ? r.ClassificationId : null,
            TaxpayerId = needsOwner ? r.TaxpayerId : null, IncludePastOwners = r.IncludePastOwners,
        };
        db.RegisterRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await ListAsync(db.RegisterRuns.Where(x => x.Id == run.Id), cancellationToken)).Single());
    }

    public async Task<Result<IReadOnlyList<RegisterRunDto>>> ListRunsAsync(CancellationToken cancellationToken = default) =>
        Result.Success(await ListAsync(db.RegisterRuns.OrderByDescending(x => x.CreatedAt).Take(200), cancellationToken));

    private static async Task<IReadOnlyList<RegisterRunDto>> ListAsync(IQueryable<RegisterRun> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().Include(x => x.Barangay).Include(x => x.Section).Include(x => x.Classification).Include(x => x.Taxpayer).ToListAsync(ct);
        return rows.Select(x => new RegisterRunDto(x.Id, x.Kind, FormCode(x.Kind), x.AsOf, x.FromDate, x.BarangayId, x.Barangay?.Name,
            x.ClassificationId, x.Classification?.Name, x.TaxpayerId,
            x.Taxpayer is { } tp ? TaxpayerNameFormatter.Format(tp.TaxpayerType, tp.LastName, tp.FirstName, tp.MiddleName, tp.Suffix, tp.CorporateName) : null,
            x.Remarks, x.CreatedAt, x.SectionId, x.Section?.IndexNumber, x.IncludePastOwners)).ToList();
    }

    private static Result<RegisterRunDto> Fail(string code, string message) => Result.Failure<RegisterRunDto>(code, message);
}

/// <summary>
/// The rows of a register run (MRPAAO Att. 5–9), from the FAAS in force on
/// the run's date: a TD approved and effective by then, and not cancelled
/// before it, with the assessment it declares (else the unit's latest posted
/// assessment by then). Nothing is computed that PRIME has not recorded.
/// The heading carries the assessor's index numbers where they are set
/// (step 10a-4), else the PSGC codes. DOMAIN VERIFICATION REQUIRED: page
/// numbering (barangay index + sheet).
/// </summary>
public sealed class RegisterFormDataProvider(IApplicationDbContext db, IClock clock, IOptions<FaasOptions> faas, IGeometryMeasurementService measurement,
    IOptions<UnitPinOptions> unitPins, IOptions<FormsOptions> forms, IOptions<RegistersOptions> registers) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.Register;

    private sealed record Faas(TaxDeclaration Td, Assessment? Assessment, DateOnly EnteredOn);

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var run = await db.RegisterRuns.AsNoTracking().Include(x => x.Barangay).ThenInclude(b => b!.Municipality).ThenInclude(m => m!.Province)
            .Include(x => x.Barangay).ThenInclude(b => b!.CityDistrict).Include(x => x.Section)
            .Include(x => x.Classification).Include(x => x.Taxpayer).FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (run is null)
        {
            return null;
        }
        var ct = cancellationToken;
        var roll = run.Kind switch
        {
            RegisterKind.AssessmentRollTaxable => await RollRowsAsync(run, Taxability.Taxable, ct),
            RegisterKind.AssessmentRollExempt => await RollRowsAsync(run, Taxability.Exempt, ct),
            _ => default((List<object> Rows, List<RollLine> Lines)?),
        };
        object rows = run.Kind switch
        {
            RegisterKind.TaxMapControlRoll => run.SectionId is null ? await TaxMapRowsAsync(run, ct) : await SectionTaxMapRowsAsync(run, ct),
            RegisterKind.PreTaxMapControlRoll => await PreTaxMapRowsAsync(run, ct),
            RegisterKind.AssessmentRollTaxable or RegisterKind.AssessmentRollExempt => roll!.Value.Rows,
            RegisterKind.OwnershipRecordCard => await OwnershipRowsAsync(run, ct),
            RegisterKind.RecordOfAssessment => await RecordRowsAsync(run, ct),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterKind)}: {run.Kind}"),
        };
        var owner = run.Taxpayer;
        var index = run.BarangayId is { } barangayId ? await PinContexts.ForBarangayAsync(db, barangayId, run.AsOf.Year, run.Section?.IndexNumber, ct) : null;
        // A city or Metro Manila municipality with its own 3-digit number heads the PIN in place of the province (MRPAAO p.35).
        var cityHeads = run.Barangay?.Municipality?.PinIndexNumber is { Length: 3 };
        var data = FormData.ToJson(new
        {
            register = new
            {
                kind = run.Kind.ToString(), formCode = RegisterService.FormCode(run.Kind), asOf = run.AsOf, fromDate = run.FromDate, remarks = run.Remarks,
                barangay = run.Barangay?.Name, barangayCode = run.Barangay?.PsgcCode,
                municipality = run.Barangay?.Municipality?.Name, municipalityCode = run.Barangay?.Municipality?.PsgcCode,
                province = run.Barangay?.Municipality?.Province?.Name, provinceCode = run.Barangay?.Municipality?.Province?.PsgcCode,
                classification = run.Classification?.Name, classificationCode = run.Classification?.Code,
                // The assessor's index numbers (MRPAAO Ch. II §1), null where not yet set.
                index = index is null ? null : new
                {
                    lguName = cityHeads ? run.Barangay!.Municipality!.Name : run.Barangay!.Municipality!.Province!.Name, lgu = index.LguIndex,
                    municipalityName = run.Barangay.CityDistrict?.Name ?? (cityHeads ? null : run.Barangay.Municipality.Name), municipality = index.MunicipalityIndex,
                    barangay = index.BarangayIndex, section = run.Section?.IndexNumber,
                },
                sectionRetiredOn = run.Section?.RetiredOn,
                // The general revision year: the revision year of the SMV the listed assessments used.
                revisionYear = await RevisionYearAsync(run, ct),
                owner = owner is null ? null : new
                {
                    name = TaxpayerNameFormatter.Format(owner.TaxpayerType, owner.LastName, owner.FirstName, owner.MiddleName, owner.Suffix, owner.CorporateName),
                    address = owner.Address, tin = owner.Tin, contactNumber = owner.ContactNumber, since = owner.CreatedAt,
                },
            },
            rows,
            // The LAM versions (docs/analysis/records-and-forms.md §4.2): each row also carries a `lam` object; the
            // ownership record form's heading adds the owner's email (and sex when enabled).
            lam = owner is null ? null : new
            {
                ownerEmail = owner.Email,
                ownerSex = forms.Value.PrintOwnerSex ? owner.Sex?.ToString() : null,
            },
        });
        return new FormSubjectData(null, data, null, roll?.Lines);
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

    private async Task<(string Names, string? Address)> OwnersAsync(Guid propertyId, Guid? rpuId, DateOnly asOf, CancellationToken ct)
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
        await UnitPin.ForUnitAsync(db, unitPins.Value, rpu, p.PropertyIdentificationNumber, await db.PropertyTaxpayers.AnyAsync(
            x => x.RpuId == rpu.Id && x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf), ct), ct);

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

    /// <summary>The administrators of the unit (else the property) on the date, with the first address recorded (LAM Assessment Roll).</summary>
    private async Task<(string? Names, string? Address)> AdministratorAsync(Guid propertyId, Guid rpuId, DateOnly asOf, CancellationToken ct)
    {
        var rows = (await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, propertyId, rpuId,
                x => x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf), ct), ct))
            .Where(o => o.Role is not (PropertyPartyRole.Owner or PropertyPartyRole.UnknownOwner)).ToList();
        return (rows.Count == 0 ? null : string.Join("; ", rows.Select(o => o.TaxpayerDisplayName)),
            rows.Select(o => o.Address).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)));
    }

    /// <summary>The number of the latest issued or served notice giving the assessment (LAM ownership record form).</summary>
    private async Task<string?> NoticeNumberAsync(Guid? assessmentId, CancellationToken ct) =>
        assessmentId is not { } id ? null
            : await db.NoticesOfAssessment.Where(n => (n.AssessmentId == id || n.Items.Any(i => i.AssessmentId == id))
                    && (n.Status == NoticeStatus.Issued || n.Status == NoticeStatus.Served))
                .OrderByDescending(n => n.IssuedAt).Select(n => n.NoticeNumber).FirstOrDefaultAsync(ct);

    /// <summary>The section and parcel numbers of the property's permanent PIN in force on the date (LAM Record of Assessment).</summary>
    private async Task<string?> SectionParcelAsync(Guid propertyId, DateOnly asOf, CancellationToken ct) =>
        await SectionAndParcelAsync(propertyId, asOf, ct) is { Section: { } section } sp ? $"{section}-{sp.Parcel}" : null;

    /// <summary>The section index and the parcel number (as the configured PIN prints it, CLAUDE.md §115) of the permanent PIN in force.</summary>
    private async Task<(string? Section, string? Parcel)> SectionAndParcelAsync(Guid propertyId, DateOnly asOf, CancellationToken ct)
    {
        var pin = (await db.PinAssignments.AsNoTracking().Include(x => x.Section)
                .Where(x => x.PropertyId == propertyId && x.Kind == PinKind.Permanent).ToListAsync(ct))
            .Where(x => clock.LocalDate(x.AssignedAt) <= asOf && (x.RetiredAt is not { } r || clock.LocalDate(r) > asOf))
            .MaxBy(x => x.AssignedAt);
        return pin?.Section?.IndexNumber is { } section ? (section, ParcelNumber(pin.Pin)) : (null, null);
    }

    // ---------------- Registers ----------------

    /// <summary>MRPAAO Att. 5 (p.158–159): one row per land parcel of the barangay, in PIN order.</summary>
    private async Task<List<object>> TaxMapRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => x.Property!.BarangayId == run.BarangayId), run.AsOf, ct);
        var rows = new List<(string Pin, object Row)>();
        foreach (var f in all.Where(x => x.Td.Rpu!.RpuType == RpuType.Land))
        {
            rows.Add(await TaxMapRowAsync(f, await PropertyAsync(f.Td.PropertyId, ct), all, run.AsOf, ct));
        }
        return rows.OrderBy(x => x.Pin, StringComparer.Ordinal).Select(x => x.Row).ToList();
    }

    /// <summary>
    /// The post-TMCR of one tax map section (MRPAAO Ch. II §2 C.5, Figure 10): every
    /// parcel given a permanent PIN in the section by the run's date, in parcel-number
    /// order. A PIN retired by then (subdivision, consolidation) is noted, not dropped
    /// (Ch. II §1 D.4); a parcel without a land FAAS in force is listed from its parcel.
    /// </summary>
    private async Task<List<object>> SectionTaxMapRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var assignments = (await db.PinAssignments.AsNoTracking().Include(x => x.Parcel)
                .Where(x => x.SectionId == run.SectionId && x.Kind == PinKind.Permanent).ToListAsync(ct))
            .Where(x => clock.LocalDate(x.AssignedAt) <= run.AsOf)
            .OrderBy(x => x.ParcelNumber).ThenBy(x => x.AssignedAt).ToList();
        var propertyIds = assignments.Select(x => x.PropertyId).Distinct().ToList();
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => propertyIds.Contains(x.PropertyId)), run.AsOf, ct);
        var rows = new List<object>();
        foreach (var a in assignments)
        {
            var lot = ParcelNumber(a.Pin);
            if (a.RetiredAt is { } retired && clock.LocalDate(retired) <= run.AsOf)
            {
                rows.Add(new { assessorLotNumber = lot, pin = a.Pin, retired = true, remarks = $"PIN retired {clock.LocalDate(retired):yyyy-MM-dd}: {a.RetirementReason}" });
                continue;
            }
            var p = await PropertyAsync(a.PropertyId, ct);
            var lands = all.Where(x => x.Td.PropertyId == a.PropertyId && x.Td.Rpu!.RpuType == RpuType.Land).ToList();
            foreach (var f in lands)
            {
                rows.Add((await TaxMapRowAsync(f, p, all, run.AsOf, ct)).Row);
            }
            if (lands.Count == 0)
            {
                var (owners, _) = await OwnersAsync(p.Id, null, run.AsOf, ct);
                rows.Add(new
                {
                    assessorLotNumber = lot, pin = a.Pin, surveyNumber = a.Parcel?.SurveyNumber ?? p.SurveyNumber,
                    lotNumber = a.Parcel?.LotNumber ?? p.LotNumber, blockNumber = a.Parcel?.BlockNumber ?? p.BlockNumber,
                    titleNumber = p.TitleNumber, area = a.Parcel?.Area, areaUnit = a.Parcel?.Area is null ? null : "sqm",
                    owner = owners, buildings = 0, machinery = false, others = "", remarks = "No land FAAS in force",
                    lam = new
                    {
                        cadastralNumber = a.Parcel?.CadastralNumber ?? p.CadastralNumber,
                        previousPin = await LamFormData.PreviousPinAsync(db, clock, p.Id, run.AsOf, ct),
                        marketValue = (decimal?)null,
                    },
                });
            }
        }
        return rows;
    }

    /// <summary>
    /// The pre-TMCR (MRPAAO Ch. II §2 A.d, Figure 3): the land FAAS in force in the
    /// barangay, in temporary-PIN order, with the final PIN once tax-mapped, the
    /// declared area and the area measured from the tax-mapped parcel, and the office
    /// tie-up and field confirmation marks recorded by the run's date (step 10a-5).
    /// </summary>
    private async Task<List<object>> PreTaxMapRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => x.Property!.BarangayId == run.BarangayId), run.AsOf, ct);
        var lands = all.Where(x => x.Td.Rpu!.RpuType == RpuType.Land).ToList();
        var propertyIds = lands.Select(x => x.Td.PropertyId).Distinct().ToList();
        var pins = (await db.PinAssignments.AsNoTracking().Where(x => propertyIds.Contains(x.PropertyId)).ToListAsync(ct))
            .Where(x => clock.LocalDate(x.AssignedAt) <= run.AsOf).ToLookup(x => x.PropertyId);
        var parcels = (await db.Parcels.AsNoTracking().Where(x => propertyIds.Contains(x.PropertyId) && x.Status == RecordStatus.Active).ToListAsync(ct))
            .ToLookup(x => x.PropertyId);
        var measured = await measurement.GetParcelAreasAsync(parcels.SelectMany(g => g).Select(x => x.Id).ToList(), ct);
        var rows = new List<(string? Temporary, string Owner, object Row)>();
        foreach (var f in lands)
        {
            var p = await PropertyAsync(f.Td.PropertyId, ct);
            var temporaryPin = pins[p.Id].Where(x => x.Kind == PinKind.Temporary).MaxBy(x => x.AssignedAt);
            var temporary = temporaryPin?.Pin;
            var final = pins[p.Id].Where(x => x.Kind == PinKind.Permanent && (x.RetiredAt is not { } r || clock.LocalDate(r) > run.AsOf)).MaxBy(x => x.AssignedAt)?.Pin;
            var parcel = parcels[p.Id].OrderByDescending(x => x.SectionId is not null).ThenBy(x => x.CreatedAt).FirstOrDefault();
            var (owners, address) = await OwnersAsync(p.Id, f.Td.RpuId, run.AsOf, ct);
            var (area, unit) = await AreaAsync(f.Td, ct);
            var units = all.Where(x => x.Td.PropertyId == p.Id).Select(x => KindCode(x.Td.Rpu!.RpuType)).Where(k => k is "B" or "M").Distinct().Order();
            rows.Add((temporary, owners, new
            {
                temporaryPin = temporary, finalPin = final,
                officeTieUp = temporaryPin?.OfficeTieUpAt is { } o && clock.LocalDate(o) <= run.AsOf,
                fieldConfirmed = temporaryPin?.FieldConfirmedAt is { } f2 && clock.LocalDate(f2) <= run.AsOf,
                tieUpRemarks = temporaryPin?.TieUpRemarks, owner = owners, ownerAddress = address, tdNumber = f.Td.TaxDeclarationNumber,
                surveyBefore = p.SurveyNumber, surveyAfter = parcel?.SurveyNumber is { } s && s != p.SurveyNumber ? s : null,
                lotNumber = p.LotNumber, titleNumber = p.TitleNumber,
                areaDeclared = area, areaUnit = unit,
                areaTaxMapped = parcel is not null && measured.TryGetValue(parcel.Id, out var m) ? Math.Round(m, 2) : (decimal?)null,
                kindOfLand = f.Td.Classification!.Code, improvement = string.Concat(units), remarks = f.Td.TransactionCode,
            }));
        }
        // Temporary PINs first, in their order; land not given one yet follows, by owner.
        return rows.OrderBy(x => x.Temporary is null).ThenBy(x => x.Temporary, StringComparer.Ordinal).ThenBy(x => x.Owner, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Row).ToList();
    }

    /// <summary>One TMCR row: a land FAAS in force, with the property's other units.</summary>
    private async Task<(string Pin, object Row)> TaxMapRowAsync(Faas f, PropertyEntity p, List<Faas> all, DateOnly asOf, CancellationToken ct)
    {
        var units = all.Where(x => x.Td.PropertyId == f.Td.PropertyId).Select(x => x.Td.Rpu!.RpuType).ToList();
        var machineRpus = all.Where(x => x.Td.PropertyId == f.Td.PropertyId && x.Td.Rpu!.RpuType == RpuType.Machinery).Select(x => x.Td.RpuId).ToList();
        var improvements = await db.LandImprovements.Where(i => db.Lands.Any(l => l.Id == i.LandId && l.RpuId == f.Td.RpuId))
            .Select(i => i.ImprovementKind!.Name).Distinct().ToListAsync(ct);
        var (area, unit) = await AreaAsync(f.Td, ct);
        var (owners, _) = await OwnersAsync(f.Td.PropertyId, f.Td.RpuId, asOf, ct);
        return (p.PropertyIdentificationNumber, new
        {
            assessorLotNumber = ParcelNumber(p.PropertyIdentificationNumber), pin = p.PropertyIdentificationNumber,
            surveyNumber = p.SurveyNumber, lotNumber = p.LotNumber, blockNumber = p.BlockNumber,
            titleNumber = p.TitleNumber, area, areaUnit = unit, classCode = f.Td.Classification!.Code, owner = owners,
            arpNumber = Arp(f), tdNumber = f.Td.TaxDeclarationNumber,
            buildings = units.Count(t => t == RpuType.Building), machinery = units.Any(t => t == RpuType.Machinery),
            others = string.Join(", ", improvements.Concat(units.Where(t => t == RpuType.OtherImprovement).Select(_ => "other improvement")).Distinct()),
            remarks = f.Td.TransactionCode,
            lam = new
            {
                cadastralNumber = await db.Parcels.Where(x => x.PropertyId == p.Id && x.Status == RecordStatus.Active && x.CadastralNumber != null)
                    .OrderBy(x => x.CreatedAt).Select(x => x.CadastralNumber).FirstOrDefaultAsync(ct) ?? p.CadastralNumber,
                previousPin = await LamFormData.PreviousPinAsync(db, clock, p.Id, asOf, ct),
                marketValue = f.Assessment?.MarketValue,
                // The LAM's count of machines (the MRPAAO column only marks whether there are any).
                machineCount = await db.MachineryUnits.CountAsync(m => machineRpus.Contains(m.RpuId), ct),
            },
        });
    }

    /// <summary>The 1-based page and line of the roll's row at <paramref name="index"/>; 0 rows per page numbers every line on page 1.</summary>
    public static (int Page, int Line) RollPosition(int index, int rowsPerPage) =>
        rowsPerPage > 0 ? (index / rowsPerPage + 1, index % rowsPerPage + 1) : (1, index + 1);

    /// <summary>
    /// MRPAAO Att. 6–7 (p.160–163): the FAAS of the barangay, taxable or exempt; a supplement lists only those entered
    /// since the from-date. Each row's page and line follow <see cref="RegistersOptions.AssessmentRollRowsPerPage"/>,
    /// and are recorded per TD when the roll is issued (docs/analysis/records-and-forms.md §4.3).
    /// </summary>
    private async Task<(List<object> Rows, List<RollLine> Lines)> RollRowsAsync(RegisterRun run, Taxability taxability, CancellationToken ct)
    {
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => x.Property!.BarangayId == run.BarangayId), run.AsOf, ct);
        var perPage = registers.Value.AssessmentRollRowsPerPage;
        var rows = new List<object>();
        var lines = new List<RollLine>();
        foreach (var f in all.Where(x => taxability == Taxability.Taxable ? x.Td.Taxability == Taxability.Taxable : x.Td.Taxability != Taxability.Taxable)
                     .Where(x => run.FromDate is not { } from || x.EnteredOn >= from)
                     .OrderBy(x => x.Td.Rpu!.RpuNumber))
        {
            var p = await PropertyAsync(f.Td.PropertyId, ct);
            var (owners, address) = await OwnersAsync(f.Td.PropertyId, f.Td.RpuId, run.AsOf, ct);
            var (administrator, administratorAddress) = await AdministratorAsync(f.Td.PropertyId, f.Td.RpuId, run.AsOf, ct);
            var (page, lineNumber) = RollPosition(rows.Count, perPage);
            var (section, parcel) = await SectionAndParcelAsync(p.Id, run.AsOf, ct);
            var useCode = await db.ActualUses.Where(x => x.Id == f.Td.ActualUseId).Select(x => x.Code).FirstOrDefaultAsync(ct);
            var line = new RollLine(f.Td.Id, run.Kind, page, lineNumber);
            lines.Add(line);
            rows.Add(new
            {
                arpNumber = Arp(f), tdNumber = f.Td.TaxDeclarationNumber,
                pin = await UnitPinAsync(p, f.Td.Rpu!, run.AsOf, ct),
                lotBlock = $"{p.LotNumber ?? "—"} / {p.BlockNumber ?? "—"}", owner = owners, ownerAddress = address,
                kind = KindCode(f.Td.Rpu!.RpuType), classCode = f.Td.Classification!.Code, assessedValue = f.Assessment?.AssessedValue,
                previousArpNumber = f.Td.PreviousTaxDeclaration is { } prev
                    ? faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? prev.TaxDeclarationNumber : prev.Assessment?.FaasNumber : null, previousTdNumber = f.Td.PreviousTaxDeclaration?.TaxDeclarationNumber,
                legalBasis = (string?)null, // exemptions are not yet modelled (CLAUDE.md §43)
                effectivity = Effectivity(f.Td.EffectivityDate), remarks = f.Td.TransactionCode, enteredOn = f.EnteredOn,
                lam = new { administrator, administratorAddress, page = line.Page, line = line.Line, sectionIndex = section, parcelNumber = parcel, actualUseCode = useCode },
            });
        }
        return (rows, lines);
    }

    /// <summary>
    /// MRPAAO Att. 8 (p.164–166): the owner's properties in force, the owner's own units and the properties they own
    /// whole. With <see cref="RegisterRun.IncludePastOwners"/>, also the units the owner held before, each as declared
    /// on the last day held and marked past (records-and-forms.md §4.5).
    /// </summary>
    private async Task<List<object>> OwnershipRowsAsync(RegisterRun run, CancellationToken ct)
    {
        var asOf = run.AsOf;
        var holdings = await db.PropertyTaxpayers.AsNoTracking()
            .Where(x => x.TaxpayerId == run.TaxpayerId && x.Role == PropertyPartyRole.Owner && x.StartDate <= asOf)
            .Select(x => new { x.PropertyId, x.EndDate, x.EndReason }).ToListAsync(ct);
        var propertyIds = holdings.Where(h => h.EndDate is null || h.EndDate > asOf).Select(h => h.PropertyId).Distinct().ToList();
        var all = await FaasInForceAsync(db.TaxDeclarations.Where(x => propertyIds.Contains(x.PropertyId)), asOf, ct);
        var rows = new List<object>();
        var listed = new HashSet<Guid>();
        foreach (var f in all.OrderBy(x => x.Td.PropertyId).ThenBy(x => x.Td.Rpu!.RpuNumber))
        {
            if (await OwnedAsync(f, run.TaxpayerId!.Value, asOf, lastDayHeld: false, ct))
            {
                listed.Add(f.Td.Id);
                rows.Add(await OwnershipRowAsync(f, asOf, past: null, ct));
            }
        }
        if (!run.IncludePastOwners)
        {
            return rows;
        }
        // Holdings ended by the run's date, latest first; a unit already listed (held again, or still held) is not repeated.
        foreach (var h in holdings.Where(h => h.EndDate is { } end && end <= asOf).OrderByDescending(h => h.EndDate))
        {
            var lastDay = h.EndDate!.Value;
            var then = await FaasInForceAsync(db.TaxDeclarations.Where(x => x.PropertyId == h.PropertyId), lastDay, ct);
            foreach (var f in then.OrderBy(x => x.Td.Rpu!.RpuNumber))
            {
                if (listed.Add(f.Td.Id) && await OwnedAsync(f, run.TaxpayerId!.Value, lastDay, lastDayHeld: true, ct))
                {
                    rows.Add(await OwnershipRowAsync(f, lastDay, new { heldUntil = lastDay, endReason = h.EndReason }, ct));
                }
            }
        }
        return rows;
    }

    /// <summary>
    /// The unit is the owner's when the owner holds it, or holds the property and the unit has no owners of its own.
    /// In force on a date: ended after it, as everywhere in the registers; <paramref name="lastDayHeld"/> also counts a
    /// holding ending on the date itself (a past holding, on its last day).
    /// </summary>
    private async Task<bool> OwnedAsync(Faas f, Guid taxpayerId, DateOnly on, bool lastDayHeld, CancellationToken ct)
    {
        var scoped = await PropertyParties.ScopeAsync(db, f.Td.PropertyId, f.Td.RpuId,
            x => x.StartDate <= on && (x.EndDate == null || x.EndDate > on || (lastDayHeld && x.EndDate == on)), ct);
        return await scoped.AnyAsync(x => x.TaxpayerId == taxpayerId && x.Role == PropertyPartyRole.Owner, ct);
    }

    /// <summary>One ORF row, as declared on <paramref name="on"/>; <paramref name="past"/> is null for a unit still held.</summary>
    private async Task<object> OwnershipRowAsync(Faas f, DateOnly on, object? past, CancellationToken ct)
    {
        var p = await PropertyAsync(f.Td.PropertyId, ct);
        var (area, unit) = await AreaAsync(f.Td, ct);
        var previousOwner = f.Td.PreviousTaxDeclaration is { } prev
            ? (await OwnersAsync(prev.PropertyId, prev.RpuId, Entered(prev), ct)).Names : null;
        return new
        {
            enteredOn = f.EnteredOn, kind = KindCode(f.Td.Rpu!.RpuType), classCode = f.Td.Classification!.Code,
            pin = await UnitPinAsync(p, f.Td.Rpu, on, ct),
            titleNumber = p.TitleNumber, lotBlock = $"{p.LotNumber ?? "—"} / {p.BlockNumber ?? "—"}",
            arpNumber = Arp(f), tdNumber = f.Td.TaxDeclarationNumber, previousOwner,
            location = string.Join(", ", new[] { p.Street, p.Barangay!.Name }.Where(x => !string.IsNullOrWhiteSpace(x))),
            area, areaUnit = unit, marketValue = f.Assessment?.MarketValue, assessedValue = f.Assessment?.AssessedValue,
            remarks = f.Td.TransactionCode,
            lam = new
            {
                noticeNumber = await NoticeNumberAsync(f.Assessment?.Id, ct),
                machines = f.Td.Rpu.RpuType != RpuType.Machinery ? null : string.Join("; ", (await db.MachineryUnits.Where(m => m.RpuId == f.Td.RpuId)
                        .OrderBy(m => m.CreatedAt).Select(m => new { m.Brand, m.Model, m.Capacity, m.CapacityUnit }).ToListAsync(ct))
                    .Select(m => string.Join(" ", new[] { m.Brand, m.Model, m.Capacity is { } c ? $"{c:0.##} {m.CapacityUnit}".Trim() : null }
                        .Where(x => !string.IsNullOrWhiteSpace(x))))),
                // Null while held; the last day held and why it ended for a unit the owner no longer holds.
                past,
                // Provincial records add the municipality to the location (LAM Annex I-J).
                municipality = await db.Municipalities.Where(x => x.Id == p.MunicipalityId).Select(x => x.Name).FirstOrDefaultAsync(ct),
            },
        };
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
                lam = new
                {
                    sectionParcel = await SectionParcelAsync(p.Id, Entered(td), ct),
                    buildingArea = kind == "B" ? (await AreaAsync(td, ct)).Area : null,
                    marketValueTaxable = taxable ? mv : null, marketValueExempt = taxable ? null : mv,
                    assessedValueTaxable = taxable ? av : null, assessedValueExempt = taxable ? null : av,
                },
            });
        }
        return rows;
    }
}
