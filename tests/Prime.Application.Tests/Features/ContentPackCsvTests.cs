using System.Text;
using Prime.Application.Features.ContentPacks;
using Shouldly;
using Xunit;

namespace Prime.Application.Tests.Features;

/// <summary>Content pack CSV reading and path rules (docs/analysis/lgu-content-pack.md, step C1).</summary>
public class ContentPackCsvTests
{
    private static CsvTable Parse(string text, bool bom = false) =>
        ContentPackCsv.Parse((bom ? [0xEF, 0xBB, 0xBF] : Array.Empty<byte>()).Concat(Encoding.UTF8.GetBytes(text)).ToArray());

    [Fact]
    public void ReadsHeaderAndRows_TrimmingAndLowerCasingColumnNames()
    {
        var t = Parse(" Code ,NAME\nA,Alpha\nB, Beta \n");
        t.Error.ShouldBeNull();
        t.Columns.ShouldBe(["code", "name"]);
        t.Rows.Count.ShouldBe(2);
        t.Rows[1].Get("name").ShouldBe("Beta");
        t.Rows[1].Line.ShouldBe(3);
    }

    [Fact]
    public void QuotedFields_HoldCommasQuotesAndLineBreaks_AndLineNumbersFollowThem()
    {
        var t = Parse("code,name\r\n1,\"Poblacion, Town\"\r\n2,\"Two\r\nlines\"\r\n3,\"Say \"\"hi\"\"\"\r\n", bom: true);
        t.Error.ShouldBeNull();
        t.Rows.Select(r => r.Get("name")).ShouldBe(["Poblacion, Town", "Two\r\nlines", "Say \"hi\""]);
        t.Rows.Select(r => r.Line).ShouldBe([2, 3, 5]);
    }

    [Fact]
    public void BlankLinesAreSkipped_AndBlankCellsReadAsNull()
    {
        var t = Parse("code,name\n\nA,\n\n");
        t.Rows.Count.ShouldBe(1);
        t.Rows[0].Get("name").ShouldBeNull();
        t.Rows[0].Get("absent").ShouldBeNull();
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("code,,name\n", "blank column")]
    [InlineData("code,code\n", "more than once")]
    [InlineData("code,name\nA\n", "1 fields")]
    [InlineData("code,name\nA,\"open\n", "not closed")]
    [InlineData("code,name\nA,x\"y\n", "quote")]
    public void MalformedFiles_AreRejectedWithAReason(string text, string reason)
    {
        var t = Parse(text);
        t.Error.ShouldNotBeNull();
        t.Error.ShouldContain(reason);
        t.Rows.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("geography/provinces.csv", true)]
    [InlineData("lookups/a.csv", true)]
    [InlineData("../secret.csv", false)]
    [InlineData("geography/../../x.csv", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("C:/x.csv", false)]
    [InlineData("geography\\provinces.csv", false)]
    [InlineData("geography//x.csv", false)]
    [InlineData("./x.csv", false)]
    [InlineData("", false)]
    public void PackPaths_AreRelativeAndStayInsideThePack(string path, bool allowed) =>
        (ContentPackService.PathProblem(path) is null).ShouldBe(allowed);
}
