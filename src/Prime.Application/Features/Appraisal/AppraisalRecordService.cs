using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Properties;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Appraisal;

public sealed record AppraisalPropertyDto(
    Guid Id, string Pin, string? Street, string? Sitio, string? LotNumber, string? BlockNumber, string? SurveyNumber,
    string? TitleNumber, string? TaxMapNumber, string Barangay, string Municipality, string Province,
    string? TitleType = null, DateOnly? TitleDate = null,
    string? BoundaryNorth = null, string? BoundaryEast = null, string? BoundarySouth = null, string? BoundaryWest = null);

public sealed record AppraisalPartyDto(string Name, PropertyPartyRole Role, string RoleLabel, decimal SharePercent, string? Address);

public sealed record AppraisalRpuDto(Guid Id, string Number, RpuType Type);

public sealed record AppraisalTaxDeclarationDto(Guid Id, string Number, int RevisionNumber, DateOnly EffectivityDate, WorkflowStatus Status,
    string? TransactionCode = null);

/// <summary>The Record of Assessment entry: when the assessment was posted, and by whom (MRPAAO Att. 1–3).</summary>
public sealed record AppraisalRecordEntryDto(DateTimeOffset PostedAt, string? PostedBy);

public sealed record AppraisalLandDto(
    Guid Id, decimal Area, string AreaUnit, string Classification, string ActualUse, string? SubClassification, string? Zone,
    decimal? LocationFactor, decimal? RoadFrontage, string? RoadType, bool IsCornerLot, string? Zoning);

public sealed record AppraisalBuildingComponentDto(string ComponentType, string? Description, decimal? Quantity, decimal? UnitCost, decimal? Cost);

public sealed record AppraisalBuildingFloorDto(int FloorNumber, decimal Area);

public sealed record AppraisalBuildingMaterialDto(string StructuralPart, string Material, int? FloorNumber);

public sealed record AppraisalBuildingDto(
    Guid Id, string BuildingType, string StructuralType, string ActualUse, int NumberOfStoreys, decimal FloorArea, decimal TotalFloorArea,
    int? YearConstructed, int? YearCompleted, string Condition, decimal CompletionPercentage,
    IReadOnlyList<AppraisalBuildingComponentDto> Components,
    string? BuildingPermitNumber = null, DateOnly? BuildingPermitDate = null, string? CondominiumCertificateNumber = null,
    DateOnly? CertificateOfCompletionDate = null, DateOnly? CertificateOfOccupancyDate = null, DateOnly? DateConstructed = null,
    DateOnly? DateOccupied = null, IReadOnlyList<AppraisalBuildingFloorDto>? Floors = null,
    IReadOnlyList<AppraisalBuildingMaterialDto>? Materials = null);

public sealed record AppraisalMachineryDto(
    Guid Id, string MachineryType, string? Description, string? Brand, string? Model, string? SerialNumber, decimal? Capacity,
    string? CapacityUnit, DateOnly? DateAcquired, decimal AcquisitionCost, decimal? InstallationCost, decimal? OtherCost, bool IsBrandNew,
    decimal? ReplacementCost, int? EconomicLifeYears, int? RemainingLifeYears,
    int? YearInstalled = null, int? YearOfInitialOperation = null, decimal? ConversionFactor = null);

public sealed record AppraisalSmvDto(Guid Id, string OrdinanceNumber, DateOnly OrdinanceDate, DateOnly EffectivityDate, int RevisionYear, string? Description);

public sealed record AppraisalBreakdownLineDto(string Key, decimal Value);

/// <summary>One FAAS appraisal row, with its own schedule rate and breakdown (docs/analysis/mrpaao-forms-model.md §8.2).</summary>
public sealed record AppraisalValuationLineDto(
    int Sequence, ValuationLineSource Source, string? Description, string? Classification, string? SubClassification, string? ActualUse,
    decimal? Quantity, string? Unit, decimal? UnitValue, decimal MarketValue, IReadOnlyList<AppraisalBreakdownLineDto> Breakdown);

