using Prime.Domain.Common;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Registers;

/// <summary>
/// Where a TD was entered in an issued Assessment Roll (docs/analysis/records-and-forms.md §4.3):
/// the page and line it was printed on, the date and the issuing user. Written once, when the roll
/// is issued, and never changed. A cancelled roll keeps its entries; they count as cancelled
/// through their issued form's status, and the TD's latest entry in a valid roll is the one the FAAS prints.
/// </summary>
public sealed class AssessmentRollEntry : Entity
{
    public Guid IssuedFormId { get; set; }
    public IssuedForm? IssuedForm { get; set; }
    public Guid TaxDeclarationId { get; set; }
    public TaxDeclaration? TaxDeclaration { get; set; }

    /// <summary><see cref="RegisterKind.AssessmentRollTaxable"/> or <see cref="RegisterKind.AssessmentRollExempt"/>.</summary>
    public RegisterKind Kind { get; set; }
    public int Page { get; set; }
    public int Line { get; set; }

    /// <summary>The local date the roll was issued.</summary>
    public DateOnly EnteredOn { get; set; }
    public Guid? EnteredBy { get; set; }
}
