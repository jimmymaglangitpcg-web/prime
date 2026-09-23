namespace Prime.Domain.Enums;

/// <summary>
/// Structural lifecycle status shared by Property, Taxpayer, Parcel, RPU,
/// Land, Building, and Machinery. Deliberately a fixed C# enum, not an
/// LGU-configurable reference table: these values describe PRIME's own
/// record lifecycle (is this row live, retired, or subsumed by another
/// record?), not a legal/business classification an LGU ordinance could
/// vary. Contrast with WorkflowStatus (maker-checker approval state) and
/// the reference tables in Entities/Reference (LGU-configurable
/// classifications called out in CLAUDE.md §27).
/// </summary>
public enum RecordStatus
{
    Active = 0,
    Inactive = 1,
    Cancelled = 2,
    Subdivided = 3,
    Consolidated = 4,
    Superseded = 5,
}
