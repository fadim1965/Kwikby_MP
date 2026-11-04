using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using kwikby_MP.Services;
using Xunit;

namespace kwikby_MP.Tests;

/// <summary>
/// Every controller in the Vendor and Admin areas must carry an authorization policy. The policies
/// themselves are coarse (VendorOnly only requires a signed-in user); per-shop access is checked in
/// each action through IShopService.ResolveAccessAsync. This test cannot see that, but it does stop
/// a new area controller from being added with no [Authorize] at all.
/// </summary>
public class AreaAuthorizationTests
{
    private static IEnumerable<Type> AreaControllers(string area) =>
        typeof(ShopAccessPolicy).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Controller).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttributes<AreaAttribute>(inherit: true).Any(a => a.RouteValue == area));

    [Fact]
    public void Every_vendor_controller_requires_the_VendorOnly_policy()
    {
        var controllers = AreaControllers("Vendor").ToList();
        Assert.NotEmpty(controllers);

        var missing = controllers
            .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(a => a.Policy == "VendorOnly"))
            .Select(t => t.Name)
            .ToList();

        Assert.True(missing.Count == 0, "Vendor controllers without [Authorize(Policy = \"VendorOnly\")]: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_admin_controller_requires_the_AdminOnly_policy()
    {
        var controllers = AreaControllers("Admin").ToList();
        Assert.NotEmpty(controllers);

        var missing = controllers
            .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(a => a.Policy == "AdminOnly"))
            .Select(t => t.Name)
            .ToList();

        Assert.True(missing.Count == 0, "Admin controllers without [Authorize(Policy = \"AdminOnly\")]: " + string.Join(", ", missing));
    }
}
