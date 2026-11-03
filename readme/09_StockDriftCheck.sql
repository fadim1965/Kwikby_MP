/* =====================================================================
   Reports products whose shop-listing stock does not match the sum of
   their active variant stock. Read-only. Run it before deciding which
   stock figure to keep. Variant stock is the intended source of truth.
   ===================================================================== */
SET NOCOUNT ON;

SELECT
    sl.Id                          AS ShopListingId,
    sl.ShopId,
    sl.ProductId,
    sl.StockQuantity               AS ListingStock,
    ISNULL(SUM(v.StockQuantity), 0) AS VariantStock,
    ISNULL(SUM(v.StockQuantity), 0) - sl.StockQuantity AS Difference
FROM dbo.ShopListings AS sl
LEFT JOIN dbo.ProductVariants AS v
       ON v.ProductId = sl.ProductId AND v.IsActive = 1
GROUP BY sl.Id, sl.ShopId, sl.ProductId, sl.StockQuantity
HAVING sl.StockQuantity <> ISNULL(SUM(v.StockQuantity), 0)
ORDER BY ABS(ISNULL(SUM(v.StockQuantity), 0) - sl.StockQuantity) DESC;
