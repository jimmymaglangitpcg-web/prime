using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Descriptions;

public sealed record UpdatePropertyDescriptionRequest(
    string? Street, string? Sitio, string? LotNumber, string? BlockNumber, string? SurveyNumber, string? TitleNumber,
    Guid? TitleTypeId, DateOnly? TitleDate, string? TaxMapNumber,
    string? BoundaryNorth, string? BoundaryEast, string? BoundarySouth, string? BoundaryWest, string Reason);

public sealed record UpdateBuildingDescriptionRequest(
    int? NumberOfStoreys, int? YearConstructed, int? YearCompleted, string? BuildingPermitNumber, DateOnly? BuildingPermitDate,
    string? CondominiumCertificateNumber, DateOnly? CertificateOfCompletionDate, DateOnly? CertificateOfOccupancyDate,
    DateOnly? DateConstructed, DateOnly? DateOccupied, string Reason);

public sealed record AddBuildingFloorRequest(int FloorNumber, decimal Area);

public sealed record AddBuildingMaterialRequest(Guid StructuralPartId, Guid? StructuralMaterialId, string? OtherSpecify, int? FloorNumber);

public sealed record UpdateMachineryDescriptionRequest(
    string? Description, string? Brand, string? Model, string? SerialNumber, decimal? Capacity, string? CapacityUnit,
    int? YearInstalled, int? YearOfInitialOperation, decimal? ConversionFactor, string Reason);

public sealed record SetTransferTaxClearanceRequest(
    string? CarNumber, DateOnly? CarDate, string? TransferorName, string? TransferorTin, string? TransfereeTin,
    decimal? CapitalGainsTax, string? CapitalGainsTaxReceipt, DateOnly? CapitalGainsTaxDate,
    decimal? DocumentaryStampTax, string? DocumentaryStampTaxReceipt, DateOnly? DocumentaryStampTaxDate,
    decimal? TransferTax, string? TransferTaxReceipt, DateOnly? TransferTaxDate, string? Remarks);

