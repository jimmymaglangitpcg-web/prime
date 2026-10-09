using Prime.Domain.Common;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Registers;

/// <summary>
/// A municipality's monthly assessment roll, submitted to the province
/// (docs/analysis/province-wide-operation.md §3.7, LP-6). PRIME prepares the
/// month's Assessment Rolls (taxable and exempt) for every barangay with
/// entries and issues them, so the submission points to frozen printed
/// copies. The province acknowledges it or returns it with remarks; a returned
/// submission is kept and a new one is prepared for the same month. Nothing is
/// deleted. A month with no entries is a nil return (no items).
/// </summary>
public sealed class AssessmentRollSubmission : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }

    /// <summary>The municipal office that submitted it (the one covering the municipality then).</summary>
    public Guid OfficeId { get; set; }
    public Office? Office { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public AssessmentRollSubmissionStatus Status { get; set; } = AssessmentRollSubmissionStatus.Submitted;
    public string? Remarks { get; set; }

    /// <summary>The province's acknowledgement or return.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewRemarks { get; set; }

    public List<AssessmentRollSubmissionItem> Items { get; set; } = [];
}

/// <summary>One barangay's roll in a submission: the register run and its issued (frozen) form.</summary>
public sealed class AssessmentRollSubmissionItem : Entity
{
    public Guid SubmissionId { get; set; }
    public Guid RegisterRunId { get; set; }
    public RegisterRun? RegisterRun { get; set; }
    public Guid IssuedFormId { get; set; }
    public IssuedForm? IssuedForm { get; set; }
    public Guid BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    /// <summary><see cref="RegisterKind.AssessmentRollTaxable"/> or <see cref="RegisterKind.AssessmentRollExempt"/>.</summary>
    public RegisterKind Kind { get; set; }
    public int EntryCount { get; set; }
}