public sealed record AppraisalValuationDto(
    Guid Id, ValuationMethod Method, decimal MarketValue, DateOnly EffectiveDate, DateTimeOffset ComputedAt,
    AppraisalSmvDto? Smv, string? ScheduleUnit, decimal? ScheduleRate, IReadOnlyList<AppraisalBreakdownLineDto> Breakdown,
    IReadOnlyList<AppraisalValuationLineDto> Lines);

/// <summary>One FAAS "Property Assessment" row, with the level row it used (frozen percent, bracket, ordinance).</summary>
public sealed record AppraisalAssessmentLineDto(
    int Sequence, string Classification, string ActualUse, string PropertyType, decimal MarketValue, decimal AssessmentLevelPercent,
    decimal LevelLowerValue, decimal? LevelUpperValue, string LevelOrdinanceNumber, DateOnly? LevelOrdinanceDate, decimal AssessedValue);

/// <summary>
/// The assessment totals. The single-row fields (classification … level
/// ordinance) are the principal line's — the largest by market value;
/// <see cref="AssessmentLevelPercent"/> is null when the lines carry
/// different levels. <see cref="Lines"/> are the FAAS rows.
/// </summary>
public sealed record AppraisalAssessmentDto(
    int Year, DateOnly EffectiveDate, string Classification, string ActualUse, string PropertyType,
    decimal MarketValue, decimal? AssessmentLevelPercent, decimal LevelLowerValue, decimal? LevelUpperValue,
    string LevelOrdinanceNumber, DateOnly? LevelOrdinanceDate, decimal AssessedValue,
    Guid? RevisionReference, string? Remarks, IReadOnlyList<AppraisalAssessmentLineDto> Lines);

public sealed record AppraisalPreviousDto(Guid AssessmentId, string? FaasNumber, int Year, DateOnly EffectiveDate, decimal MarketValue,
    decimal AssessedValue, decimal AssessedValueChange);

/// <summary>A completed approval step, as frozen when it was signed.</summary>
public sealed record AppraisalSignatureDto(string Label, string Name, string? Position, DateTimeOffset SignedAt);

public sealed record AppraisalNoticeDto(Guid Id, string? Number, NoticeStatus Status, DateTimeOffset? IssuedAt, DateOnly? ReceivedDate, DateOnly? AppealDeadline);

/// <summary>
/// The appraisal record of one assessment — the data a FAAS shows,
/// whatever its eventual layout (docs/FORMS-REVISION-PLAN.md A7).
/// Values that decided the result come from the frozen records (the
/// valuation's inputs and breakdown, the assessment level row and
/// percentage it used, the approval records); the asset's descriptive
/// fields are its registered values.
/// </summary>
public sealed record AppraisalRecordDto(
    Guid AssessmentId, string? FaasNumber, ValuationSourceType Kind, WorkflowStatus Status,
    AppraisalPropertyDto Property,
    DateOnly PartiesAsOf, IReadOnlyList<AppraisalPartyDto> Parties,
    AppraisalRpuDto Rpu, AppraisalTaxDeclarationDto? TaxDeclaration,
    AppraisalLandDto? Land, AppraisalBuildingDto? Building, AppraisalMachineryDto? Machinery, IReadOnlyList<AppraisalMachineryDto> MachineryUnits,
    AppraisalValuationDto Valuation, AppraisalAssessmentDto Assessment, AppraisalPreviousDto? Previous,
    string? RecordedBy, DateTimeOffset RecordedAt, IReadOnlyList<AppraisalSignatureDto> Signatures,
    IReadOnlyList<AppraisalNoticeDto> Notices,
    AppraisalRecordEntryDto? RecordEntry = null);

