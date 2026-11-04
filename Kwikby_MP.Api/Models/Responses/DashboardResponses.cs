namespace kwikby_MP.Api.Models.Responses;

public sealed class ShopDashboardResponse
{
    public int Days { get; set; }

    public ShopDashboardFigures Figures { get; set; } = new();

    public IReadOnlyList<TopProductResponse> TopProducts { get; set; } = Array.Empty<TopProductResponse>();
}

public sealed class ShopDashboardFigures
{
    public int OrdersPlaced { get; set; }

    public int OrdersPaidOrLater { get; set; }

    public int OrdersCancelled { get; set; }

    public decimal Revenue { get; set; }

    public int LowStockVariants { get; set; }

    public int ListingsAwaitingReview { get; set; }
}

public sealed class TopProductResponse
{
    public string ProductName { get; set; } = string.Empty;

    public int UnitsSold { get; set; }

    public decimal Sales { get; set; }
}

public sealed class PlatformDashboardResponse
{
    public int Days { get; set; }

    public decimal Gmv { get; set; }

    public int OrdersPlaced { get; set; }

    public int ActiveShops { get; set; }

    public int ShopsAwaitingApproval { get; set; }

    public int ListingsAwaitingReview { get; set; }

    public int ReviewsAwaitingModeration { get; set; }
}
