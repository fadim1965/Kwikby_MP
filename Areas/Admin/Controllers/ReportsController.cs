using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebDS_Test.Areas.Admin.Models;
using WebDS_Test.Data;
using WebDS_Test.Domain;
using WebDS_Test.Services;
using WebDS_Test.Services.Reports;

namespace WebDS_Test.Areas.Admin.Controllers;

/// <summary>
/// The marketplace owner's reports. The catalogue is served by <see cref="IReportingService"/>;
/// this controller only applies the filter, picks the output — an HTML preview or the PDF the
/// filter form's download button asks for — and hands the document over.
/// </summary>
public class ReportsController : AdminControllerBase
{
    private readonly IReportingService _reports;

    public ReportsController(ApplicationDbContext db, IOptions<StoreSettings> settings, IReportingService reports)
        : base(db, settings)
    {
        _reports = reports;
    }

    /// <summary>The catalogue: every admin report with a one-line description.</summary>
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        return View(new AdminReportsIndexViewModel { Reports = _reports.AdminReports });
    }

    /// <summary>
    /// One report. <c>from</c>/<c>to</c> are inclusive days, <c>groupBy</c> is one of the
    /// descriptor's options, and <c>format=pdf</c> streams the rendered PDF instead of previewing
    /// the same document on screen.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Report(
        string? key, DateTime? from, DateTime? to, string? groupBy, string? format, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var descriptor = _reports.AdminReports.FirstOrDefault(r => r.Key == key);
        if (descriptor is null)
        {
            return NotFound();
        }

        var filter = new ReportFilter { From = from, To = to, GroupBy = groupBy };
        if (filter.IsEmptyWindow)
        {
            Flash("The end date is before the start date, so no report was produced.", success: false);
            return RedirectToAction(nameof(Report), new { key, from = from?.ToString("yyyy-MM-dd") });
        }

        var document = await _reports.BuildAdminReportAsync(key, filter, ct);
        if (document is null)
        {
            return NotFound();
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = ReportPdf.Render(document, Settings);
            var name = $"report-{key}-{filter.FromStart:yyyyMMdd}-{filter.ToExclusive.AddDays(-1):yyyyMMdd}.pdf";
            return File(bytes, "application/pdf", name);
        }

        ViewData["Title"] = descriptor.Name;
        return View(new AdminReportPageViewModel
        {
            Descriptor = descriptor,
            Filter = filter,
            Document = document
        });
    }
}
