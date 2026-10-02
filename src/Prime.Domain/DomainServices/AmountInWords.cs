using System.Globalization;

namespace Prime.Domain.DomainServices;

/// <summary>
/// An amount in pesos written out, as the Tax Declaration prints its total assessed value ("Amount in Words",
/// MRPAAO Att. 4; LAM TD, docs/analysis/records-and-forms.md Q9): upper-case English words, the centavos as a
/// fraction — 100,000.50 → "ONE HUNDRED THOUSAND PESOS AND 50/100", 100,000 → "ONE HUNDRED THOUSAND PESOS ONLY".
/// The single implementation: the form renderer's <c>amount_words</c> filter and the TD form data both use it.
/// </summary>
public static class AmountInWords
{
    private static readonly string[] Ones =
    [
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "THIRTEEN",
        "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN",
    ];

    private static readonly string[] Tens = ["", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"];

    private static readonly (long Value, string Name)[] Scales =
        [(1_000_000_000_000, "TRILLION"), (1_000_000_000, "BILLION"), (1_000_000, "MILLION"), (1_000, "THOUSAND")];

    /// <summary>The amount (its absolute value, rounded to centavos) in words.</summary>
    public static string Pesos(decimal amount)
    {
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var pesos = (long)Math.Floor(amount);
        var centavos = (int)((amount - pesos) * 100);
        var words = WholeInWords(pesos) + (pesos == 1 ? " PESO" : " PESOS");
        return centavos == 0 ? words + " ONLY" : $"{words} AND {centavos.ToString("00", CultureInfo.InvariantCulture)}/100";
    }

    private static string WholeInWords(long n)
    {
        if (n < 1000)
        {
            return BelowThousand((int)n);
        }
        var parts = new List<string>();
        foreach (var (value, name) in Scales)
        {
            if (n >= value)
            {
                parts.Add($"{WholeInWords(n / value)} {name}");
                n %= value;
            }
        }
        if (n > 0)
        {
            parts.Add(BelowThousand((int)n));
        }
        return string.Join(" ", parts);
    }

    private static string BelowThousand(int n)
    {
        if (n < 20)
        {
            return Ones[n];
        }
        if (n < 100)
        {
            return Tens[n / 10] + (n % 10 == 0 ? "" : " " + Ones[n % 10]);
        }
        return Ones[n / 100] + " HUNDRED" + (n % 100 == 0 ? "" : " " + BelowThousand(n % 100));
    }
}
