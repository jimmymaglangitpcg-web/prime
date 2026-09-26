namespace Prime.Domain.Enums;

/// <summary>How a property's PIN was given (docs/analysis/property-identification.md §3.3–§3.4).</summary>
public enum PinKind
{
    /// <summary>Given at registration: typed, or from a PIN scheme without a section (and every PIN from before step 10a-2).</summary>
    Registered = 0,

    /// <summary>A temporary PIN before tax mapping (MRPAAO Ch. II §2 D.1.b(2)), from the TemporaryPin numbering scheme.</summary>
    Temporary = 1,

    /// <summary>The permanent PIN: index numbers, tax map section and parcel number (MRPAAO Ch. II §1 D).</summary>
    Permanent = 2,
}