public interface IAppraisalRecordService
{
    Task<Result<AppraisalRecordDto>> GetAsync(Guid assessmentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assembles the appraisal record (FAAS aggregate) from property, parties,
/// RPU, Tax Declaration, the valued asset, valuation, assessment, approval
/// records and notices. Read-only: it computes nothing (CLAUDE.md Rule 9) —
/// every value is one PRIME already recorded.
/// </summary>
public sealed class AppraisalRecordService(IApplicationDbContext db, IClock clock) : IAppraisalRecordService
{
    public async Task<Result<AppraisalRecordDto>> GetAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var a = await db.Assessments.AsNoTracking()
            .Include(x => x.Rpu)
            .Include(x => x.Valuation).ThenInclude(v => v!.Smv)
            .Include(x => x.Valuation).ThenInclude(v => v!.SmvSchedule)
            .Include(x => x.Valuation).ThenInclude(v => v!.Lines).ThenInclude(l => l.Classification)
            .Include(x => x.Valuation).ThenInclude(v => v!.Lines).ThenInclude(l => l.SubClassification)
            .Include(x => x.Valuation).ThenInclude(v => v!.Lines).ThenInclude(l => l.ActualUse)
            .Include(x => x.Lines).ThenInclude(l => l.Classification)
            .Include(x => x.Lines).ThenInclude(l => l.ActualUse)
            .Include(x => x.Lines).ThenInclude(l => l.PropertyType)
            .Include(x => x.Lines).ThenInclude(l => l.AssessmentLevel)
            .FirstOrDefaultAsync(x => x.Id == assessmentId, ct);
        if (a is null)
        {
            return Result.Failure<AppraisalRecordDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.");
        }
        var valuation = a.Valuation!;
        var assessmentLines = a.Lines.OrderBy(l => l.Sequence).ToList();
        var principal = assessmentLines.OrderByDescending(l => l.MarketValue).ThenBy(l => l.Sequence).First();
        var level = principal.AssessmentLevel!;

        var property = await db.Properties.AsNoTracking().Where(p => p.Id == a.PropertyId).Select(p => new AppraisalPropertyDto(
            p.Id, p.PropertyIdentificationNumber, p.Street, p.Sitio, p.LotNumber, p.BlockNumber, p.SurveyNumber, p.TitleNumber,
            p.TaxMapNumber, p.Barangay!.Name, p.Municipality!.Name, p.Province!.Name,
            p.TitleType == null ? null : p.TitleType.Name, p.TitleDate, p.BoundaryNorth, p.BoundaryEast, p.BoundarySouth, p.BoundaryWest)).FirstAsync(ct);

        // The parties in whose name the property was declared on the assessment's effective date (LGC §§204–205).
        var eff = a.EffectiveDate;
        var parties = (await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, a.PropertyId, a.RpuId,
                x => x.StartDate <= eff && (x.EndDate == null || x.EndDate > eff), ct), ct))
            .Select(o => new AppraisalPartyDto(o.TaxpayerDisplayName, o.Role, PropertyParties.RoleLabel(o.Role), o.OwnershipPercentage, o.Address))
            .ToList();

        var taxDeclaration = await TaxDeclarationInForceAsync(a.RpuId, eff, ct);

