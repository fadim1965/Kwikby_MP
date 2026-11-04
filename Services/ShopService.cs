using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using kwikby_MP.Areas.Admin.Models;
using kwikby_MP.Data;
using kwikby_MP.Domain;
using kwikby_MP.Services.Email;

namespace kwikby_MP.Services;

/// <summary>Outcome of a shop management operation.</summary>
public sealed record ShopResult(bool Success, string Message, Shop? Shop = null)
{
    public static ShopResult Ok(Shop shop, string message) => new(true, message, shop);
    public static ShopResult Ok(string message) => new(true, message);
    public static ShopResult Fail(string message) => new(false, message);
}

/// <summary>Registration input for a new vendor shop.</summary>
public sealed class ShopRegistration
{
    public string Name { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? Description { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "Lebanon";
    public string? PickupLocation { get; set; }

    // KYC fields
    public string? BusinessRegistrationNumber { get; set; }
    public string? TaxId { get; set; }
    public string? GovernmentIdPath { get; set; }
    public string? ProofOfAddressPath { get; set; }
    public string? BankStatementPath { get; set; }

    /// <summary>Where settlements are sent. Demo-grade: free text, no bank integration.</summary>
    public string? BankAccountName { get; set; }
    public string? BankName { get; set; }
    public string? BankIban { get; set; }

    public string? ThemeKey { get; set; }
    public bool AcceptTerms { get; set; }
}

/// <summary>
/// Shop lifecycle and access control. Every vendor-facing operation that touches shop data must
/// resolve access through here rather than trusting a shop id from the request.
/// </summary>
public interface IShopService
{
    Task<Shop?> GetByIdAsync(int shopId, CancellationToken ct = default);
    Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Shops visible in the marketplace directory.</summary>
    Task<IReadOnlyList<Shop>> GetTradingShopsAsync(string? search = null, CancellationToken ct = default);

    Task<IReadOnlyList<Shop>> GetAllAsync(ShopStatus? status = null, CancellationToken ct = default);

    /// <summary>Every shop the given user can operate, whatever the status.</summary>
    Task<IReadOnlyList<Shop>> GetShopsForUserAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// The single shop a vendor lands on by default. Null when they have none, which is the signal
    /// to start onboarding.
    /// </summary>
    Task<Shop?> GetPrimaryShopForUserAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the user's access to a shop. Returns null when the user has no access, so callers
    /// cannot forget the check — the absence of a value is the denial.
    /// </summary>
    Task<ShopAccess?> ResolveAccessAsync(string userId, int shopId, CancellationToken ct = default);

    Task<ShopResult> RegisterAsync(string ownerUserId, ShopRegistration registration, CancellationToken ct = default);

    /// <summary>
    /// Resubmits an existing application for review — the path a rejected vendor takes to fix
    /// their details and apply again, instead of being told they already have a shop.
    /// </summary>
    Task<ShopResult> ReapplyAsync(string ownerUserId, ShopRegistration registration, CancellationToken ct = default);
    Task<ShopResult> UpdateSettingsAsync(int shopId, Action<Shop> apply, CancellationToken ct = default);

    Task<ShopResult> ApproveAsync(int shopId, decimal? commissionRate, CancellationToken ct = default);
    Task<ShopResult> RejectAsync(int shopId, string reason, CancellationToken ct = default);
    Task<ShopResult> SuspendAsync(int shopId, string reason, CancellationToken ct = default);
    Task<ShopResult> ReinstateAsync(int shopId, CancellationToken ct = default);

    // KYC / Seller Verification
    Task<ShopResult> SubmitVerificationAsync(int shopId, ShopRegistration registration, CancellationToken ct = default);
    Task<ShopResult> ApproveVerificationAsync(int shopId, CancellationToken ct = default);
    Task<ShopResult> RejectVerificationAsync(int shopId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<Shop>> GetVerificationQueueAsync(VerificationStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<VerificationStatus, int>> GetVerificationCountsAsync(CancellationToken ct = default);

    /// <summary>Adds a colleague to a shop so staff can manage it without sharing credentials.</summary>
    Task<ShopResult> AddUserAsync(int shopId, string email, ShopRole role, CancellationToken ct = default);
    Task<ShopResult> RemoveUserAsync(int shopId, string userId, CancellationToken ct = default);

    /// <summary>Aggregate figures for the vendor dashboard.</summary>
    Task<ShopDashboard> GetDashboardAsync(int shopId, CancellationToken ct = default);

    /// <summary>
    /// Synchronizes structural layout updates directly back down to database records safely.
    /// </summary>
    Task UpdateShopAsync(Shop shop, CancellationToken ct = default);
}

/// <summary>What a user is allowed to do in a shop.</summary>
public sealed record ShopAccess(Shop Shop, ShopRole Role)
{
    public bool CanManageCatalogue => Role is ShopRole.Owner or ShopRole.Manager;
    public bool CanManageSettings => Role == ShopRole.Owner;
    public bool CanFulfilOrders => Role is ShopRole.Owner or ShopRole.Manager or ShopRole.Fulfilment;
}

/// <summary>Headline figures for a vendor's own dashboard.</summary>
public sealed class ShopDashboard
{
    public int ShopId { get; init; }
    public string ShopName { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public int ActiveListingCount { get; init; }
    public int OutOfStockCount { get; init; }
    public int LowStockCount { get; init; }
    public int TotalOrders { get; init; }
    public int AwaitingFulfilmentCount { get; init; }
    public int InTransitCount { get; init; }
    public decimal GrossSales { get; init; }
    public decimal CommissionOwed { get; init; }
    public decimal NetPayable { get; init; }
    public decimal UnsettledPayable { get; init; }
    public IReadOnlyList<Order> RecentOrders { get; init; } = Array.Empty<Order>();
    public IReadOnlyList<ShopListing> LowStockListings { get; init; } = Array.Empty<ShopListing>();

    /// <summary>Marketing figures for the vendor dashboard.</summary>
    public IReadOnlyList<DailyRevenue> DailyRevenue { get; init; } = Array.Empty<DailyRevenue>();
    public IReadOnlyList<ProductSalesRank> TopProductsByRevenue { get; init; } = Array.Empty<ProductSalesRank>();
    public IReadOnlyList<ProductSalesRank> TopProductsByUnits { get; init; } = Array.Empty<ProductSalesRank>();
    public IReadOnlyList<PaymentMethodBreakdown> PaymentMethods { get; init; } = Array.Empty<PaymentMethodBreakdown>();
    public decimal RevenueLast7Days { get; init; }
    public int OrdersLast7Days { get; init; }
}

public sealed class ShopService : IShopService
{
    private const decimal DefaultCommissionRate = 0.10m;

    private readonly ApplicationDbContext _db;
    private readonly ISlugService _slugs;
    private readonly IThemeCatalog _themes;
    private readonly IEmailService? _emails;
    private readonly ILogger<ShopService> _logger;

    public ShopService(
        ApplicationDbContext db,
        ISlugService slugs,
        IThemeCatalog themes,
        ILogger<ShopService> logger,
        IEmailService? emails = null)
    {
        _db = db;
        _slugs = slugs;
        _themes = themes;
        _emails = emails;
        _logger = logger;
    }

    /// <summary>
    /// Queues the decision email to the shop's owner. Best-effort: the status change is the real
    /// work and is already committed, so a failing email is logged, not propagated.
    /// </summary>
    private async Task NotifyOwnerAsync(Shop shop, string change, CancellationToken ct)
    {
        if (_emails is null)
        {
            return;
        }

        try
        {
            var ownerEmail = await _db.Users.AsNoTracking()
                .Where(u => u.Id == shop.OwnerUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(ownerEmail))
            {
                return;
            }

            switch (change)
            {
                case "verification submitted":
                    await _emails.NotifyVerificationSubmittedAsync(shop, ownerEmail, ct).ConfigureAwait(false);
                    break;
                case "verification approved":
                    await _emails.NotifyVerificationApprovedAsync(shop, ownerEmail, ct).ConfigureAwait(false);
                    break;
                case "verification rejected":
                    await _emails.NotifyVerificationRejectedAsync(shop, ownerEmail, shop.VerificationReason ?? "", ct).ConfigureAwait(false);
                    break;
                default:
                    await _emails.NotifyShopDecisionAsync(shop, ownerEmail, change, ct).ConfigureAwait(false);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shop {ShopId}: failed to queue the owner notification email", shop.Id);
        }
    }

    public Task<Shop?> GetByIdAsync(int shopId, CancellationToken ct = default) =>
        _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shopId, ct);

    public Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == slug, ct);

    public async Task<IReadOnlyList<Shop>> GetTradingShopsAsync(string? search = null, CancellationToken ct = default)
    {
        IQueryable<Shop> query = _db.Shops.AsNoTracking().Where(s => s.Status == ShopStatus.Approved);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{Escape(search.Trim())}%";
            query = query.Where(s =>
                EF.Functions.Like(s.Name, term, "\\") ||
                (s.Tagline != null && EF.Functions.Like(s.Tagline, term, "\\")) ||
                (s.Description != null && EF.Functions.Like(s.Description, term, "\\")));
        }

        return await query
            .OrderByDescending(s => s.IsFeatured)
            .ThenBy(s => s.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Shop>> GetAllAsync(ShopStatus? status = null, CancellationToken ct = default)
    {
        IQueryable<Shop> query = _db.Shops.AsNoTracking();

        if (status is { } s)
        {
            query = query.Where(shop => shop.Status == s);
        }

        return await query.OrderByDescending(shop => shop.CreatedUtc).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Shop>> GetShopsForUserAsync(string userId, CancellationToken ct = default) =>
        await _db.ShopUsers.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Shop!)
            .OrderBy(s => s.Name)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<Shop?> GetPrimaryShopForUserAsync(string userId, CancellationToken ct = default) =>
        await _db.ShopUsers.AsNoTracking()
            .Where(u => u.UserId == userId)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.ShopId)
            .Select(u => u.Shop)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

    public async Task<ShopAccess?> ResolveAccessAsync(string userId, int shopId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var membership = await _db.ShopUsers.AsNoTracking()
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.ShopId == shopId, ct).ConfigureAwait(false);

        return membership?.Shop is null ? null : new ShopAccess(membership.Shop, membership.Role);
    }

    public async Task<ShopResult> RegisterAsync(string ownerUserId, ShopRegistration registration, CancellationToken ct = default)
    {
        if (!registration.AcceptTerms)
        {
            return ShopResult.Fail("Please accept the marketplace terms to continue.");
        }

        if (string.IsNullOrWhiteSpace(registration.Name))
        {
            return ShopResult.Fail("Enter a shop name.");
        }

        if (await _db.Shops.AnyAsync(s => s.OwnerUserId == ownerUserId, ct).ConfigureAwait(false))
        {
            return ShopResult.Fail("You already have a shop registered on this account.");
        }

        var shop = new Shop
        {
            OwnerUserId = ownerUserId,
            CommissionRate = DefaultCommissionRate,
            Status = ShopStatus.PendingApproval,
            CreatedUtc = DateTime.UtcNow
        };

        ApplyRegistration(shop, registration);

        shop.Slug = await EnsureUniqueSlugAsync(shop.Name, ct).ConfigureAwait(false);
        shop.Users.Add(new ShopUser
        {
            UserId = ownerUserId,
            Role = ShopRole.Owner,
            AcceptedTermsUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow
        });

        _db.Shops.Add(shop);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Two submissions racing, or a slug taken between the check and the insert.
            _logger.LogWarning(ex, "Shop registration for {UserId} hit a database constraint", ownerUserId);
            _db.ChangeTracker.Clear();

            return await _db.Shops.AnyAsync(s => s.OwnerUserId == ownerUserId, ct).ConfigureAwait(false)
                ? ShopResult.Fail("You already have a shop registered on this account.")
                : ShopResult.Fail("That shop name was just taken. Please try again.");
        }

        _logger.LogInformation("Shop {ShopId} ({Name}) registered by {UserId}", shop.Id, shop.Name, ownerUserId);

        return ShopResult.Ok(shop, "Your shop has been registered and is awaiting approval.");
    }

    public async Task<ShopResult> ReapplyAsync(string ownerUserId, ShopRegistration registration, CancellationToken ct = default)
    {
        if (!registration.AcceptTerms)
        {
            return ShopResult.Fail("Please accept the marketplace terms to continue.");
        }

        if (string.IsNullOrWhiteSpace(registration.Name))
        {
            return ShopResult.Fail("Enter a shop name.");
        }

        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("No shop is registered on this account.");
        }

        if (shop.Status == ShopStatus.Suspended)
        {
            return ShopResult.Fail($"This shop is suspended: {shop.StatusReason ?? "contact support to resolve it."}");
        }

        if (shop.Status == ShopStatus.Approved)
        {
            return ShopResult.Fail("This shop is already approved — no application needs to be resubmitted.");
        }

        if (shop.Status == ShopStatus.Closed)
        {
            return ShopResult.Fail("This shop was closed. Contact support to reopen it.");
        }

        ApplyRegistration(shop, registration);
        shop.Status = ShopStatus.PendingApproval;
        shop.StatusReason = null;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Shop {ShopId} ({Name}) application resubmitted by {UserId}", shop.Id, shop.Name, ownerUserId);

        return ShopResult.Ok(shop, "Your application has been resubmitted and is awaiting approval.");
    }

    private void ApplyRegistration(Shop shop, ShopRegistration registration)
    {
        shop.Name = registration.Name.Trim();
        shop.Tagline = Trim(registration.Tagline);
        shop.Description = Trim(registration.Description);
        shop.ContactName = Trim(registration.ContactName);
        shop.Phone = Trim(registration.Phone);
        shop.Email = Trim(registration.Email);
        shop.AddressLine1 = Trim(registration.AddressLine1);
        shop.City = Trim(registration.City);
        shop.PostalCode = Trim(registration.PostalCode);
        shop.Country = Trim(registration.Country) ?? "Lebanon";
        shop.PickupLocation = Trim(registration.PickupLocation);
        shop.BusinessRegistrationNumber = Trim(registration.BusinessRegistrationNumber);
        shop.TaxId = Trim(registration.TaxId);
        shop.GovernmentIdPath = Trim(registration.GovernmentIdPath);
        shop.ProofOfAddressPath = Trim(registration.ProofOfAddressPath);
        shop.BankStatementPath = Trim(registration.BankStatementPath);
        shop.BankAccountName = Trim(registration.BankAccountName);
        shop.BankName = Trim(registration.BankName);
        shop.BankIban = Trim(registration.BankIban);
        shop.ThemeKey = _themes.Get(registration.ThemeKey).Key;
    }

    public async Task<ShopResult> UpdateSettingsAsync(int shopId, Action<Shop> apply, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        // The callback may be fed from a vendor's form. Fields that have their own workflow
        // (ownership, approval, commission) are captured first and put back afterwards, so this
        // method can never be used to approve a shop or change a commission rate.
        var ownerUserId = shop.OwnerUserId;
        var status = shop.Status;
        var statusReason = shop.StatusReason;
        var approvedUtc = shop.ApprovedUtc;
        var suspendedUtc = shop.SuspendedUtc;
        var commissionRate = shop.CommissionRate;
        var previousName = shop.Name;

        apply(shop);

        shop.OwnerUserId = ownerUserId;
        shop.Status = status;
        shop.StatusReason = statusReason;
        shop.ApprovedUtc = approvedUtc;
        shop.SuspendedUtc = suspendedUtc;
        shop.CommissionRate = commissionRate;

        if (!string.Equals(previousName, shop.Name, StringComparison.Ordinal))
        {
            // The slug is the public URL, so it only changes deliberately: a rename keeps it.
            shop.Slug = string.IsNullOrWhiteSpace(shop.Slug)
                ? await EnsureUniqueSlugAsync(shop.Name, ct).ConfigureAwait(false)
                : shop.Slug;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, "Shop settings saved.");
    }

    public async Task<ShopResult> ApproveAsync(int shopId, decimal? commissionRate, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.Status is not (ShopStatus.PendingApproval or ShopStatus.Rejected))
        {
            return ShopResult.Fail($"A shop that is {shop.Status.ToDisplay().ToLowerInvariant()} cannot be approved.");
        }

        if (commissionRate is { } rate)
        {
            if (rate is < 0m or > 0.9m)
            {
                return ShopResult.Fail("Commission must be between 0% and 90%.");
            }

            shop.CommissionRate = rate;
        }

        shop.Status = ShopStatus.Approved;
        shop.StatusReason = null;
        shop.ApprovedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Shop {ShopId} approved at {Commission:P1} commission", shop.Id, shop.CommissionRate);

        await NotifyOwnerAsync(shop, "approved", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"'{shop.Name}' is now approved and trading.");
    }

