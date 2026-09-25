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
    string? TitleNumber, string? TaxMapNumber, string Barangay, string Municipality, string Province);

public sealed record AppraisalPartyDto(string Name, PropertyPartyRole Role, string RoleLabel, decimal SharePercent, string? Address);

public sealed record AppraisalRpuDto(Guid Id, string Number, RpuType Type);

public sealed record AppraisalTaxDeclarationDto(Guid Id, string Number, int RevisionNumber, DateOnly EffectivityDate, WorkflowStatus Status);

public sealed record AppraisalLandDto(
    Guid Id, decimal Area, string AreaUnit, string Classification, string ActualUse, string? SubClassification, string? Zone,
    decimal? LocationFactor, decimal? RoadFrontage, string? RoadType, bool IsCornerLot, string? Zoning);

public sealed record AppraisalBuildingComponentDto(string ComponentType, string? Description, decimal? Quantity, decimal? UnitCost, decimal? Cost);

public sealed record AppraisalBuildingDto(
    Guid Id, string BuildingType, string StructuralType, string ActualUse, int NumberOfStoreys, decimal FloorArea, decimal TotalFloorArea,
    int? YearConstructed, int? YearCompleted, string Condition, decimal CompletionPercentage,
    IReadOnlyList<AppraisalBuildingComponentDto> Components);

public sealed record AppraisalMachineryDto(
    Guid Id, string MachineryType, string? Description, string? Brand, string? Model, string? SerialNumber, decimal? Capacity,
    string? CapacityUnit, DateOnly? DateAcquired, decimal AcquisitionCost, decimal? InstallationCost, decimal? OtherCost, bool IsBrandNew,
    decimal? ReplacementCost, int? EconomicLifeYears, int? RemainingLifeYears);

public sealed record AppraisalSmvDto(Guid Id, string OrdinanceNumber, DateOnly OrdinanceDate, DateOnly EffectivityDate, int RevisionYear, string? Description);

public sealed record AppraisalBreakdownLineDto(string Key, decimal Value);

public sealed record AppraisalValuationDto(
    Guid Id, ValuationMethod Method, decimal MarketValue, DateOnly EffectiveDate, DateTimeOffset ComputedAt,
    AppraisalSmvDto? Smv, string? ScheduleUnit, decimal? ScheduleRate, IReadOnlyList<AppraisalBreakdownLineDto> Breakdown);

