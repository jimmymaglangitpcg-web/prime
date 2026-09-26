using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;

namespace Prime.Infrastructure.Documents;

/// <summary>
/// Renders form templates with Fluid (Liquid) — docs/FORMS-REVISION-PLAN.md
/// §4.2. Liquid is logic-light and sandboxed (no code execution, no I/O).
/// Every output value is HTML-encoded. The page wrapper added here sets a
/// Content-Security-Policy that forbids scripts and outside resources, and,
/// for provisional forms, a watermark the template cannot remove.
/// Filters: <c>money</c> ("1,234.50"), <c>percent</c>, <c>date_ph</c> ("25 September 2026" —
/// timestamps are shown on the LGU's calendar, <c>Lgu:TimeZone</c>).
/// </summary>
public sealed class FluidFormRenderer(IOptions<LguOptions> lgu) : IFormRenderer
{
    private static readonly FluidParser Parser = new();
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly TemplateOptions options = CreateOptions(
        TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(lgu.Value.TimeZone) ? "UTC" : lgu.Value.TimeZone));

    public string? Validate(string templateBody) =>
        Parser.TryParse(templateBody, out _, out var error) ? null : error;

    public string Render(string templateBody, JsonObject data, bool provisional)
    {
        if (!Parser.TryParse(templateBody, out var template, out var error))
        {
            throw new InvalidOperationException($"Form template does not parse: {error}");
        }
        var context = new TemplateContext(options);
        foreach (var (key, value) in data)
        {
            context.SetValue(key, ToModel(value));
        }
        var body = template.Render(context, HtmlEncoder.Default);
        var title = WebUtility.HtmlEncode(data["form"]?["title"]?.GetValue<string>() ?? "Form");
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src data:">
            <title>{{title}}</title>
            <style>
            @page { size: A4 portrait; margin: 12mm; }
            body { font-family: Arial, Helvetica, sans-serif; font-size: 11pt; color: #000; margin: 0; }
            .prime-watermark { position: fixed; top: 40%; left: 0; right: 0; text-align: center; transform: rotate(-30deg);
              font-size: 40pt; font-weight: bold; color: rgba(200, 0, 0, 0.14); pointer-events: none; z-index: 1000; }
            .prime-provisional-banner { border: 2px solid #b00; color: #b00; padding: 4px 8px; margin-bottom: 8px; font-weight: bold; text-align: center; }
            </style>
            </head>
            <body>
            {{(provisional ? "<div class=\"prime-watermark\">PROVISIONAL — NOT AN OFFICIAL FORM</div>\n<div class=\"prime-provisional-banner\">PROVISIONAL LAYOUT — NOT AN OFFICIAL FORM. For testing and training only (docs/FORMS-REVISION-PLAN.md).</div>" : "")}}
            {{body}}
            </body>
            </html>
            """;
    }

    private static TemplateOptions CreateOptions(TimeZoneInfo zone)
    {
        var options = new TemplateOptions { CultureInfo = Invariant };
        options.Filters.AddFilter("money", (input, _, _) =>
            new StringValue(input.IsNil() ? "" : input.ToNumberValue().ToString("#,##0.00", Invariant)));
        options.Filters.AddFilter("percent", (input, _, _) =>
            new StringValue(input.IsNil() ? "" : input.ToNumberValue().ToString("0.##", Invariant) + "%"));
        // Areas and counts: grouped, trailing zeros dropped (500.0000 → 500; 1234.5 → 1,234.5).
        options.Filters.AddFilter("num", (input, _, _) =>
            new StringValue(input.IsNil() ? "" : input.ToNumberValue().ToString("#,##0.####", Invariant)));
        // "Total Assessed Value (Amount in Words)" (MRPAAO Att. 4), e.g. ONE HUNDRED THOUSAND PESOS AND 50/100.
        options.Filters.AddFilter("amount_words", (input, _, _) =>
            new StringValue(input.IsNil() ? "" : AmountInWords(input.ToNumberValue())));
        options.Filters.AddFilter("date_ph", (input, _, _) =>
        {
            var text = input.ToStringValue();
            // A plain date is a calendar date already; a timestamp is shown on the LGU's calendar.
            if (DateOnly.TryParseExact(text, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var date))
            {
                return new StringValue(date.ToString("d MMMM yyyy", Invariant));
            }
            return new StringValue(DateTimeOffset.TryParse(text, Invariant, DateTimeStyles.AssumeUniversal, out var value)
                ? TimeZoneInfo.ConvertTime(value, zone).ToString("d MMMM yyyy", Invariant)
                : text);
        });
        // Date and time on the LGU's clock, e.g. a receipt's time of payment (eOR: "date and time of receipt").
        options.Filters.AddFilter("datetime_ph", (input, _, _) =>
        {
            var text = input.ToStringValue();
            return new StringValue(DateTimeOffset.TryParse(text, Invariant, DateTimeStyles.AssumeUniversal, out var value)
                ? TimeZoneInfo.ConvertTime(value, zone).ToString("d MMMM yyyy, h:mm tt", Invariant)
                : text);
        });
        return options;
    }

    private static readonly string[] Ones =
    [
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "THIRTEEN",
        "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN",
    ];

    private static readonly string[] Tens = ["", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY"];

    private static readonly (long Value, string Name)[] Scales =
        [(1_000_000_000_000, "TRILLION"), (1_000_000_000, "BILLION"), (1_000_000, "MILLION"), (1_000, "THOUSAND")];

    /// <summary>Pesos in words, with centavos as a fraction: 100,000.50 → ONE HUNDRED THOUSAND PESOS AND 50/100.</summary>
    public static string AmountInWords(decimal amount)
    {
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var pesos = (long)Math.Floor(amount);
        var centavos = (int)((amount - pesos) * 100);
        var words = WholeInWords(pesos) + (pesos == 1 ? " PESO" : " PESOS");
        return centavos == 0 ? words + " ONLY" : $"{words} AND {centavos:00}/100";
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

    /// <summary>JSON snapshot → plain CLR values Fluid can navigate (dictionaries, lists, decimal, string, bool).</summary>
    private static object? ToModel(JsonNode? node) => node switch
    {
        null => null,
        JsonObject o => o.ToDictionary(p => p.Key, p => ToModel(p.Value)),
        JsonArray a => a.Select(ToModel).ToList(),
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.Number => decimal.Parse(v.ToJsonString(), NumberStyles.Float, Invariant),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => v.GetValue<string>(),
            _ => null,
        },
        _ => throw new NotSupportedException($"Unexpected JSON node {node.GetType().Name}."),
    };
}
