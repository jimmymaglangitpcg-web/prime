using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Infrastructure.Documents;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Forms;

/// <summary>Renderer guarantees from docs/FORMS-REVISION-PLAN.md §4.2 (no database needed).</summary>
public class FluidFormRendererTests
{
    private readonly FluidFormRenderer renderer = new(Options.Create(new LguOptions { TimeZone = "Asia/Manila" }));

    private static JsonObject Data(string value) => new()
    {
        ["form"] = new JsonObject { ["title"] = "DEMO form" },
        ["x"] = new JsonObject { ["text"] = value, ["amount"] = 1234.5m, ["items"] = new JsonArray(1, 2, 3) },
    };

    [Fact]
    public void Render_EncodesValues_SoDataCannotInjectMarkup()
    {
        var html = renderer.Render("<p>{{ x.text }}</p>", Data("<script>alert(1)</script>"), provisional: false);

        html.ShouldNotContain("<script>alert(1)</script>");
        html.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void Render_ForbidsScriptsAndOutsideResources()
    {
        var html = renderer.Render("<p>x</p>", Data(""), provisional: false);

        html.ShouldContain("Content-Security-Policy");
        html.ShouldContain("default-src 'none'");
    }

    [Fact]
    public void Render_Provisional_AddsWatermarkOutsideTheTemplate()
    {
        renderer.Render("<p>x</p>", Data(""), provisional: true).ShouldContain("PROVISIONAL — NOT AN OFFICIAL FORM");
        renderer.Render("<p>x</p>", Data(""), provisional: false).ShouldNotContain("NOT AN OFFICIAL FORM");
    }

    [Fact]
    public void Render_Filters_AndNestedData()
    {
        var html = renderer.Render("{{ x.amount | money }}|{{ x.items | size }}|{% for i in x.items %}{{ i }}{% endfor %}", Data(""), provisional: false);

        html.ShouldContain("1,234.50|3|123");
    }

    [Fact]
    public void DateFilter_ShowsTimestampsOnTheLguCalendar_AndPlainDatesAsIs()
    {
        var data = new JsonObject { ["form"] = new JsonObject { ["title"] = "t" }, ["ts"] = "2026-09-24T22:30:00+00:00", ["d"] = "2026-09-24" };

        renderer.Render("{{ ts | date_ph }}|{{ d | date_ph }}", data, provisional: false).ShouldContain("25 September 2026|24 September 2026");
    }

    [Fact]
    public void Validate_ReportsParseErrors()
    {
        renderer.Validate("{% if x %}unterminated").ShouldNotBeNull();
        renderer.Validate("{{ x.text }}").ShouldBeNull();
    }

    [Theory]
    [InlineData("TAX_BILL.v1.liquid")]
    [InlineData("TAX_DECLARATION.v1.liquid")]
    [InlineData("TAX_DECLARATION.v2.liquid")]
    [InlineData("NOTICE_OF_ASSESSMENT.v1.liquid")]
    [InlineData("FAAS.v1.liquid")]
    [InlineData("STATEMENT_OF_ACCOUNT.v1.liquid")]
    public void ProvisionalTemplates_AreEmbeddedAndParse(string file) =>
        renderer.Validate(ProvisionalFormSeeder.ReadTemplate(file)).ShouldBeNull();
}
