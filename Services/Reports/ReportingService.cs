using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using kwikby_MP.Data;
using kwikby_MP.Domain;
using kwikby_MP.Services.Reports;

namespace kwikby_MP.Services;

/// <summary>
/// Assembles the marketplace's management reports from the same tables the shop runs on —
/// orders, the vendor ledger, payouts, returns, stock movements and reviews — so a report can
/// always be reconciled back to the records that produced it. Every query is windowed by the
/// filter's <c>[FromStart, ToExclusive)</c> pair, and vendor reports are scoped by shop id, the
/// same tenant boundary the rest of the vendor area uses.
/// </summary>
public sealed class ReportingService : IReportingService
{
    private readonly ApplicationDbContext _db;
    private readonly StoreSettings _store;

    public ReportingService(ApplicationDbContext db, IOptions<StoreSettings> settings)
    {
        _db = db;
        _store = settings.Value;
    }

    // ------------------------------------------------------------------
    // Catalogue
    // ------------------------------------------------------------------

    private static readonly IReadOnlyList<ReportOption> SalesGroupings = new ReportOption[]
    {
        new("total", "Grand total"),
        new("day", "By day"),
        new("week", "By week"),
        new("month", "By month"),
        new("status", "By order status"),
        new("method", "By payment method")
    };

    private static readonly IReadOnlyList<ReportOption> AdminSalesGroupings =
        SalesGroupings.Append(new ReportOption("shop", "By shop")).ToList();

    public IReadOnlyList<ReportDescriptor> AdminReports { get; } = new ReportDescriptor[]
    {
        new() { Key = "sales", Name = "Sales", Description = "Orders, discounts, tax, shipping and refunds over a period, grouped the way you need it.", GroupBy = AdminSalesGroupings },
        new() { Key = "products", Name = "Sales by product", Description = "Which items sold, in what quantity, and what they earned." },
        new() { Key = "categories", Name = "Sales by category", Description = "Turnover split across the product taxonomy." },
        new() { Key = "shops", Name = "Shops overview", Description = "Every shop with its trading volume, commission and outstanding balance." },
        new() { Key = "orders", Name = "Order register", Description = "Line-by-line register of the orders placed in the period." },
        new() { Key = "customers", Name = "Customers", Description = "New accounts in the period, and the shoppers who spent the most." },
        new() { Key = "returns", Name = "Returns and refunds", Description = "Return requests raised in the period, their status and the money refunded." },
        new() { Key = "payouts", Name = "Payouts", Description = "Settlements issued in the period, including failed and reversed ones." },
        new() { Key = "commission", Name = "Commission income", Description = "Marketplace earnings from the vendor ledger, by shop and by entry type.", GroupBy = new ReportOption[] { new("total", "Grand total"), new("month", "By month"), new("shop", "By shop") } },
        new() { Key = "ledger", Name = "Vendor ledger", Description = "Every ledger movement in the period: sales, reversals, adjustments and payouts.", GroupBy = new ReportOption[] { new("total", "Grand total"), new("shop", "By shop"), new("type", "By entry type") } },
        new() { Key = "inventory", Name = "Inventory on hand", Description = "Current stock per SKU, with low-stock and out-of-stock flags." },
        new() { Key = "stock-movements", Name = "Stock movements", Description = "The inventory ledger for the period: sales, returns, restocks and adjustments.", GroupBy = new ReportOption[] { new("total", "Grand total"), new("reason", "By reason") } },
        new() { Key = "coupons", Name = "Coupons", Description = "Every discount code, its redemptions in the period, and the discount granted." },
        new() { Key = "tax", Name = "Tax collected", Description = "Tax charged on orders in the period, monthly or in total.", GroupBy = new ReportOption[] { new("total", "Grand total"), new("month", "By month") } },
        new() { Key = "reviews", Name = "Reviews", Description = "Rating distribution, moderation backlog and the seller reputation per shop." }
    };

    public IReadOnlyList<ReportDescriptor> VendorReports { get; } = new ReportDescriptor[]
    {
        new() { Key = "sales", Name = "My sales", Description = "Your orders, discounts, tax and refunds over a period, grouped the way you need it.", GroupBy = SalesGroupings },
        new() { Key = "products", Name = "Sales by product", Description = "Which of your items sold, in what quantity, and what they earned." },
        new() { Key = "orders", Name = "Order register", Description = "Line-by-line register of your orders placed in the period." },
        new() { Key = "returns", Name = "Returns and refunds", Description = "Return requests on your orders in the period, and the money refunded." },
        new() { Key = "payouts", Name = "My payouts", Description = "Your settlements in the period, including failed and reversed ones." },
        new() { Key = "statement", Name = "Earnings statement", Description = "Your ledger entries for the period with the balance the marketplace owes you." },
        new() { Key = "inventory", Name = "Inventory on hand", Description = "Your current stock per SKU, with low-stock and out-of-stock flags." },
        new() { Key = "stock-movements", Name = "Stock movements", Description = "Your inventory ledger for the period: sales, returns, restocks and adjustments.", GroupBy = new ReportOption[] { new("total", "Grand total"), new("reason", "By reason") } },
        new() { Key = "tax", Name = "Tax on my orders", Description = "Tax charged on your orders in the period, monthly or in total.", GroupBy = new ReportOption[] { new("total", "Grand total"), new("month", "By month") } },
        new() { Key = "reviews", Name = "My reviews", Description = "Ratings your products received in the period, and your reply coverage." }
    };

    // ------------------------------------------------------------------
    // Entry points
    // ------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<ReportDocument?> BuildAdminReportAsync(string key, ReportFilter filter, CancellationToken ct = default) =>
        await BuildAsync(null, key, filter, ct);

    /// <inheritdoc />
    public async Task<ReportDocument?> BuildVendorReportAsync(int shopId, string key, ReportFilter filter, CancellationToken ct = default) =>
        await BuildAsync(shopId, key, filter, ct);

