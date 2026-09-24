namespace Prime.Domain.Enums;

/// <summary>
/// What a <c>NumberingScheme</c> numbers (docs/FORMS-REVISION-PLAN.md §4.4).
/// The kinds are system concepts; their formats are configuration.
/// </summary>
public enum NumberedDocumentKind
{
    PropertyIdentificationNumber = 0,
    TaxDeclaration = 1,
    TaxBill = 2,
    Faas = 3,
    NoticeOfAssessment = 4,
    OfficialReceipt = 5,
}

/// <summary>Who prescribes a form version (docs/FORMS-REVISION-PLAN.md §4.1).</summary>
public enum FormAuthority
{
    /// <summary>A PRIME layout for testing and training — always watermarked, never an official form.</summary>
    PrimeProvisional = 0,
    /// <summary>Prescribed under the Local Assessment Manual (DOF DC 004-2025).</summary>
    Lam = 1,
    Blgf = 2,
    LguOrdinance = 3,
    Other = 4,
}

/// <summary>The record a form renders. Each has a data provider that builds the form's snapshot.</summary>
public enum FormSubjectType
{
    TaxBill = 0,
    TaxDeclaration = 1,
}

/// <summary>Records whose approval can follow a configured <c>ApprovalChain</c>.</summary>
public enum ApprovalSubjectType
{
    Assessment = 0,
}
