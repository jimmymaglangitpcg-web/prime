using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Enums;

namespace Prime.Application.Features.MarketData;

public sealed record CreateMarketDataReportRequest(MarketDataReportKind Kind, Guid MunicipalityId, DateOnly FromDate, DateOnly ToDate, string? Remarks);

public sealed record MarketDataReportRunDto(
    Guid Id, MarketDataReportKind Kind, string FormCode, Guid MunicipalityId, string MunicipalityName, DateOnly FromDate, DateOnly ToDate, string? Remarks,
    DateTimeOffset CreatedAt);

public interface IMarketDataReportService
{
    Task<Result<MarketDataReportRunDto>> CreateAsync(CreateMarketDataReportRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MarketDataReportRunDto>>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The abstracts and the sales report for a city/municipality and a period (LAM 2025 Book I pp.22–25; Annexes I-M,
/// I-N, I-O, I-R; docs/analysis/smv-preparation-general-revision.md §4.1). A run names what to print; the rows are
/// read when the form is issued and frozen in its snapshot.
/// </summary>
public sealed class MarketDataReportService(IApplicationDbContext db, IJurisdiction jurisdiction) : IMarketDataReportService
{
    public static string FormCode(MarketDataReportKind kind) => kind switch
    {
        MarketDataReportKind.TransactionsAbstract => "MARKET_ABSTRACT_TRANSACTIONS",
        MarketDataReportKind.BuildingPermitsAbstract => "MARKET_ABSTRACT_BUILDING_PERMITS",
        MarketDataReportKind.MachineryRegistrationsAbstract => "MARKET_ABSTRACT_MACHINERY",
        MarketDataReportKind.SalesReport => "MARKET_SALES_REPORT",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public async Task<Result<MarketDataReportRunDto>> CreateAsync(CreateMarketDataReportRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.Kind) || r.FromDate == default || r.ToDate == default || r.FromDate > r.ToDate || r.Remarks?.Length > 1000)
        {
            return Result.Failure<MarketDataReportRunDto>("VALIDATION_FAILED", "A kind and a period (from on or before to) are required; remarks max 1000.");
        }
        if (!await db.Municipalities.AnyAsync(x => x.Id == r.MunicipalityId, cancellationToken))
        {
            return Result.Failure<MarketDataReportRunDto>("MUNICIPALITY_NOT_FOUND", "The specified city/municipality does not exist.");
        }
        if (!jurisdiction.Allows(r.MunicipalityId))
        {
            return Result.Failure<MarketDataReportRunDto>(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        var run = new MarketDataReportRun
        {
            Kind = r.Kind, MunicipalityId = r.MunicipalityId, FromDate = r.FromDate, ToDate = r.ToDate,
            Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim(),
        };
        db.MarketDataReportRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success((await Project(db.MarketDataReportRuns.Where(x => x.Id == run.Id)).ToListAsync(cancellationToken)).Single());
    }

    public async Task<Result<IReadOnlyList<MarketDataReportRunDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<MarketDataReportRunDto>>(await Project(db.MarketDataReportRuns.OrderByDescending(x => x.CreatedAt).Take(200)).ToListAsync(cancellationToken));

    private static IQueryable<MarketDataReportRunDto> Project(IQueryable<MarketDataReportRun> query) => query.AsNoTracking()
        .Select(x => new MarketDataReportRunDto(x.Id, x.Kind,
            x.Kind == MarketDataReportKind.TransactionsAbstract ? "MARKET_ABSTRACT_TRANSACTIONS"
            : x.Kind == MarketDataReportKind.BuildingPermitsAbstract ? "MARKET_ABSTRACT_BUILDING_PERMITS"
            : x.Kind == MarketDataReportKind.MachineryRegistrationsAbstract ? "MARKET_ABSTRACT_MACHINERY" : "MARKET_SALES_REPORT",
            x.MunicipalityId, x.Municipality!.Name, x.FromDate, x.ToDate, x.Remarks, x.CreatedAt));
}

/// <summary>The rows of a market-data report run. Recorded values only; the sales report uses accepted sales only.</summary>
public sealed class MarketDataReportFormDataProvider(IApplicationDbContext db) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.MarketDataReport;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var run = await db.MarketDataReportRuns.AsNoTracking().Include(x => x.Municipality).ThenInclude(m => m!.Province)
            .FirstOrDefaultAsync(x => x.Id == subjectId, ct);
        if (run is null)
        {
            return null;
        }
        object body = run.Kind switch
        {
            MarketDataReportKind.TransactionsAbstract => new { transactions = await TransactionsAsync(run, ct) },
            MarketDataReportKind.BuildingPermitsAbstract => new { permits = await PermitsAsync(run, ct) },
            MarketDataReportKind.MachineryRegistrationsAbstract => new { registrations = await RegistrationsAsync(run, ct) },
            MarketDataReportKind.SalesReport => await SalesAsync(run, ct),
            _ => throw new InvalidOperationException($"Unhandled {nameof(MarketDataReportKind)}: {run.Kind}"),
        };
        var data = FormData.ToJson(new
        {
            report = new
            {
                kind = run.Kind.ToString(), formCode = MarketDataReportService.FormCode(run.Kind), fromDate = run.FromDate, toDate = run.ToDate,
                remarks = run.Remarks, municipality = run.Municipality!.Name, province = run.Municipality.Province?.Name,
            },
            data = body,
        });
        return new FormSubjectData(null, data, null);
    }

    private static string Unit(AreaMeasure unit) => unit == AreaMeasure.Hectare ? "ha" : "sqm";

    private async Task<object> TransactionsAsync(MarketDataReportRun run, CancellationToken ct) =>
        (await db.MarketTransactions.AsNoTracking()
            .Where(x => x.MunicipalityId == run.MunicipalityId && x.CancelledAt == null && x.TransactionDate >= run.FromDate && x.TransactionDate <= run.ToDate)
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.TransactionDate, x.GrantorNames, x.GranteeNames, x.GranteeAddress, Barangay = x.Barangay != null ? x.Barangay.Name : null, x.Location,
                Mode = x.ConveyanceMode != null ? x.ConveyanceMode.Name : null, x.Consideration, x.ConveysLand, x.ConveysBuilding, x.LandArea, x.LandAreaUnit,
                x.BuildingFloorArea, x.LotNumber, x.PreviousTitleNumber, x.NewTitleNumber, x.DocumentReference,
            }).ToListAsync(ct))
        .Select(x => new
        {
            date = x.TransactionDate, from = x.GrantorNames, to = x.GranteeNames, toAddress = x.GranteeAddress,
            location = string.Join(", ", new[] { x.Location, x.Barangay }.Where(s => !string.IsNullOrEmpty(s))), mode = x.Mode,
            consideration = x.Consideration, land = x.ConveysLand, building = x.ConveysBuilding,
            landArea = x.LandArea, landAreaUnit = Unit(x.LandAreaUnit), floorArea = x.BuildingFloorArea,
            lot = x.LotNumber, previousTitle = x.PreviousTitleNumber, newTitle = x.NewTitleNumber, document = x.DocumentReference,
        }).ToList();

