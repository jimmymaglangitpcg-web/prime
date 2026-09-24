using Prime.Application.Features.Gis;
using Shouldly;
using Xunit;

namespace Prime.Application.Tests.Features;

public class GisBboxParsingTests
{
    [Fact]
    public void ValidBbox_ParsesInLonLatOrder()
    {
        var result = GisService.ParseBbox("120.5, 14.25,121,14.75");

        result.IsSuccess.ShouldBeTrue();
        result.Value.MinX.ShouldBe(120.5);
        result.Value.MinY.ShouldBe(14.25);
        result.Value.MaxX.ShouldBe(121);
        result.Value.MaxY.ShouldBe(14.75);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("a,2,3,4")]
    [InlineData("121,14,120,15")]   // min lon > max lon
    [InlineData("120,15,121,14")]   // min lat > max lat
    [InlineData("120,14,120,15")]   // zero width
    [InlineData("-181,14,121,15")]
    [InlineData("120,-91,121,15")]
    [InlineData("120,14,NaN,15")]
    [InlineData("120;14;121;15")]
    public void InvalidBbox_IsRejected(string? bbox)
    {
        var result = GisService.ParseBbox(bbox);

        result.IsFailure.ShouldBeTrue();
        result.Code.ShouldBe("INVALID_BBOX");
    }

    [Fact]
    public void Bbox_IsCultureInvariant()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            // A comma-decimal culture must not change how "120.5" parses.
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            GisService.ParseBbox("120.5,14,121,15").Value.MinX.ShouldBe(120.5);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
