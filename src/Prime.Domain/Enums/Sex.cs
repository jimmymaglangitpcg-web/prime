namespace Prime.Domain.Enums;

/// <summary>
/// An individual owner's or administrator's sex, as the LAM FAAS, TD and ownership forms ask for it (LAM Annex I-D
/// p.138, I-G p.164). Optional: personal information under the Data Privacy Act, printed only when
/// <c>Forms:PrintOwnerSex</c> is on (docs/analysis/records-and-forms.md §4.1, Q3).
/// </summary>
public enum Sex
{
    Male = 0,
    Female = 1,
}
