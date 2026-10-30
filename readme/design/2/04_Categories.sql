/* =====================================================================
   Kwikby: global categories (idempotent)
   Creates each global category only if no global category with that slug
   exists. Safe to re-run.
   Columns: Id, ShopId, IsGlobal, Name, Slug, Description, ParentId,
            DisplayOrder, IsActive, CommissionRate
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

INSERT INTO dbo.Categories
    (ShopId, IsGlobal, Name, Slug, Description, ParentId, DisplayOrder, IsActive, CommissionRate)
SELECT NULL, 1, v.Name, v.Slug, v.Description, NULL, v.DisplayOrder, 1, NULL
FROM (VALUES
    (N'Books & Notebooks',       N'books-notebooks',       N'Notebooks, planners and sketchbooks.',                                  7),
    (N'Gifts & Crafts',          N'gifts-crafts',          N'Handmade gifts, souvenirs and gift wrap.',                              8),
    (N'Electronics & Accessories', N'electronics',         N'Cables, chargers, audio and desk accessories.',                         9),
    (N'Office Supplies',         N'office-supplies',       N'Counter consumables, labels and stationery.',                           10),
    (N'Counter Consumables',     N'counter-consumables',   N'Labels, rolls and dispensers.',                                         11),
    (N'Philately & Stamps',      N'philately',             N'Commemorative issues, first day covers and collector albums.',          12),
    (N'Commemorative Issues',    N'commemorative-issues',  N'Limited and themed stamp issues.',                                      13),
    (N'Albums & Accessories',    N'albums-accessories',    N'Binders, pages and handling tools.',                                    14),
    (N'Boxes',                   N'boxes',                 N'Single and double-wall shipping boxes.',                                15),
    (N'Protective Wrapping',     N'protective-wrapping',   N'Bubble wrap, void fill and tape.',                                      16),
    (N'Envelopes & Mailers',     N'envelopes-mailers',     N'Pre-paid and plain envelopes.',                                         17)
) AS v (Name, Slug, Description, DisplayOrder)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Categories AS c
    WHERE c.IsGlobal = 1 AND c.Slug = v.Slug
);

SELECT COUNT(*) AS GlobalCategories FROM dbo.Categories WHERE IsGlobal = 1;