        AppraisalLandDto? land = null;
        AppraisalBuildingDto? building = null;
        AppraisalMachineryDto? machinery = null;
        List<AppraisalMachineryDto> machineryUnits = [];
        switch (valuation.SourceType)
        {
            case ValuationSourceType.Land:
                land = await db.Lands.AsNoTracking().Where(x => x.Id == valuation.SourceId).Select(x => new AppraisalLandDto(
                    x.Id, x.Area, x.AreaUnit, x.Classification!.Name, x.ActualUse!.Name,
                    x.SubClassification == null ? null : x.SubClassification.Name, x.Zone == null ? null : x.Zone.Name,
                    x.LocationFactor, x.RoadFrontage, x.RoadType == null ? null : x.RoadType.Name, x.IsCornerLot, x.Zoning)).FirstOrDefaultAsync(ct);
                break;
            case ValuationSourceType.Building:
                var b = await db.Buildings.AsNoTracking()
                    .Include(x => x.BuildingType).Include(x => x.StructuralType).Include(x => x.ActualUse).Include(x => x.Condition)
                    .Include(x => x.Components).ThenInclude(c => c.ComponentType)
                    .Include(x => x.Floors)
                    .Include(x => x.Materials).ThenInclude(m => m.StructuralPart)
                    .Include(x => x.Materials).ThenInclude(m => m.StructuralMaterial)
                    .FirstOrDefaultAsync(x => x.Id == valuation.SourceId, ct);
                if (b is not null)
                {
                    building = new AppraisalBuildingDto(b.Id, b.BuildingType!.Name, b.StructuralType!.Name, b.ActualUse!.Name, b.NumberOfStoreys,
                        b.FloorArea, b.TotalFloorArea, b.YearConstructed, b.YearCompleted, b.Condition!.Name, b.CompletionPercentage,
                        b.Components.OrderBy(c => c.ComponentType!.Name).ThenBy(c => c.CreatedAt)
                            .Select(c => new AppraisalBuildingComponentDto(c.ComponentType!.Name, c.Description, c.Quantity, c.UnitCost, c.Cost)).ToList(),
                        b.BuildingPermitNumber, b.BuildingPermitDate, b.CondominiumCertificateNumber, b.CertificateOfCompletionDate,
                        b.CertificateOfOccupancyDate, b.DateConstructed, b.DateOccupied,
                        b.Floors.OrderBy(f => f.FloorNumber).Select(f => new AppraisalBuildingFloorDto(f.FloorNumber, f.Area)).ToList(),
                        b.Materials.OrderBy(m => m.StructuralPart!.SortOrder).ThenBy(m => m.FloorNumber)
                            .Select(m => new AppraisalBuildingMaterialDto(m.StructuralPart!.Name, m.StructuralMaterial?.Name ?? m.OtherSpecify!, m.FloorNumber)).ToList());
                }
                break;
            case ValuationSourceType.Machinery:
                // Every machine this valuation valued (one line each), in line order.
                var machineIds = valuation.Lines.OrderBy(l => l.Sequence).Select(l => l.SourceId).OfType<Guid>().ToList();
                var found = await db.MachineryUnits.AsNoTracking().Where(x => machineIds.Contains(x.Id)).Select(x => new AppraisalMachineryDto(
                    x.Id, x.MachineryType!.Name, x.Description, x.Brand, x.Model, x.SerialNumber, x.Capacity, x.CapacityUnit, x.DateAcquired,
                    x.AcquisitionCost, x.InstallationCost, x.OtherCost, x.IsBrandNew, x.ReplacementCost, x.EconomicLifeYears, x.RemainingLifeYears,
                    x.YearInstalled, x.YearOfInitialOperation, x.ConversionFactor))
                    .ToListAsync(ct);
                machineryUnits = machineIds.Select(id => found.FirstOrDefault(m => m.Id == id)).OfType<AppraisalMachineryDto>().ToList();
                machinery = machineryUnits.FirstOrDefault();
                break;
        }

        var previous = await AssessmentHistory.PreviousAsync(db, a, ct);

        var userIds = new[] { a.CreatedBy, a.ApprovedBy, a.PostedBy }.OfType<Guid>().ToList();
        var userNames = await db.AppUsers.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var signatures = (await db.ApprovalRecords.AsNoTracking()
                .Where(x => x.SubjectType == ApprovalSubjectType.Assessment && x.SubjectId == a.Id)
                .OrderBy(x => x.StepSequence).ToListAsync(ct))
            .Select(r => new AppraisalSignatureDto(r.Label, r.SignatoryName, r.SignatoryPosition, r.SignedAt)).ToList();
        if (signatures.Count == 0 && a.ApprovedBy is { } approver && a.ApprovedAt is { } approvedAt)
        {
            // Approved under the two-person maker-checker (no chain in force): the approver is the only signature.
            signatures.Add(new AppraisalSignatureDto("Approved by", userNames.GetValueOrDefault(approver) ?? "(unknown user)", null, approvedAt));
        }

        var notices = await db.NoticesOfAssessment.AsNoTracking().Where(x => x.AssessmentId == a.Id).OrderBy(x => x.CreatedAt)
            .Select(x => new AppraisalNoticeDto(x.Id, x.NoticeNumber, x.Status, x.IssuedAt, x.ReceivedDate, x.AppealDeadline)).ToListAsync(ct);

