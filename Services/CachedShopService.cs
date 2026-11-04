using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kwikby_MP.Domain;

namespace kwikby_MP.Services;

/// <summary>
/// Decorator implementation that overlays a high-performance in-memory caching boundary 
/// around the core ShopService to protect database resources from lookup resource exhaustion.
/// </summary>
public sealed class CachedShopService : IShopService
{
    private readonly IShopService _inner;
    private readonly IMemoryCache _cache;

    // Explicit constants to isolate tenant lookup caching keys
    private const string SlugCacheKeyPrefix = "tenant_slug_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public CachedShopService(IShopService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    /// <summary>
    /// Intercepts public slug queries, serving raw items straight out of memory when warmed.
    /// </summary>
    public async Task<Shop?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var cacheKey = $"{SlugCacheKeyPrefix}{slug.Trim().ToLowerInvariant()}";

        // Read directly from the memory ring boundary or fetch up from database layer
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            // Apply defensive evaluation lifespan limits to protect against memory leak scenarios
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.SlidingExpiration = TimeSpan.FromMinutes(3);

            return await _inner.GetBySlugAsync(slug, ct);
        });
    }

    // --- Direct pass-through implementations ensuring contract coverage ---
    public Task<Shop?> GetByIdAsync(int shopId, CancellationToken ct = default) => _inner.GetByIdAsync(shopId, ct);
    public Task<Shop?> GetPrimaryShopForUserAsync(string userId, CancellationToken ct = default) => _inner.GetPrimaryShopForUserAsync(userId, ct);
    public Task<ShopAccess?> ResolveAccessAsync(string userId, int shopId, CancellationToken ct = default) => _inner.ResolveAccessAsync(userId, shopId, ct);
    public Task<ShopDashboard> GetDashboardAsync(int shopId, CancellationToken ct = default) => _inner.GetDashboardAsync(shopId, ct);
    public Task<IReadOnlyList<Shop>> GetTradingShopsAsync(string? search = null, CancellationToken ct = default) => _inner.GetTradingShopsAsync(search, ct);
    public Task<IReadOnlyList<Shop>> GetAllAsync(ShopStatus? status = null, CancellationToken ct = default) => _inner.GetAllAsync(status, ct);
    public Task<IReadOnlyList<Shop>> GetShopsForUserAsync(string userId, CancellationToken ct = default) => _inner.GetShopsForUserAsync(userId, ct);
    public Task<ShopResult> RegisterAsync(string ownerUserId, ShopRegistration registration, CancellationToken ct = default) => _inner.RegisterAsync(ownerUserId, registration, ct);
    public Task<ShopResult> ReapplyAsync(string ownerUserId, ShopRegistration registration, CancellationToken ct = default) => _inner.ReapplyAsync(ownerUserId, registration, ct);
    public Task<ShopResult> UpdateSettingsAsync(int shopId, Action<Shop> apply, CancellationToken ct = default) => _inner.UpdateSettingsAsync(shopId, apply, ct);
    public Task<ShopResult> ApproveAsync(int shopId, decimal? commissionRate, CancellationToken ct = default) => _inner.ApproveAsync(shopId, commissionRate, ct);
    public Task<ShopResult> RejectAsync(int shopId, string reason, CancellationToken ct = default) => _inner.RejectAsync(shopId, reason, ct);
    public Task<ShopResult> SuspendAsync(int shopId, string reason, CancellationToken ct = default) => _inner.SuspendAsync(shopId, reason, ct);
    public Task<ShopResult> ReinstateAsync(int shopId, CancellationToken ct = default) => _inner.ReinstateAsync(shopId, ct);
    public Task<ShopResult> AddUserAsync(int shopId, string email, ShopRole role, CancellationToken ct = default) => _inner.AddUserAsync(shopId, email, role, ct);
    public Task<ShopResult> RemoveUserAsync(int shopId, string userId, CancellationToken ct = default) => _inner.RemoveUserAsync(shopId, userId, ct);

    // KYC / Seller Verification (write-through, no caching)
    public Task<ShopResult> SubmitVerificationAsync(int shopId, ShopRegistration registration, CancellationToken ct = default) => _inner.SubmitVerificationAsync(shopId, registration, ct);
    public Task<ShopResult> ApproveVerificationAsync(int shopId, CancellationToken ct = default) => _inner.ApproveVerificationAsync(shopId, ct);
    public Task<ShopResult> RejectVerificationAsync(int shopId, string reason, CancellationToken ct = default) => _inner.RejectVerificationAsync(shopId, reason, ct);
    public Task<IReadOnlyList<Shop>> GetVerificationQueueAsync(VerificationStatus? status = null, CancellationToken ct = default) => _inner.GetVerificationQueueAsync(status, ct);
    public Task<IReadOnlyDictionary<VerificationStatus, int>> GetVerificationCountsAsync(CancellationToken ct = default) => _inner.GetVerificationCountsAsync(ct);

    /// <summary>
    /// Evicts the modified cache record before executing flush cascades to ensure data fresh alignment.
    /// </summary>
    public async Task UpdateShopAsync(Shop shop, CancellationToken ct = default)
    {
        await _inner.UpdateShopAsync(shop, ct);

        // Explicit cache purge operation based on mutated entity properties
        if (!string.IsNullOrWhiteSpace(shop.Slug))
        {
            var cacheKey = $"{SlugCacheKeyPrefix}{shop.Slug.Trim().ToLowerInvariant()}";
            _cache.Remove(cacheKey);
        }
    }
}
