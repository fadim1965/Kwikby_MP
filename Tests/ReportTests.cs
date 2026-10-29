using System.Text;
using System.Text.RegularExpressions;
using WebDS_Test.Data;
using WebDS_Test.Domain;
using WebDS_Test.Services;
using WebDS_Test.Services.Reports;
using Xunit;

namespace WebDS_Test.Tests;

/// <summary>
/// The management reports: filters decide the window, the numbers must reconcile with the
/// tables they are read from, vendor reports must never see another shop, and the hand-rolled
/// PDF writer must produce a structurally valid, 7-bit ASCII file.
/// </summary>
public class ReportTests
{
    private static readonly DateTime From = new(2026, 3, 1);
    private static readonly DateTime To = new(2026, 3, 31);

    private static ReportFilter Filter(string? groupBy = null) => new() { From = From, To = To, GroupBy = groupBy };

    private static ReportingService Service(ApplicationDbContext db) =>
        new(db, TestSupport.Options(TestSupport.Store()));

    private static Order MakeOrder(int id, DateTime placed, OrderStatus status, int shopId,
        decimal subtotal, decimal grand, decimal refunded = 0m, int quantity = 1)
    {
        var order = TestSupport.Order(id: id, number: $"ORD-2026-{id:D4}", status: status);
        order.PlacedUtc = placed;
        order.ShopId = shopId;
        order.Subtotal = subtotal;
        order.GrandTotal = grand;
        order.RefundedTotal = refunded;

        var line = order.Lines.First();
        line.ProductName = $"Product {id}";
        line.VariantName = string.Empty;
        line.Sku = $"SKU-{id}";
        line.Quantity = quantity;
        line.UnitPrice = subtotal;
        line.LineTotal = subtotal;
        return order;
    }

    /// <summary>
    /// Two shops, four orders: one trading shop-1 order, one cancelled, one from February (outside
    /// every window below), and one trading shop-2 order that was partly refunded.
    /// </summary>
    private static async Task<ApplicationDbContext> SalesDb()
    {
        var db = TestSupport.NewDb();
        db.Shops.AddRange(
            new Shop { Id = 1, Name = "Alpha Shop", Slug = "alpha", OwnerUserId = "owner-1", CommissionRate = 0.10m, Status = ShopStatus.Approved },
            new Shop { Id = 2, Name = "Beta Shop", Slug = "beta", OwnerUserId = "owner-2", CommissionRate = 0.20m, Status = ShopStatus.Approved });
        db.Orders.AddRange(
            MakeOrder(1, new DateTime(2026, 3, 5, 12, 0, 0), OrderStatus.Paid, shopId: 1, subtotal: 100m, grand: 105m),
            MakeOrder(2, new DateTime(2026, 3, 10, 9, 0, 0), OrderStatus.Cancelled, shopId: 1, subtotal: 50m, grand: 50m),
            MakeOrder(3, new DateTime(2026, 2, 20, 9, 0, 0), OrderStatus.Paid, shopId: 1, subtotal: 999m, grand: 999m),
            MakeOrder(4, new DateTime(2026, 3, 15, 18, 0, 0), OrderStatus.Paid, shopId: 2, subtotal: 200m, grand: 200m, refunded: 20m));
        await db.SaveChangesAsync();
        return db;
    }

    private static ReportDocument Document(string title, IEnumerable<object?[]> rows) => new()
    {
        Title = title,
        Scope = "All shops",
        Period = "01 Mar 2026 – 31 Mar 2026",
        Figures = new[] { new ReportFigure("Orders", "2") },
        Sections = new[]
        {
            new ReportSection
            {
                Title = "Rows",
                Headers = new[] { "Name", "Amount" },
                RightAligned = new[] { false, true },
                Rows = rows.ToList(),
                Totals = null
            }
        },
        Notes = new[] { "A note." }
    };

