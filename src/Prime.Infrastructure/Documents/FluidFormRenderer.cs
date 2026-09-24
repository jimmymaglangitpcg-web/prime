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
        return options;
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
