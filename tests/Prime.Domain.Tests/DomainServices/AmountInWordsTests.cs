using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>The TD's total assessed value in words (MRPAAO Att. 4; LAM TD, docs/analysis/records-and-forms.md Q9).</summary>
public class AmountInWordsTests
{
    [Theory]
    [InlineData("0", "ZERO PESOS ONLY")]
    [InlineData("1", "ONE PESO ONLY")]
    [InlineData("0.5", "ZERO PESOS AND 50/100")]
    [InlineData("21", "TWENTY ONE PESOS ONLY")]
    [InlineData("100", "ONE HUNDRED PESOS ONLY")]
    [InlineData("120000.50", "ONE HUNDRED TWENTY THOUSAND PESOS AND 50/100")]
    [InlineData("1000001", "ONE MILLION ONE PESOS ONLY")]
    [InlineData("2512345.07", "TWO MILLION FIVE HUNDRED TWELVE THOUSAND THREE HUNDRED FORTY FIVE PESOS AND 07/100")]
    [InlineData("3000000000", "THREE BILLION PESOS ONLY")]
    [InlineData("19.995", "TWENTY PESOS ONLY")]  // rounded to centavos first
    public void Pesos_WritesTheAmount(string amount, string expected) =>
        AmountInWords.Pesos(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
}
