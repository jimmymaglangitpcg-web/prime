using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Audit;

/// <summary>
/// The audit viewer and a record's history (CLAUDE.md §48, §50 "Audit History"; docs/analysis/workflow-security.md
/// §4.3), and the EXPORT rows of prints and downloads made in the browser. Read-only over the audit log: no path here
/// changes or deletes a row.
/// </summary>
public interface IAuditTrailService
{
    Task<Result<PagedResult<AuditLogDto>>> ListAsync(AuditLogQuery query, CancellationToken ct = default);
    Task<Result<AuditLogDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> ListTablesAsync(CancellationToken ct = default);
    Task<Result<bool>> RecordExportAsync(RecordExportRequest request, CancellationToken ct = default);
}

public sealed class AuditLogQuery : PagedRequest
{
    public Guid? UserId { get; set; }
    public string? TableName { get; set; }
    public Guid? RecordId { get; set; }
    /// <summary>With <see cref="RecordId"/>: also the rows of the record's children (assignment roles, transaction requirements …).</summary>
    public bool IncludeChildren { get; set; }
    /// <summary>A property's history: the property and its owners, parcels, RPUs, land, buildings, machinery, valuations,
    /// assessments, Tax Declarations and transactions, with their child rows (CLAUDE.md §50 "Audit History").</summary>
    public Guid? PropertyId { get; set; }
    public AuditAction? Action { get; set; }
    public string? Module { get; set; }
    public DateTimeOffset? From { get; set; }
    /// <summary>Exclusive.</summary>
    public DateTimeOffset? To { get; set; }
}

public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid? UserId,
    string? UserName,
    string Module,
    string TableName,
    Guid RecordId,
    string? ParentTableName,
    Guid? ParentRecordId,
    AuditAction Action,
    string? OldValue,
    string? NewValue,
    string? Reason,
    string? IpAddress);

/// <summary>A print or download made in the browser, reported by the page that made it.</summary>
public sealed record RecordExportRequest(string TableName, Guid? RecordId, string What, string Format);

public sealed class AuditTrailService(IApplicationDbContext db, ISecurityEventLog events) : IAuditTrailService
{
    public const string ExportModule = "Exports";

    public async Task<Result<PagedResult<AuditLogDto>>> ListAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        if (query.From is { } from && query.To is { } to && to <= from)
        {
            return Result.Failure<PagedResult<AuditLogDto>>("VALIDATION_FAILED", "The end of the period must be after its start.");
        }

        var logs = db.AuditLogs;
        if (query.UserId is { } userId)
        {
            logs = logs.Where(a => a.UserId == userId);
        }
        if (!string.IsNullOrWhiteSpace(query.TableName))
        {
            var table = query.TableName.Trim();
            logs = query.IncludeChildren ? logs.Where(a => a.TableName == table || a.ParentTableName == table) : logs.Where(a => a.TableName == table);
        }
        if (query.RecordId is { } recordId)
        {
            logs = query.IncludeChildren ? logs.Where(a => a.RecordId == recordId || a.ParentRecordId == recordId) : logs.Where(a => a.RecordId == recordId);
        }
        if (query.PropertyId is { } propertyId)
        {
            var ids = await PropertyRecordIdsAsync(propertyId, ct);
            logs = logs.Where(a => ids.Contains(a.RecordId) || (a.ParentRecordId != null && ids.Contains(a.ParentRecordId.Value)));
        }
        if (query.Action is { } action)
        {
            logs = logs.Where(a => a.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim();
            logs = logs.Where(a => a.Module == module);
        }
        if (query.From is { } start)
        {
            logs = logs.Where(a => a.Timestamp >= start);
        }
        if (query.To is { } end)
        {
            logs = logs.Where(a => a.Timestamp < end);
        }

        var total = await logs.CountAsync(ct);
        var items = await Project(logs.OrderByDescending(a => a.Timestamp).ThenBy(a => a.Id)
                .Skip((Math.Max(query.Page, 1) - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<AuditLogDto> { Items = items, TotalCount = total, Page = Math.Max(query.Page, 1), PageSize = query.PageSize });
    }

    public async Task<Result<AuditLogDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await Project(db.AuditLogs.Where(a => a.Id == id)).FirstOrDefaultAsync(ct);
        return row is null ? Result.Failure<AuditLogDto>("AUDIT_LOG_NOT_FOUND", "No such audit row.") : Result.Success(row);
    }

    public async Task<Result<IReadOnlyList<string>>> ListTablesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> tables = await db.AuditLogs.Select(a => a.TableName).Distinct().OrderBy(t => t).ToListAsync(ct);
        return Result.Success(tables);
    }

    public async Task<Result<bool>> RecordExportAsync(RecordExportRequest request, CancellationToken ct = default)
    {
        var table = request.TableName?.Trim() ?? string.Empty;
        var what = request.What?.Trim() ?? string.Empty;
        if (table.Length is 0 or > 100 || what.Length is 0 or > 200
            || !ExportFormats.All.Contains(request.Format ?? string.Empty) || request.Format == ExportFormats.Issued)
        {
            return Result.Failure<bool>("VALIDATION_FAILED",
                "Give the table (at most 100), what was exported (at most 200) and the format: Print, Csv, Excel or Pdf.");
        }
        await events.WriteExportAsync(new ExportEvent(ExportModule, table, request.RecordId ?? Guid.Empty, what, request.Format!), ct);
        return Result.Success(true);
    }

    private async Task<List<Guid>> PropertyRecordIdsAsync(Guid propertyId, CancellationToken ct)
    {
        var ids = new List<Guid> { propertyId };
        ids.AddRange(await db.PropertyTaxpayers.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.Parcels.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.RealPropertyUnits.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.Lands.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.Buildings.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.MachineryUnits.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.Valuations.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.Assessments.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.TaxDeclarations.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        ids.AddRange(await db.PropertyTransactions.Where(x => x.PropertyId == propertyId).Select(x => x.Id).ToListAsync(ct));
        return ids;
    }

    private IQueryable<AuditLogDto> Project(IQueryable<Prime.Domain.Entities.Audit.AuditLog> logs) =>
        from a in logs
        join u in db.AppUsers.AsNoTracking() on a.UserId equals u.Id into users
        from u in users.DefaultIfEmpty()
        select new AuditLogDto(a.Id, a.Timestamp, a.UserId, u == null ? null : u.DisplayName, a.Module, a.TableName, a.RecordId,
            a.ParentTableName, a.ParentRecordId, a.Action, a.OldValue, a.NewValue, a.Reason, a.IpAddress);
}