    public async Task<ShopResult> RejectAsync(int shopId, string reason, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.Status != ShopStatus.PendingApproval)
        {
            return ShopResult.Fail("Only an application awaiting approval can be rejected.");
        }

        shop.Status = ShopStatus.Rejected;
        shop.StatusReason = Trim(reason) ?? "Application rejected.";
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await NotifyOwnerAsync(shop, "rejected", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"'{shop.Name}' was rejected.");
    }

    public async Task<ShopResult> SuspendAsync(int shopId, string reason, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.Status != ShopStatus.Approved)
        {
            return ShopResult.Fail("Only a trading shop can be suspended.");
        }

        shop.Status = ShopStatus.Suspended;
        shop.StatusReason = Trim(reason) ?? "Suspended by the marketplace.";
        shop.SuspendedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogWarning("Shop {ShopId} suspended: {Reason}", shop.Id, shop.StatusReason);

        await NotifyOwnerAsync(shop, "suspended", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"'{shop.Name}' is suspended and no longer trading.");
    }

    public async Task<ShopResult> ReinstateAsync(int shopId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.Status != ShopStatus.Suspended)
        {
            return ShopResult.Fail("Only a suspended shop can be reinstated.");
        }

        shop.Status = ShopStatus.Approved;
        shop.StatusReason = null;
        shop.SuspendedUtc = null;
        shop.ApprovedUtc ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await NotifyOwnerAsync(shop, "reinstated", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"'{shop.Name}' has been reinstated.");
    }

    // ---- KYC / Seller Verification -------------------------------------------

    public async Task<ShopResult> SubmitVerificationAsync(int shopId, ShopRegistration registration, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.VerificationStatus == VerificationStatus.Approved)
        {
            return ShopResult.Fail("This shop is already verified.");
        }

        if (shop.VerificationStatus == VerificationStatus.Pending)
        {
            return ShopResult.Fail("A verification request is already under review.");
        }

        // All KYC fields are required for submission
        if (string.IsNullOrWhiteSpace(registration.BusinessRegistrationNumber))
        {
            return ShopResult.Fail("Business registration number is required.");
        }
        if (string.IsNullOrWhiteSpace(registration.TaxId))
        {
            return ShopResult.Fail("Tax ID is required.");
        }
        if (string.IsNullOrWhiteSpace(registration.GovernmentIdPath))
        {
            return ShopResult.Fail("Government ID document is required.");
        }
        if (string.IsNullOrWhiteSpace(registration.ProofOfAddressPath))
        {
            return ShopResult.Fail("Proof of address document is required.");
        }
        if (string.IsNullOrWhiteSpace(registration.BankStatementPath))
        {
            return ShopResult.Fail("Bank statement is required.");
        }

        shop.BusinessRegistrationNumber = Trim(registration.BusinessRegistrationNumber);
        shop.TaxId = Trim(registration.TaxId);
        shop.GovernmentIdPath = Trim(registration.GovernmentIdPath);
        shop.ProofOfAddressPath = Trim(registration.ProofOfAddressPath);
        shop.BankStatementPath = Trim(registration.BankStatementPath);
        shop.VerificationStatus = VerificationStatus.Pending;
        shop.VerificationReason = null;
        shop.RejectedUtc = null;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Shop {ShopId} submitted for KYC verification", shop.Id);

        await NotifyOwnerAsync(shop, "verification submitted", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, "Your verification documents have been submitted and are under review.");
    }

    public async Task<ShopResult> ApproveVerificationAsync(int shopId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.VerificationStatus != VerificationStatus.Pending)
        {
            return ShopResult.Fail($"A shop with verification status {shop.VerificationStatus.ToDisplay().ToLowerInvariant()} cannot be approved.");
        }

        shop.VerificationStatus = VerificationStatus.Approved;
        shop.VerificationReason = null;
        shop.VerifiedUtc = DateTime.UtcNow;
        shop.RejectedUtc = null;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Shop {ShopId} KYC verification approved", shop.Id);

        await NotifyOwnerAsync(shop, "verification approved", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"'{shop.Name}' is now a Verified Seller.");
    }

    public async Task<ShopResult> RejectVerificationAsync(int shopId, string reason, CancellationToken ct = default)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        if (shop.VerificationStatus != VerificationStatus.Pending)
        {
            return ShopResult.Fail($"A shop with verification status {shop.VerificationStatus.ToDisplay().ToLowerInvariant()} cannot be rejected.");
        }

        shop.VerificationStatus = VerificationStatus.Rejected;
        shop.VerificationReason = Trim(reason) ?? "Verification rejected.";
        shop.RejectedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogWarning("Shop {ShopId} KYC verification rejected: {Reason}", shop.Id, shop.VerificationReason);

        await NotifyOwnerAsync(shop, "verification rejected", ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"'{shop.Name}' verification was rejected.");
    }

    public async Task<IReadOnlyList<Shop>> GetVerificationQueueAsync(VerificationStatus? status = null, CancellationToken ct = default)
    {
        IQueryable<Shop> query = _db.Shops.AsNoTracking();

        if (status is { } s)
        {
            query = query.Where(shop => shop.VerificationStatus == s);
        }
        else
        {
            // Default: show pending and rejected (re-submissions)
            query = query.Where(shop => shop.VerificationStatus == VerificationStatus.Pending ||
                                         shop.VerificationStatus == VerificationStatus.Rejected);
        }

        return await query
            .OrderBy(s => s.VerificationStatus == VerificationStatus.Pending ? 0 : 1)
            .ThenBy(s => s.CreatedUtc)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<VerificationStatus, int>> GetVerificationCountsAsync(CancellationToken ct = default)
    {
        var counts = await _db.Shops.AsNoTracking()
            .GroupBy(s => s.VerificationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public async Task<ShopResult> AddUserAsync(int shopId, string email, ShopRole role, CancellationToken ct = default)
    {
        if (role == ShopRole.Owner)
        {
            return ShopResult.Fail("A shop has one owner. Add this person as a manager or fulfilment user.");
        }

        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false);
        if (shop is null)
        {
            return ShopResult.Fail("Shop not found.");
        }

        var normalized = email.Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct).ConfigureAwait(false);
        if (user is null)
        {
            return ShopResult.Fail("No account exists with that email address. Ask them to register first.");
        }

        if (await _db.ShopUsers.AnyAsync(u => u.ShopId == shopId && u.UserId == user.Id, ct).ConfigureAwait(false))
        {
            return ShopResult.Fail("That person already has access to this shop.");
        }

        _db.ShopUsers.Add(new ShopUser
        {
            ShopId = shopId,
            UserId = user.Id,
            Role = role,
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ShopResult.Ok(shop, $"{user.DisplayName} can now access '{shop.Name}' as {role}.");
    }

    public async Task<ShopResult> RemoveUserAsync(int shopId, string userId, CancellationToken ct = default)
    {
        var membership = await _db.ShopUsers.FirstOrDefaultAsync(u => u.ShopId == shopId && u.UserId == userId, ct).ConfigureAwait(false);
        if (membership is null)
        {
            return ShopResult.Fail("That person does not have access to this shop.");
        }

        if (membership.Role == ShopRole.Owner)
        {
            return ShopResult.Fail("The shop owner cannot be removed. Transfer ownership first.");
        }

        _db.ShopUsers.Remove(membership);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return ShopResult.Ok("Access removed.");
    }

    public async Task<ShopDashboard> GetDashboardAsync(int shopId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"Shop {shopId} not found.");

        var listings = await _db.ShopListings.AsNoTracking()
            .Include(l => l.Product)
            .Where(l => l.ShopId == shopId)
            .ToListAsync(ct).ConfigureAwait(false);

        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.ShopId == shopId)
            .OrderByDescending(o => o.PlacedUtc)
            .ToListAsync(ct).ConfigureAwait(false);

        var ledger = await _db.VendorLedgerEntries.AsNoTracking()
            .Where(l => l.ShopId == shopId)
            .ToListAsync(ct).ConfigureAwait(false);

        var collected = new[] { OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered };

        var cutoff7 = DateTime.UtcNow.AddDays(-7);
        var completedOrders = orders.Where(o => collected.Contains(o.Status)).ToList();
        var last7Orders = completedOrders.Where(o => o.PlacedUtc >= cutoff7).ToList();

        var dailyRevenue = new List<DailyRevenue>();
        for (int i = 6; i >= 0; i--)
        {
            var day = DateTime.UtcNow.Date.AddDays(-i);
            var dayOrders = completedOrders.Where(o => o.PlacedUtc.Date == day.Date).ToList();
            dailyRevenue.Add(new DailyRevenue
            {
                Date = day,
                Revenue = dayOrders.Sum(o => o.GrandTotal),
                Orders = dayOrders.Count
            });
        }

        // Product rankings come from the snapshot on each order line. The orders above are loaded
        // without their variants, so reading line.ProductVariant would always be null and the
        // rankings would silently come out empty.
        var soldLines = completedOrders
            .SelectMany(o => o.Lines
                .Where(l => l.ProductId != null)
                .Select(l => new
                {
                    OrderId = o.Id,
                    ProductId = l.ProductId!.Value,
                    l.ProductName,
                    l.Sku,
                    l.Quantity,
                    l.LineTotal
                }))
            .ToList();

        var soldProductIds = soldLines.Select(x => x.ProductId).Distinct().ToArray();
        var categoryNames = soldProductIds.Length == 0
            ? new Dictionary<int, string>()
            : await _db.Products.AsNoTracking()
                .Where(p => EF.Constant(soldProductIds).Contains(p.Id))
                .Select(p => new { p.Id, CategoryName = p.Category != null ? p.Category.Name : null })
                .ToDictionaryAsync(p => p.Id, p => p.CategoryName ?? "Uncategorised", ct)
                .ConfigureAwait(false);

        var productSales = soldLines
            .GroupBy(x => new { x.ProductId, x.ProductName, x.Sku })
            .Select(g => new ProductSalesRank
            {
                ProductId = g.Key.ProductId,
                Name = g.Key.ProductName,
                Sku = g.Key.Sku,
                Category = categoryNames.TryGetValue(g.Key.ProductId, out var category) ? category : "Uncategorised",
                Units = g.Sum(x => x.Quantity),
                Orders = g.Select(x => x.OrderId).Distinct().Count(),
                Revenue = g.Sum(x => x.LineTotal)
            })
            .ToList();

        var paymentMethods = completedOrders
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new PaymentMethodBreakdown
            {
                Method = g.Key,
                Label = g.Key.ToString(),
                Orders = g.Count(),
                RevenueRaw = g.Sum(o => o.GrandTotal)
            })
            .OrderByDescending(p => p.RevenueRaw)
            .ToList();

        return new ShopDashboard
        {
            ShopId = shop.Id,
            ShopName = shop.Name,
            ProductCount = listings.Count,
            ActiveListingCount = listings.Count(l => l.IsActive),
            OutOfStockCount = listings.Count(l => l.IsActive && l.StockQuantity <= 0),
            LowStockCount = listings.Count(l => l.IsActive && l.StockQuantity > 0 && l.StockQuantity <= l.LowStockThreshold),
            TotalOrders = orders.Count,
            AwaitingFulfilmentCount = orders.Count(o => o.Status is OrderStatus.Paid or OrderStatus.Pending),
            InTransitCount = orders.Count(o => o.Status == OrderStatus.Shipped),
            GrossSales = orders.Where(o => collected.Contains(o.Status)).Sum(o => o.Subtotal),
            CommissionOwed = ledger.Sum(l => l.CommissionAmount),
            NetPayable = ledger.Sum(l => l.NetAmount),
            UnsettledPayable = ledger.Where(l => l.SettledUtc == null).Sum(l => l.NetAmount),
            RecentOrders = orders.Take(8).ToList(),
            LowStockListings = listings
                .Where(l => l.IsActive && l.StockQuantity <= l.LowStockThreshold)
                .OrderBy(l => l.StockQuantity)
                .Take(8)
                .ToList(),
            DailyRevenue = dailyRevenue,
            TopProductsByRevenue = productSales.OrderByDescending(p => p.Revenue).ThenByDescending(p => p.Units).Take(8).ToList(),
            TopProductsByUnits = productSales.OrderByDescending(p => p.Units).ThenByDescending(p => p.Revenue).Take(8).ToList(),
            PaymentMethods = paymentMethods,
            RevenueLast7Days = last7Orders.Sum(o => o.GrandTotal),
            OrdersLast7Days = last7Orders.Count
        };
    }

    public async Task UpdateShopAsync(Shop shop, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shop);

        var tracked = await _db.Shops.FindAsync(new object[] { shop.Id }, cancellationToken: ct).ConfigureAwait(false)
                      ?? throw new InvalidOperationException($"Shop {shop.Id} not found.");

        // Ownership and the approval workflow have their own methods; this one must not move them.
        var ownerUserId = tracked.OwnerUserId;
        var status = tracked.Status;
        var statusReason = tracked.StatusReason;
        var approvedUtc = tracked.ApprovedUtc;
        var suspendedUtc = tracked.SuspendedUtc;

        _db.Entry(tracked).CurrentValues.SetValues(shop);

        tracked.OwnerUserId = ownerUserId;
        tracked.Status = status;
        tracked.StatusReason = statusReason;
        tracked.ApprovedUtc = approvedUtc;
        tracked.SuspendedUtc = suspendedUtc;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<string> EnsureUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = _slugs.Slugify(name);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = $"shop-{Guid.NewGuid():N}"[..16];
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await _db.Shops.AnyAsync(s => s.Slug == candidate, ct).ConfigureAwait(false))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Escapes LIKE wildcards so a search for "50%" matches the text, not everything.</summary>
    private static string Escape(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}