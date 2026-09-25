using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Forms;

/// <summary>
/// The FAAS as the MRPAAO prints it (Att. 1–3; docs/analysis/mrpaao-forms-model.md
/// §13): a Tax Declaration together with the assessment it declares. The
/// subject is the TD; its number is the FAAS (ARP) number. Every table row the
/// forms print is prepared here from recorded values — templates only lay them
/// out, and compute nothing.
/// </summary>
public sealed class FaasFormDataProvider(IApplicationDbContext db, IAppraisalRecordService appraisals, IOptions<FaasOptions> faas, IClock clock) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.Faas;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var td = await db.TaxDeclarations.AsNoTracking().Include(x => x.Rpu).Include(x => x.Assessment)
            .FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (td is null)
        {
            return null;
        }
        var record = td.AssessmentId is { } assessmentId ? await appraisals.GetAsync(assessmentId, cancellationToken) : null;
        var r = record is { IsSuccess: true } ? record.Value : null;
        var rpu = td.Rpu!;
        var eff = td.EffectivityDate;

        var pin = await db.Properties.Where(p => p.Id == td.PropertyId).Select(p => p.PropertyIdentificationNumber).FirstAsync(cancellationToken);
        var parties = await PartiesAsync(td.PropertyId, td.RpuId, eff, cancellationToken);
        var ownedSeparately = await db.PropertyTaxpayers.AnyAsync(x => x.RpuId == td.RpuId && x.IsCurrent
            && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner), cancellationToken);

        var number = faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? td.TaxDeclarationNumber : td.Assessment?.FaasNumber;
        var data = FormData.ToJson(new
        {
            faas = new
            {
                number,
                tdNumber = td.TaxDeclarationNumber,
                transactionCode = td.TransactionCode,
                kind = rpu.RpuType.ToString(),
                pin = UnitPin.Compose(pin, rpu.PinSuffix, ownedSeparately),
                tdStatus = td.Status.ToString(),
                taxability = td.Taxability.ToString(),
                effectivity = new { date = eff, quarter = (eff.Month - 1) / 3 + 1, year = eff.Year },
                memoranda = string.Join(" ", new[] { td.Remarks, r?.Assessment.Remarks }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                owners = parties.Where(p => p.role is "Owner" or "UnknownOwner"),
                administrators = parties.Where(p => p.role is not ("Owner" or "UnknownOwner")),
                superseded = await SupersededAsync(td, cancellationToken),
                landReference = await LandReferenceAsync(rpu, cancellationToken),
                buildingReference = await BuildingReferenceAsync(rpu, cancellationToken),
                landRows = r is null ? null : LandRows(r),
                improvementRows = r is null ? null : ImprovementRows(r),
                adjustmentRows = r is null ? null : AdjustmentRows(r),
                buildingRows = r is null ? null : BuildingRows(r),
                additionalItems = rpu.RpuType == RpuType.Building ? await AdditionalItemsAsync(td.RpuId, cancellationToken) : null,
                machineRows = r is null ? null : MachineRows(r),
            },
            appraisal = r,
        });

        string? blocker = null;
        if (r is null)
        {
            blocker = "This Tax Declaration declares no assessment yet, so it is not yet a FAAS; preview it instead.";
        }
        else if (td.Status is not (WorkflowStatus.Approved or WorkflowStatus.Cancelled))
        {
            blocker = $"Only an approved Tax Declaration's FAAS can be issued (this one is {td.Status}); preview it instead.";
        }
        return new FormSubjectData(number, data, blocker);
    }

    /// <summary>The parties declared on the TD's effectivity date (the unit's own, else the property's), with TIN and contact.</summary>
    private async Task<List<FaasParty>> PartiesAsync(Guid propertyId, Guid rpuId, DateOnly asOf, CancellationToken ct)
    {
        var scope = await PropertyParties.ScopeAsync(db, propertyId, rpuId, x => x.StartDate <= asOf && (x.EndDate == null || x.EndDate > asOf), ct);
        var rows = await PropertyParties.ProjectAsync(scope, ct);
        var ids = rows.Select(x => x.TaxpayerId).OfType<Guid>().ToList();
        var contact = await db.Taxpayers.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => new { t.Tin, t.ContactNumber }, ct);
        return rows.Select(o => new FaasParty(o.TaxpayerDisplayName, o.Role.ToString(), PropertyParties.RoleLabel(o.Role), o.OwnershipPercentage,
            o.Address, o.TaxpayerId is { } id ? contact.GetValueOrDefault(id)?.Tin : null,
            o.TaxpayerId is { } id2 ? contact.GetValueOrDefault(id2)?.ContactNumber : null)).ToList();
    }

    private sealed record FaasParty(string name, string role, string roleLabel, decimal sharePercent, string? address, string? tin, string? contactNumber);

    /// <summary>MRPAAO "Record of Superseded Assessment": the TD this one replaces, its assessed value and owners.</summary>
    private async Task<object?> SupersededAsync(TaxDeclaration td, CancellationToken ct)
    {
        if (td.PreviousTaxDeclarationId is not { } previousId)
        {
            return null;
        }
        var previous = await db.TaxDeclarations.AsNoTracking().Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == previousId, ct);
        if (previous is null)
        {
            return null;
        }
        var owners = (await PartiesAsync(previous.PropertyId, previous.RpuId, previous.EffectivityDate, ct))
            .Where(p => p.role is "Owner" or "UnknownOwner").Select(p => p.name);
        var pin = await db.Properties.Where(p => p.Id == previous.PropertyId).Select(p => p.PropertyIdentificationNumber).FirstAsync(ct);
        var rpu = await db.RealPropertyUnits.Where(x => x.Id == previous.RpuId).Select(x => x.PinSuffix).FirstAsync(ct);
        return new
        {
            pin = UnitPin.Compose(pin, rpu, false),
            arpNumber = faas.Value.NumberSource == FaasNumberSource.TaxDeclaration ? previous.TaxDeclarationNumber : previous.Assessment?.FaasNumber,
            tdNumber = previous.TaxDeclarationNumber,
            totalAssessedValue = previous.Assessment?.AssessedValue,
            previousOwner = string.Join("; ", owners),
            effectivity = new { date = previous.EffectivityDate, quarter = (previous.EffectivityDate.Month - 1) / 3 + 1, year = previous.EffectivityDate.Year },
        };
    }

    /// <summary>MRPAAO Att. 2 "Land Reference": the land a building (or machinery) stands on.</summary>
    private async Task<object?> LandReferenceAsync(RealPropertyUnit rpu, CancellationToken ct)
    {
        if (rpu.LandRpuId is not { } landRpuId)
        {
            return null;
        }
        var property = await db.Properties.AsNoTracking().Include(p => p.TitleType).FirstAsync(p => p.Id == rpu.PropertyId, ct);
        var land = await db.Lands.AsNoTracking().Where(x => x.RpuId == landRpuId).Select(x => new { x.Area, x.AreaUnit }).FirstOrDefaultAsync(ct);
        var landTd = await db.TaxDeclarations.Where(x => x.RpuId == landRpuId && x.Status == WorkflowStatus.Approved)
            .Select(x => x.TaxDeclarationNumber).FirstOrDefaultAsync(ct);
        var today = clock.Today;
        var owners = (await PartiesAsync(rpu.PropertyId, landRpuId, today, ct)).Where(p => p.role is "Owner" or "UnknownOwner").Select(p => p.name);
        return new
        {
            owner = string.Join("; ", owners),
            titleType = property.TitleType?.Code,
            titleNumber = property.TitleNumber,
            surveyNumber = property.SurveyNumber,
            lotNumber = property.LotNumber,
            blockNumber = property.BlockNumber,
            tdNumber = landTd,
            area = land?.Area,
            areaUnit = land?.AreaUnit,
            pin = property.PropertyIdentificationNumber,
        };
    }

    /// <summary>MRPAAO Att. 3 "Building Owner / PIN": the building a machine is installed in.</summary>
    private async Task<object?> BuildingReferenceAsync(RealPropertyUnit rpu, CancellationToken ct)
    {
        if (rpu.HostRpuId is not { } hostId)
        {
            return null;
        }
        var host = await db.RealPropertyUnits.AsNoTracking().FirstAsync(x => x.Id == hostId, ct);
        var pin = await db.Properties.Where(p => p.Id == host.PropertyId).Select(p => p.PropertyIdentificationNumber).FirstAsync(ct);
        var today = clock.Today;
        var owners = (await PartiesAsync(host.PropertyId, host.Id, today, ct)).Where(p => p.role is "Owner" or "UnknownOwner").Select(p => p.name).ToList();
        var ownedSeparately = await db.PropertyTaxpayers.AnyAsync(x => x.RpuId == host.Id && x.IsCurrent
            && (x.Role == PropertyPartyRole.Owner || x.Role == PropertyPartyRole.UnknownOwner), ct);
        return new { owner = string.Join("; ", owners), pin = UnitPin.Compose(pin, host.PinSuffix, ownedSeparately) };
    }

    private async Task<object> AdditionalItemsAsync(Guid rpuId, CancellationToken ct) =>
        await db.BuildingComponents.AsNoTracking().Where(c => c.IsAdditionalItem && db.Buildings.Any(b => b.Id == c.BuildingId && b.RpuId == rpuId))
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { kind = c.ComponentType!.Name, description = c.Description, cost = c.Cost })
            .ToListAsync(ct);

    private static decimal? Key(AppraisalValuationLineDto line, string key) => line.Breakdown.FirstOrDefault(b => b.Key == key)?.Value;

    private static IEnumerable<object> LandRows(AppraisalRecordDto r) => r.Valuation.Lines
        .Where(l => l.Source is ValuationLineSource.Land or ValuationLineSource.LandStrip)
        .Select(l => new
        {
            classification = l.Classification, subClassification = l.SubClassification, actualUse = l.ActualUse,
            area = l.Quantity, unit = l.Unit, unitValue = l.UnitValue, baseMarketValue = Key(l, "BaseValue"),
        });

    private static IEnumerable<object> ImprovementRows(AppraisalRecordDto r) => r.Valuation.Lines
        .Where(l => l.Source == ValuationLineSource.LandImprovement)
        .Select(l => new { kind = l.Description, number = l.Quantity, unit = l.Unit, unitValue = l.UnitValue, baseMarketValue = Key(l, "BaseValue") });

    /// <summary>MRPAAO Att. 1 "Market Value": base value, the factors (code and percent), total %, value adjustment, market value.</summary>
    private static IEnumerable<object> AdjustmentRows(AppraisalRecordDto r) => r.Valuation.Lines
        .Where(l => l.Source is ValuationLineSource.Land or ValuationLineSource.LandStrip)
        .Select(l => new
        {
            baseMarketValue = Key(l, "BaseValue"),
            factors = string.Join(", ", l.Breakdown.Where(b => b.Key.StartsWith(Domain.DomainServices.ValuationCalculator.AdjustmentKeyPrefix, StringComparison.Ordinal))
                .Select(b => $"{b.Key[Domain.DomainServices.ValuationCalculator.AdjustmentKeyPrefix.Length..]} {(b.Value >= 0 ? "+" : "")}{b.Value:0.##}%")),
            adjustmentPercent = Key(l, "AdjustmentPercent") ?? 0m,
            valueAdjustment = Key(l, "ValueAdjustment") ?? 0m,
            locationFactor = Key(l, "LocationFactor"),
            marketValue = l.MarketValue,
        });

    /// <summary>MRPAAO Att. 2 "Property Appraisal" per use portion.</summary>
    private static IEnumerable<object> BuildingRows(AppraisalRecordDto r) => r.Valuation.Lines
        .Where(l => l.Source is ValuationLineSource.Building or ValuationLineSource.BuildingUsePortion)
        .Select(l => new
        {
            actualUse = l.ActualUse, classification = l.Classification,
            floorArea = Key(l, "FloorArea") ?? Key(l, "TotalFloorArea") ?? l.Quantity, unitConstructionCost = l.UnitValue,
            buildingCore = Key(l, "BaseValue"), additionalItems = Key(l, "AdditionalItemsCost") ?? 0m,
            totalConstructionCost = Key(l, "TotalConstructionCost") ?? Key(l, "BaseValue"),
            completionPercent = Key(l, "CompletionPercentage"), marketValue = l.MarketValue,
        });

    /// <summary>MRPAAO Att. 3 "Property Appraisal", one row per machine (valuation lines follow the machines' order).</summary>
    private static IEnumerable<object> MachineRows(AppraisalRecordDto r)
    {
        var lines = r.Valuation.Lines.Where(l => l.Source == ValuationLineSource.Machinery).ToList();
        return r.MachineryUnits.Select((m, i) =>
        {
            var line = i < lines.Count ? lines[i] : null;
            var fraction = line is null ? null : Key(line, "RemainingFraction");
            var rcn = m.ReplacementCost;
            return (object)new
            {
                kind = m.MachineryType, brandModel = string.Join(" ", new[] { m.Brand, m.Model }.Where(x => !string.IsNullOrWhiteSpace(x))),
                capacity = m.Capacity is { } cap ? $"{cap:0.##} {m.CapacityUnit}".Trim() : null, dateAcquired = m.DateAcquired,
                condition = m.IsBrandNew ? "New" : "Second hand", economicLife = m.EconomicLifeYears, remainingLife = m.RemainingLifeYears,
                yearInstalled = m.YearInstalled, yearOfInitialOperation = m.YearOfInitialOperation,
                originalCost = m.AcquisitionCost + (m.InstallationCost ?? 0m) + (m.OtherCost ?? 0m), conversionFactor = m.ConversionFactor,
                rcn, yearsUsed = m.EconomicLifeYears is { } e && m.RemainingLifeYears is { } rl ? e - rl : (int?)null,
                depreciationPercent = fraction is { } f ? Math.Round((1m - f) * 100m, 2) : (decimal?)null,
                depreciationValue = fraction is { } f2 && rcn is { } c ? Math.Round(c * (1m - f2), 2) : (decimal?)null,
                depreciatedValue = line is null ? null : Key(line, "DepreciatedValue"),
                marketValue = line?.MarketValue,
            };
        }).ToList();
    }
}
