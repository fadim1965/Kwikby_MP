using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using kwikby_MP.Areas.Vendor.Models;
using kwikby_MP.Services;

namespace kwikby_MP.Areas.Vendor.Controllers;

/// <summary>
/// A vendor's own settlements, read-only. Vendors see exactly what the marketplace owes them and the
/// payout that cleared it, but they cannot create a payout — that is the marketplace's action.
/// </summary>
[Area("Vendor")]
[Authorize(Policy = "VendorOnly")]
public class PayoutsController : Controller
{
    private readonly IShopService _shops;
    private readonly IPayoutService _payouts;
    private readonly ApplicationDbContext _db;

    public PayoutsController(IShopService shops, IPayoutService payouts, ApplicationDbContext db = null!)
    {
        _shops = shops;
        _payouts = payouts;
        _db = db ?? new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
    }

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

        // Payouts, the ledger and the bank destination are financial data. Fulfilment staff operate
        // the packing bench, not the shop's money.
        var access = await _shops.ResolveAccessAsync(userId, shop.Id, ct);
        if (access?.CanViewFinancials() != true)
        {
            return Forbid();
        }

        ViewData["Title"] = "Payouts";
        ViewData["Shop"] = shop;
        ViewData["CanEditBank"] = access.CanManagePayoutDetails();

        return View(new VendorPayoutViewModel
        {
            Shop = shop,
            Outstanding = await _payouts.GetOutstandingAsync(shop.Id, ct),
            Payouts = await _payouts.GetPayoutsAsync(shop.Id, 50, ct),
            Ledger = await _payouts.GetLedgerAsync(shop.Id, 25, ct)
        });
    }

    /// <summary>
    /// Updates the shop's payout destination. Registrations collect it too; this keeps an approved
    /// shop's bank details theirs to maintain rather than a support ticket.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBank(
        string? bankAccountName, string? bankName, string? bankIban, CancellationToken ct)
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

        // Redirecting a shop's settlements is the owner's decision alone.
        var access = await _shops.ResolveAccessAsync(userId, shop.Id, ct);
        if (access?.CanManagePayoutDetails() != true)
        {
            return Forbid();
        }

        var result = await _shops.UpdateSettingsAsync(shop.Id, s =>
        {
            s.BankAccountName = Truncate(bankAccountName, 120);
            s.BankName = Truncate(bankName, 120);
            s.BankIban = Truncate(bankIban, 40);
        }, ct);

        TempData["Message"] = result.Success
            ? "Payout details saved."
            : result.Message;
        TempData["MessageType"] = result.Success ? "success" : "danger";

        return RedirectToAction(nameof(Index));
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
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

        var access = await _shops.ResolveAccessAsync(userId, shop.Id, ct);
        if (access?.CanViewFinancials() != true)
        {
            return Forbid();
        }

        var payout = await _payouts.GetPayoutAsync(id, ct);

        // Payout ids are sequential and guessable, so the shop check is the authorisation — a vendor
        // asking for another vendor's payout must get nothing, not a redacted something.
        if (payout is null || payout.ShopId != shop.Id)
        {
            return NotFound();
        }

        ViewData["Title"] = $"Payout {payout.Reference}";
        ViewData["Shop"] = shop;

        return View(new VendorPayoutDetailViewModel
        {
            Shop = shop,
            Payout = payout,
            Lines = await _payouts.GetStatementLinesAsync(id, ct)
        });
    }

    /// <summary>
    /// Shows which COD orders have been included in a reconciled remittance.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RemittanceStatus(CancellationToken ct)
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

        var access = await _shops.ResolveAccessAsync(userId, shop.Id, ct);
        if (access?.CanViewFinancials() != true)
        {
            return Forbid();
        }

        // Collect all COD orders for this shop.
        var codOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.ShopId == shop.Id && o.PaymentMethod == PaymentMethod.CashOnDelivery)
            .OrderByDescending(o => o.PlacedUtc)
            .ToListAsync(ct);

        // Build a set of order IDs that appear in any reconciled remittance line.
        var remittedIds = await _db.CarrierRemittanceLines.AsNoTracking()
            .Where(l => l.Order.ShopId == shop.Id
                     && l.Status == RemittanceLineStatus.Matched
                     && l.OrderId.HasValue)
            .Select(l => l.OrderId!.Value)
            .ToListAsync(ct);

        // Also include lines with RemittanceLineStatus.Over (overpaid = effectively remitted).
        var overIds = await _db.CarrierRemittanceLines.AsNoTracking()
            .Where(l => l.Order.ShopId == shop.Id
                     && l.Status == RemittanceLineStatus.Over
                     && l.OrderId.HasValue)
            .Select(l => l.OrderId!.Value)
            .ToListAsync(ct);

        var allRemitted = remittedIds.Union(overIds).ToHashSet();

        ViewData["Title"] = "COD remittance status";
        ViewData["Shop"] = shop;

        return View(new VendorRemittanceStatusViewModel
        {
            Shop = shop,
            Orders = codOrders.Select(o => new VendorRemittanceOrderRow
            {
                OrderNumber = o.OrderNumber,
                PlacedUtc = o.PlacedUtc,
                CodAmount = o.CodAmount,
                Remitted = allRemitted.Contains(o.Id),
                RemittanceRef = remittedIds.Contains(o.Id)
                    ? "Remitted in " + _db.CarrierRemittances.AsNoTracking()
                        .Where(r => r.Lines.Any(l => l.OrderId == o.Id && l.Status == RemittanceLineStatus.Matched))
                        .Select(r => r.Carrier + "/" + r.Reference)
                        .FirstOrDefault() ?? "—"
                    : "—"
            }).ToList()
        });
    }

    /// <summary>
    /// Model for the remittance status view.
    /// </summary>
    public sealed class VendorRemittanceStatusViewModel
    {
        public Shop Shop { get; set; } = null!;

        public IReadOnlyList<VendorRemittanceOrderRow> Orders { get; set; } = null!;
    }

    /// <summary>
    /// A single order row in the remittance status view.
    /// </model>
    public sealed class VendorRemittanceOrderRow
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime PlacedUtc { get; set; }
        public decimal CodAmount { get; set; }
        public bool Remitted { get; set; }
        public string RemittanceRef { get; set; } = "—";
    }
}