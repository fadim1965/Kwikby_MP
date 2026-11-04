using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using kwikby_MP.Data;
using kwikby_MP.Services;
using kwikby_MP.Services.Email;
using kwikby_MP.Services.Images;
using kwikby_MP.Services.Imports;
using kwikby_MP.Services.Localization;
using kwikby_MP.Services.Logging;
using kwikby_MP.Services.Shipping;
using kwikby_MP.Services.Shopify;

namespace kwikby_MP;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<StoreSettings>(configuration.GetSection(StoreSettings.SectionName));
        services.Configure<BrandingOptions>(configuration.GetSection(BrandingOptions.SectionName));

        var sqliteConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/store.db";
        var sqlServerConnectionString = configuration.GetConnectionString("MultiVendorsStoresConn");
        var useSqlServer = configuration["Database:Provider"] is { } provider
                           && provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                           && !string.IsNullOrWhiteSpace(sqlServerConnectionString);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (useSqlServer)
            {
                options.UseSqlServer(sqlServerConnectionString!, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }
            else
            {
                options.UseSqlite(sqliteConnectionString, sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }
            options.EnableSensitiveDataLogging(environment.IsDevelopment());
        });

        services.AddHttpContextAccessor();
        services.AddDistributedMemoryCache();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISlugService, SlugService>();
        services.AddSingleton<IThemeCatalog, ThemeCatalog>();
        services.AddSingleton<ITaxCalculator, TaxCalculator>();
        services.AddSingleton<ITranslationStore, TranslationStore>();
        services.AddSingleton<ISiteSettingStore, SiteSettingStore>();

        var paymentProvider = configuration["Payments:Provider"] ?? "sandbox";
        if (paymentProvider.Equals("simulator", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SimulatorPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<SimulatorPaymentGateway>());
            services.AddSingleton<IHostedPaymentGateway>(sp => sp.GetRequiredService<SimulatorPaymentGateway>());
        }
        else
        {
            services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
        }

        services.AddSingleton<IShippingCarrier, SimulatorShippingCarrier>();
        services.AddSingleton<ICarrierRegistry, CarrierRegistry>();

        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IInventoryService, InventoryService>();

        services.AddScoped<ShopService>();
        services.AddScoped<IShopService>(sp =>
        {
            var inner = sp.GetRequiredService<ShopService>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            return new CachedShopService(inner, cache);
        });

        services.AddScoped<IFulfilmentService, FulfilmentService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IPostSaleService, PostSaleService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IProductImportService, ProductImportService>();
        services.AddScoped<IVendorLedgerService, VendorLedgerService>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<ICodRemittanceService, CodRemittanceService>();
        services.AddScoped<IListingModerationService, ListingModerationService>();
        services.AddScoped<IReportingService, ReportingService>();

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton<IEmailTransport>(sp =>
        {
            if (emailOptions.Transport.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            {
                return new SmtpEmailTransport(
                    sp.GetRequiredService<IOptions<EmailOptions>>(),
                    sp.GetRequiredService<ILogger<SmtpEmailTransport>>());
            }

            return new FileEmailTransport(
                sp.GetRequiredService<IOptions<EmailOptions>>(),
                sp.GetRequiredService<IHostEnvironment>(),
                sp.GetRequiredService<ILogger<FileEmailTransport>>());
        });
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        var shopifyEnabled = configuration.GetValue<bool>("Shopify:Enabled");
        if (shopifyEnabled)
        {
            services.AddScoped<ShopifyCatalogService>();
            services.AddScoped<ICatalogService>(sp => sp.GetRequiredService<ShopifyCatalogService>());
        }
        else
        {
            services.AddScoped<ICatalogService, CatalogService>();
        }

        services.AddScoped<IPaymentWebhookHandler, PaymentWebhookHandler>();
        services.AddSingleton<IImageStore, LocalImageStore>();

        return services;
    }
}
