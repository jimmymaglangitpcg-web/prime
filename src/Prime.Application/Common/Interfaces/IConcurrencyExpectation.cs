namespace Prime.Application.Common.Interfaces;

/// <summary>
/// The version of the record a request acts on, as the client displayed it
/// (docs/analysis/production-hardening.md §4.4): the route's <c>{id}</c> and
/// the <c>If-Match</c> header of an approve, reject, post, cancel or edit. The
/// first save that touches that record is refused with a
/// <c>DbUpdateConcurrencyException</c> (409 <c>CONCURRENCY_CONFLICT</c>) when
/// the row has changed since, so a user never acts on a stale screen. Empty
/// when the header is absent (older clients, tests, background jobs); the row
/// version still guards races between load and save.
/// </summary>
public interface IConcurrencyExpectation
{
    Guid? SubjectId { get; }
    uint? ExpectedVersion { get; }

    void Expect(Guid subjectId, uint version);
}
