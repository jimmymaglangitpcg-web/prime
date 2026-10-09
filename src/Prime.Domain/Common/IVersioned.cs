namespace Prime.Domain.Common;

/// <summary>
/// A record that goes through a workflow status or is edited in place, and
/// so carries an optimistic-concurrency token (CLAUDE.md §66;
/// docs/analysis/production-hardening.md §4.4). <see cref="RowVersion"/> maps to
/// PostgreSQL's <c>xmin</c> system column for every implementing entity (one
/// model convention in PrimeDbContext; no physical column, the Parcel
/// precedent). EF adds "WHERE xmin = @loaded" to each UPDATE and DELETE, so of
/// two requests that load the same row and both change it, exactly one saves.
/// A client that echoes the version it displayed in an <c>If-Match</c> header
/// is refused when the row has changed since.
/// </summary>
public interface IVersioned
{
    uint RowVersion { get; set; }
}
