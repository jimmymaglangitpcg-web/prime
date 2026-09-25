namespace Prime.Domain.Entities.Reference;

// Each of these is a distinct table with identical shape (Code, Name,
// Description, SortOrder, IsActive — see LookupEntity). Grouped in one file
// because there is nothing type-specific to add; splitting into twelve
// near-empty files would hurt navigability more than it would help.
// Seed only clearly-labeled demo/test rows here (CLAUDE.md §81) — never
// invented LGU classification values.

/// <summary>CLAUDE.md §27 "Zone".</summary>
public sealed class Zone : LookupEntity;

/// <summary>CLAUDE.md §27 "Classification" (e.g. Residential, Commercial, Agricultural, Industrial).</summary>
public sealed class Classification : LookupEntity;

/// <summary>CLAUDE.md §27 "Actual Use".</summary>
public sealed class ActualUse : LookupEntity;

/// <summary>CLAUDE.md §27 "Sub-Classification".</summary>
public sealed class SubClassification : LookupEntity;

/// <summary>CLAUDE.md §27 "Road Type".</summary>
public sealed class RoadType : LookupEntity;

/// <summary>CLAUDE.md §27 "Condition" (building/machinery physical condition).</summary>
public sealed class Condition : LookupEntity;

/// <summary>CLAUDE.md §27 "Building Type".</summary>
public sealed class BuildingType : LookupEntity;

/// <summary>CLAUDE.md §27 "Structural Type".</summary>
public sealed class StructuralType : LookupEntity;

/// <summary>
/// CLAUDE.md §25/§27 "Building Component" — component types and costs must
/// be configurable; the illustrative list in §25 (Foundation, Structural
/// Frame, ...) is seed/demo data for this table, not a closed enum.
/// </summary>
public sealed class BuildingComponentType : LookupEntity;

/// <summary>CLAUDE.md §27 "Machinery Type".</summary>
public sealed class MachineryType : LookupEntity;

/// <summary>
/// CLAUDE.md §20/§27 "Ownership Type" — no closed list is given in the
/// spec (unlike TaxpayerType), and §27 explicitly calls it out as
/// configurable reference data.
/// </summary>
public sealed class OwnershipType : LookupEntity;

/// <summary>
/// Kinds of Tax Declaration annotation (e.g. levy, adverse claim).
/// LGU-configurable: the list and wording come from the LAM
/// (docs/FORMS-REVISION-PLAN.md §5 A4).
/// </summary>
public sealed class AnnotationType : LookupEntity;

/// <summary>
/// Supports Document (§59, DOMAIN-MODEL.md §3.19a) — Title, Deed, Tax
/// Declaration scan, Assessment document, Exemption document, Valuation
/// document, Other. Reference table, not an enum, since the set of
/// document kinds an LGU handles is realistically extensible.
/// </summary>
public sealed class DocumentType : LookupEntity;

/// <summary>
/// CLAUDE.md §27 "Property Type" — keyed by SmvSchedule/AssessmentLevel
/// (§28/§29) alongside Classification/ActualUse. Not built in Phase 3;
/// added in Phase 5 because it's the first feature that actually needs it.
/// </summary>
public sealed class PropertyType : LookupEntity;

/// <summary>
/// CLAUDE.md §27 "Tax Type" — the tax/levy a bill line belongs to (e.g. basic
/// RPT, Special Education Fund). LGU-configurable; drives per-fund
/// collection reporting (§41). docs/BILLING.md §3.1.
/// </summary>
public sealed class TaxType : LookupEntity;

/// <summary>
/// Kinds of land improvement other than buildings — trees, plants and the
/// like (MRPAAO Att. 1 "Other Improvements"). The list comes from the LGU's
/// SMV; none is built in.
/// </summary>
public sealed class ImprovementKind : LookupEntity;

/// <summary>Kinds of land title (OCT, TCT, CLOA, CCT …) for the FAAS and TD (MRPAAO Att. 1, 4).</summary>
public sealed class TitleType : LookupEntity;

/// <summary>Structure parts of the FAAS structural materials checklist (MRPAAO p.150–152): foundation, columns, roofing …</summary>
public sealed class StructuralPart : LookupEntity;

/// <summary>A material for one structure part (MRPAAO p.150–152), e.g. roofing — G.I. sheet.</summary>
public sealed class StructuralMaterial : LookupEntity
{
    public Guid StructuralPartId { get; set; }
    public StructuralPart? StructuralPart { get; set; }
}
