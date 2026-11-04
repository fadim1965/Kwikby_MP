using System.Globalization;
using System.Text;

namespace kwikby_MP.Services.Reports;

/// <summary>
/// A minimal, dependency-free PDF writer for management reports.
/// </summary>
/// <remarks>
/// <para>
/// The marketplace has no route to nuget.org, so reports cannot rely on a PDF library. This
/// writer covers exactly what a report needs and nothing more: A4 landscape pages, the PDF
/// base-14 Helvetica family (no font embedding), headings, a headline-figure band, tables with
/// wrapped cells, repeating headers across page breaks, a bold totals row, notes, and a footer
/// with the page number and generation stamp.
/// </para>
/// <para>
/// Text is written with <c>WinAnsiEncoding</c>; every string is transcoded to that single byte
/// range and escaped, so the produced file is pure ASCII and the xref offsets are simply the
/// character counts. Content streams are stored uncompressed, which keeps the file inspectable
/// (and testable) with a text search.
/// </para>
/// </remarks>
public static class ReportPdf
{
    // A4 landscape, rounded. PDF points (1/72 inch).
    private const double PageWidth = 842;
    private const double PageHeight = 595;
    private const double MarginLeft = 40;
    private const double MarginRight = 40;
    private const double MarginTop = 40;
    private const double MarginBottom = 46;

    private const double RowFontSize = 8;
    private const double RowLineHeight = 10;
    private const double RowPadding = 3.5;

    /// <summary>Renders a document into a complete PDF file.</summary>
    public static byte[] Render(ReportDocument doc, StoreSettings store)
    {
        var b = new Builder(store, doc);
        b.Build();
        return Encoding.ASCII.GetBytes(b.GetFile());
    }

    // ------------------------------------------------------------------
    // Layout engine
    // ------------------------------------------------------------------

    private sealed class Builder
    {
        private readonly StoreSettings _store;
        private readonly ReportDocument _doc;
        private readonly List<StringBuilder> _pages = new();
        private StringBuilder _cur = null!;
        private double _y;
        private string _runningHeader = string.Empty;

        public Builder(StoreSettings store, ReportDocument doc)
        {
            _store = store;
            _doc = doc;
        }

        private double ContentWidth => PageWidth - MarginLeft - MarginRight;
        private double BottomLimit => MarginBottom;

        public void Build()
        {
            NewPage(first: true);
            WriteHeader();
            WriteFigures();
            foreach (var section in _doc.Sections)
            {
                WriteSection(section);
            }
            WriteNotes();
            StampFooters();
        }

        // ---- primitives ------------------------------------------------

        private void NewPage(bool first = false)
        {
            _cur = new StringBuilder();
            _pages.Add(_cur);
            _y = PageHeight - MarginTop;

            if (!first)
            {
                // Running header so a multi-page table still says what it is.
                Text(_runningHeader, 8, bold: false, italic: true, gray: .45, x: MarginLeft, y: _y);
                _y -= 12;
                Rule(.6, gray: .75);
                _y -= 10;
            }
        }

        private void Ensure(double space)
        {
            if (_y - space < BottomLimit)
            {
                NewPage();
            }
        }

