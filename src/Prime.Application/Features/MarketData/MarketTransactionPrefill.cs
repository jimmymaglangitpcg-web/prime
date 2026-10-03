using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.MarketData;

/// <summary>
/// The market transaction a transfer leaves behind (docs/analysis/smv-preparation-general-revision.md §4.1, Q3): when
/// the deed's consideration was recorded on the transfer's tax clearance, approval prefills an Unreviewed record from
/// what PRIME knows of the property and the parties. The assessor completes and reviews it; it is never used
/// unreviewed. A machinery-only transfer leaves none (neither land nor a building changed hands).
/// </summary>
public static class MarketTransactionPrefill
{
    /// <summary>Adds the record (the caller saves), or does nothing. <paramref name="endingOwnerIds"/>: the owners the transfer ends.</summary>
    public static async Task FromTransferAsync(IApplicationDbContext db, PropertyTransaction tx, IReadOnlyCollection<Guid> endingOwnerIds, CancellationToken ct)
    {
        var clearance = await db.TransferTaxClearances.AsNoTracking().FirstOrDefaultAsync(x => x.PropertyTransactionId == tx.Id, ct);
        if (clearance?.Consideration is not { } consideration || await db.MarketTransactions.AnyAsync(x => x.PropertyTransactionId == tx.Id, ct))
        {
            return;
        }
        var property = await db.Properties.AsNoTracking().FirstAsync(x => x.Id == tx.PropertyId, ct);
        var units = await db.RealPropertyUnits.AsNoTracking()
            .Where(x => x.PropertyId == tx.PropertyId && x.Status == RecordStatus.Active && (tx.TransferRpuId == null || x.Id == tx.TransferRpuId))
            .Select(x => new { x.Id, x.RpuType }).ToListAsync(ct);
        var conveysLand = units.Any(u => u.RpuType == RpuType.Land);
        var conveysBuilding = units.Any(u => u.RpuType == RpuType.Building);
        if (!conveysLand && !conveysBuilding)
        {
            return;
        }
        var unitIds = units.Select(u => u.Id).ToList();
        var lands = conveysLand
            ? await db.Lands.AsNoTracking().Where(x => unitIds.Contains(x.RpuId) && x.Status == RecordStatus.Active)
                .Select(x => new { x.Area, x.AreaUnit, x.ClassificationId, x.SubClassificationId, x.ActualUseId, x.RpuId }).ToListAsync(ct)
            : [];
        // An area is given only when every land is measured in the same unit.
        var areaUnits = lands.Select(l => l.AreaUnit.Trim().ToLowerInvariant()).Distinct().ToList();
        var hectares = areaUnits is ["ha"] or ["hectare"] or ["hectares"];
        decimal? landArea = lands.Count > 0 && (areaUnits is ["sqm"] || hectares) ? lands.Sum(l => l.Area) : null;
        Guid? One(IEnumerable<Guid?> ids) => ids.Distinct().ToList() is [var only] ? only : null;
        var landRpuId = units.FirstOrDefault(u => u.RpuType == RpuType.Land)?.Id;
        var tdNumber = landRpuId is { } lr
            ? await db.TaxDeclarations.AsNoTracking().Where(x => x.RpuId == lr && x.Status == WorkflowStatus.Approved).Select(x => x.TaxDeclarationNumber).FirstOrDefaultAsync(ct)
            : null;
        var floorArea = conveysBuilding
            ? await db.Buildings.AsNoTracking().Where(x => unitIds.Contains(x.RpuId)).SumAsync(x => (decimal?)(x.TotalFloorArea > 0 ? x.TotalFloorArea : x.FloorArea), ct)
            : null;
        var endingOwnerNames = (await db.Taxpayers.AsNoTracking().Where(t => endingOwnerIds.Contains(t.Id))
                .Select(t => new { t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName }).ToListAsync(ct))
            .Select(g => TaxpayerNameFormatter.Format(g.TaxpayerType, g.LastName, g.FirstName, g.MiddleName, g.Suffix, g.CorporateName)).ToList();
        var granteeIds = tx.NewParties.Where(p => p.Role == PropertyPartyRole.Owner && p.TaxpayerId != null).Select(p => p.TaxpayerId!.Value).ToList();
        var grantees = await db.Taxpayers.AsNoTracking().Where(t => granteeIds.Contains(t.Id))
            .Select(t => new { t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName }).ToListAsync(ct);

        var record = new MarketTransaction
        {
            Source = MarketDataSource.TransferDeed,
            TransactionDate = tx.EffectiveDate,
            DocumentReference = clearance.CarNumber is { } car ? $"CAR {car}" : null,
            GrantorNames = Trim(clearance.TransferorName ?? (endingOwnerNames.Count > 0 ? string.Join("; ", endingOwnerNames) : null), 1000),
            GranteeNames = Trim(grantees.Count > 0
                ? string.Join("; ", grantees.Select(g => TaxpayerNameFormatter.Format(g.TaxpayerType, g.LastName, g.FirstName, g.MiddleName, g.Suffix, g.CorporateName)))
                : null, 1000),
            MunicipalityId = property.MunicipalityId,
            BarangayId = property.BarangayId,
            Location = Trim(property.Street, 300),
            PropertyId = property.Id,
            Pin = property.PropertyIdentificationNumber,
            TaxDeclarationNumber = tdNumber,
            LotNumber = property.LotNumber,
            PreviousTitleNumber = property.TitleNumber,
            ConveysLand = conveysLand,
            ConveysBuilding = conveysBuilding,
            ClassificationId = One(lands.Select(l => (Guid?)l.ClassificationId)),
            SubClassificationId = One(lands.Select(l => l.SubClassificationId)),
            ActualUseId = One(lands.Select(l => (Guid?)l.ActualUseId)),
            LandArea = landArea,
            LandAreaUnit = hectares ? AreaMeasure.Hectare : AreaMeasure.SquareMetre,
            BuildingFloorArea = floorArea,
            Consideration = consideration,
            PropertyTransactionId = tx.Id,
            Remarks = $"Prefilled from transfer {tx.TransactionNumber ?? tx.TypeCode}; the date is the transfer's effective date — check the deed.",
        };
        (record.LandUnitPrice, record.BuildingUnitPrice) = MarketPrices.Compute(record.ConveysLand, record.ConveysBuilding, record.Consideration,
            record.LandConsideration, record.LandArea, record.BuildingFloorArea);
        db.MarketTransactions.Add(record);
    }

    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value.Trim() : value[..max];
}