    // ------------------------------------------------------------------
    // The sales report
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sales_report_counts_only_trading_orders_inside_the_window()
    {
        var db = await SalesDb();
        var doc = await Service(db).BuildAdminReportAsync("sales", Filter());

        Assert.NotNull(doc);
        // The default grouping is a single "grand total" row — that row *is* the totals line.
        var row = doc!.Sections[0].Rows.Single();
        Assert.Equal(2, row[1]);      // two trading orders in March
        Assert.Equal(300m, row[3]);   // gross = 100 + 200
        Assert.Equal(305m, row[7]);   // charged = 105 + 200
        Assert.Equal(20m, row[8]);    // refunds
        Assert.Equal(285m, row[9]);   // net = charged - refunds

        // The cancelled order is reported as a headline figure, never mixed into the money.
        Assert.Equal("1", doc.Figures.Single(f => f.Label == "Cancelled orders").Value);
        Assert.Equal("300.00", doc.Figures.Single(f => f.Label == "Gross sales").Value.TrimStart('$'));
    }

    [Fact]
    public async Task Sales_report_grouped_by_day_lists_one_row_per_day()
    {
        var db = await SalesDb();
        var doc = await Service(db).BuildAdminReportAsync("sales", Filter("day"));

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("05 Mar 2026", rows[0][0]);
        Assert.Equal("15 Mar 2026", rows[1][0]);
        Assert.NotNull(doc.Sections[0].Totals);
        Assert.Equal(2, doc.Sections[0].Totals![1]);
    }

    [Fact]
    public async Task Sales_report_grouped_by_shop_separates_the_two_shops()
    {
        var db = await SalesDb();
        var doc = await Service(db).BuildAdminReportAsync("sales", Filter("shop"));

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("Alpha Shop", rows[0][0]);
        Assert.Equal(100m, rows[0][3]);
        Assert.Equal("Beta Shop", rows[1][0]);
        Assert.Equal(200m, rows[1][3]);
    }

    [Fact]
    public async Task The_sales_report_of_a_shop_never_sees_another_shop()
    {
        var db = await SalesDb();
        var doc = await Service(db).BuildVendorReportAsync(shopId: 1, "sales", Filter());

        Assert.NotNull(doc);
        Assert.Equal("Alpha Shop", doc!.Scope);
        var row = doc.Sections[0].Rows.Single();
        Assert.Equal(1, row[1]);      // only order 1
        Assert.Equal(105m, row[7]);   // order 4 belongs to shop 2
        Assert.Equal(0m, row[8]);
    }

    // ------------------------------------------------------------------
    // Line-level and register reports
    // ------------------------------------------------------------------

    [Fact]
    public async Task Products_report_aggregates_units_and_share_of_gross()
    {
        var db = await SalesDb();
        var doc = await Service(db).BuildAdminReportAsync("products", Filter());

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("Product 4", rows[0][0]);   // biggest gross first
        Assert.Equal(200m, rows[0][4]);
        Assert.Equal("66.7%", rows[0][5]);
        Assert.Equal(300m, doc.Sections[0].Totals![4]);
    }

    [Fact]
    public async Task Order_register_lists_every_order_in_the_window_including_cancelled()
    {
        var db = await SalesDb();
        var doc = await Service(db).BuildAdminReportAsync("orders", Filter());

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(3, rows.Count);                    // February order excluded
        Assert.Contains(rows, r => Equals(r[3], "Cancelled"));
        Assert.Equal(355m, doc.Sections[0].Totals![10]); // 105 + 50 + 200
    }

    // ------------------------------------------------------------------
    // Money reports: commission, returns, statement
    // ------------------------------------------------------------------

