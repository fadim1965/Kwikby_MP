using kwikby_MP.Api.Infrastructure;

namespace kwikby_MP.Api.Repositories;

public interface IAuthRepository
{
    Task<AuthUser?> GetUserForLoginAsync(string email, CancellationToken cancellationToken);

    Task<AuthClaims?> GetClaimsAsync(string userId, CancellationToken cancellationToken);

    Task RecordLoginAsync(string userId, CancellationToken cancellationToken);

    Task RecordFailedLoginAsync(string email, CancellationToken cancellationToken);
}

public sealed class AuthUser
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int AccessFailedCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<int> ShopIds { get; set; } = Array.Empty<int>();

    public bool IsLockedOut => LockoutEndUtc is { } end && end > DateTime.UtcNow;
}

public sealed class AuthClaims
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<int> ShopIds { get; set; } = Array.Empty<int>();
}

public sealed class AuthRepository : IAuthRepository
{
    private readonly IStoredProcedureGateway _db;

    public AuthRepository(IStoredProcedureGateway db) => _db = db;

    public async Task<AuthUser?> GetUserForLoginAsync(string email, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_Auth_GetUserForLogin", new { Email = email }, cancellationToken)
            .ConfigureAwait(false);

        var row = await result.ReadSingleOrDefaultAsync<AuthUserRow>().ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var roles = await result.ReadAsync<RoleRow>().ConfigureAwait(false);
        var shops = await result.ReadAsync<ShopRoleRow>().ConfigureAwait(false);

        return new AuthUser
        {
            Id = row.Id,
            Email = row.Email,
            DisplayName = row.DisplayName,
            PasswordHash = row.PasswordHash,
            IsActive = row.IsActive,
            AccessFailedCount = row.AccessFailedCount,
            LockoutEndUtc = row.LockoutEndUtc,
            LastLoginUtc = row.LastLoginUtc,
            Roles = roles.Select(r => r.RoleName).ToArray(),
            ShopIds = shops.Select(s => s.ShopId).Distinct().ToArray()
        };
    }

    public async Task<AuthClaims?> GetClaimsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var result = await _db
            .QueryMultipleAsync("usp_Auth_GetUserClaims", new { UserId = userId }, cancellationToken)
            .ConfigureAwait(false);

        var row = await result.ReadSingleOrDefaultAsync<AuthClaimsRow>().ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var roles = await result.ReadAsync<RoleRow>().ConfigureAwait(false);
        var shops = await result.ReadAsync<ShopRoleRow>().ConfigureAwait(false);

        return new AuthClaims
        {
            UserId = row.Id,
            Email = row.Email,
            DisplayName = row.DisplayName,
            IsActive = row.IsActive,
            Roles = roles.Select(r => r.RoleName).ToArray(),
            ShopIds = shops.Select(s => s.ShopId).Distinct().ToArray()
        };
    }

    public Task RecordLoginAsync(string userId, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Auth_RecordLogin", new { UserId = userId }, cancellationToken);

    public Task RecordFailedLoginAsync(string email, CancellationToken cancellationToken) =>
        _db.ExecuteAsync("usp_Auth_RecordFailedLogin", new { Email = email }, cancellationToken);

    private sealed class AuthUserRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }

    private sealed class AuthClaimsRow
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private sealed class RoleRow
    {
        public string RoleName { get; set; } = string.Empty;
    }

    private sealed class ShopRoleRow
    {
        public int ShopId { get; set; }
        public int Role { get; set; }
    }
}
