using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebDS_Test.Areas.Vendor.Models;
using WebDS_Test.Domain;
using WebDS_Test.Services;
using WebDS_Test.Services.Reports;

namespace WebDS_Test.Areas.Vendor.Controllers;

/// <summary>
/// A shop's own reports. Everything is scoped to the signed-in vendor's primary shop — the same
/// tenant boundary the rest of the vendor area uses — so a report can only ever cover one shop's
/// orders, ledger, stock and reviews.
/// </summary>
[Area("Vendor")]
[Authorize(Policy = "VendorOnly")]
public class ReportsController : Controller
{
    private readonly IShopService _shops;
    private readonly IReportingService _reports;
    private readonly StoreSettings _settings;

    public ReportsController(IShopService shops, IReportingService reports, IOptions<StoreSettings> settings)
    {
        _shops = shops;
        _reports = reports;
        _settings = settings.Value;
    }

    /// <summary>The catalogue: every report this shop can run about itself.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return Challenge();
        }

        var shop = await _shops.GetPrimaryShopForUserAsync(userId, ct);
        if (shop is null)
        {
            return RedirectToAction("Register", "Onboarding", new { area = "Vendor" });
        }

        ViewData["Title"] = "Reports";
        return View(new VendorReportsIndexViewModel { Shop = shop, Reports = _reports.VendorReports });
    }

    /// <summary>
    /// One report for this shop. <c>from</c>/<c>to</c> are inclusive days, <c>groupBy</c> is one
    /// of the descriptor's options, and <c>format=pdf</c> streams the rendered PDF instead of
    /// previewing the same document on screen.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Report(
        string? key, DateTime? from, DateTime? to, string? groupBy, string? format, CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return Challenge();
        }

        var shop = await _shops.GetPrimaryShopForUserAsync(userId, ct);
        if (shop is null)
        {
            return RedirectToAction("Register", "Onboarding", new { area = "Vendor" });
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var descriptor = _reports.VendorReports.FirstOrDefault(r => r.Key == key);
        if (descriptor is null)
        {
            return NotFound();
        }

        var filter = new ReportFilter { From = from, To = to, GroupBy = groupBy };
        if (filter.IsEmptyWindow)
        {
            TempData["Message"] = "The end date is before the start date, so no report was produced.";
            TempData["MessageType"] = "danger";
            return RedirectToAction(nameof(Report), new { key, from = from?.ToString("yyyy-MM-dd") });
        }

        var document = await _reports.BuildVendorReportAsync(shop.Id, key, filter, ct);
        if (document is null)
        {
            return NotFound();
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = ReportPdf.Render(document, _settings);
            var name = $"report-{key}-{filter.FromStart:yyyyMMdd}-{filter.ToExclusive.AddDays(-1):yyyyMMdd}.pdf";
            return File(bytes, "application/pdf", name);
        }

        ViewData["Title"] = descriptor.Name;
        return View(new VendorReportPageViewModel
        {
            Shop = shop,
            Descriptor = descriptor,
            Filter = filter,
            Document = document
        });
    }
}