    [Fact]
    public async Task Commission_report_excludes_payouts_and_rows_outside_the_window()
    {
        var db = await SalesDb();
        db.VendorLedgerEntries.AddRange(
            new VendorLedgerEntry { Id = 1, ShopId = 1, Type = VendorLedgerEntryType.Sale, GrossAmount = 100m, CommissionAmount = 10m, NetAmount = 90m, CreatedUtc = new DateTime(2026, 3, 6) },
            new VendorLedgerEntry { Id = 2, ShopId = 2, Type = VendorLedgerEntryType.Sale, GrossAmount = 200m, CommissionAmount = 40m, NetAmount = 160m, CreatedUtc = new DateTime(2026, 3, 16) },
            new VendorLedgerEntry { Id = 3, ShopId = 1, Type = VendorLedgerEntryType.Payout, GrossAmount = 100m, CommissionAmount = 10m, NetAmount = 90m, CreatedUtc = new DateTime(2026, 3, 20) },
            new VendorLedgerEntry { Id = 4, ShopId = 1, Type = VendorLedgerEntryType.Sale, GrossAmount = 500m, CommissionAmount = 50m, NetAmount = 450m, CreatedUtc = new DateTime(2026, 1, 1) });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildAdminReportAsync("commission", Filter());

        Assert.NotNull(doc);
        var totals = doc!.Sections[0].Totals!;
        Assert.Equal(2, totals[1]);     // sale entries only
        Assert.Equal(300m, totals[2]);
        Assert.Equal(50m, totals[3]);
        Assert.Equal(250m, totals[4]);

        var byType = doc.Sections[1].Rows;
        Assert.Single(byType);
        Assert.Equal("Sale", byType[0][0]);
    }

    [Fact]
    public async Task Returns_report_totals_the_refunds_of_its_window()
    {
        var db = await SalesDb();
        db.ReturnRequests.AddRange(
            new ReturnRequest
            {
                Id = 1,
                Reference = "RMA-2026-0001",
                Email = "shopper@example.com",
                ShopId = 1,
                OrderId = 1,
                Status = ReturnStatus.Refunded,
                RequestedUtc = new DateTime(2026, 3, 7),
                RefundAmount = 20m
            },
            new ReturnRequest
            {
                Id = 2,
                Reference = "RMA-2026-0002",
                Email = "shopper@example.com",
                ShopId = 1,
                OrderId = 3,
                Status = ReturnStatus.Refunded,
                RequestedUtc = new DateTime(2026, 2, 1),
                RefundAmount = 99m
            });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildAdminReportAsync("returns", Filter());

        Assert.NotNull(doc);
        Assert.Equal("1", doc!.Figures.Single(f => f.Label == "Requests").Value);
        Assert.Equal("$20.00", doc.Figures.Single(f => f.Label == "Refunded amount").Value);
    }

    [Fact]
    public async Task The_statement_reports_the_outstanding_balance_of_the_shop()
    {
        var db = await SalesDb();
        db.VendorLedgerEntries.Add(new VendorLedgerEntry
        {
            Id = 1,
            ShopId = 1,
            Type = VendorLedgerEntryType.Sale,
            GrossAmount = 100m,
            CommissionAmount = 10m,
            NetAmount = 90m,
            CreatedUtc = new DateTime(2026, 3, 6)
        });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildVendorReportAsync(shopId: 1, "statement", Filter());

        Assert.NotNull(doc);
        Assert.Equal("$90.00", doc!.Figures.Single(f => f.Label == "Outstanding balance").Value);
        Assert.Equal("No", doc.Sections[0].Rows[0][7]);
    }

    // ------------------------------------------------------------------
    // Stock movements
    // ------------------------------------------------------------------