    private async Task<ReportDocument?> BuildAsync(int? shopId, string key, ReportFilter filter, CancellationToken ct)
    {
        var from = filter.FromStart;
        var to = filter.ToExclusive;
        var groupBy = filter.GroupBy ?? "total";

        switch (key)
        {
            case "sales":
                return await SalesAsync(shopId, from, to, groupBy, filter.PeriodLabel, ct);
            case "products":
                return await ProductsAsync(shopId, from, to, filter.PeriodLabel, ct);
            case "categories" when shopId is null:
                return await CategoriesAsync(from, to, filter.PeriodLabel, ct);
            case "shops" when shopId is null:
                return await ShopsAsync(from, to, filter.PeriodLabel, ct);
            case "orders":
                return await OrdersAsync(shopId, from, to, filter.PeriodLabel, ct);
            case "customers" when shopId is null:
                return await CustomersAsync(from, to, filter.PeriodLabel, ct);
            case "returns":
                return await ReturnsAsync(shopId, from, to, filter.PeriodLabel, ct);
            case "payouts":
                return await PayoutsAsync(shopId, from, to, filter.PeriodLabel, ct);
            case "commission" when shopId is null:
                return await CommissionAsync(from, to, groupBy, filter.PeriodLabel, ct);
            case "ledger":
            case "statement":
                return await LedgerAsync(shopId, from, to, groupBy, filter.PeriodLabel, vendorFacing: key == "statement", ct);
            case "inventory":
                return await InventoryAsync(shopId, ct);
            case "stock-movements":
                return await StockMovementsAsync(shopId, from, to, groupBy, filter.PeriodLabel, ct);
            case "coupons" when shopId is null:
                return await CouponsAsync(from, to, filter.PeriodLabel, ct);
            case "tax":
                return await TaxAsync(shopId, from, to, groupBy, filter.PeriodLabel, ct);
            case "reviews":
                return await ReviewsAsync(shopId, from, to, filter.PeriodLabel, ct);
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private string Money(decimal value) => _store.CurrencySymbol + value.ToString("N2", CultureInfo.InvariantCulture);

    private string Percent(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.CashOnDelivery => "Cash on delivery",
        PaymentMethod.Card => "Card",
        PaymentMethod.CounterPayment => "Counter payment",
        _ => method.ToString()
    };

    private static string MovementReasonLabel(StockMovementReason reason) => reason switch
    {
        StockMovementReason.InitialStock => "Initial stock",
        StockMovementReason.Restock => "Restock",
        StockMovementReason.Sale => "Sale",
        StockMovementReason.Return => "Return",
        StockMovementReason.Adjustment => "Adjustment",
        StockMovementReason.Cancellation => "Cancellation",
        _ => reason.ToString()
    };

    private static string LedgerTypeLabel(VendorLedgerEntryType type) => type switch
    {
        VendorLedgerEntryType.Sale => "Sale",
        VendorLedgerEntryType.Reversal => "Reversal",
        VendorLedgerEntryType.Adjustment => "Adjustment",
        VendorLedgerEntryType.Payout => "Payout",
        _ => type.ToString()
    };

    private static string Share(decimal part, decimal whole) =>
        whole == 0m ? "—" : (part * 100m / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static bool[] Right(params bool[] flags) => flags;

    private static ReportSection Section(string title, string[] headers, bool[] right,
        IEnumerable<object?[]> rows, object?[]? totals = null) =>
        new()
        {
            Title = title,
            Headers = headers,
            RightAligned = right,
            Rows = rows.ToList(),
            Totals = totals
        };

    /// <summary>Headline scope: "All shops" for the marketplace, the shop's name for a vendor.</summary>
    private async Task<string> ScopeLabelAsync(int? shopId, CancellationToken ct)
    {
        if (shopId is null)
        {
            return "All shops";
        }

        var name = await _db.Shops.AsNoTracking()
            .Where(s => s.Id == shopId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
        return name ?? $"Shop #{shopId}";
    }

    /// <summary>
    /// Turns the <c>CreatedBy</c> stamp of a stock movement into something readable. Movements made
    /// by a shopper's purchase or cancellation carry the buyer's user id, so those are resolved to a
    /// display name; anything else (a staff actor, <c>guest</c>, a missing account) is shown as
    /// recorded rather than guessed at.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ActorNamesAsync(
        IEnumerable<string> actors, CancellationToken ct)
    {
        // User ids are 32-character hex GUIDs; names and role labels are both plain text, so the
        // shape of the value decides whether a lookup is even attempted.
        var ids = actors
            .Where(a => a.Length == 32 && a.All(Uri.IsHexDigit))
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var users = await _db.Users.AsNoTracking()
            .Where(u => EF.Constant(ids).Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        return users.ToDictionary(u => u.Id, u => u.DisplayName);
    }

    // ------------------------------------------------------------------
    // Sales
    // ------------------------------------------------------------------

    private async Task<ReportDocument> SalesAsync(int? shopId, DateTime from, DateTime to, string groupBy,
        string period, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Shop)
            .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(o => o.ShopId == shopId.Value);
        }

        var orders = await query.ToListAsync(ct);
        var trading = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        var groups = new List<(string Sort, string Label, List<Order>? Orders)>();
        void AddTo(string sort, string label, Order order)
        {
            var g = groups.FirstOrDefault(x => x.Sort == sort);
            if (g.Orders is null)
            {
                groups.Add((sort, label, new List<Order> { order }));
            }
            else
            {
                g.Orders.Add(order);
            }
        }

        foreach (var order in trading)
        {
            switch (groupBy)
            {
                case "day":
                    AddTo(order.PlacedUtc.ToString("yyyy-MM-dd"), order.PlacedUtc.ToString("dd MMM yyyy"), order);
                    break;
                case "week":
                    var monday = order.PlacedUtc.Date.AddDays(-(((int)order.PlacedUtc.DayOfWeek + 6) % 7));
                    AddTo(monday.ToString("yyyy-MM-dd"), $"Week of {monday:dd MMM yyyy}", order);
                    break;
                case "month":
                    AddTo(order.PlacedUtc.ToString("yyyy-MM"), order.PlacedUtc.ToString("MMM yyyy"), order);
                    break;
                case "status":
                    AddTo(order.Status.ToDisplay(), order.Status.ToDisplay(), order);
                    break;
                case "method":
                    AddTo(MethodLabel(order.PaymentMethod), MethodLabel(order.PaymentMethod), order);
                    break;
                case "shop" when shopId is null:
                    var shopName = order.Shop?.Name ?? $"Shop #{order.ShopId}";
                    AddTo(shopName, shopName, order);
                    break;
                default:
                    AddTo("all", "All orders", order);
                    break;
            }
        }

        groups.Sort((a, b) => string.CompareOrdinal(a.Sort, b.Sort));

        var rows = groups.Select(g =>
        {
            var members = g.Orders!;
            var gross = members.Sum(o => o.Subtotal);
            var charged = members.Sum(o => o.GrandTotal);
            var refunds = members.Sum(o => o.RefundedTotal);
            return new object?[]
            {
                g.Label,
                members.Count,
                members.Sum(o => o.Lines.Sum(l => l.Quantity)),
                gross,
                members.Sum(o => o.DiscountTotal),
                members.Sum(o => o.ShippingTotal),
                members.Sum(o => o.TaxTotal),
                charged,
                refunds,
                charged - refunds
            };
        }).ToList();

        object?[]? totals = rows.Count > 1
            ? new object?[]
            {
                "Total",
                trading.Count,
                trading.Sum(o => o.Lines.Sum(l => l.Quantity)),
                trading.Sum(o => o.Subtotal),
                trading.Sum(o => o.DiscountTotal),
                trading.Sum(o => o.ShippingTotal),
                trading.Sum(o => o.TaxTotal),
                trading.Sum(o => o.GrandTotal),
                trading.Sum(o => o.RefundedTotal),
                trading.Sum(o => o.GrandTotal - o.RefundedTotal)
            }
            : null;

        var cancelled = orders.Where(o => o.Status == OrderStatus.Cancelled).ToList();

        return new ReportDocument
        {
            Title = "Sales report",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Orders", trading.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Gross sales", Money(trading.Sum(o => o.Subtotal))),
                new ReportFigure("Refunds", Money(trading.Sum(o => o.RefundedTotal))),
                new ReportFigure("Net sales", Money(trading.Sum(o => o.GrandTotal - o.RefundedTotal))),
                new ReportFigure("Cancelled orders", cancelled.Count.ToString("N0", CultureInfo.InvariantCulture))
            },
            Sections = new[]
            {
                Section("Sales", new[]
                    { "Period", "Orders", "Units", "Gross", "Discounts", "Shipping", "Tax", "Charged", "Refunds", "Net" },
                    Right(false, true, true, true, true, true, true, true, true, true),
                    rows, totals)
            },
            Notes = new[]
            {
                "Trading orders are orders that were not cancelled; pending orders are included because they have been placed.",
                "Net = grand total charged to shoppers, minus refunds issued against those orders.",
                "Cancelled orders are excluded from every column and counted in the headline figure instead."
            }
        };
    }

