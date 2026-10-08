using Prime.Domain.Enums;

namespace Prime.Application.Common.Interfaces;

/// <summary>
/// Writes audit rows for events that change no record: sign-in and sign-out, and exports and prints (CLAUDE.md §48;
/// docs/analysis/workflow-security.md §4.2–§4.3). Every record change is still audited only by the save interceptor.
/// </summary>
public interface ISecurityEventLog
{
    /// <summary>One <c>AuditLog</c> row for the acting user, against their <c>AppUsers</c> row.</summary>
    Task WriteAsync(AuditAction action, Guid appUserId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>One EXPORT row for the acting user, against the exported or printed record.</summary>
    Task WriteExportAsync(ExportEvent export, CancellationToken cancellationToken = default);
}

/// <summary>
/// What was exported or printed: the record (its table and id; <see cref="Guid.Empty"/> for a list or a map), a short
/// description and the format (<see cref="ExportFormats"/>).
/// </summary>
public sealed record ExportEvent(string Module, string TableName, Guid RecordId, string What, string Format);

public static class ExportFormats
{
    public const string Print = "Print";
    public const string Csv = "Csv";
    public const string Excel = "Excel";
    public const string Pdf = "Pdf";
    /// <summary>A record issued or a register run by the server, printed from PRIME afterwards.</summary>
    public const string Issued = "Issued";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Print, Csv, Excel, Pdf, Issued };
}
