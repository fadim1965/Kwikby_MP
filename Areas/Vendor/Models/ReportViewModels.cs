using WebDS_Test.Domain;
using WebDS_Test.Services.Reports;

namespace WebDS_Test.Areas.Vendor.Models;

/// <summary>The catalogue page: every report a shop can run about itself.</summary>
public sealed class VendorReportsIndexViewModel
{
    public required Shop Shop { get; init; }
    public required IReadOnlyList<ReportDescriptor> Reports { get; init; }
}

/// <summary>One shop report with its applied filter and the document it produced.</summary>
public sealed class VendorReportPageViewModel
{
    public required Shop Shop { get; init; }
    public required ReportDescriptor Descriptor { get; init; }
    public required ReportFilter Filter { get; init; }
    public required ReportDocument Document { get; init; }
}
