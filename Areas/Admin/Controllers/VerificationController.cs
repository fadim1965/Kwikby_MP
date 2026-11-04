using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using kwikby_MP.Domain;
using kwikby_MP.Services;

namespace kwikby_MP.Areas.Admin.Controllers;

/// <summary>
/// Seller KYC verification queue: admins review submitted documents and approve or reject.
/// </summary>
[Area("Admin")]
public class VerificationController : AdminControllerBase
{
    private readonly IShopService _shops;
    private readonly IAuditTrail _audit;

    public VerificationController(
        ApplicationDbContext db,
        IOptions<StoreSettings> settings,
        IShopService shops,
        IAuditTrail audit)
        : base(db, settings)
    {
        _shops = shops;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        VerificationStatus? status = null,
        int page = 1,
        CancellationToken ct = default)
    {
        ViewData["Title"] = "Seller Verification";

        var vm = new AdminVerificationListViewModel
        {
            Shops = await _shops.GetVerificationQueueAsync(status, ct),
            Counts = await _shops.GetVerificationCountsAsync(ct),
            Status = status
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id,
        VerificationStatus? filter = null,
        int page = 1,
        CancellationToken ct = default)
    {
        var result = await _shops.ApproveVerificationAsync(id, ct);
        Flash(result.Message, result.Success);

        if (result.Success && result.Shop is { } approved)
        {
            await _audit.RecordAsync(CurrentUserName, "shop.verification_approved", "Shop", id,
                $"Approved KYC verification for {approved.Name}");
        }

        return RedirectToAction(nameof(Index), new { status = filter, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        int id,
        string reason,
        VerificationStatus? filter = null,
        int page = 1,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Flash("A rejection reason is required.", false);
            return RedirectToAction(nameof(Index), new { status = filter, page });
        }

        var result = await _shops.RejectVerificationAsync(id, reason, ct);
        Flash(result.Message, result.Success);

        if (result.Success && result.Shop is { } rejected)
        {
            await _audit.RecordAsync(CurrentUserName, "shop.verification_rejected", "Shop", id,
                $"Rejected KYC verification for {rejected.Name}: {reason}");
        }

        return RedirectToAction(nameof(Index), new { status = filter, page });
    }
}

/// <summary>View model for the admin verification queue.</summary>
public sealed class AdminVerificationListViewModel
{
    public required IReadOnlyList<Shop> Shops { get; init; }
    public required IReadOnlyDictionary<VerificationStatus, int> Counts { get; init; }
    public VerificationStatus? Status { get; init; }

    public int Total => Shops.Count;
}