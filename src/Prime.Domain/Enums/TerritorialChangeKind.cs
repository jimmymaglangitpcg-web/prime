namespace Prime.Domain.Enums;

/// <summary>Why properties move to other index numbers (LAM Bk II p.40 §4.E–F).</summary>
public enum TerritorialChangeKind
{
    /// <summary>A new province, city, municipality or barangay is created: all its PINs are retired and assigned anew.</summary>
    CreatedLgu = 0,
    /// <summary>A portion of an LGU is transferred to another by law or court order.</summary>
    TransferredTerritory = 1,
}

/// <summary>How a territorial change gives the new PINs (docs/analysis/identification-numbering.md §4.3, Q9).</summary>
public enum TerritorialChangePinMode
{
    /// <summary>Keep each property's section and parcel number under the new index numbers.</summary>
    KeepParcelNumbers = 0,
    /// <summary>Give temporary PINs until the area is re-tax-mapped.</summary>
    TemporaryPins = 1,
}

public enum TerritorialChangeItemStatus
{
    Pending = 0,
    Done = 1,
    Failed = 2,
}