public sealed record AppraisalAssessmentDto(
    int Year, DateOnly EffectiveDate, string Classification, string ActualUse, string PropertyType,
    decimal MarketValue, decimal AssessmentLevelPercent, decimal LevelLowerValue, decimal? LevelUpperValue,
    string LevelOrdinanceNumber, DateOnly? LevelOrdinanceDate, decimal AssessedValue,
    Guid? RevisionReference, string? Remarks);

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
    AppraisalLandDto? Land, AppraisalBuildingDto? Building, AppraisalMachineryDto? Machinery,
    AppraisalValuationDto Valuation, AppraisalAssessmentDto Assessment, AppraisalPreviousDto? Previous,
    string? RecordedBy, DateTimeOffset RecordedAt, IReadOnlyList<AppraisalSignatureDto> Signatures,
    IReadOnlyList<AppraisalNoticeDto> Notices);

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
            .Include(x => x.AssessmentLevel).ThenInclude(l => l!.Classification)
            .Include(x => x.AssessmentLevel).ThenInclude(l => l!.ActualUse)
            .Include(x => x.AssessmentLevel).ThenInclude(l => l!.PropertyType)
            .FirstOrDefaultAsync(x => x.Id == assessmentId, ct);
        if (a is null)
        {
            return Result.Failure<AppraisalRecordDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.");
        }
        var valuation = a.Valuation!;
        var level = a.AssessmentLevel!;

        var property = await db.Properties.AsNoTracking().Where(p => p.Id == a.PropertyId).Select(p => new AppraisalPropertyDto(
            p.Id, p.PropertyIdentificationNumber, p.Street, p.Sitio, p.LotNumber, p.BlockNumber, p.SurveyNumber, p.TitleNumber,
            p.TaxMapNumber, p.Barangay!.Name, p.Municipality!.Name, p.Province!.Name)).FirstAsync(ct);

        // The parties in whose name the property was declared on the assessment's effective date (LGC §§204–205).
        var eff = a.EffectiveDate;
        var parties = (await PropertyParties.ProjectAsync(db.PropertyTaxpayers.Where(x => x.PropertyId == a.PropertyId
                && x.StartDate <= eff && (x.EndDate == null || x.EndDate > eff)), ct))
            .Select(o => new AppraisalPartyDto(o.TaxpayerDisplayName, o.Role, PropertyParties.RoleLabel(o.Role), o.OwnershipPercentage, o.Address))
            .ToList();

        var taxDeclaration = await TaxDeclarationInForceAsync(a.RpuId, eff, ct);

        AppraisalLandDto? land = null;
        AppraisalBuildingDto? building = null;
        AppraisalMachineryDto? machinery = null;
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
                    .FirstOrDefaultAsync(x => x.Id == valuation.SourceId, ct);
                if (b is not null)
                {
                    building = new AppraisalBuildingDto(b.Id, b.BuildingType!.Name, b.StructuralType!.Name, b.ActualUse!.Name, b.NumberOfStoreys,
                        b.FloorArea, b.TotalFloorArea, b.YearConstructed, b.YearCompleted, b.Condition!.Name, b.CompletionPercentage,
                        b.Components.OrderBy(c => c.ComponentType!.Name).ThenBy(c => c.CreatedAt)
                            .Select(c => new AppraisalBuildingComponentDto(c.ComponentType!.Name, c.Description, c.Quantity, c.UnitCost, c.Cost)).ToList());
                }
                break;
            case ValuationSourceType.Machinery:
                machinery = await db.MachineryUnits.AsNoTracking().Where(x => x.Id == valuation.SourceId).Select(x => new AppraisalMachineryDto(
                    x.Id, x.MachineryType!.Name, x.Description, x.Brand, x.Model, x.SerialNumber, x.Capacity, x.CapacityUnit, x.DateAcquired,
                    x.AcquisitionCost, x.InstallationCost, x.OtherCost, x.IsBrandNew, x.ReplacementCost, x.EconomicLifeYears, x.RemainingLifeYears))
                    .FirstOrDefaultAsync(ct);
                break;
        }

        var previous = await AssessmentHistory.PreviousAsync(db, a, ct);

        var userIds = new[] { a.CreatedBy, a.ApprovedBy }.OfType<Guid>().ToList();
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
            land, building, machinery,
            new AppraisalValuationDto(valuation.Id, valuation.ValuationMethod, valuation.ComputedMarketValue, valuation.EffectiveDate,
                valuation.ComputedAt,
                valuation.Smv is { } smv ? new AppraisalSmvDto(smv.Id, smv.OrdinanceNumber, smv.OrdinanceDate, smv.EffectivityDate, smv.RevisionYear, smv.Description) : null,
                valuation.SmvSchedule?.Unit, valuation.SmvSchedule?.MarketValue,
                Breakdown(valuation.BreakdownJson)),
            new AppraisalAssessmentDto(a.AssessmentYear, a.EffectiveDate, level.Classification!.Name, level.ActualUse!.Name, level.PropertyType!.Name,
                a.MarketValue, a.AssessmentPercentage, level.LowerValue, level.UpperValue, level.OrdinanceNumber, level.OrdinanceDate,
                a.AssessedValue, a.RevisionReference, a.Remarks),
            previous is null ? null : new AppraisalPreviousDto(previous.Id, previous.FaasNumber, previous.AssessmentYear, previous.EffectiveDate,
                previous.MarketValue, previous.AssessedValue, a.AssessedValue - previous.AssessedValue),
            a.CreatedBy is { } creator ? userNames.GetValueOrDefault(creator) ?? "(unknown user)" : null, a.CreatedAt, signatures,
            notices));
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
            .Select(x => new { x.Id, x.TaxDeclarationNumber, x.RevisionNumber, x.EffectivityDate, x.Status, x.CancelledAt })
            .ToListAsync(ct);
        var td = candidates.FirstOrDefault(x => x.CancelledAt is not { } cancelledAt || clock.LocalDate(cancelledAt) > date);
        return td is null ? null : new AppraisalTaxDeclarationDto(td.Id, td.TaxDeclarationNumber, td.RevisionNumber, td.EffectivityDate, td.Status);
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