    // ------------------------------------------------------------------
    // Sales by product / by category
    // ------------------------------------------------------------------

    private async Task<ReportDocument> ProductsAsync(int? shopId, DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to && o.Status != OrderStatus.Cancelled);
        if (shopId.HasValue)
        {
            query = query.Where(o => o.ShopId == shopId.Value);
        }

        var orders = await query.ToListAsync(ct);
        var lines = orders.SelectMany(o => o.Lines.Select(l => (Order: o, Line: l))).ToList();

        var rows = lines
            .GroupBy(x => x.Line.ProductId is int pid
                ? "p" + pid.ToString(CultureInfo.InvariantCulture)
                : "n" + x.Line.ProductName + "|" + x.Line.VariantName)
            .Select(g =>
            {
                var first = g.First().Line;
                var label = string.IsNullOrWhiteSpace(first.VariantName)
                    ? first.ProductName
                    : $"{first.ProductName} — {first.VariantName}";
                return (Label: label, Sku: first.Sku, Units: g.Sum(x => x.Line.Quantity),
                    Orders: g.Select(x => x.Order.Id).Distinct().Count(),
                    Gross: g.Sum(x => x.Line.LineTotal));
            })
            .OrderByDescending(x => x.Gross)
            .ToList();

        var grandGross = rows.Sum(x => x.Gross);

