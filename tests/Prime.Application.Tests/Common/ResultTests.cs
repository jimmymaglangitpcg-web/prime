using Prime.Application.Common;
using Shouldly;
using Xunit;

namespace Prime.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccessAndHasNoCode()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Code.ShouldBeNull();
    }

    [Fact]
    public void Failure_CarriesCodeAndMessage()
    {
        var result = Result.Failure("ASSESSMENT_RULE_NOT_FOUND", "No applicable assessment rule was found.");

        result.IsFailure.ShouldBeTrue();
        result.Code.ShouldBe("ASSESSMENT_RULE_NOT_FOUND");
    }

    [Fact]
    public void FailureOfT_ThrowsOnValueAccess()
    {
        var result = Result.Failure<int>("X", "message");

        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
