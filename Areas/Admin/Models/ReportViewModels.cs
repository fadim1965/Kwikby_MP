using WebDS_Test.Services.Reports;

namespace WebDS_Test.Areas.Admin.Models;

/// <summary>The catalogue page: every report an administrator can run.</summary>
public sealed class AdminReportsIndexViewModel
{
    public required IReadOnlyList<ReportDescriptor> Reports { get; init; }
}

/// <summary>One report with its applied filter and the document it produced.</summary>
public sealed class AdminReportPageViewModel
{
    public required ReportDescriptor Descriptor { get; init; }
    public required ReportFilter Filter { get; init; }
    public required ReportDocument Document { get; init; }
}
