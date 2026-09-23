namespace Prime.Application.Common;

/// <summary>
/// Uniform outcome type for Application-layer use cases, so a validation or
/// business-rule failure (e.g. "no applicable assessment rule found") is
/// returned rather than thrown, and maps cleanly to the CLAUDE.md §63 API
/// error shape ({code, message, details, traceId}) at the WebApi boundary.
/// Exceptions remain reserved for truly exceptional/unexpected failures.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? code, string? message)
    {
        if (isSuccess && code is not null)
        {
            throw new InvalidOperationException("A successful result cannot carry an error code.");
        }

        if (!isSuccess && code is null)
        {
            throw new InvalidOperationException("A failed result must carry an error code.");
        }

        IsSuccess = isSuccess;
        Code = code;
        Message = message;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Code { get; }
    public string? Message { get; }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string code, string message) => new(false, code, message);
    public static Result<T> Success<T>(T value) => new(value, true, null, null);
    public static Result<T> Failure<T>(string code, string message) => new(default, false, code, message);
}

public sealed class Result<T> : Result
{
    internal Result(T? value, bool isSuccess, string? code, string? message)
        : base(isSuccess, code, message)
    {
        _value = value;
    }

    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");
}