    [Fact]
    public async Task Stock_movements_resolve_the_order_number_and_stay_inside_the_window()
    {
        var db = TestSupport.NewDb();
        db.Shops.Add(new Shop { Id = 1, Name = "Alpha Shop", Slug = "alpha", OwnerUserId = "owner-1", Status = ShopStatus.Approved });
        db.Products.Add(new Product { Id = 1, ShopId = 1, Name = "Widget", Slug = "widget" });
        db.ProductVariants.Add(new ProductVariant { Id = 1, ProductId = 1, Sku = "W-1", Name = "Single", Price = 5m, StockQuantity = 10 });
        db.Orders.Add(MakeOrder(7, new DateTime(2026, 3, 5), OrderStatus.Paid, shopId: 1, subtotal: 10m, grand: 10m));
        var shopper = TestSupport.User(email: "dana@example.com", displayName: "Dana Whitfield");
        shopper.Id = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
        db.Users.Add(shopper);
        db.StockMovements.AddRange(
            new StockMovement { Id = 1, ProductVariantId = 1, QuantityDelta = -2, StockAfter = 8, Reason = StockMovementReason.Sale, OrderId = 7, CreatedBy = shopper.Id, CreatedUtc = new DateTime(2026, 3, 5, 12, 0, 0) },
            new StockMovement { Id = 2, ProductVariantId = 1, QuantityDelta = 1, StockAfter = 9, Reason = StockMovementReason.Return, OrderId = 7, CreatedBy = "Space Administrator", CreatedUtc = new DateTime(2026, 3, 6, 12, 0, 0) },
            new StockMovement { Id = 3, ProductVariantId = 1, QuantityDelta = 5, StockAfter = 14, Reason = StockMovementReason.Adjustment, CreatedBy = "Space Administrator", CreatedUtc = new DateTime(2026, 2, 20, 12, 0, 0) });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildAdminReportAsync("stock-movements", Filter());

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(2, rows.Count);                        // the February adjustment is outside the window
        Assert.Equal("ORD-2026-0007", rows[0][5]);          // order number resolved through OrderId
        Assert.Equal("Dana Whitfield", rows[0][7]);         // a buyer's user id is shown as their name
        Assert.Equal("Sale", rows[0][4]);
        Assert.Equal("Space Administrator", rows[1][7]);    // a non-id actor is shown as recorded
    }

    [Fact]
    public async Task Stock_movements_show_an_unresolvable_order_and_actor_without_guessing()
    {
        var db = TestSupport.NewDb();
        db.Shops.Add(new Shop { Id = 1, Name = "Alpha Shop", Slug = "alpha", OwnerUserId = "owner-1", Status = ShopStatus.Approved });
        db.Products.Add(new Product { Id = 1, ShopId = 1, Name = "Widget", Slug = "widget" });
        db.ProductVariants.Add(new ProductVariant { Id = 1, ProductId = 1, Sku = "W-1", Name = "Single", Price = 5m, StockQuantity = 10 });
        db.StockMovements.AddRange(
            // An order id with no matching order: the id is shown, not guessed away.
            new StockMovement { Id = 1, ProductVariantId = 1, QuantityDelta = -2, StockAfter = 8, Reason = StockMovementReason.Sale, OrderId = 42, CreatedBy = "00000000000000000000000000000000", CreatedUtc = new DateTime(2026, 3, 5) },
            // No order and no actor at all.
            new StockMovement { Id = 2, ProductVariantId = 1, QuantityDelta = 1, StockAfter = 9, Reason = StockMovementReason.Adjustment, CreatedUtc = new DateTime(2026, 3, 6) });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildAdminReportAsync("stock-movements", Filter());

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal("Order #42", rows[0][5]);
        Assert.Equal("00000000000000000000000000000000", rows[0][7]);   // no such account: shown as-is
        Assert.Equal("—", rows[1][5]);
        Assert.Equal("—", rows[1][7]);
    }

    [Fact]
    public async Task Stock_movements_grouped_by_reason_net_their_deltas()
    {
        var db = TestSupport.NewDb();
        db.Shops.Add(new Shop { Id = 1, Name = "Alpha Shop", Slug = "alpha", OwnerUserId = "owner-1", Status = ShopStatus.Approved });
        db.Products.Add(new Product { Id = 1, ShopId = 1, Name = "Widget", Slug = "widget" });
        db.ProductVariants.Add(new ProductVariant { Id = 1, ProductId = 1, Sku = "W-1", Name = "Single", Price = 5m, StockQuantity = 10 });
        db.StockMovements.AddRange(
            new StockMovement { Id = 1, ProductVariantId = 1, QuantityDelta = -2, StockAfter = 8, Reason = StockMovementReason.Sale, CreatedUtc = new DateTime(2026, 3, 5) },
            new StockMovement { Id = 2, ProductVariantId = 1, QuantityDelta = -1, StockAfter = 7, Reason = StockMovementReason.Sale, CreatedUtc = new DateTime(2026, 3, 6) },
            new StockMovement { Id = 3, ProductVariantId = 1, QuantityDelta = 1, StockAfter = 8, Reason = StockMovementReason.Return, CreatedUtc = new DateTime(2026, 3, 7) });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildAdminReportAsync("stock-movements", Filter("reason"));

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("Sale", rows[0][0]);
        Assert.Equal(2, rows[0][1]);      // movements
        Assert.Equal(-3, rows[0][2]);     // net units
        Assert.Equal(-2, doc.Sections[0].Totals![2]);
    }

