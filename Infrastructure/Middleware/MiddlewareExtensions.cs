using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using WebDS_Test.Services;

namespace WebDS_Test.Infrastructure.Middleware;

/// <summary>
/// Pipeline interceptor that resolves the current tenant vendor storefront context early 
/// in the request lifecycle and injects the corresponding theme palette tokens into Context.Items.
/// </summary>
public sealed class TenantThemeMiddleware
{
    private readonly RequestDelegate _next;

    public TenantThemeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IShopService shopService, IThemeCatalog themeCatalog)
    {
        var pathSegments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);

        string? shopSlug = null;
        if (pathSegments != null && pathSegments.Length >= 2 && string.Equals(pathSegments[0], "shop", StringComparison.OrdinalIgnoreCase))
        {
            shopSlug = pathSegments[1];
        }

        if (!string.IsNullOrEmpty(shopSlug))
        {
            var ct = context.RequestAborted;
            var activeShop = await shopService.GetBySlugAsync(shopSlug, ct);

            if (activeShop != null)
            {
                var resolvedTheme = themeCatalog.Get(activeShop.ThemeKey);
                context.Items["Tenant_Shop_Entity"] = activeShop;
                context.Items["Tenant_Shop_Theme"] = resolvedTheme;
            }
        }

        await _next(context);
    }
}

public static class MiddlewareExtensions
{
    /// <summary>
    /// Registers the kwikby multi-vendor dynamic layout mapping middleware engine into the pipeline.
    /// </summary>
    public static IApplicationBuilder UseTenantThemes(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantThemeMiddleware>();
    }
}
