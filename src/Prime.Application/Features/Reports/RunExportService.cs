using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Common.Security;
using Prime.Application.Features.Audit;
using Prime.Application.Features.Forms;
using Prime.Application.Features.MarketData;
using Prime.Application.Features.Offices;
using Prime.Application.Features.Registers;
using Prime.Application.Features.Security;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Reports;

public interface IRunExportService
{
    /// <summary>The rows of a register run (TMCR, pre-TMCR, Assessment Rolls, ORF, ROA) as CSV or Excel.</summary>
    Task<Result<ReportExport>> RegisterRunAsync(Guid runId, ReportFormat format, CancellationToken cancellationToken = default);

    /// <summary>The groups of a lowest-to-highest sales report run (LAM Annex I-R) as CSV or Excel.</summary>
    Task<Result<ReportExport>> SalesReportRunAsync(Guid runId, ReportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads of the printed registers and the sales report (docs/analysis/reporting.md §4.2, step R3). A run already issued
/// is exported from its frozen snapshot, so the file holds what was printed; a run not yet issued is read from the records
/// now, by the same form data provider the print uses, and the file says so. The columns are the snapshot's fields; the
/// official layouts stay with the forms. Owner and administrator addresses are hidden from users without
/// taxpayer.view-personal (P12-5): a file leaves the system, unlike a printed form. Each download writes an EXPORT row.
/// </summary>
public sealed class RunExportService(
    IApplicationDbContext db,
    IEnumerable<IFormDataProvider> providers,
    IEnumerable<IReportFileWriter> writers,
    IPermissionService permissions,
    IClock clock,
    IOfficeContext office,
    ICurrentUserService currentUser,
    ISecurityEventLog events,
    IOptions<LguOptions> lgu) : IRunExportService
{
    private sealed record Field(string Path, string Title, ReportColumnType Type, bool Personal = false);

    private static readonly Field[] TaxMapFields =
    [
        new("assessorLotNumber", "Assessor's lot no.", ReportColumnType.Text), new("pin", "PIN", ReportColumnType.Text),
        new("surveyNumber", "Survey no.", ReportColumnType.Text), new("lotNumber", "Lot no.", ReportColumnType.Text),
        new("blockNumber", "Block no.", ReportColumnType.Text), new("lam.cadastralNumber", "Cadastral no.", ReportColumnType.Text),
        new("titleNumber", "Title no.", ReportColumnType.Text), new("area", "Area", ReportColumnType.Area), new("areaUnit", "Unit", ReportColumnType.Text),
        new("classCode", "Class", ReportColumnType.Text), new("owner", "Owner", ReportColumnType.Text), new("arpNumber", "ARP no.", ReportColumnType.Text),
        new("tdNumber", "TD no.", ReportColumnType.Text), new("lam.marketValue", "Market value", ReportColumnType.Money),
        new("buildings", "Buildings", ReportColumnType.Integer), new("machinery", "Machinery", ReportColumnType.Text),
        new("lam.machineCount", "Machines", ReportColumnType.Integer), new("others", "Other improvements", ReportColumnType.Text),
        new("lam.previousPin", "Previous PIN", ReportColumnType.Text), new("remarks", "Remarks", ReportColumnType.Text),
    ];

    private static readonly Field[] PreTaxMapFields =
    [
        new("temporaryPin", "Temporary PIN", ReportColumnType.Text), new("finalPin", "Final PIN", ReportColumnType.Text),
        new("officeTieUp", "Office tie-up", ReportColumnType.Text), new("fieldConfirmed", "Field confirmed", ReportColumnType.Text),
        new("tieUpRemarks", "Tie-up remarks", ReportColumnType.Text), new("owner", "Owner", ReportColumnType.Text),
        new("ownerAddress", "Owner's address", ReportColumnType.Text, Personal: true), new("tdNumber", "TD no.", ReportColumnType.Text),
        new("surveyBefore", "Survey no.", ReportColumnType.Text), new("surveyAfter", "Survey no. (tax-mapped)", ReportColumnType.Text),
        new("lotNumber", "Lot no.", ReportColumnType.Text), new("titleNumber", "Title no.", ReportColumnType.Text),
        new("areaDeclared", "Area declared", ReportColumnType.Area), new("areaUnit", "Unit", ReportColumnType.Text),
        new("areaTaxMapped", "Area tax-mapped (sq m)", ReportColumnType.Area), new("kindOfLand", "Kind of land", ReportColumnType.Text),
        new("improvement", "Improvements", ReportColumnType.Text), new("remarks", "Remarks", ReportColumnType.Text),
    ];

    private static readonly Field[] RollFields =
    [
        new("lam.page", "Page", ReportColumnType.Integer), new("lam.line", "Line", ReportColumnType.Integer),
        new("arpNumber", "ARP no.", ReportColumnType.Text), new("tdNumber", "TD no.", ReportColumnType.Text), new("pin", "PIN", ReportColumnType.Text),
        new("lotBlock", "Lot / block", ReportColumnType.Text), new("owner", "Owner", ReportColumnType.Text),
        new("ownerAddress", "Owner's address", ReportColumnType.Text, Personal: true), new("lam.administrator", "Administrator", ReportColumnType.Text),
        new("lam.administratorAddress", "Administrator's address", ReportColumnType.Text, Personal: true),
        new("kind", "Kind", ReportColumnType.Text), new("classCode", "Class", ReportColumnType.Text), new("lam.actualUseCode", "Actual use", ReportColumnType.Text),
        new("assessedValue", "Assessed value", ReportColumnType.Money), new("lam.partlyExempt", "Partly exempt", ReportColumnType.Text),
        new("lam.wholeAssessedValue", "Assessed value of the whole FAAS", ReportColumnType.Money), new("legalBasis", "Legal basis of exemption", ReportColumnType.Text),
        new("previousArpNumber", "Previous ARP no.", ReportColumnType.Text), new("previousTdNumber", "Previous TD no.", ReportColumnType.Text),
        new("effectivity.quarter", "Effectivity quarter", ReportColumnType.Integer), new("effectivity.year", "Effectivity year", ReportColumnType.Integer),
        new("enteredOn", "Entered on", ReportColumnType.Date), new("remarks", "Remarks", ReportColumnType.Text),
    ];

    private static readonly Field[] OwnershipFields =
    [
        new("enteredOn", "Entered on", ReportColumnType.Date), new("kind", "Kind", ReportColumnType.Text), new("classCode", "Class", ReportColumnType.Text),
        new("pin", "PIN", ReportColumnType.Text), new("titleNumber", "Title no.", ReportColumnType.Text), new("lotBlock", "Lot / block", ReportColumnType.Text),
        new("arpNumber", "ARP no.", ReportColumnType.Text), new("tdNumber", "TD no.", ReportColumnType.Text),
        new("previousOwner", "Previous owner", ReportColumnType.Text), new("location", "Location", ReportColumnType.Text),
        new("lam.municipality", "Municipality", ReportColumnType.Text), new("area", "Area", ReportColumnType.Area), new("areaUnit", "Unit", ReportColumnType.Text),
        new("marketValue", "Market value", ReportColumnType.Money), new("assessedValue", "Assessed value", ReportColumnType.Money),
        new("lam.noticeNumber", "Notice no.", ReportColumnType.Text), new("lam.machines", "Machines", ReportColumnType.Text),
        new("lam.past.heldUntil", "Held until", ReportColumnType.Date), new("lam.past.endReason", "Why it ended", ReportColumnType.Text),
        new("remarks", "Remarks", ReportColumnType.Text),
    ];

    private static readonly Field[] RecordFields =
    [
        new("date", "Date", ReportColumnType.Date), new("arpNumber", "ARP no.", ReportColumnType.Text), new("tdNumber", "TD no.", ReportColumnType.Text),
        new("owner", "Owner", ReportColumnType.Text), new("pin", "PIN", ReportColumnType.Text), new("lam.sectionParcel", "Section / parcel", ReportColumnType.Text),
        new("location", "Location", ReportColumnType.Text), new("taxable", "Taxable", ReportColumnType.Text), new("kind", "Kind", ReportColumnType.Text),
        new("landArea", "Land area", ReportColumnType.Area), new("lam.buildingArea", "Building floor area", ReportColumnType.Area),
        new("marketValueL", "Market value, land", ReportColumnType.Money), new("marketValueB", "Market value, building", ReportColumnType.Money),
        new("marketValueM", "Market value, machinery", ReportColumnType.Money), new("assessedValueL", "Assessed value, land", ReportColumnType.Money),
        new("assessedValueB", "Assessed value, building", ReportColumnType.Money), new("assessedValueM", "Assessed value, machinery", ReportColumnType.Money),
        new("lam.marketValueTaxable", "Market value, taxable", ReportColumnType.Money), new("lam.marketValueExempt", "Market value, exempt", ReportColumnType.Money),
        new("lam.assessedValueTaxable", "Assessed value, taxable", ReportColumnType.Money), new("lam.assessedValueExempt", "Assessed value, exempt", ReportColumnType.Money),
        new("lam.legalBasis", "Legal basis of exemption", ReportColumnType.Text), new("yearTaxesBegin", "Year taxes begin", ReportColumnType.Integer),
        new("transactionCode", "Transaction code", ReportColumnType.Text),
    ];

    private static readonly Field[] SalesFields =
    [
        new("kind", "Kind", ReportColumnType.Text), new("classification", "Classification", ReportColumnType.Text),
        new("subClass", "Sub-class / building type", ReportColumnType.Text), new("unit", "Unit", ReportColumnType.Text),
        new("count", "Sales", ReportColumnType.Integer), new("lowest", "Lowest unit price", ReportColumnType.Money),
        new("median", "Median unit price", ReportColumnType.Money), new("highest", "Highest unit price", ReportColumnType.Money),
    ];

    public async Task<Result<ReportExport>> RegisterRunAsync(Guid runId, ReportFormat format, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var run = await db.RegisterRuns.AsNoTracking().Where(x => x.Id == runId)
            .Select(x => new
            {
                x.Kind, x.AsOf, x.FromDate, Barangay = x.Barangay != null ? x.Barangay.Name : null,
                Municipality = x.Barangay != null ? x.Barangay.Municipality!.Name : null, Section = x.Section != null ? x.Section.IndexNumber : null,
                Classification = x.Classification != null ? x.Classification.Name : null,
                Owner = x.Taxpayer != null ? new { x.Taxpayer.TaxpayerType, x.Taxpayer.LastName, x.Taxpayer.FirstName, x.Taxpayer.MiddleName, x.Taxpayer.Suffix, x.Taxpayer.CorporateName } : null,
                x.IncludePastOwners,
            })
            .FirstOrDefaultAsync(ct);
        if (run is null)
        {
            return Result.Failure<ReportExport>("REGISTER_RUN_NOT_FOUND", "There is no such register run.");
        }
        var (title, fields) = run.Kind switch
        {
            RegisterKind.TaxMapControlRoll => ("Tax Map Control Roll", TaxMapFields),
            RegisterKind.PreTaxMapControlRoll => ("Pre-Tax Map Control Roll", PreTaxMapFields),
            RegisterKind.AssessmentRollTaxable => ("Assessment Roll, taxable properties", RollFields),
            RegisterKind.AssessmentRollExempt => ("Assessment Roll, exempt properties", RollFields),
            RegisterKind.OwnershipRecordCard => ("Ownership Record Form", OwnershipFields),
            RegisterKind.RecordOfAssessment => ("Record of Assessment", RecordFields),
            _ => throw new InvalidOperationException($"Unhandled {nameof(RegisterKind)}: {run.Kind}"),
        };
        var lines = new List<string>();
        if (run.Municipality is not null)
        {
            lines.Add($"Barangay: {run.Barangay}, {run.Municipality}");
        }
        if (run.Section is not null)
        {
            lines.Add($"Tax map section: {run.Section}");
        }
        if (run.Classification is not null)
        {
            lines.Add($"Classification: {run.Classification}");
        }
        if (run.Owner is { } o)
        {
            lines.Add($"Owner: {TaxpayerNameFormatter.Format(o.TaxpayerType, o.LastName, o.FirstName, o.MiddleName, o.Suffix, o.CorporateName)}"
                      + (run.IncludePastOwners ? " (with past holdings)" : string.Empty));
        }
        lines.Add(run.FromDate is { } from ? $"Period: {from:yyyy-MM-dd} to {run.AsOf:yyyy-MM-dd}" : $"As of: {run.AsOf:yyyy-MM-dd}");
        return await ExportAsync(FormSubjectType.Register, runId, title, lines, fields, data => data["rows"] as JsonArray,
            $"{RegisterService.FormCode(run.Kind).ToLowerInvariant().Replace('_', '-')}-{run.AsOf:yyyyMMdd}", format, ct);
    }

    public async Task<Result<ReportExport>> SalesReportRunAsync(Guid runId, ReportFormat format, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var run = await db.MarketDataReportRuns.AsNoTracking().Where(x => x.Id == runId)
            .Select(x => new { x.Kind, x.FromDate, x.ToDate, Municipality = x.Municipality!.Name }).FirstOrDefaultAsync(ct);
        if (run is null)
        {
            return Result.Failure<ReportExport>("MARKET_DATA_REPORT_NOT_FOUND", "There is no such market data report run.");
        }
        if (run.Kind != MarketDataReportKind.SalesReport)
        {
            return Result.Failure<ReportExport>("VALIDATION_FAILED", "Only the lowest-to-highest sales report is downloaded as a file; print the abstracts.");
        }
        string[] lines = [$"Municipality: {run.Municipality}", $"Period: {run.FromDate:yyyy-MM-dd} to {run.ToDate:yyyy-MM-dd}"];
        return await ExportAsync(FormSubjectType.MarketDataReport, runId, "Lowest, median and highest recorded sales", lines, SalesFields,
            data => data["data"]?["groups"] as JsonArray, $"sales-report-{run.ToDate:yyyyMMdd}", format, ct);
    }

    private async Task<Result<ReportExport>> ExportAsync(FormSubjectType subjectType, Guid subjectId, string title, IReadOnlyList<string> lines,
        IReadOnlyList<Field> fields, Func<JsonObject, JsonArray?> rowsOf, string fileName, ReportFormat format, CancellationToken ct)
    {
        if (writers.FirstOrDefault(w => w.Format == format) is not { } writer)
        {
            return Result.Failure<ReportExport>("VALIDATION_FAILED", "The format must be csv or xlsx.");
        }
        // The latest issue in force holds what was printed; without one, the rows are read now.
        var issued = await db.IssuedForms.AsNoTracking()
            .Where(x => x.SubjectType == subjectType && x.SubjectId == subjectId && x.Status == WorkflowStatus.Posted)
            .OrderByDescending(x => x.IssuedAt).Select(x => new { x.FormCode, x.FormVersion, x.IssuedAt, x.DataSnapshotJson }).FirstOrDefaultAsync(ct);
        JsonObject data;
        var parameterLines = lines.ToList();
        if (issued is not null)
        {
            data = JsonNode.Parse(issued.DataSnapshotJson) as JsonObject ?? [];
            parameterLines.Add($"Issued {clock.LocalDate(issued.IssuedAt):yyyy-MM-dd} on form {issued.FormCode} v{issued.FormVersion}");
        }
        else
        {
            var provider = providers.Single(p => p.SubjectType == subjectType);
            if (await provider.BuildAsync(subjectId, ct) is not { } built)
            {
                return Result.Failure<ReportExport>("FORM_SUBJECT_NOT_FOUND", "The run could not be read.");
            }
            data = built.Data;
            parameterLines.Add("Not issued: read from the records on the day of the download");
        }

        var rows = rowsOf(data) ?? [];
        var seesPersonal = (await permissions.GetAsync(ct)).Contains(Permissions.TaxpayerViewPersonal);
        var cells = rows.OfType<JsonObject>().Select(row => fields.Select(f => Cell(row, f, maskPersonal: !seesPersonal)).ToArray()).ToList<object?[]>();
        var notes = new List<string>();
        if (!seesPersonal && fields.Any(f => f.Personal))
        {
            notes.Add("Addresses are hidden: seeing them needs the permission to see personal data.");
        }

        var columns = fields.Select(f => new ReportColumn(f.Path.Replace('.', '_'), f.Title, f.Type)).ToList();
        var header = await ReportHeader.BuildAsync(db, office, currentUser, clock, lgu.Value, title, parameterLines, ct);
        var document = new ReportDocument(title, header, columns, cells, null, notes);
        var what = $"{title} ({string.Join("; ", parameterLines)}), {cells.Count:N0} rows";
        await events.WriteExportAsync(new ExportEvent(AuditTrailService.ExportModule, ReportService.AuditTable, subjectId, ReportService.Truncate(what, 200),
            format == ReportFormat.Csv ? ExportFormats.Csv : ExportFormats.Excel), ct);
        return Result.Success(new ReportExport($"{fileName}.{writer.Extension}", writer.ContentType, (stream, token) => writer.WriteAsync(document, stream, token)));
    }

    /// <summary>A field of a snapshot row as a typed cell: numbers as decimals or integers, dates as dates, yes/no for flags.</summary>
    private static object? Cell(JsonObject row, Field field, bool maskPersonal)
    {
        JsonNode? node = row;
        foreach (var part in field.Path.Split('.'))
        {
            node = node is JsonObject o ? o[part] : null;
        }
        if (node is not JsonValue value)
        {
            return null;
        }
        if (value.TryGetValue<bool>(out var flag))
        {
            return flag ? "Yes" : "No";
        }
        if (field.Personal && maskPersonal)
        {
            return PersonalData.MaskAddress(value.ToString());
        }
        return field.Type switch
        {
            ReportColumnType.Money or ReportColumnType.Area when value.TryGetValue<decimal>(out var d) => d,
            ReportColumnType.Integer when value.TryGetValue<int>(out var i) => i,
            ReportColumnType.Date when value.TryGetValue<string>(out var s)
                && DateOnly.TryParse(s.Length >= 10 ? s[..10] : s, CultureInfo.InvariantCulture, out var date) => date,
            _ => value.ToString(),
        };
    }
}