        return Result.Success(new AppraisalRecordDto(
            a.Id, a.FaasNumber, valuation.SourceType, a.Status,
            property,
            eff, parties,
            new AppraisalRpuDto(a.RpuId, a.Rpu!.RpuNumber, a.Rpu.RpuType),
            taxDeclaration,
            land, building, machinery, machineryUnits,
            new AppraisalValuationDto(valuation.Id, valuation.ValuationMethod, valuation.ComputedMarketValue, valuation.EffectiveDate,
                valuation.ComputedAt,
                valuation.Smv is { } smv ? new AppraisalSmvDto(smv.Id, smv.OrdinanceNumber, smv.OrdinanceDate, smv.EffectivityDate, smv.RevisionYear, smv.Description) : null,
                valuation.SmvSchedule?.Unit, valuation.SmvSchedule?.MarketValue,
                Breakdown(valuation.BreakdownJson),
                valuation.Lines.OrderBy(l => l.Sequence).Select(l => new AppraisalValuationLineDto(l.Sequence, l.Source, l.Description,
                    l.Classification?.Name, l.SubClassification?.Name, l.ActualUse?.Name, l.Quantity, l.Unit, l.UnitValue, l.MarketValue,
                    Breakdown(l.BreakdownJson))).ToList()),
            new AppraisalAssessmentDto(a.AssessmentYear, a.EffectiveDate, principal.Classification!.Name, principal.ActualUse!.Name, principal.PropertyType!.Name,
                a.MarketValue, a.AssessmentPercentage, level.LowerValue, level.UpperValue, level.OrdinanceNumber, level.OrdinanceDate,
                a.AssessedValue, a.RevisionReference, a.Remarks,
                assessmentLines.Select(l => new AppraisalAssessmentLineDto(l.Sequence, l.Classification!.Name, l.ActualUse!.Name, l.PropertyType!.Name,
                    l.MarketValue, l.AssessmentPercentage, l.AssessmentLevel!.LowerValue, l.AssessmentLevel.UpperValue,
                    l.AssessmentLevel.OrdinanceNumber, l.AssessmentLevel.OrdinanceDate, l.AssessedValue)).ToList()),
            previous is null ? null : new AppraisalPreviousDto(previous.Id, previous.FaasNumber, previous.AssessmentYear, previous.EffectiveDate,
                previous.MarketValue, previous.AssessedValue, a.AssessedValue - previous.AssessedValue),
            a.CreatedBy is { } creator ? userNames.GetValueOrDefault(creator) ?? "(unknown user)" : null, a.CreatedAt, signatures,
            notices,
            a.PostedAt is { } postedAt
                ? new AppraisalRecordEntryDto(postedAt, a.PostedBy is { } poster ? userNames.GetValueOrDefault(poster) ?? "(unknown user)" : null)
                : null));
    }

    /// <summary>
    /// The RPU's Tax Declaration in force on <paramref name="date"/>: an approved
    /// declaration effective by then and not cancelled before it (a later
    /// cancellation does not change what was in force). Null when none was.
    /// </summary>
    private async Task<AppraisalTaxDeclarationDto?> TaxDeclarationInForceAsync(Guid rpuId, DateOnly date, CancellationToken ct)
    {
        var candidates = await db.TaxDeclarations.AsNoTracking()
            .Where(x => x.RpuId == rpuId && x.EffectivityDate <= date
                && (x.Status == WorkflowStatus.Approved || x.Status == WorkflowStatus.Cancelled))
            .OrderByDescending(x => x.EffectivityDate).ThenByDescending(x => x.RevisionNumber)
            .Select(x => new { x.Id, x.TaxDeclarationNumber, x.RevisionNumber, x.EffectivityDate, x.Status, x.CancelledAt, x.TransactionCode })
            .ToListAsync(ct);
        var td = candidates.FirstOrDefault(x => x.CancelledAt is not { } cancelledAt || clock.LocalDate(cancelledAt) > date);
        return td is null ? null : new AppraisalTaxDeclarationDto(td.Id, td.TaxDeclarationNumber, td.RevisionNumber, td.EffectivityDate, td.Status, td.TransactionCode);
    }

    private static readonly Dictionary<string, int> BreakdownRank =
        ValuationCalculator.BreakdownOrder.Select((key, i) => (key, i)).ToDictionary(x => x.key, x => x.i);

    // Inputs, then intermediate values, then the market value (ValuationCalculator.BreakdownOrder).
    private static List<AppraisalBreakdownLineDto> Breakdown(string json) =>
        (JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [])
        .OrderBy(kv => kv.Key == "MarketValue" ? int.MaxValue : BreakdownRank.GetValueOrDefault(kv.Key, int.MaxValue - 1))
        .ThenBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => new AppraisalBreakdownLineDto(kv.Key, kv.Value)).ToList();
}