        private void Text(string text, double size, bool bold, bool italic, double gray, double x, double y)
        {
            var font = bold ? "F2" : italic ? "F3" : "F1";
            _cur.Append(FormattableString.Invariant(
                $"BT {gray.ToString("0.##", CultureInfo.InvariantCulture)} g /{font} {size.ToString("0.##", CultureInfo.InvariantCulture)} Tf 1 0 0 1 {x.ToString("0.##", CultureInfo.InvariantCulture)} {y.ToString("0.##", CultureInfo.InvariantCulture)} Tm ({Esc(text)}) Tj ET\n"));
        }

        private void Fill(double gray, double x, double y, double w, double h)
        {
            _cur.Append(FormattableString.Invariant(
                $"{gray.ToString("0.##", CultureInfo.InvariantCulture)} g {x.ToString("0.##", CultureInfo.InvariantCulture)} {y.ToString("0.##", CultureInfo.InvariantCulture)} {w.ToString("0.##", CultureInfo.InvariantCulture)} {h.ToString("0.##", CultureInfo.InvariantCulture)} re f\n"));
        }

        private void Rule(double width, double gray, double x1 = MarginLeft, double? x2 = null, double? y = null)
        {
            var yy = y ?? _y;
            var right = x2 ?? PageWidth - MarginRight;
            _cur.Append(FormattableString.Invariant(
                $"{width.ToString("0.##", CultureInfo.InvariantCulture)} w {gray.ToString("0.##", CultureInfo.InvariantCulture)} g {x1.ToString("0.##", CultureInfo.InvariantCulture)} {yy.ToString("0.##", CultureInfo.InvariantCulture)} m {right.ToString("0.##", CultureInfo.InvariantCulture)} {yy.ToString("0.##", CultureInfo.InvariantCulture)} l S\n"));
        }

        // ---- page furniture --------------------------------------------

        private void WriteHeader()
        {
            _runningHeader = $"{_doc.Title} · {_doc.Period}";

            Text(_store.Name, 10, bold: false, italic: false, gray: .45, MarginLeft, _y);
            _y -= 22;
            Text(_doc.Title, 20, bold: true, italic: false, gray: 0, MarginLeft, _y);
            _y -= 16;
            Text($"{_doc.Period} · Scope: {_doc.Scope}", 10, bold: false, italic: false, gray: .35, MarginLeft, _y);
            _y -= 12;
            Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC", 8, bold: false, italic: true, gray: .5, MarginLeft, _y);
            _y -= 8;
            Rule(1.1, gray: .2);
            _y -= 16;
        }

        private void WriteFigures()
        {
            if (_doc.Figures.Count == 0)
            {
                return;
            }

            const double boxHeight = 44;
            const double gap = 10;
            var count = _doc.Figures.Count;
            var boxWidth = (ContentWidth - gap * (count - 1)) / count;

            Ensure(boxHeight + 14);

            var x = MarginLeft;
            foreach (var figure in _doc.Figures)
            {
                Fill(.955, x, _y - boxHeight, boxWidth, boxHeight);
                Text(figure.Value, 13, bold: true, italic: false, gray: 0, x + 8, _y - 18);
                Text(figure.Label.ToUpperInvariant(), 7, bold: false, italic: false, gray: .45, x + 8, _y - 32);
                x += boxWidth + gap;
            }

            _y -= boxHeight + 18;
        }

        // ---- sections --------------------------------------------------

        private void WriteSection(ReportSection section)
        {
            Ensure(46);
            Text(section.Title, 12, bold: true, italic: false, gray: 0, MarginLeft, _y);
            _y -= 14;

            var widths = ColumnWidths(section);

            // Column x positions (left edges), in order.
            var xs = new double[widths.Count];
            var x = MarginLeft;
            for (var i = 0; i < widths.Count; i++)
            {
                xs[i] = x;
                x += widths[i];
            }

            WriteTableHeader(section.Headers, xs, widths);

            for (var r = 0; r < section.Rows.Count; r++)
            {
                WriteRow(section.Rows[r], xs, widths, section.RightAligned, zebra: r % 2 == 1, bold: false);
            }

            if (section.Totals is not null)
            {
                _y -= 2;
                Rule(.9, gray: .35);
                _y -= RowLineHeight + RowPadding;
                WriteRow(section.Totals, xs, widths, section.RightAligned, zebra: false, bold: true);
                _y -= 2;
                Rule(.9, gray: .35);
            }

            _y -= 12;
        }

        private void WriteTableHeader(IReadOnlyList<string> headers, double[] xs, IReadOnlyList<double> widths)
        {
            const double headerHeight = 15;
            Ensure(headerHeight + 20);
            Fill(.92, MarginLeft, _y - headerHeight + 3, ContentWidth, headerHeight);
            for (var i = 0; i < headers.Count; i++)
            {
                Text(headers[i], 7.6, bold: true, italic: false, gray: .25, xs[i] + 4, _y - 6);
            }
            _y -= headerHeight - 1;
            Rule(.7, gray: .5);
            _y -= 4;
        }

        private void WriteRow(object?[] cells, double[] xs, IReadOnlyList<double> widths,
            IReadOnlyList<bool> rightAligned, bool zebra, bool bold)
        {
            var wrapped = new List<string>[cells.Length];
            var maxLines = 1;
            for (var i = 0; i < cells.Length; i++)
            {
                wrapped[i] = Wrap(CellText(cells[i]), widths[i] - 8);
                maxLines = Math.Max(maxLines, wrapped[i].Count);
            }

            var rowHeight = maxLines * RowLineHeight + RowPadding * 2;
            Ensure(rowHeight);

            if (zebra)
            {
                Fill(.968, MarginLeft, _y - rowHeight + 4, ContentWidth, rowHeight);
            }

            for (var i = 0; i < cells.Length; i++)
            {
                var lines = wrapped[i];
                for (var line = 0; line < lines.Count; line++)
                {
                    var text = lines[line];
                    double tx;
                    if (rightAligned[i])
                    {
                        tx = xs[i] + widths[i] - 4 - Measure(text, RowFontSize, bold);
                    }
                    else
                    {
                        tx = xs[i] + 4;
                    }

                    Text(text, RowFontSize, bold, italic: false, gray: 0, tx, _y - RowFontSize - line * RowLineHeight);
                }
            }

            _y -= rowHeight;
        }

        private void WriteNotes()
        {
            if (_doc.Notes.Count == 0)
            {
                return;
            }

            Ensure(30);
            Text("Notes", 9.5, bold: true, italic: false, gray: 0, MarginLeft, _y);
            _y -= 12;

            foreach (var note in _doc.Notes)
            {
                foreach (var line in Wrap(note, ContentWidth - 12))
                {
                    Ensure(RowLineHeight + 2);
                    Text(line, 7.6, bold: false, italic: true, gray: .4, MarginLeft + 2, _y - 7);
                    _y -= RowLineHeight;
                }
                _y -= 3;
            }
        }

        private void StampFooters()
        {
            var total = _pages.Count;
            var stamp = $"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC · {_store.Name}";
            for (var i = 0; i < total; i++)
            {
                var page = _pages[i];
                page.Append(FormattableString.Invariant(
                    $"0.6 w .7 g {MarginLeft.ToString(CultureInfo.InvariantCulture)} 34 m {(PageWidth - MarginRight).ToString(CultureInfo.InvariantCulture)} 34 l S\n"));
                page.Append(FormattableString.Invariant(
                    $"BT .45 g /F1 7.5 Tf 1 0 0 1 {MarginLeft.ToString(CultureInfo.InvariantCulture)} 22 Tm ({Esc(stamp)}) Tj ET\n"));
                var pageNo = $"Page {i + 1} of {total}";
                var right = PageWidth - MarginRight - Measure(pageNo, 7.5, bold: false);
                page.Append(FormattableString.Invariant(
                    $"BT .45 g /F1 7.5 Tf 1 0 0 1 {right.ToString("0.##", CultureInfo.InvariantCulture)} 22 Tm ({Esc(pageNo)}) Tj ET\n"));
            }
        }

        // ---- column sizing ---------------------------------------------

        /// <summary>
        /// Splits the available width between the columns in proportion to their natural width
        /// (the widest cell, sampled), so a narrow "Status" column stays narrow and long product
        /// names get the room they need. A floor keeps tiny columns legible.
        /// </summary>
        private IReadOnlyList<double> ColumnWidths(ReportSection section)
        {
            const double floor = 40;
            var natural = new double[section.Headers.Count];
            for (var i = 0; i < natural.Length; i++)
            {
                natural[i] = Measure(section.Headers[i], 7.6, bold: true) + 10;
                var sampled = 0;
                foreach (var row in section.Rows)
                {
                    if (i < row.Length)
                    {
                        var text = CellText(row[i]);
                        if (text.Length > 44)
                        {
                            text = text[..44];
                        }
                        natural[i] = Math.Max(natural[i], Measure(text, RowFontSize, bold: false) + 10);
                        if (++sampled >= 80)
                        {
                            break;
                        }
                    }
                }
                natural[i] = Math.Max(natural[i], floor);
            }

            var widths = new double[natural.Length];
            var naturalTotal = natural.Sum();
            if (naturalTotal <= ContentWidth)
            {
                // Room to spare: take the natural widths and split the surplus between the
                // columns that sit above the floor.
                var spare = ContentWidth - naturalTotal;
                var aboveFloor = natural.Sum(n => n - floor);
                for (var i = 0; i < widths.Length; i++)
                {
                    widths[i] = aboveFloor <= 0
                        ? ContentWidth / widths.Length
                        : natural[i] + (natural[i] - floor) / aboveFloor * spare;
                }
            }
            else
            {
                // Too many columns to honour the floors: shrink proportionally rather than let
                // a column run off the page.
                for (var i = 0; i < widths.Length; i++)
                {
                    widths[i] = natural[i] * ContentWidth / naturalTotal;
                }
            }

            return widths;
        }

        // ---- text helpers ----------------------------------------------

        private static string CellText(object? value) => value switch
        {
            null => "—",
            decimal d => d.ToString("N2", CultureInfo.InvariantCulture),
            int i => i.ToString("N0", CultureInfo.InvariantCulture),
            long l => l.ToString("N0", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
            bool flag => flag ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty
        };

        /// <summary>Greedy word wrap to a measured width; long words are hard-broken.</summary>
        private static List<string> Wrap(string text, double maxWidth)
        {
            var lines = new List<string>();
            if (maxWidth <= 0)
            {
                maxWidth = 40;
            }

            foreach (var paragraph in text.Split('\n'))
            {
                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                var current = string.Empty;
                foreach (var word in words)
                {
                    var candidate = current.Length == 0 ? word : current + " " + word;
                    if (Measure(candidate, RowFontSize, bold: false) <= maxWidth)
                    {
                        current = candidate;
                        continue;
                    }

                    if (current.Length > 0)
                    {
                        lines.Add(current);
                    }

                    // Hard-break a word that cannot fit on a line of its own.
                    var rest = word;
                    while (Measure(rest, RowFontSize, bold: false) > maxWidth && rest.Length > 1)
                    {
                        var cut = rest.Length;
                        while (cut > 1 && Measure(rest[..cut], RowFontSize, bold: false) > maxWidth)
                        {
                            cut--;
                        }
                        lines.Add(rest[..cut]);
                        rest = rest[cut..];
                    }
                    current = rest;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }
            }

            return lines.Count == 0 ? new List<string> { string.Empty } : lines;
        }

        /// <summary>Width of a string in points, using the Helvetica AFM advance widths.</summary>
        private static double Measure(string text, double size, bool bold)
        {
            var widths = bold ? HelveticaBoldWidths : HelveticaWidths;
            var total = 0;
            foreach (var ch in text)
            {
                var code = WinAnsiByte(ch);
                total += code switch
                {
                    >= 32 and <= 126 => widths[code - 32],
                    _ => SpecialWidths.GetValueOrDefault(code, 556)
                };
            }

            return total * size / 1000.0;
        }

        /// <summary>
        /// Transcodes a string to WinAnsi bytes and escapes it as a PDF literal, so the output
        /// file stays 7-bit ASCII. Characters WinAnsi cannot carry are dropped.
        /// </summary>
        private static string Esc(string text)
        {
            var sb = new StringBuilder(text.Length + 8);
            foreach (var ch in text)
            {
                var b = WinAnsiByte(ch);
                switch (b)
                {
                    case (byte)'\\':
                    case (byte)'(':
                    case (byte)')':
                        sb.Append('\\').Append((char)b);
                        break;
                    default:
                        if (b is >= 32 and <= 126)
                        {
                            sb.Append((char)b);
                        }
                        else
                        {
                            sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Maps a .NET char to the WinAnsi (CP1252) byte range used by the fonts.</summary>
        private static byte WinAnsiByte(char ch) => ch switch
        {
            '\t' => (byte)' ',
            '\r' or '\n' => (byte)' ',
            <= '\u007E' => (byte)ch,
            '\u20AC' => 0x80, // €
            '\u201A' => 0x82,
            '\u0192' => 0x83,
            '\u201E' => 0x84,
            '\u2026' => 0x85, // …
            '\u2020' => 0x86,
            '\u2021' => 0x87,
            '\u02C6' => 0x88,
            '\u2030' => 0x89,
            '\u0160' => 0x8A,
            '\u2039' => 0x8B,
            '\u0152' => 0x8C,
            '\u017D' => 0x8E,
            '\u2018' => 0x91, // '
            '\u2019' => 0x92, // '
            '\u201C' => 0x93, // "
            '\u201D' => 0x94, // "
            '\u2022' => 0x95, // •
            '\u2013' => 0x96, // –
            '\u2014' => 0x97, // —
            '\u02DC' => 0x98,
            '\u2122' => 0x99, // ™
            '\u0161' => 0x9A,
            '\u203A' => 0x9B,
            '\u0153' => 0x9C,
            '\u017E' => 0x9E,
            '\u0178' => 0x9F,
            ' ' => (byte)' ',
            <= 'ÿ' => (byte)ch,
            _ => (byte)'?'
        };

        // ---- file assembly ---------------------------------------------

        /// <summary>Builds the complete PDF: header, objects, xref table, trailer.</summary>
        public string GetFile()
        {
            var sb = new StringBuilder(64 * 1024);
            var offsets = new List<int> { 0 }; // object 0 is the free head

            void BeginObject(int id)
            {
                while (offsets.Count < id)
                {
                    offsets.Add(0);
                }
                offsets.Add(sb.Length);
                sb.Append(id).Append(" 0 obj\n");
            }

            sb.Append("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

            var pageCount = _pages.Count;
            var infoId = 6 + pageCount * 2;

            BeginObject(1);
            sb.Append("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            BeginObject(2);
            sb.Append("<< /Type /Pages /Kids [");
            for (var i = 0; i < pageCount; i++)
            {
                sb.Append(6 + i * 2).Append(" 0 R ");
            }
            sb.Append("] /Count ").Append(pageCount).Append(" >>\nendobj\n");

            BeginObject(3);
            sb.Append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
            BeginObject(4);
            sb.Append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
            BeginObject(5);
            sb.Append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>\nendobj\n");

            for (var i = 0; i < pageCount; i++)
            {
                BeginObject(6 + i * 2);
                sb.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ")
                  .Append(PageWidth.ToString("0", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(PageHeight.ToString("0", CultureInfo.InvariantCulture))
                  .Append("] /Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> >> /Contents ")
                  .Append(7 + i * 2).Append(" 0 R >>\nendobj\n");

                var stream = _pages[i].ToString();
                BeginObject(7 + i * 2);
                sb.Append("<< /Length ").Append(stream.Length).Append(" >>\nstream\n")
                  .Append(stream)
                  .Append("endstream\nendobj\n");
            }

            BeginObject(infoId);
            sb.Append("<< /Title (").Append(Esc(_doc.Title)).Append(") /Producer (LibanPost reports) /CreationDate (D:")
              .Append(DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture))
              .Append("Z) >>\nendobj\n");

            var xrefAt = sb.Length;
            var count = offsets.Count;
            sb.Append("xref\n0 ").Append(count).Append('\n');
            sb.Append("0000000000 65535 f \n");
            for (var id = 1; id < count; id++)
            {
                sb.Append(offsets[id].ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }
            sb.Append("trailer\n<< /Size ").Append(count)
              .Append(" /Root 1 0 R /Info ").Append(infoId).Append(" 0 R >>\nstartxref\n")
              .Append(xrefAt).Append("\n%%EOF\n");

            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------
    // Helvetica AFM advance widths (1/1000 em), printable ASCII 32..126
    // ------------------------------------------------------------------

    private static readonly int[] HelveticaWidths =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,
        556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    private static readonly int[] HelveticaBoldWidths =
    {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
        975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
        333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
        611,611,389,556,333,611,556,778,556,556,500,389,280,389,584
    };

    /// <summary>Advance widths for the WinAnsi special range; 556 elsewhere.</summary>
    private static readonly Dictionary<byte, int> SpecialWidths = new()
    {
        [0x80] = 556, // €
        [0x85] = 1000, // …
        [0x91] = 222, // '
        [0x92] = 222, // '
        [0x93] = 333, // "
        [0x94] = 333, // "
        [0x95] = 350, // •
        [0x96] = 556, // –
        [0x97] = 1000, // —
        [0x99] = 1000 // ™
    };
}