    private async Task<object> PermitsAsync(MarketDataReportRun run, CancellationToken ct) =>
        await db.BuildingPermitAbstracts.AsNoTracking()
            .Where(x => x.MunicipalityId == run.MunicipalityId && x.CancelledAt == null && x.IssuedOn >= run.FromDate && x.IssuedOn <= run.ToDate)
            .OrderBy(x => x.IssuedOn).ThenBy(x => x.PermitNumber)
            .Select(x => new
            {
                issuedOn = x.IssuedOn, constructionDate = x.ProposedConstructionDate, completionDate = x.ExpectedCompletionDate, number = x.PermitNumber,
                permittee = x.PermitteeName, td = x.TaxDeclarationNumber, address = x.PermitteeAddress, blockLot = x.BlockLotNumber, street = x.Street,
                barangay = x.Barangay != null ? x.Barangay.Name : null, scope = x.Scope.ToString(),
                kind = x.BuildingType != null ? x.BuildingType.Name : null, structuralType = x.StructuralType != null ? x.StructuralType.Name : null,
                storeys = x.Storeys, floorArea = x.TotalFloorArea, cost = x.EstimatedCost,
                classification = x.Classification != null ? x.Classification.Name : null,
                declaredPin = x.Building != null ? x.Building.Property!.PropertyIdentificationNumber : null,
            }).ToListAsync(ct);

