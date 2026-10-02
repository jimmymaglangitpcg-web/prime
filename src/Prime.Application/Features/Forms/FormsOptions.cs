namespace Prime.Application.Features.Forms;

/// <summary>"Forms" configuration section: what the form data may carry.</summary>
public sealed class FormsOptions
{
    public const string SectionName = "Forms";

    /// <summary>
    /// Whether the owners' and administrators' sex goes into the form data (the LAM FAAS, TD and ORF ask for it).
    /// Off by default: personal information under the Data Privacy Act, printed only when the office decides to
    /// (docs/analysis/records-and-forms.md Q3).
    /// </summary>
    public bool PrintOwnerSex { get; set; }
}