public interface IDescriptionService
{
    Task<Result> UpdatePropertyAsync(Guid propertyId, UpdatePropertyDescriptionRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateBuildingAsync(Guid buildingId, UpdateBuildingDescriptionRequest request, CancellationToken cancellationToken = default);
    Task<Result> AddBuildingFloorAsync(Guid buildingId, AddBuildingFloorRequest request, CancellationToken cancellationToken = default);
    Task<Result> AddBuildingMaterialAsync(Guid buildingId, AddBuildingMaterialRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateMachineryAsync(Guid machineryId, UpdateMachineryDescriptionRequest request, CancellationToken cancellationToken = default);
    Task<Result> SetTransferTaxClearanceAsync(Guid transactionId, SetTransferTaxClearanceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Descriptive fields the FAAS and TD print (docs/analysis/mrpaao-forms-model.md
/// §10). They describe the property and value nothing: a correction replaces
/// them in place with a required reason, and the audit log keeps the old and
/// new values. An issued form keeps what it printed. Valued fields (area,
/// classification, costs) are never changed here.
/// </summary>
public sealed class DescriptionService(IApplicationDbContext db, ICurrentUserService currentUser) : IDescriptionService
{
    public async Task<Result> UpdatePropertyAsync(Guid propertyId, UpdatePropertyDescriptionRequest r, CancellationToken cancellationToken = default)
    {
        if ((ReasonProblem(r.Reason) ?? TooLong(500, r.BoundaryNorth, r.BoundaryEast, r.BoundarySouth, r.BoundaryWest)
            ?? TooLong(100, r.Street, r.Sitio, r.LotNumber, r.BlockNumber, r.SurveyNumber, r.TitleNumber, r.TaxMapNumber)) is { } problem)
        {
            return Result.Failure("VALIDATION_FAILED", problem);
        }
        var property = await db.Properties.FirstOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Result.Failure("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        if (r.TitleTypeId is { } t && !await db.TitleTypes.AnyAsync(x => x.Id == t && x.IsActive, cancellationToken))
        {
            return Result.Failure("TITLE_TYPE_NOT_FOUND", "The specified title type does not exist or is inactive.");
        }
        property.Street = Clean(r.Street);
        property.Sitio = Clean(r.Sitio);
        property.LotNumber = Clean(r.LotNumber);
        property.BlockNumber = Clean(r.BlockNumber);
        property.SurveyNumber = Clean(r.SurveyNumber);
        property.TitleNumber = Clean(r.TitleNumber);
        property.TitleTypeId = r.TitleTypeId;
        property.TitleDate = r.TitleDate;
        property.TaxMapNumber = Clean(r.TaxMapNumber);
        property.BoundaryNorth = Clean(r.BoundaryNorth);
        property.BoundaryEast = Clean(r.BoundaryEast);
        property.BoundarySouth = Clean(r.BoundarySouth);
        property.BoundaryWest = Clean(r.BoundaryWest);
        return await SaveAsync(r.Reason, cancellationToken);
    }

    public async Task<Result> UpdateBuildingAsync(Guid buildingId, UpdateBuildingDescriptionRequest r, CancellationToken cancellationToken = default)
    {
        if ((ReasonProblem(r.Reason) ?? TooLong(100, r.BuildingPermitNumber, r.CondominiumCertificateNumber)) is { } problem)
        {
            return Result.Failure("VALIDATION_FAILED", problem);
        }
        if (r.NumberOfStoreys is < 1 || r.YearConstructed is < 1800 or > 2200 || r.YearCompleted is < 1800 or > 2200)
        {
            return Result.Failure("VALIDATION_FAILED", "Storeys must be at least 1; years must be between 1800 and 2200.");
        }
        var building = await db.Buildings.FirstOrDefaultAsync(x => x.Id == buildingId, cancellationToken);
        if (building is null)
        {
            return Result.Failure("BUILDING_NOT_FOUND", "No Building record was found with the given id.");
        }
        building.NumberOfStoreys = r.NumberOfStoreys ?? building.NumberOfStoreys;
        building.YearConstructed = r.YearConstructed;
        building.YearCompleted = r.YearCompleted;
        building.BuildingPermitNumber = Clean(r.BuildingPermitNumber);
        building.BuildingPermitDate = r.BuildingPermitDate;
        building.CondominiumCertificateNumber = Clean(r.CondominiumCertificateNumber);
        building.CertificateOfCompletionDate = r.CertificateOfCompletionDate;
        building.CertificateOfOccupancyDate = r.CertificateOfOccupancyDate;
        building.DateConstructed = r.DateConstructed;
        building.DateOccupied = r.DateOccupied;
        return await SaveAsync(r.Reason, cancellationToken);
    }

    public async Task<Result> AddBuildingFloorAsync(Guid buildingId, AddBuildingFloorRequest r, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.Include(x => x.Floors).FirstOrDefaultAsync(x => x.Id == buildingId, cancellationToken);
        if (building is null)
        {
            return Result.Failure("BUILDING_NOT_FOUND", "No Building record was found with the given id.");
        }
        if (r.FloorNumber < 1 || r.Area <= 0)
        {
            return Result.Failure("VALIDATION_FAILED", "The floor number must be at least 1 and the area greater than zero.");
        }
        if (building.Floors.Any(x => x.FloorNumber == r.FloorNumber))
        {
            return Result.Failure("BUILDING_FLOOR_DUPLICATE", $"Floor {r.FloorNumber} is already recorded.");
        }
        var total = building.Floors.Sum(x => x.Area) + r.Area;
        if (total > building.TotalFloorArea)
        {
            return Result.Failure("BUILDING_FLOORS_EXCEED_FLOOR_AREA",
                $"The floors would total {total:#,0.####} sqm, more than the building's {building.TotalFloorArea:#,0.####} sqm total floor area.");
        }
        db.BuildingFloors.Add(new BuildingFloor { BuildingId = building.Id, FloorNumber = r.FloorNumber, Area = r.Area });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AddBuildingMaterialAsync(Guid buildingId, AddBuildingMaterialRequest r, CancellationToken cancellationToken = default)
    {
        var other = Clean(r.OtherSpecify);
        if ((r.StructuralMaterialId is null) == (other is null) || other?.Length > 200 || r.FloorNumber is < 1)
        {
            return Result.Failure("VALIDATION_FAILED", "Name a catalogue material or specify another (max 200), not both; a floor number must be at least 1.");
        }
        if (!await db.Buildings.AnyAsync(x => x.Id == buildingId, cancellationToken))
        {
            return Result.Failure("BUILDING_NOT_FOUND", "No Building record was found with the given id.");
        }
        if (!await db.StructuralParts.AnyAsync(x => x.Id == r.StructuralPartId && x.IsActive, cancellationToken))
        {
            return Result.Failure("STRUCTURAL_PART_NOT_FOUND", "The specified structure part does not exist or is inactive.");
        }
        if (r.StructuralMaterialId is { } m
            && !await db.StructuralMaterials.AnyAsync(x => x.Id == m && x.StructuralPartId == r.StructuralPartId && x.IsActive, cancellationToken))
        {
            return Result.Failure("STRUCTURAL_MATERIAL_NOT_FOUND", "The material is not an active material of that structure part.");
        }
        db.BuildingMaterials.Add(new BuildingMaterial
        {
            BuildingId = buildingId, StructuralPartId = r.StructuralPartId, StructuralMaterialId = r.StructuralMaterialId,
            OtherSpecify = other, FloorNumber = r.FloorNumber,
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UpdateMachineryAsync(Guid machineryId, UpdateMachineryDescriptionRequest r, CancellationToken cancellationToken = default)
    {
        if ((ReasonProblem(r.Reason) ?? TooLong(200, r.Brand, r.Model, r.SerialNumber, r.CapacityUnit) ?? TooLong(500, r.Description)) is { } problem)
        {
            return Result.Failure("VALIDATION_FAILED", problem);
        }
        if (r.Capacity < 0 || r.ConversionFactor <= 0 || r.YearInstalled is < 1800 or > 2200 || r.YearOfInitialOperation is < 1800 or > 2200)
        {
            return Result.Failure("VALIDATION_FAILED", "Capacity cannot be negative; the conversion factor must be positive; years between 1800 and 2200.");
        }
        var machinery = await db.MachineryUnits.FirstOrDefaultAsync(x => x.Id == machineryId, cancellationToken);
        if (machinery is null)
        {
            return Result.Failure("MACHINERY_NOT_FOUND", "No Machinery record was found with the given id.");
        }
        machinery.Description = Clean(r.Description);
        machinery.Brand = Clean(r.Brand);
        machinery.Model = Clean(r.Model);
        machinery.SerialNumber = Clean(r.SerialNumber);
        machinery.Capacity = r.Capacity;
        machinery.CapacityUnit = Clean(r.CapacityUnit);
        machinery.YearInstalled = r.YearInstalled;
        machinery.YearOfInitialOperation = r.YearOfInitialOperation;
        machinery.ConversionFactor = r.ConversionFactor;
        return await SaveAsync(r.Reason, cancellationToken);
    }

    /// <summary>
    /// Records the BIR clearance of a transfer while it is still a Draft or pending
    /// review; it is part of what the approver checks. Setting it again replaces it.
    /// </summary>
    public async Task<Result> SetTransferTaxClearanceAsync(Guid transactionId, SetTransferTaxClearanceRequest r, CancellationToken cancellationToken = default)
    {
        if ((TooLong(100, r.CarNumber, r.CapitalGainsTaxReceipt, r.DocumentaryStampTaxReceipt, r.TransferTaxReceipt)
            ?? TooLong(300, r.TransferorName) ?? TooLong(20, r.TransferorTin, r.TransfereeTin) ?? TooLong(1000, r.Remarks)) is { } problem)
        {
            return Result.Failure("VALIDATION_FAILED", problem);
        }
        if (r.CapitalGainsTax < 0 || r.DocumentaryStampTax < 0 || r.TransferTax < 0)
        {
            return Result.Failure("VALIDATION_FAILED", "Tax amounts cannot be negative.");
        }
        var tx = await db.PropertyTransactions.Include(x => x.TaxClearance).FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        if (tx is null)
        {
            return Result.Failure("PROPERTY_TRANSACTION_NOT_FOUND", "No property transaction was found with the given id.");
        }
        if (tx.Kind != PropertyTransactionKind.Transfer)
        {
            return Result.Failure("TRANSACTION_NOT_TRANSFER", "Only a transfer carries a BIR tax clearance.");
        }
        if (tx.Status is not (WorkflowStatus.Draft or WorkflowStatus.PendingReview))
        {
            return Result.Failure("PROPERTY_TRANSACTION_CLOSED", "The clearance can be recorded only before the transfer is approved.");
        }
        var c = tx.TaxClearance;
        if (c is null)
        {
            c = new TransferTaxClearance { PropertyTransactionId = tx.Id };
            db.TransferTaxClearances.Add(c);
        }
        c.CarNumber = Clean(r.CarNumber);
        c.CarDate = r.CarDate;
        c.TransferorName = Clean(r.TransferorName);
        c.TransferorTin = Clean(r.TransferorTin);
        c.TransfereeTin = Clean(r.TransfereeTin);
        c.CapitalGainsTax = r.CapitalGainsTax;
        c.CapitalGainsTaxReceipt = Clean(r.CapitalGainsTaxReceipt);
        c.CapitalGainsTaxDate = r.CapitalGainsTaxDate;
        c.DocumentaryStampTax = r.DocumentaryStampTax;
        c.DocumentaryStampTaxReceipt = Clean(r.DocumentaryStampTaxReceipt);
        c.DocumentaryStampTaxDate = r.DocumentaryStampTaxDate;
        c.TransferTax = r.TransferTax;
        c.TransferTaxReceipt = Clean(r.TransferTaxReceipt);
        c.TransferTaxDate = r.TransferTaxDate;
        c.Remarks = Clean(r.Remarks);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SaveAsync(string reason, CancellationToken ct)
    {
        currentUser.Reason = reason.Trim(); // the audit log's reason for this correction
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string? ReasonProblem(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 ? "A reason for the correction is required (max 1000)." : null;

    private static string? TooLong(int max, params string?[] values) =>
        values.Any(v => v?.Trim().Length > max) ? $"A text field is longer than {max} characters." : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
