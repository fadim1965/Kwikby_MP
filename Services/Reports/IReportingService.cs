using kwikby_MP.Services.Reports;

namespace kwikby_MP.Services;

/// <summary>Builds the management reports offered to the marketplace owner and to shops.</summary>
public interface IReportingService
{
    /// <summary>Every report an administrator can run, in catalogue order.</summary>
    IReadOnlyList<ReportDescriptor> AdminReports { get; }

    /// <summary>Every report a shop can run about itself, in catalogue order.</summary>
    IReadOnlyList<ReportDescriptor> VendorReports { get; }

    /// <summary>Builds an admin report, or null when the key is unknown.</summary>
    Task<ReportDocument?> BuildAdminReportAsync(string key, ReportFilter filter, CancellationToken ct = default);

    /// <summary>Builds a report scoped to one shop, or null when the key is unknown.</summary>
    Task<ReportDocument?> BuildVendorReportAsync(int shopId, string key, ReportFilter filter, CancellationToken ct = default);
}