    // ------------------------------------------------------------------
    // Inventory
    // ------------------------------------------------------------------

    [Fact]
    public async Task Inventory_report_flags_low_and_out_of_stock()
    {
        var db = TestSupport.NewDb();
        db.Shops.Add(new Shop { Id = 1, Name = "Alpha Shop", Slug = "alpha", OwnerUserId = "owner-1", Status = ShopStatus.Approved });
        db.Products.Add(new Product { Id = 1, ShopId = 1, Name = "Widget", Slug = "widget" });
        db.ProductVariants.AddRange(
            new ProductVariant { Id = 1, ProductId = 1, Sku = "W-0", Name = "Empty", Price = 5m, StockQuantity = 0, LowStockThreshold = 5 },
            new ProductVariant { Id = 2, ProductId = 1, Sku = "W-3", Name = "Low", Price = 5m, StockQuantity = 3, LowStockThreshold = 5 },
            new ProductVariant { Id = 3, ProductId = 1, Sku = "W-50", Name = "Plenty", Price = 5m, StockQuantity = 50, LowStockThreshold = 5 });
        await db.SaveChangesAsync();

        var doc = await Service(db).BuildAdminReportAsync("inventory", new ReportFilter());

        Assert.NotNull(doc);
        var rows = doc!.Sections[0].Rows;
        Assert.Equal(3, rows.Count);
        Assert.Equal("Out of stock", rows[0][5]);   // sorted by stock ascending
        Assert.Equal("Low stock", rows[1][5]);
        Assert.Equal("In stock", rows[2][5]);
        Assert.Equal("1", doc.Figures.Single(f => f.Label == "Low stock").Value);
        Assert.Equal("1", doc.Figures.Single(f => f.Label == "Out of stock").Value);
    }

    // ------------------------------------------------------------------
    // Catalogue integrity
    // ------------------------------------------------------------------

    [Fact]
    public async Task Every_catalogued_report_can_be_built()
    {
        var db = TestSupport.NewDb();
        var service = Service(db);

        Assert.Equal(service.AdminReports.Count, service.AdminReports.Select(r => r.Key).Distinct().Count());
        Assert.Equal(service.VendorReports.Count, service.VendorReports.Select(r => r.Key).Distinct().Count());

        foreach (var descriptor in service.AdminReports)
        {
            var doc = await service.BuildAdminReportAsync(descriptor.Key, Filter());
            Assert.True(doc is not null, $"admin report '{descriptor.Key}' failed to build");
        }

        foreach (var descriptor in service.VendorReports)
        {
            var doc = await service.BuildVendorReportAsync(shopId: 1, descriptor.Key, Filter());
            Assert.True(doc is not null, $"vendor report '{descriptor.Key}' failed to build");
        }
    }

    [Fact]
    public async Task An_unknown_key_and_an_admin_only_key_for_a_vendor_return_null()
    {
        var db = TestSupport.NewDb();
        var service = Service(db);

        Assert.Null(await service.BuildAdminReportAsync("no-such-report", Filter()));
        Assert.Null(await service.BuildVendorReportAsync(shopId: 1, key: "commission", filter: Filter()));
        Assert.Null(await service.BuildVendorReportAsync(shopId: 1, key: "customers", filter: Filter()));
    }

    // ------------------------------------------------------------------
    // Filters
    // ------------------------------------------------------------------

