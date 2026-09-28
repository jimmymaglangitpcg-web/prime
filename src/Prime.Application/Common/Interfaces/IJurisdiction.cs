namespace Prime.Application.Common.Interfaces;

/// <summary>
/// The municipalities the current request may read and write
/// (docs/analysis/province-wide-operation.md §3.3). A request by a municipal
/// user is restricted to the municipalities their office covers today; a
/// provincial or province-wide user is not restricted. Outside a request
/// (background jobs, start-up seeders, tests calling services directly)
/// nothing is restricted. The same state drives the database's query filters,
/// so reads and write checks always agree.
/// </summary>
public interface IJurisdiction
{
    bool Restricted { get; }

    /// <summary>The municipalities a restricted request may work in; empty for a user with no office.</summary>
    IReadOnlyCollection<Guid> MunicipalityIds { get; }

    bool Allows(Guid municipalityId) => !Restricted || MunicipalityIds.Contains(municipalityId);
}
