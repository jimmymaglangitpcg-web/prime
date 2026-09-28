using Prime.Application.Common.Interfaces;

namespace Prime.Infrastructure.Identity;

/// <summary>
/// Request-scoped holder of <see cref="IJurisdiction"/>. Unrestricted until
/// <c>JurisdictionMiddleware</c> restricts it for a municipal user's request;
/// <see cref="Persistence.PrimeDbContext"/> reads it in its query filters.
/// </summary>
public sealed class JurisdictionState : IJurisdiction
{
    private List<Guid> municipalityIds = [];

    public bool Restricted { get; private set; }

    public IReadOnlyCollection<Guid> MunicipalityIds => municipalityIds;

    /// <summary>The list the query filters use (a List, so it translates to <c>= ANY(...)</c>).</summary>
    internal List<Guid> FilterIds => municipalityIds;

    public void Restrict(IEnumerable<Guid> municipalities)
    {
        municipalityIds = municipalities.Distinct().ToList();
        Restricted = true;
    }
}
