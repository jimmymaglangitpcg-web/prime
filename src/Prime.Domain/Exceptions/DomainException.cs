namespace Prime.Domain.Exceptions;

/// <summary>
/// Base type for business-rule violations raised from Domain/Application
/// logic (e.g. self-approval on a maker-checker transaction, posting a
/// non-balanced bill). Prime.WebApi's global exception middleware maps
/// these to the CLAUDE.md §63 error shape and never leaks the stack trace
/// to the caller.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
