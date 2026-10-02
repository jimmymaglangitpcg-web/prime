namespace Prime.Application.Features.Registers;

/// <summary>"Registers" configuration section.</summary>
public sealed class RegistersOptions
{
    public const string SectionName = "Registers";

    /// <summary>
    /// The rows the office's Assessment Roll template prints per page, so the page and line PRIME
    /// records on issuance are the ones printed (docs/analysis/records-and-forms.md §4.3, Q5). It
    /// belongs to the template in use; 0 (the default) numbers every line on page 1.
    /// </summary>
    public int AssessmentRollRowsPerPage { get; set; }
}