    private async Task<object> RegistrationsAsync(MarketDataReportRun run, CancellationToken ct)
    {
        var rows = await db.MachineryRegistrationAbstracts.AsNoTracking()
            .Where(x => x.MunicipalityId == run.MunicipalityId && x.CancelledAt == null && x.IssuedOn >= run.FromDate && x.IssuedOn <= run.ToDate)
            .OrderBy(x => x.IssuedOn).ThenBy(x => x.CertificateNumber)
            .Select(x => new
            {
                x.IssuedOn, x.CertificateNumber, x.OwnerName, x.OwnerAddress, x.Location, Barangay = x.Barangay != null ? x.Barangay.Name : null,
                x.TaxDeclarationNumber, Type = x.MachineryType != null ? x.MachineryType.Name : x.Description, x.BrandModel, x.YearAcquired, x.Manufacturer,
                x.Cost, x.CurrentCondition, x.InstallationDate, RpuId = x.Machinery != null ? (Guid?)x.Machinery.RpuId : null,
            }).ToListAsync(ct);
        // The assessed value of the declared machinery: its latest posted assessment effective by the end of the period.
        var rpuIds = rows.Where(r => r.RpuId is not null).Select(r => r.RpuId!.Value).Distinct().ToList();
        var assessed = (await db.Assessments.AsNoTracking()
                .Where(a => rpuIds.Contains(a.RpuId) && a.Status == WorkflowStatus.Posted && a.EffectiveDate <= run.ToDate)
                .Select(a => new { a.RpuId, a.EffectiveDate, a.AssessedValue }).ToListAsync(ct))
            .GroupBy(a => a.RpuId).ToDictionary(g => g.Key, g => g.MaxBy(a => a.EffectiveDate)!.AssessedValue);
        return rows.Select(x => new
        {
            issuedOn = x.IssuedOn, number = x.CertificateNumber, owner = x.OwnerName, ownerAddress = x.OwnerAddress,
            location = string.Join(", ", new[] { x.Location, x.Barangay }.Where(s => !string.IsNullOrEmpty(s))), td = x.TaxDeclarationNumber,
            type = x.Type, brandModel = x.BrandModel, yearAcquired = x.YearAcquired, manufacturer = x.Manufacturer, cost = x.Cost,
            condition = x.CurrentCondition, assessedValue = x.RpuId is { } id && assessed.TryGetValue(id, out var av) ? av : (decimal?)null,
            installedOn = x.InstallationDate,
        }).ToList();
    }

    private async Task<object> SalesAsync(MarketDataReportRun run, CancellationToken ct)
    {
        var sales = await db.MarketTransactions.AsNoTracking()
            .Where(x => x.MunicipalityId == run.MunicipalityId && x.CancelledAt == null && x.Review == MarketDataReview.Accepted
                && x.TransactionDate >= run.FromDate && x.TransactionDate <= run.ToDate)
            .OrderBy(x => x.TransactionDate)
            .Select(x => new
            {
                x.TransactionDate, x.GranteeNames, Pin = x.Property != null ? x.Property.PropertyIdentificationNumber : x.Pin,
                Barangay = x.Barangay != null ? x.Barangay.Name : null, x.Location,
                Classification = x.Classification != null ? x.Classification.Name : null, SubClass = x.SubClassification != null ? x.SubClassification.Name : null,
                BuildingType = x.BuildingType != null ? x.BuildingType.Name : null,
                x.LandArea, x.LandAreaUnit, x.BuildingFloorArea, x.LandUnitPrice, x.BuildingUnitPrice,
            }).ToListAsync(ct);
        var lines = sales.SelectMany(s => new[]
        {
            s.LandUnitPrice is { } lp ? new { kind = "Land", s.TransactionDate, owner = s.GranteeNames, pin = s.Pin,
                location = string.Join(", ", new[] { s.Location, s.Barangay }.Where(v => !string.IsNullOrEmpty(v))),
                classification = s.Classification, subClass = s.SubClass, area = s.LandArea, unit = Unit(s.LandAreaUnit), unitPrice = lp } : null,
            s.BuildingUnitPrice is { } bp ? new { kind = "Building", s.TransactionDate, owner = s.GranteeNames, pin = s.Pin,
                location = string.Join(", ", new[] { s.Location, s.Barangay }.Where(v => !string.IsNullOrEmpty(v))),
                classification = s.Classification, subClass = s.BuildingType, area = s.BuildingFloorArea, unit = "sqm", unitPrice = bp } : null,
        }).Where(l => l is not null).Select(l => l!).ToList();
        var groups = lines.GroupBy(l => (l.kind, l.classification, l.subClass, l.unit))
            .OrderBy(g => g.Key.kind).ThenBy(g => g.Key.classification).ThenBy(g => g.Key.subClass)
            .Select(g =>
            {
                var spread = MarketPrices.Spread(g.Select(l => l.unitPrice))!.Value;
                return new
                {
                    g.Key.kind, g.Key.classification, g.Key.subClass, g.Key.unit, count = g.Count(),
                    lowest = spread.Lowest, median = spread.Median, highest = spread.Highest,
                };
            }).ToList();
        return new { groups, sales = lines.Select(l => new
        {
            l.kind, date = l.TransactionDate, l.owner, l.pin, l.location, l.classification, l.subClass, l.area, l.unit, l.unitPrice,
        }).ToList() };
    }
}
