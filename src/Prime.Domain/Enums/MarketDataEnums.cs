namespace Prime.Domain.Enums;

/// <summary>Where a market transaction was learned from (docs/analysis/smv-preparation-general-revision.md §4.1).</summary>
public enum MarketDataSource
{
    /// <summary>The Registry of Deeds' abstract of registered transactions (LAM 2025 Book I p.22; Annex I-M).</summary>
    RegistryAbstract = 0,
    /// <summary>The deed presented with a transfer (prefilled from the transfer's tax clearance).</summary>
    TransferDeed = 1,
    SwornStatement = 2,
    /// <summary>A data collection sheet from field validation (LAM 2025 Book IV p.104).</summary>
    FieldDataSheet = 3,
    Other = 4,
}

/// <summary>Whether a market transaction may be used as evidence in the SMV's sales analysis.</summary>
public enum MarketDataReview
{
    Unreviewed = 0,
    /// <summary>Validated and usable in the analysis.</summary>
    Accepted = 1,
    /// <summary>Kept on record, not used (the reason is recorded: not at arm's length, partial interest …).</summary>
    Excluded = 2,
}

public enum AreaMeasure
{
    SquareMetre = 0,
    Hectare = 1,
}

/// <summary>The scope of work of a building permit (LAM 2025 Annex I-N).</summary>
public enum BuildingPermitScope
{
    NewConstruction = 0,
    Addition = 1,
    Repair = 2,
    Renovation = 3,
    Demolition = 4,
    Other = 5,
}

/// <summary>The lists and report printed from market data (docs/analysis/smv-preparation-general-revision.md §4.1).</summary>
public enum MarketDataReportKind
{
    /// <summary>Abstract of registered real property transactions (LAM 2025 Book I p.22; Annex I-M).</summary>
    TransactionsAbstract = 0,
    /// <summary>Abstract of building permits (Book I pp.22–23; Annex I-N).</summary>
    BuildingPermitsAbstract = 1,
    /// <summary>Abstract of certificates of registration of installation of machinery (Book I p.23; Annex I-O).</summary>
    MachineryRegistrationsAbstract = 2,
    /// <summary>Report of lowest, median and highest recorded sales (Book I p.25; Annex I-R).</summary>
    SalesReport = 3,
}