        return new ReportDocument
        {
            Title = "Sales by product",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Products sold", rows.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Units", rows.Sum(x => x.Units).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Gross", Money(grandGross))
            },
            Sections = new[]
            {
                Section("Sales by product",
                    new[] { "Product", "SKU", "Units", "Orders", "Gross", "Share" },
                    Right(false, false, true, true, true, true),
                    rows.Select(x => new object?[] { x.Label, x.Sku, x.Units, x.Orders, x.Gross, Share(x.Gross, grandGross) }),
                    new object?[] { "Total", null, rows.Sum(x => x.Units), rows.Sum(x => x.Orders), grandGross, "100%" })
            },
            Notes = new[]
            {
                "Figures come from the order lines snapshotted at purchase time, so later catalogue edits never rewrite this report.",
                "Cancelled orders are excluded."
            }
        };
    }

    private async Task<ReportDocument> CategoriesAsync(DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var lines = await _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to && o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Lines)
            .ToListAsync(ct);

        // Array + EF.Constant so the predicate is a literal IN (…), not OPENJSON — SQL Server
        // 2014 has no OPENJSON (see CatalogService.BrowseAsync).
        var pids = lines.Where(l => l.ProductId != null).Select(l => l.ProductId!.Value).Distinct().ToArray();
        var products = await _db.Products.AsNoTracking()
            .Where(p => EF.Constant(pids).Contains(p.Id))
            .Select(p => new { p.Id, p.CategoryId })
            .ToListAsync(ct);
        var categoryById = await _db.Categories.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var lookup = products.ToDictionary(p => p.Id, p => p.CategoryId);

        var rows = lines
            .GroupBy(l => l.ProductId is int pid && lookup.TryGetValue(pid, out var cid) && categoryById.TryGetValue(cid, out var cname)
                ? cname
                : "Uncategorised")
            .Select(g => (Category: g.Key, Units: g.Sum(l => l.Quantity), Gross: g.Sum(l => l.LineTotal)))
            .OrderByDescending(x => x.Gross)
            .ToList();
        var grandGross = rows.Sum(x => x.Gross);

        return new ReportDocument
        {
            Title = "Sales by category",
            Scope = "All shops",
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Categories", rows.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Units", rows.Sum(x => x.Units).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Gross", Money(grandGross))
            },
            Sections = new[]
            {
                Section("Sales by category",
                    new[] { "Category", "Units", "Gross", "Share" },
                    Right(false, true, true, true),
                    rows.Select(x => new object?[] { x.Category, x.Units, x.Gross, Share(x.Gross, grandGross) }),
                    new object?[] { "Total", rows.Sum(x => x.Units), grandGross, "100%" })
            },
            Notes = new[] { "Products without a category are listed as Uncategorised. Cancelled orders are excluded." }
        };
    }

    // ------------------------------------------------------------------
    // Shops overview
    // ------------------------------------------------------------------

    private async Task<ReportDocument> ShopsAsync(DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var shops = await _db.Shops.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);

        var productCounts = (await _db.Products.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .GroupBy(p => p.ShopId)
                .Select(g => new { ShopId = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.ShopId, x => x.Count);

        var orderStats = (await _db.Orders.AsNoTracking()
                .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to && o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.ShopId)
                .Select(g => new { ShopId = g.Key, Count = g.Count(), Gross = g.Sum(o => o.GrandTotal), Refunds = g.Sum(o => o.RefundedTotal) })
                .ToListAsync(ct))
            .ToDictionary(x => x.ShopId, x => x);

        var commission = (await _db.VendorLedgerEntries.AsNoTracking()
                .Where(l => l.CreatedUtc >= from && l.CreatedUtc < to && l.Type != VendorLedgerEntryType.Payout)
                .GroupBy(l => l.ShopId)
                .Select(g => new { ShopId = g.Key, Amount = g.Sum(l => l.CommissionAmount) })
                .ToListAsync(ct))
            .ToDictionary(x => x.ShopId, x => x.Amount);

        var outstanding = (await _db.VendorLedgerEntries.AsNoTracking()
                .Where(l => l.SettledUtc == null)
                .GroupBy(l => l.ShopId)
                .Select(g => new { ShopId = g.Key, Amount = g.Sum(l => l.NetAmount) })
                .ToListAsync(ct))
            .ToDictionary(x => x.ShopId, x => x.Amount);

        var rows = shops.Select(s =>
        {
            orderStats.TryGetValue(s.Id, out var stats);
            return new object?[]
            {
                s.Name,
                s.Status.ToDisplay(),
                Percent(s.CommissionRate * 100m),
                productCounts.GetValueOrDefault(s.Id),
                stats?.Count ?? 0,
                stats?.Gross ?? 0m,
                stats?.Refunds ?? 0m,
                commission.GetValueOrDefault(s.Id),
                outstanding.GetValueOrDefault(s.Id)
            };
        }).ToList();

        return new ReportDocument
        {
            Title = "Shops overview",
            Scope = "All shops",
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Shops", shops.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Trading shops", shops.Count(s => s.Status == ShopStatus.Approved).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("GMV in period", Money(orderStats.Values.Sum(x => x.Gross))),
                new ReportFigure("Commission in period", Money(commission.Values.Sum()))
            },
            Sections = new[]
            {
                Section("Shops",
                    new[] { "Shop", "Status", "Commission", "Products", "Orders", "GMV", "Refunds", "Commission earned", "Outstanding" },
                    Right(false, false, false, true, true, true, true, true, true),
                    rows,
                    new object?[]
                    {
                        "Total", null, null,
                        productCounts.Values.Sum(),
                        orderStats.Values.Sum(x => x.Count),
                        orderStats.Values.Sum(x => x.Gross),
                        orderStats.Values.Sum(x => x.Refunds),
                        commission.Values.Sum(),
                        outstanding.Values.Sum()
                    })
            },
            Notes = new[]
            {
                "GMV is the grand total of trading orders placed in the period for that shop.",
                "Commission earned covers ledger entries raised in the period; reversals appear as negative amounts.",
                "Outstanding is what the marketplace still owes the shop right now — the sum of its unsettled ledger entries."
            }
        };
    }

    // ------------------------------------------------------------------
    // Order register
    // ------------------------------------------------------------------

    private async Task<ReportDocument> OrdersAsync(int? shopId, DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking()
            .Include(o => o.Shop)
            .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(o => o.ShopId == shopId.Value);
        }

        var orders = await query.OrderByDescending(o => o.PlacedUtc).ToListAsync(ct);
        var trading = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        var rows = orders.Select(o => new object?[]
        {
            o.OrderNumber,
            o.PlacedUtc.ToString("dd MMM yyyy HH:mm"),
            o.CustomerName,
            o.Status.ToDisplay(),
            MethodLabel(o.PaymentMethod),
            o.TotalQuantity,
            o.Subtotal,
            o.DiscountTotal,
            o.ShippingTotal,
            o.TaxTotal,
            o.GrandTotal,
            o.RefundedTotal
        }).ToList();

        return new ReportDocument
        {
            Title = "Order register",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Orders", orders.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Charged", Money(trading.Sum(o => o.GrandTotal))),
                new ReportFigure("Refunded", Money(trading.Sum(o => o.RefundedTotal)))
            },
            Sections = new[]
            {
                Section("Orders placed",
                    new[] { "Order", "Placed (UTC)", "Customer", "Status", "Method", "Items", "Subtotal", "Discount", "Shipping", "Tax", "Grand", "Refunded" },
                    Right(false, false, false, false, false, true, true, true, true, true, true, true),
                    rows,
                    new object?[]
                    {
                        "Total", null, null, null, null,
                        orders.Sum(o => o.TotalQuantity),
                        orders.Sum(o => o.Subtotal),
                        orders.Sum(o => o.DiscountTotal),
                        orders.Sum(o => o.ShippingTotal),
                        orders.Sum(o => o.TaxTotal),
                        orders.Sum(o => o.GrandTotal),
                        orders.Sum(o => o.RefundedTotal)
                    })
            },
            Notes = new[]
            {
                "Cancelled orders are included in the register with their status, and their money counts towards the totals row.",
                "All timestamps are UTC, the store's single clock."
            }
        };
    }

    // ------------------------------------------------------------------
    // Customers
    // ------------------------------------------------------------------

    private async Task<ReportDocument> CustomersAsync(DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var newUsers = await _db.Users.AsNoTracking()
            .Where(u => u.CreatedUtc >= from && u.CreatedUtc < to)
            .OrderBy(u => u.CreatedUtc)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Email,
                u.CreatedUtc,
                Orders = u.Orders.Count,
                Spend = u.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.GrandTotal)
            })
            .ToListAsync(ct);

        var windowOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to && o.Status != OrderStatus.Cancelled && o.UserId != null)
            .Select(o => new { UserId = o.UserId!, o.GrandTotal, o.RefundedTotal })
            .ToListAsync(ct);

        var userIds = windowOrders.Select(o => o.UserId).Distinct().ToArray();
        var usersById = await _db.Users.AsNoTracking()
            .Where(u => EF.Constant(userIds).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.Email }, ct);

        var top = windowOrders
            .GroupBy(o => o.UserId)
            .Select(g =>
            {
                usersById.TryGetValue(g.Key, out var user);
                return (Name: user?.DisplayName ?? "(deleted account)",
                    Email: user?.Email ?? "—",
                    Orders: g.Count(),
                    Spend: g.Sum(o => o.GrandTotal),
                    Refunds: g.Sum(o => o.RefundedTotal));
            })
            .OrderByDescending(x => x.Spend)
            .ToList();

        return new ReportDocument
        {
            Title = "Customers",
            Scope = "All shops",
            Period = period,
            Figures = new[]
            {
                new ReportFigure("New accounts", newUsers.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Shoppers with orders", top.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Spend in period", Money(top.Sum(x => x.Spend)))
            },
            Sections = new[]
            {
                Section("New accounts in the period",
                    new[] { "Registered (UTC)", "Name", "Email", "Orders (all time)", "Lifetime spend" },
                    Right(false, false, false, true, true),
                    newUsers.Select(u => new object?[] { u.CreatedUtc.ToString("dd MMM yyyy"), u.DisplayName, u.Email, u.Orders, u.Spend })),
                Section("Top shoppers by spend in the period",
                    new[] { "Name", "Email", "Orders", "Spend", "Refunds" },
                    Right(false, false, true, true, true),
                    top.Select(x => new object?[] { x.Name, x.Email, x.Orders, x.Spend, x.Refunds }),
                    new object?[] { "Total", null, top.Sum(x => x.Orders), top.Sum(x => x.Spend), top.Sum(x => x.Refunds) })
            },
            Notes = new[]
            {
                "Guest checkouts have no account and are not attributed to a shopper in the ranking.",
                "Lifetime spend counts trading orders (paid, shipped, delivered and pending) across all time."
            }
        };
    }

    // ------------------------------------------------------------------
    // Returns
    // ------------------------------------------------------------------

    private async Task<ReportDocument> ReturnsAsync(int? shopId, DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var query = _db.ReturnRequests.AsNoTracking()
            .Include(r => r.Order)
            .Include(r => r.Shop)
            .Include(r => r.Lines)
            .Where(r => r.RequestedUtc >= from && r.RequestedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(r => r.ShopId == shopId.Value);
        }

        var returns = await query.OrderBy(r => r.RequestedUtc).ToListAsync(ct);

        return new ReportDocument
        {
            Title = "Returns and refunds",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Requests", returns.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Awaiting action", returns.Count(r => r.IsOpen).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Refunded", returns.Count(r => r.Status == ReturnStatus.Refunded).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Refunded amount", Money(returns.Sum(r => r.RefundAmount)))
            },
            Sections = new[]
            {
                Section("Return requests",
                    shopId is null
                        ? new[] { "Reference", "Requested (UTC)", "Order", "Shop", "Status", "Units", "Refund", "Handled by" }
                        : new[] { "Reference", "Requested (UTC)", "Order", "Status", "Units", "Refund", "Handled by" },
                    shopId is null
                        ? Right(false, false, false, false, false, true, true, false)
                        : Right(false, false, false, false, true, true, false),
                    returns.Select(r => shopId is null
                        ? new object?[] { r.Reference, r.RequestedUtc.ToString("dd MMM yyyy"), r.Order?.OrderNumber ?? "—", r.Shop?.Name ?? "—", r.Status.ToDisplay(), r.Lines.Sum(l => l.Quantity), r.RefundAmount, r.HandledBy ?? "—" }
                        : new object?[] { r.Reference, r.RequestedUtc.ToString("dd MMM yyyy"), r.Order?.OrderNumber ?? "—", r.Status.ToDisplay(), r.Lines.Sum(l => l.Quantity), r.RefundAmount, r.HandledBy ?? "—" }),
                    shopId is null
                        ? new object?[] { "Total", null, null, null, null, returns.Sum(r => r.Lines.Sum(l => l.Quantity)), returns.Sum(r => r.RefundAmount), null }
                        : new object?[] { "Total", null, null, null, returns.Sum(r => r.Lines.Sum(l => l.Quantity)), returns.Sum(r => r.RefundAmount), null })
            },
            Notes = new[]
            {
                "Requests raised in the period are counted, whatever their current status; the refund column shows money actually refunded.",
                "Rejected and cancelled requests carry a zero refund."
            }
        };
    }

    // ------------------------------------------------------------------
    // Payouts
    // ------------------------------------------------------------------

    private async Task<ReportDocument> PayoutsAsync(int? shopId, DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var query = _db.VendorPayouts.AsNoTracking()
            .Include(p => p.Shop)
            .Where(p => p.CreatedUtc >= from && p.CreatedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(p => p.ShopId == shopId.Value);
        }

        var payouts = await query.OrderByDescending(p => p.CreatedUtc).ToListAsync(ct);

        return new ReportDocument
        {
            Title = shopId is null ? "Payouts" : "My payouts",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Settlements", payouts.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Paid (net)", Money(payouts.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.NetAmount))),
                new ReportFigure("Failed", payouts.Count(p => p.Status == PayoutStatus.Failed).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Reversed", payouts.Count(p => p.Status == PayoutStatus.Reversed).ToString("N0", CultureInfo.InvariantCulture))
            },
            Sections = new[]
            {
                Section("Settlements issued",
                    shopId is null
                        ? new[] { "Reference", "Created (UTC)", "Shop", "Period", "Entries", "Gross", "Commission", "Net", "Status", "Handled by" }
                        : new[] { "Reference", "Created (UTC)", "Period", "Entries", "Gross", "Commission", "Net", "Status", "Handled by" },
                    shopId is null
                        ? Right(false, false, false, false, true, true, true, true, false, false)
                        : Right(false, false, false, true, true, true, true, false, false),
                    payouts.Select(p => shopId is null
                        ? new object?[]
                        {
                            p.Reference, p.CreatedUtc.ToString("dd MMM yyyy"), p.Shop?.Name ?? "—",
                            $"{p.PeriodStartUtc:dd MMM} – {p.PeriodEndUtc:dd MMM yyyy}",
                            p.EntryCount, p.GrossAmount, p.CommissionAmount, p.NetAmount,
                            p.Status.ToDisplay(), p.HandledBy ?? p.CreatedBy ?? "—"
                        }
                        : new object?[]
                        {
                            p.Reference, p.CreatedUtc.ToString("dd MMM yyyy"),
                            $"{p.PeriodStartUtc:dd MMM} – {p.PeriodEndUtc:dd MMM yyyy}",
                            p.EntryCount, p.GrossAmount, p.CommissionAmount, p.NetAmount,
                            p.Status.ToDisplay(), p.HandledBy ?? p.CreatedBy ?? "—"
                        }),
                    shopId is null
                        ? new object?[] { "Total", null, null, null, payouts.Sum(p => p.EntryCount), payouts.Sum(p => p.GrossAmount), payouts.Sum(p => p.CommissionAmount), payouts.Sum(p => p.NetAmount), null, null }
                        : new object?[] { "Total", null, null, payouts.Sum(p => p.EntryCount), payouts.Sum(p => p.GrossAmount), payouts.Sum(p => p.CommissionAmount), payouts.Sum(p => p.NetAmount), null, null })
            },
            Notes = new[]
            {
                "A failed or reversed settlement keeps its row and its totals; the ledger entries it covered return to unsettled.",
                "Commission is withheld from the vendor's earnings before the net amount is paid."
            }
        };
    }

    // ------------------------------------------------------------------
    // Commission income and the vendor ledger
    // ------------------------------------------------------------------

    private async Task<ReportDocument> CommissionAsync(DateTime from, DateTime to, string groupBy, string period, CancellationToken ct)
    {
        var entries = await _db.VendorLedgerEntries.AsNoTracking()
            .Include(l => l.Shop)
            .Where(l => l.CreatedUtc >= from && l.CreatedUtc < to && l.Type != VendorLedgerEntryType.Payout)
            .ToListAsync(ct);

        IEnumerable<object?[]> byShop;
        if (groupBy == "shop")
        {
            byShop = entries.GroupBy(l => l.Shop?.Name ?? $"Shop #{l.ShopId}")
                .OrderBy(g => g.Key)
                .Select(g => new object?[]
                {
                    g.Key, g.Count(), g.Sum(l => l.GrossAmount), g.Sum(l => l.CommissionAmount), g.Sum(l => l.NetAmount),
                    g.Sum(l => l.GrossAmount) == 0m ? "—" : Percent(g.Sum(l => l.CommissionAmount) * 100m / g.Sum(l => l.GrossAmount))
                });
        }
        else if (groupBy == "month")
        {
            byShop = entries.GroupBy(l => l.CreatedUtc.ToString("MMM yyyy"))
                .OrderBy(g => g.Key)
                .Select(g => new object?[]
                {
                    g.Key, g.Count(), g.Sum(l => l.GrossAmount), g.Sum(l => l.CommissionAmount), g.Sum(l => l.NetAmount),
                    g.Sum(l => l.GrossAmount) == 0m ? "—" : Percent(g.Sum(l => l.CommissionAmount) * 100m / g.Sum(l => l.GrossAmount))
                });
        }
        else
        {
            byShop = new[]
            {
                new object?[]
                {
                    "All shops", entries.Count, entries.Sum(l => l.GrossAmount), entries.Sum(l => l.CommissionAmount),
                    entries.Sum(l => l.NetAmount),
                    entries.Sum(l => l.GrossAmount) == 0m ? "—" : Percent(entries.Sum(l => l.CommissionAmount) * 100m / entries.Sum(l => l.GrossAmount))
                }
            };
        }

        var byType = entries
            .GroupBy(l => l.Type)
            .OrderBy(g => g.Key)
            .Select(g => new object?[]
            {
                LedgerTypeLabel(g.Key), g.Count(), g.Sum(l => l.GrossAmount), g.Sum(l => l.CommissionAmount), g.Sum(l => l.NetAmount)
            })
            .ToList();

        var gross = entries.Sum(l => l.GrossAmount);
        var commission = entries.Sum(l => l.CommissionAmount);

        return new ReportDocument
        {
            Title = "Commission income",
            Scope = "All shops",
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Entries", entries.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Gross (ledger)", Money(gross)),
                new ReportFigure("Commission", Money(commission)),
                new ReportFigure("Net to shops", Money(entries.Sum(l => l.NetAmount)))
            },
            Sections = new[]
            {
                Section(groupBy == "shop" ? "By shop" : groupBy == "month" ? "By month" : "Totals",
                    new[] { "Grouping", "Entries", "Gross", "Commission", "Net", "Effective rate" },
                    Right(false, true, true, true, true, true),
                    byShop,
                    new object?[] { "Total", entries.Count, gross, commission, entries.Sum(l => l.NetAmount), Percent(gross == 0m ? 0m : commission * 100m / gross) }),
                Section("By ledger entry type",
                    new[] { "Type", "Entries", "Gross", "Commission", "Net" },
                    Right(false, true, true, true, true),
                    byType)
            },
            Notes = new[]
            {
                "Reversals of cancelled or returned sales appear with negative amounts, which is how the ledger corrects itself.",
                "Payout movements are excluded here: they move money out, they are not earnings."
            }
        };
    }

    private async Task<ReportDocument> LedgerAsync(int? shopId, DateTime from, DateTime to, string groupBy,
        string period, bool vendorFacing, CancellationToken ct)
    {
        var query = _db.VendorLedgerEntries.AsNoTracking()
            .Include(l => l.Shop)
            .Include(l => l.Order)
            .Where(l => l.CreatedUtc >= from && l.CreatedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(l => l.ShopId == shopId.Value);
        }

        var entries = await query.OrderByDescending(l => l.CreatedUtc).ToListAsync(ct);

        IEnumerable<object?[]> rows;
        if (groupBy == "shop" && shopId is null)
        {
            rows = entries.GroupBy(l => l.Shop?.Name ?? $"Shop #{l.ShopId}")
                .OrderBy(g => g.Key)
                .Select(g => new object?[]
                {
                    g.Key, g.Count(), g.Sum(l => l.GrossAmount), g.Sum(l => l.CommissionAmount), g.Sum(l => l.NetAmount)
                });
        }
        else if (groupBy == "type")
        {
            rows = entries.GroupBy(l => l.Type)
                .OrderBy(g => g.Key)
                .Select(g => new object?[]
                {
                    LedgerTypeLabel(g.Key), g.Count(), g.Sum(l => l.GrossAmount), g.Sum(l => l.CommissionAmount), g.Sum(l => l.NetAmount)
                });
        }
        else
        {
            rows = entries.Select(l => new object?[]
            {
                l.CreatedUtc.ToString("dd MMM yyyy HH:mm"),
                l.Shop?.Name ?? $"Shop #{l.ShopId}",
                LedgerTypeLabel(l.Type),
                l.Order?.OrderNumber ?? "—",
                l.GrossAmount,
                l.CommissionAmount,
                l.NetAmount,
                l.SettledUtc is null ? "No" : $"Yes · {l.PayoutReference}"
            });
        }

        var grouped = (groupBy == "shop" && shopId is null) || groupBy == "type";
        var outstandingNow = shopId is null
            ? await _db.VendorLedgerEntries.AsNoTracking().Where(l => l.SettledUtc == null).SumAsync(l => l.NetAmount, ct)
            : await _db.VendorLedgerEntries.AsNoTracking().Where(l => l.ShopId == shopId && l.SettledUtc == null).SumAsync(l => l.NetAmount, ct);

        return new ReportDocument
        {
            Title = vendorFacing ? "Earnings statement" : "Vendor ledger",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Entries", entries.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Gross", Money(entries.Sum(l => l.GrossAmount))),
                new ReportFigure("Commission", Money(entries.Sum(l => l.CommissionAmount))),
                new ReportFigure(vendorFacing ? "Outstanding balance" : "Unsettled in period",
                    vendorFacing
                        ? Money(outstandingNow)
                        : Money(entries.Where(l => l.SettledUtc == null).Sum(l => l.NetAmount)))
            },
            Sections = new[]
            {
                Section(groupBy == "shop" && shopId is null ? "By shop" : groupBy == "type" ? "By entry type" : "Ledger entries",
                    groupBy == "shop" && shopId is null
                        ? new[] { "Shop", "Entries", "Gross", "Commission", "Net" }
                        : groupBy == "type"
                            ? new[] { "Type", "Entries", "Gross", "Commission", "Net" }
                            : new[] { "Created (UTC)", "Shop", "Type", "Order", "Gross", "Commission", "Net", "Settled" },
                    groupBy == "shop" && shopId is null
                        ? Right(false, true, true, true, true)
                        : groupBy == "type"
                            ? Right(false, true, true, true, true)
                            : Right(false, false, false, false, true, true, true, false),
                    rows,
                    grouped
                        ? null
                        : new object?[]
                        {
                            "Total", null, null, null,
                            entries.Sum(l => l.GrossAmount), entries.Sum(l => l.CommissionAmount), entries.Sum(l => l.NetAmount), null
                        })
            },
            Notes = vendorFacing
                ? new[]
                {
                    "The outstanding balance is the sum of every unsettled entry on your shop, past this window included.",
                    "A 'Yes · PAY-…' settled marker links the entry back to the payout that paid it."
                }
                : new[]
                {
                    "Every entry in the window is listed: sales credit the shop, reversals correct them, adjustments are staff corrections, payouts settle.",
                    "Settlement stamps are written by the payout run; an entry is settled by exactly one payout."
                }
        };
    }

    // ------------------------------------------------------------------
    // Inventory
    // ------------------------------------------------------------------

    private async Task<ReportDocument> InventoryAsync(int? shopId, CancellationToken ct)
    {
        var query = _db.ProductVariants.AsNoTracking()
            .Include(v => v.Product)
            .AsQueryable();
        if (shopId.HasValue)
        {
            query = query.Where(v => v.Product!.ShopId == shopId.Value);
        }

        var variants = await query.ToListAsync(ct);

        string Status(ProductVariant v) =>
            v.StockQuantity <= 0 ? "Out of stock"
            : v.StockQuantity <= Math.Max(1, v.LowStockThreshold) ? "Low stock"
            : "In stock";

        var rows = variants
            .OrderBy(v => v.StockQuantity)
            .ThenBy(v => v.Product?.Name)
            .Select(v => new object?[]
            {
                v.Product?.Name ?? "—",
                v.Name,
                v.Sku,
                v.Price,
                v.StockQuantity,
                Status(v)
            })
            .ToList();

        return new ReportDocument
        {
            Title = "Inventory on hand",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = $"On hand at {DateTime.UtcNow:dd MMM yyyy}",
            Figures = new[]
            {
                new ReportFigure("SKUs", variants.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Units in stock", variants.Sum(v => v.StockQuantity).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Low stock", variants.Count(v => v.StockQuantity > 0 && v.StockQuantity <= Math.Max(1, v.LowStockThreshold)).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Out of stock", variants.Count(v => v.StockQuantity <= 0).ToString("N0", CultureInfo.InvariantCulture))
            },
            Sections = new[]
            {
                Section("Stock on hand",
                    new[] { "Product", "Variant", "SKU", "Price", "Stock", "Status" },
                    Right(false, false, false, true, true, false),
                    rows)
            },
            Notes = new[]
            {
                "A snapshot of the current moment, not a period report: stock changes with every sale and restock.",
                "Low stock follows each variant's own threshold."
            }
        };
    }

    private async Task<ReportDocument> StockMovementsAsync(int? shopId, DateTime from, DateTime to, string groupBy,
        string period, CancellationToken ct)
    {
        var query = _db.StockMovements.AsNoTracking()
            .Include(m => m.ProductVariant).ThenInclude(v => v!.Product)
            .Where(m => m.CreatedUtc >= from && m.CreatedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(m => m.ProductVariant!.Product!.ShopId == shopId.Value);
        }

        var movements = await query.OrderBy(m => m.CreatedUtc).ToListAsync(ct);

        // StockMovement keeps an OrderId but no navigation, so the order numbers the movements
        // belong to are looked up separately rather than by an Include. Array + EF.Constant so
        // the predicate is a literal IN (…), not OPENJSON. See CatalogService.BrowseAsync.
        var orderIds = movements.Where(m => m.OrderId != null).Select(m => m.OrderId!.Value).Distinct().ToArray();
        var orderNumbers = await _db.Orders.AsNoTracking()
            .Where(o => EF.Constant(orderIds).Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.OrderNumber, ct);

        // Purchases and cancellations stamp the buyer's user id into CreatedBy; show the name.
        var actors = await ActorNamesAsync(
            movements.Where(m => m.CreatedBy != null).Select(m => m.CreatedBy!), ct);

        IEnumerable<object?[]> rows;
        string sectionTitle;
        string[] headers;
        bool[] right;
        object?[]? totals;
        if (groupBy == "reason")
        {
            sectionTitle = "By reason";
            headers = new[] { "Reason", "Movements", "Net units" };
            right = Right(false, true, true);
            rows = movements.GroupBy(m => m.Reason)
                .OrderBy(g => g.Key)
                .Select(g => new object?[] { MovementReasonLabel(g.Key), g.Count(), g.Sum(m => m.QuantityDelta) });
            totals = new object?[] { "Total", movements.Count, movements.Sum(m => m.QuantityDelta) };
        }
        else
        {
            sectionTitle = "Movements";
            headers = new[] { "When (UTC)", "Product", "SKU", "Delta", "Reason", "Order", "Stock after", "By" };
            right = Right(false, false, false, true, false, false, true, false);
            rows = movements.Select(m => new object?[]
            {
                m.CreatedUtc.ToString("dd MMM yyyy HH:mm"),
                m.ProductVariant is null ? "—" : string.IsNullOrWhiteSpace(m.ProductVariant.Name)
                    ? m.ProductVariant.Product?.Name ?? "—"
                    : $"{m.ProductVariant.Product?.Name} — {m.ProductVariant.Name}",
                m.ProductVariant?.Sku ?? "—",
                m.QuantityDelta,
                MovementReasonLabel(m.Reason),
                m.OrderId is int oid
                    ? orderNumbers.TryGetValue(oid, out var onum) ? onum : $"Order #{oid}"
                    : "—",
                m.StockAfter,
                m.CreatedBy is null ? "—"
                    : actors.TryGetValue(m.CreatedBy, out var actorName) ? actorName : m.CreatedBy
            });
            totals = new object?[]
            {
                "Total", null, null, movements.Sum(m => m.QuantityDelta), null, null, null, null
            };
        }

        var materialised = rows.ToList();

        return new ReportDocument
        {
            Title = "Stock movements",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Movements", movements.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Units sold", movements.Where(m => m.Reason == StockMovementReason.Sale).Sum(m => -m.QuantityDelta).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Units returned", movements.Where(m => m.Reason == StockMovementReason.Return).Sum(m => m.QuantityDelta).ToString("N0", CultureInfo.InvariantCulture))
            },
            Sections = new[] { Section(sectionTitle, headers, right, materialised, totals) },
            Notes = new[]
            {
                "The inventory ledger is append-only: a correction is a new movement, never an edit.",
                "A negative delta reduces stock; a positive one restores it."
            }
        };
    }

    // ------------------------------------------------------------------
    // Coupons
    // ------------------------------------------------------------------

    private async Task<ReportDocument> CouponsAsync(DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var coupons = await _db.Coupons.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        var usages = await _db.CouponUsages.AsNoTracking()
            .Include(u => u.Coupon)
            .Include(u => u.Order).ThenInclude(o => o!.Shop)
            .Where(u => u.RedeemedUtc >= from && u.RedeemedUtc < to)
            .OrderBy(u => u.RedeemedUtc)
            .ToListAsync(ct);

        var byCoupon = usages.GroupBy(u => u.CouponId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Array + EF.Constant so the predicate is a literal IN (…), not OPENJSON. See CatalogService.BrowseAsync.
        var discountOrderIds = usages.Where(u => u.OrderId != null).Select(u => u.OrderId!.Value).Distinct().ToArray();
        var discountGiven = await _db.Orders.AsNoTracking()
            .Where(o => EF.Constant(discountOrderIds).Contains(o.Id))
            .SumAsync(o => o.DiscountTotal, ct);

        return new ReportDocument
        {
            Title = "Coupons",
            Scope = "All shops",
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Codes", coupons.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Active codes", coupons.Count(c => c.IsActive).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Redemptions", usages.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Discount given", Money(discountGiven))
            },
            Sections = new[]
            {
                Section("Codes",
                    new[] { "Code", "Discount", "Minimum", "Redemptions (period)", "Uses (all time)", "Active" },
                    Right(false, false, true, true, true, false),
                    coupons.Select(c => new object?[]
                    {
                        c.Code,
                        c.Type == DiscountType.Percentage ? Percent(c.Value) : Money(c.Value),
                        c.MinSubtotal == 0m ? "—" : Money(c.MinSubtotal),
                        byCoupon.GetValueOrDefault(c.Id),
                        c.UsedCount,
                        c.IsActive ? "Yes" : "No"
                    }),
                    new object?[] { "Total", null, null, usages.Count, coupons.Sum(c => c.UsedCount), null }),
                Section("Redemptions in the period",
                    new[] { "Redeemed (UTC)", "Code", "Order", "Shop", "Shopper" },
                    Right(false, false, false, false, false),
                    usages.Select(u => new object?[]
                    {
                        u.RedeemedUtc.ToString("dd MMM yyyy"),
                        u.Coupon?.Code ?? "—",
                        u.Order?.OrderNumber ?? "—",
                        u.Order?.Shop?.Name ?? "—",
                        u.User?.Email ?? "Guest"
                    }))
            },
            Notes = new[]
            {
                "Discount given is the order-level discount of the orders redeemed in the period; a multi-line order discounts every line it applied to.",
                "Redeemed codes are never deleted, so the trail above stays replayable."
            }
        };
    }

    // ------------------------------------------------------------------
    // Tax
    // ------------------------------------------------------------------

    private async Task<ReportDocument> TaxAsync(int? shopId, DateTime from, DateTime to, string groupBy, string period, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking()
            .Where(o => o.PlacedUtc >= from && o.PlacedUtc < to && o.Status != OrderStatus.Cancelled);
        if (shopId.HasValue)
        {
            query = query.Where(o => o.ShopId == shopId.Value);
        }

        var orders = await query.ToListAsync(ct);

        IEnumerable<object?[]> rows;
        if (groupBy == "month")
        {
            rows = orders.GroupBy(o => o.PlacedUtc.ToString("MMM yyyy"))
                .OrderBy(g => g.Key)
                .Select(g => TaxRow(g.Key, g.ToList()));
        }
        else
        {
            rows = new[] { TaxRow("All orders", orders) };
        }

        var materialised = rows.ToList();
        var taxable = orders.Sum(o => o.Subtotal);
        var tax = orders.Sum(o => o.TaxTotal);

        return new ReportDocument
        {
            Title = shopId is null ? "Tax collected" : "Tax on my orders",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Orders", orders.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Taxable amount", Money(taxable)),
                new ReportFigure("Tax collected", Money(tax)),
                new ReportFigure("Effective rate", Percent(taxable == 0m ? 0m : tax * 100m / taxable))
            },
            Sections = new[]
            {
                Section("Tax",
                    new[] { "Period", "Orders", "Taxable amount", "Tax collected", "Effective rate" },
                    Right(false, true, true, true, true),
                    materialised,
                    materialised.Count > 1
                        ? new object?[] { "Total", orders.Count, taxable, tax, Percent(taxable == 0m ? 0m : tax * 100m / taxable) }
                        : null)
            },
            Notes = new[]
            {
                "Tax is charged at order level from the store tax rate or the product's tax class, and snapshotted on the order.",
                "Cancelled orders are excluded; refunded orders keep the tax they were charged, with the refund reported in the sales report."
            }
        };

        static object?[] TaxRow(string label, List<Order> group)
        {
            var sub = group.Sum(o => o.Subtotal);
            var t = group.Sum(o => o.TaxTotal);
            return new object?[]
            {
                label, group.Count, sub, t,
                sub == 0m ? "—" : (t * 100m / sub).ToString("0.#", CultureInfo.InvariantCulture) + "%"
            };
        }
    }

    // ------------------------------------------------------------------
    // Reviews
    // ------------------------------------------------------------------

    private async Task<ReportDocument> ReviewsAsync(int? shopId, DateTime from, DateTime to, string period, CancellationToken ct)
    {
        var query = _db.Reviews.AsNoTracking()
            .Include(r => r.Shop)
            .Include(r => r.Product)
            .Where(r => r.CreatedUtc >= from && r.CreatedUtc < to);
        if (shopId.HasValue)
        {
            query = query.Where(r => r.ShopId == shopId.Value);
        }

        var reviews = await query.ToListAsync(ct);
        var published = reviews.Where(r => r.Status == ReviewStatus.Published).ToList();

        var distribution = Enumerable.Range(1, 5)
            .Reverse()
            .Select(stars => new object?[]
            {
                $"{stars} star{(stars == 1 ? "" : "s")}",
                published.Count(r => r.Rating == stars),
                Share(published.Count(r => r.Rating == stars), published.Count)
            })
            .ToList();

        var average = published.Count == 0 ? 0m : Math.Round(published.Average(r => (decimal)r.Rating), 2);

        List<object?[]> detail;
        string detailTitle;
        if (shopId is null)
        {
            detailTitle = "By shop";
            detail = reviews.GroupBy(r => r.Shop?.Name ?? $"Shop #{r.ShopId}")
                .OrderBy(g => g.Key)
                .Select(g => new object?[]
                {
                    g.Key,
                    g.Count(),
                    Math.Round(g.Where(r => r.Status == ReviewStatus.Published).Average(r => (decimal?)r.Rating) ?? 0m, 2),
                    g.Count(r => r.Status == ReviewStatus.Pending),
                    g.Count(r => r.HasVendorReply)
                })
                .ToList();
        }
        else
        {
            detailTitle = "Reviews in the period";
            detail = reviews.OrderByDescending(r => r.CreatedUtc)
                .Select(r => new object?[]
                {
                    r.CreatedUtc.ToString("dd MMM yyyy"),
                    r.Product?.Name ?? "—",
                    r.Rating,
                    r.Status.ToDisplay(),
                    r.IsVerifiedPurchase ? "Yes" : "No",
                    r.HasVendorReply ? "Yes" : "No"
                })
                .ToList();
        }

        return new ReportDocument
        {
            Title = shopId is null ? "Reviews" : "My reviews",
            Scope = await ScopeLabelAsync(shopId, ct),
            Period = period,
            Figures = new[]
            {
                new ReportFigure("Reviews", reviews.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("Average (published)", published.Count == 0 ? "—" : average.ToString("0.00", CultureInfo.InvariantCulture)),
                new ReportFigure("Pending moderation", reviews.Count(r => r.Status == ReviewStatus.Pending).ToString("N0", CultureInfo.InvariantCulture)),
                new ReportFigure("With seller reply", reviews.Count(r => r.HasVendorReply).ToString("N0", CultureInfo.InvariantCulture))
            },
            Sections = new[]
            {
                Section("Rating distribution (published)",
                    new[] { "Rating", "Reviews", "Share" }, Right(false, true, true), distribution),
                Section(detailTitle,
                    shopId is null
                        ? new[] { "Shop", "Reviews", "Average", "Pending", "Replied" }
                        : new[] { "Date (UTC)", "Product", "Rating", "Status", "Verified purchase", "Replied" },
                    shopId is null
                        ? Right(false, true, true, true, true)
                        : Right(false, false, true, false, false, false),
                    detail)
            },
            Notes = new[]
            {
                "The average counts published reviews only; pending and rejected ones are excluded from the rating.",
                "A verified purchase means the reviewer had bought the product."
            }
        };
    }
}
