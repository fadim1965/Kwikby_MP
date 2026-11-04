namespace kwikby_MP.Services.Reports;

/// <summary>
/// A report filter as it arrives from a filter form: an inclusive date window and an optional
/// grouping. Every query in <see cref="ReportingService"/> works against the normalised
/// <see cref="FromStart"/>/<see cref="ToExclusive"/> pair, so a report can never disagree with the
/// period printed on it.
/// </summary>
public sealed class ReportFilter
{
    /// <summary>Inclusive first day, or null for "last 30 days".</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive last day, or null for "today".</summary>
    public DateTime? To { get; init; }

    /// <summary>Grouping key from the descriptor's options, or null for the report default.</summary>
    public string? GroupBy { get; init; }

    /// <summary>Window start: 00:00 UTC of <see cref="From"/>, or thirty days back by default.</summary>
    public DateTime FromStart => (From ?? DateTime.UtcNow.Date.AddDays(-29)).Date;

    /// <summary>Window end (exclusive): 00:00 UTC of the day after <see cref="To"/>.</summary>
    public DateTime ToExclusive => (To ?? DateTime.UtcNow.Date).Date.AddDays(1);

    /// <summary>True when the window contains no time at all (end before start).</summary>
    public bool IsEmptyWindow => ToExclusive <= FromStart;

    /// <summary>Human label for the period, printed on the report and in the PDF header.</summary>
    public string PeriodLabel
    {
        get
        {
            var lastDay = ToExclusive.AddDays(-1);
            return FromStart.Date == lastDay.Date
                ? $"{FromStart:dd MMM yyyy}"
                : $"{FromStart:dd MMM yyyy} – {lastDay:dd MMM yyyy}";
        }
    }
}

/// <summary>One selectable grouping, e.g. <c>(day, "By day")</c>.</summary>
public sealed record ReportOption(string Value, string Label);

/// <summary>
/// Everything the reports index needs to describe one report, and the filter form needs to
/// render its grouping dropdown.
/// </summary>
public sealed class ReportDescriptor
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Grouping choices. Empty for reports that have a single fixed shape.</summary>
    public IReadOnlyList<ReportOption> GroupBy { get; init; } = Array.Empty<ReportOption>();
}

/// <summary>
/// One table inside a report, described as data rather than markup — the same shape
/// <c>TabularData</c> uses for CSV/Excel, plus an optional totals row and per-column
/// right-alignment so money columns line up in both the HTML preview and the PDF.
/// </summary>
public sealed class ReportSection
{
    public required string Title { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }

    /// <summary>Positionally matched to <see cref="Headers"/>; true for money/number columns.</summary>
    public required IReadOnlyList<bool> RightAligned { get; init; }

    public required IReadOnlyList<object?[]> Rows { get; init; }

    /// <summary>Optional bold summary row, positionally matched to <see cref="Headers"/>.</summary>
    public object?[]? Totals { get; init; }
}

/// <summary>One headline figure shown above the tables.</summary>
public sealed class ReportFigure(string label, string value)
{
    public string Label { get; } = label;
    public string Value { get; } = value;
}

/// <summary>A finished report, ready to be previewed on screen or rendered to PDF.</summary>
public sealed class ReportDocument
{
    public required string Title { get; init; }

    /// <summary>Whom the numbers cover: "All shops" for the marketplace, the shop name for a vendor.</summary>
    public required string Scope { get; init; }

    public required string Period { get; init; }

    public IReadOnlyList<ReportFigure> Figures { get; init; } = Array.Empty<ReportFigure>();
    public IReadOnlyList<ReportSection> Sections { get; init; } = Array.Empty<ReportSection>();
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
