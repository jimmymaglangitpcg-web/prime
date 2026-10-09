namespace Prime.Domain.Exceptions;

/// <summary>
/// A computed amount too large to store as <c>numeric(18,2)</c>: almost always a mistyped area, quantity, cost or
/// rate. Returned as 400 VALUE_OUT_OF_RANGE, and nothing is saved.
/// </summary>
public sealed class ValueOutOfRangeException(string what, decimal value) : DomainException(ErrorCode,
    $"The {what} ({value:#,0.##}) is larger than PRIME can store. Check the areas, quantities, costs and rates entered.")
{
    public const string ErrorCode = "VALUE_OUT_OF_RANGE";
}
