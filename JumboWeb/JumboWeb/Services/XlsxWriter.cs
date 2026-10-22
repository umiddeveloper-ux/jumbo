using System.IO.Compression;
using System.Security;
using System.Text;

namespace JumboWeb.Services;

/// <summary>
/// A small writer of .xlsx files (one sheet): a bold header row, numbers as numbers, text as text, widths that fit. No library: an .xlsx is a zip
/// of a few XML files, and the reports only need a table.
/// </summary>
public static class XlsxWriter
{
    public const string MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <param name="rows">The first row is the header. A cell is a string, a number (int / double / long / decimal) or null.</param>
    public static byte[] Build(string sheetName, IReadOnlyList<IReadOnlyList<object?>> rows) => BuildWorkbook([(sheetName, rows)]);

    /// <summary>A workbook with a sheet for each item (the names are made unique and safe).</summary>
    public static byte[] BuildWorkbook(IReadOnlyList<(string Name, IReadOnlyList<IReadOnlyList<object?>> Rows)> sheets)
    {
        var names = new List<string>();
        foreach (var (name, _) in sheets)
        {
            var n = SafeSheetName(name); var candidate = n; int i = 2;
            while (names.Contains(candidate, StringComparer.OrdinalIgnoreCase)) candidate = (n.Length > 28 ? n[..28] : n) + " " + i++;
            names.Add(candidate);
        }

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                string.Concat(sheets.Select((_, i) => $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
            Add(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
                string.Concat(names.Select((n, i) => $"<sheet name=\"{Escape(n)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) + "</sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Concat(sheets.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>")) +
                $"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            // style 0: normal, style 1: bold (the header)
            Add(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
                "</styleSheet>");
            for (int i = 0; i < sheets.Count; i++) Add(zip, $"xl/worksheets/sheet{i + 1}.xml", Sheet(sheets[i].Rows));
        }
        return ms.ToArray();
    }

    private static string Sheet(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        int cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var widths = new int[cols];
        foreach (var row in rows)
            for (int c = 0; c < row.Count; c++)
                widths[c] = Math.Max(widths[c], Text(row[c]).Length);

        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        if (cols > 0)
        {
            sb.Append("<cols>");
            for (int c = 0; c < cols; c++)
                sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{Math.Clamp(widths[c] + 3, 6, 60)}\" customWidth=\"1\"/>");
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            sb.Append($"<row r=\"{r + 1}\">");
            for (int c = 0; c < rows[r].Count; c++)
            {
                var cell = $"{ColumnName(c)}{r + 1}";
                var style = r == 0 ? " s=\"1\"" : "";
                switch (rows[r][c])
                {
                    case null:
                        break;
                    case int or long or double or decimal or float or short:
                        sb.Append($"<c r=\"{cell}\"{style}><v>{Convert.ToString(rows[r][c], System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        sb.Append($"<c r=\"{cell}\"{style} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(rows[r][c]!.ToString()!)}</t></is></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string Text(object? v) => v is null ? string.Empty : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static string ColumnName(int index)
    {
        var s = string.Empty;
        for (int n = index + 1; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    private static string SafeSheetName(string name)
    {
        var clean = new string(name.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Report";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    // XML does not allow most control characters: they are dropped, the five special characters are escaped.
    private static string Escape(string s) =>
        SecurityElement.Escape(new string(s.Where(ch => ch >= ' ' || ch is '\t' or '\n' or '\r').ToArray())) ?? string.Empty;

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