    [Fact]
    public void The_filter_defaults_to_the_last_thirty_days_and_rejects_an_inverted_window()
    {
        var auto = new ReportFilter();
        Assert.False(auto.IsEmptyWindow);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-29), auto.FromStart);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1), auto.ToExclusive);

        var inverted = new ReportFilter { From = new DateTime(2026, 3, 10), To = new DateTime(2026, 3, 1) };
        Assert.True(inverted.IsEmptyWindow);

        var single = new ReportFilter { From = new DateTime(2026, 3, 1), To = new DateTime(2026, 3, 1) };
        Assert.False(single.IsEmptyWindow);
        Assert.Equal("01 Mar 2026", single.PeriodLabel);
    }

    // ------------------------------------------------------------------
    // The PDF writer
    // ------------------------------------------------------------------

    [Fact]
    public void The_pdf_output_is_a_structurally_valid_file()
    {
        var doc = Document("Sales report", new[]
        {
            new object?[] { "05 Mar 2026", 105.00m },
            new object?[] { "15 Mar 2026", 200.00m }
        });

        var text = Encoding.ASCII.GetString(ReportPdf.Render(doc, TestSupport.Store()));

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("/Type /Catalog", text);
        Assert.Contains("/Count 1", text);
        Assert.Contains("/Type /Page", text);
        Assert.Contains("Sales report", text);
        Assert.Contains("01 Mar 2026", text);
        Assert.Contains("Page 1 of 1", text);
        Assert.Contains("/Title (Sales report)", text);
        Assert.EndsWith("%%EOF\n", text);

        // A well-formed xref: every in-use entry points at "N 0 obj". The trailing \n of one
        // entry is the separator before the next, so the pattern must not start with it —
        // Regex.Matches consumes it and would then skip every other entry.
        var xref = Regex.Matches(text, @"\d{10} 00000 n \n");
        Assert.True(xref.Count >= 8, $"expected at least 8 xref entries, found {xref.Count}");
    }

    [Fact]
    public void The_pdf_output_stays_ascii_and_escapes_the_odd_characters()
    {
        var doc = Document("Café – orders (2026) ‘quoted’", new[]
        {
            new object?[] { "One – two ‘three’ (four) \\ five", 1.00m }
        });

        var text = Encoding.ASCII.GetString(ReportPdf.Render(doc, TestSupport.Store()));

        Assert.DoesNotContain(text, ch => ch > 127);
        Assert.Contains("\\226", text);   // en dash → WinAnsi 0x96, written octal
        Assert.Contains("\\222", text);   // right single quote → 0x92
        Assert.Contains("\\(", text);     // literal parenthesis is escaped
        Assert.Contains("\\\\", text);    // literal backslash is escaped
        Assert.Contains("\\351", text);   // é → Latin-1 0xE9, written octal
    }

    [Fact]
    public void A_long_table_spans_several_pages_with_page_numbers_on_each()
    {
        var rows = Enumerable.Range(1, 400)
            .Select(i => new object?[] { $"Row {i}", (decimal)i })
            .ToList();
        var doc = Document("Paged report", rows);

        var text = Encoding.ASCII.GetString(ReportPdf.Render(doc, TestSupport.Store()));

        var count = Regex.Match(text, @"/Count (\d+)");
        Assert.True(count.Success);
        Assert.True(int.Parse(count.Groups[1].Value) > 1, "a 400-row table must not fit one page");
        Assert.Contains("Page 1 of ", text);
        Assert.Contains($"Page {count.Groups[1].Value} of {count.Groups[1].Value}", text);
    }

    [Fact]
    public void A_cell_that_cannot_fit_is_wrapped_rather_than_run_off_the_page()
    {
        var word = new string('a', 300);
        var doc = Document("Wrapped report", new[] { new object?[] { word, 1.00m } });

        var text = Encoding.ASCII.GetString(ReportPdf.Render(doc, TestSupport.Store()));

        // The hard-broken word starts a line in the content stream …
        Assert.Contains("(aaaaaaaaaaaaaa", text);
        // … and the tail of it appears in a later line rather than being truncated away.
        var lastChunk = text.LastIndexOf("(aaa", StringComparison.Ordinal);
        Assert.True(lastChunk > 0);
    }
}
