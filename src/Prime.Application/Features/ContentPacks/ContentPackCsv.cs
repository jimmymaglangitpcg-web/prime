using System.Text;

namespace Prime.Application.Features.ContentPacks;

/// <summary>One data row of a content-pack CSV file, with its 1-based line number in the file.</summary>
public sealed class CsvRow(int line, IReadOnlyDictionary<string, string> values)
{
    public int Line { get; } = line;

    /// <summary>The trimmed value of <paramref name="column"/>, or null when the column is absent or blank.</summary>
    public string? Get(string column) =>
        values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

/// <summary>A parsed CSV file: its header (lower-cased, trimmed) and data rows, or the reason it could not be read.</summary>
public sealed record CsvTable(IReadOnlyList<string> Columns, IReadOnlyList<CsvRow> Rows, string? Error, int? ErrorLine);

/// <summary>
/// RFC 4180 CSV for content packs (docs/analysis/lgu-content-pack.md §3.1):
/// comma-separated, double-quoted fields may hold commas, quotes ("") and line
/// breaks; UTF-8 with or without a byte-order mark; LF or CRLF. The first
/// record is the header; blank lines are skipped. A row whose field count
/// differs from the header's is an error, not silently padded (CLAUDE.md §60).
/// </summary>
public static class ContentPackCsv
{
    public static CsvTable Parse(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content);
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var records = new List<(int Line, List<string> Fields)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var inQuotes = false;
        var fieldStarted = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            fieldStarted = false;
        }

        void EndRecord()
        {
            EndField();
            if (!(fields.Count == 1 && fields[0].Length == 0))
            {
                records.Add((recordLine, fields));
            }
            fields = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (ch == '\n')
                    {
                        line++;
                    }
                    field.Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"' when !fieldStarted:
                    inQuotes = true;
                    fieldStarted = true;
                    break;
                case '"':
                    return new CsvTable([], [], "A quote may only open a field; quote the whole field and double any quote inside it.", line);
                case ',':
                    EndField();
                    break;
                case '\r' when i + 1 < text.Length && text[i + 1] == '\n':
                    break;
                case '\r':
                case '\n':
                    EndRecord();
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(ch);
                    fieldStarted = true;
                    break;
            }
        }

        if (inQuotes)
        {
            return new CsvTable([], [], "A quoted field is not closed.", recordLine);
        }
        if (field.Length > 0 || fields.Count > 0 || fieldStarted)
        {
            EndRecord();
        }

        if (records.Count == 0)
        {
            return new CsvTable([], [], "The file is empty; a header row is required.", 1);
        }

        var header = records[0].Fields.Select(h => h.Trim().ToLowerInvariant()).ToList();
        if (header.Any(h => h.Length == 0))
        {
            return new CsvTable([], [], "The header has a blank column name.", records[0].Line);
        }
        if (header.Distinct().Count() != header.Count)
        {
            return new CsvTable([], [], "The header names a column more than once.", records[0].Line);
        }

        var rows = new List<CsvRow>(records.Count - 1);
        foreach (var (recLine, values) in records.Skip(1))
        {
            if (values.Count != header.Count)
            {
                return new CsvTable(header, [], $"The row has {values.Count} fields; the header has {header.Count}.", recLine);
            }
            rows.Add(new CsvRow(recLine, header.Zip(values).ToDictionary(p => p.First, p => p.Second)));
        }
        return new CsvTable(header, rows, null, null);
    }
}
