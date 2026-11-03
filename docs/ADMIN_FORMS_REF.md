# Admin form and field reference

## 5. Admin area (`/Admin`)

All admin pages share `_AdminLayout` (sidebar). Antiforgery is global.

### Dashboard — `/Admin/Dashboard` (`Dashboard/Index`)
- **Model:** `DashboardViewModel`. No forms. Links to other admin pages.

### Products list — `/Admin/Products` (`Products/Index`)
- **Model:** `AdminProductListViewModel`
- **Forms:** four.
  1. **Filter** (`GET Products/Index`) — `q` (search), `categoryId` (select), `state` (select).
  2. **Toggle published** (`Products/TogglePublished`, POST) — `id` (hidden).
  3. **Restore** (`Products/Restore`, POST) — `id` (hidden).
  4. **Delete** (`Products/Delete`, POST) — `id` (hidden), confirm dialog.

### Product edit — `/Admin/Products/Edit` (and Create) (`Products/Edit` / `Save`)
- **Model:** `ProductEditViewModel`
- **Form:** one `POST` form (`Products/Save`), `enctype="multipart/form-data"`.
  | Field | Type | Purpose |
  |---|---|---|
  | `Id` | hidden | 0 for new. |
  | `Name` | input | Product name. |
  | `Slug` | input | URL slug (auto-derived if blank). |
  | `Brand` | input | Brand. |
  | `ShortDescription` | textarea | Short description. |
  | `Description` | textarea | Full description. |
  | `CategoryId` | select | Category. |
  | `IsPublished` | checkbox | Published. |
  | `IsFeatured` | checkbox | Featured. |
  | `ImageUrls` | textarea | One URL per line. |
  | `ImageFiles` | file (multiple) | Uploaded images. |
  | `Variants[i].Id` | hidden | Variant id (0 for new). |
  | `Variants[i].Sku` | input | SKU. |
  | `Variants[i].Name` | input | Variant name. |
  | `Variants[i].Price` | number | Price. |
  | `Variants[i].CompareAtPrice` | number | Compare-at price. |
  | `Variants[i].StockQuantity` | number | Stock. |
  | `Variants[i].LowStockThreshold` | number | Low-stock threshold. |
  | `Variants[i].IsActive` | checkbox | Active. |
  | `Variants[i].Delete` | checkbox | Mark for deletion. |

### Bulk import — `/Admin/Import` (`Import/Index`)
- **Model:** `ImportController.ImportViewModel`
- **Form:** one `POST` form, `enctype="multipart/form-data"`.
  | Field | Type | Purpose |
  |---|---|---|
  | `shopId` | select | Target shop. |
  | `file` | file | CSV file (max 5 MB). |
  | `createMissing` | checkbox | Create products that do not exist yet. |

### Categories — `/Admin/Categories` (`Categories/Index`, `Save`, `Delete`)
- **Model:** `CategoryTreeViewModel` (index), `CategoryEditViewModel` (edit).
- **Forms:**
  1. **Save** (`Categories/Save`, POST) — `Id` (hidden), `Name`, `Slug`, `Description`, `ParentId` (select), `DisplayOrder` (number), `IsActive` (checkbox).
  2. **Delete** (`Categories/Delete`, POST) — `id` (hidden), confirm dialog.

### Inventory — `/Admin/Inventory` (`Inventory/Index`, `Adjust`, `SetStock`)
- **Model:** `InventoryViewModel`
- **Forms:** three.
  1. **Filter** (`GET Inventory/Index`) — `q` (search), `filter` (select).
  2. **Adjust stock** (`Inventory/Adjust`, POST) — `variantId` (hidden), `returnUrl` (hidden), `delta` (number, +/-), `reason` (select), `note` (text).
  3. **Set stock** (`Inventory/SetStock`, POST) — `variantId` (hidden), `newQuantity` (number), `note` (text).

### Orders list — `/Admin/Orders` (`Orders/Index`)
- **Model:** `AdminOrderListViewModel`
- **Form:** one `GET` filter form — `q` (search), `status` (select).

### Order detail — `/Admin/Orders/Details/{id}` (`Orders/Details`)
- **Model:** `Order`
- **Form:** one `POST` form (`Orders/UpdateStatus`).
  | Field | Type | Purpose |
  |---|---|---|
  | `id` | hidden | Order id. |
  | `status` | select | New status (legal transitions only). |
  | `trackingNumber` | text | Tracking number (for shipped). |
- Also shown: **Refunded so far** and **Still owed to shopper** totals under the money summary, and
  the order's returns with their statuses (read-only here — the returns work at
  [the returns queue](#returns-queue--adminreturns-adminreturns-index)).

### Returns queue — `/Admin/Returns` (`Returns/Index`)
- **Model:** `IReadOnlyList<ReturnRequest>` (order, shop and lines included).
- **Forms:** two.
  1. **Filter** (`GET Returns/Index`) — `status` (select: all plus each status), `q` (text, matches
     RMA reference, shopper email or order number).
  2. **Export** — GET links (`Returns/Export?format=csv|excel`), honouring the current filter.
- Shows per-status workload counters above the table; each row links to the return detail. The page
  lists up to 200 rows.

### Return detail — `/Admin/Returns/Details/{id}` (`Returns/Details`)
- **Model:** `ReturnRequest` (with order, shop, lines, and the per-line product snapshots).
- **Purpose:** Act on one return. Shows the shopper's request, the shop's decision, the audit
  history for the return, and — depending on state — the administrator's actions: decide
  (approve/reject) on the shop's behalf, record the goods as received, and **Issue refund**.
- **Forms:** four (`POST`).
  1. **Approve** (`Returns/Decide`) — `id` (hidden), `approve=true` (hidden), `note` (textarea, the
     decision note the vendor and shopper see).
  2. **Reject** (`Returns/Decide`) — same shape, `approve=false`; a reason is expected.
  3. **Receive** (`Returns/Receive`) — `id` (hidden); restocks the goods and opens the refund step.
  4. **Refund** (`Returns/Refund`) — `id` (hidden), `note` (textarea). Issues the money: goods +
     proportional tax (+ shipping only for a whole-order return), capped by the ledger remainder and
     `GrandTotal − RefundedTotal`; refuses loudly when the sale is not on the ledger to reverse.

### Customers list — `/Admin/Customers` (`Customers/Index`)
- **Model:** `AdminCustomerListViewModel`
- **Form:** one `GET` search form — `q` (search).

### Customer detail — `/Admin/Customers/Details/{id}` (`Customers/Details`)
- **Model:** `AdminCustomerDetailViewModel`
- **Forms:** two (both `Customers/SetActive`).
  1. Deactivate (confirm dialog) — `id` (hidden), `isActive` (hidden, false).
  2. Activate — `id` (hidden), `isActive` (hidden, true).

### Coupons list — `/Admin/Coupons` (`Coupons/Index`)
- **Model:** `IReadOnlyList<Coupon>`
- **Form:** one `POST` per coupon (`Coupons/Delete`) — `id` (hidden), confirm dialog.

### Coupon create — `/Admin/Coupons/Create` (`Coupons/Create`)
- **Model:** `Coupon`
- **Form:** one `POST` form (`Coupons/Create`).

### Coupon edit — `/Admin/Coupons/Edit/{id}` (`Coupons/Edit`)
- **Model:** `Coupon`
- **Form:** one `POST` form (`Coupons/Edit`).

The shared partial `_Form.cshtml` (used by both create and edit) exposes:
  | Field | Type | Purpose |
  |---|---|---|
  | `Id` | hidden | 0 for new. |
  | `Code` | input | Coupon code. |
  | `Type` | select | Percentage or fixed amount. |
  | `Value` | number | Discount value. |
  | `MinSubtotal` | number | Minimum order subtotal. |
  | `MaxUses` | number | Global usage cap. |
  | `MaxUsesPerUser` | number | Per-shopper cap. |
  | `StartsUtc` | datetime-local | Valid from. |
  | `ExpiresUtc` | datetime-local | Valid until. |
  | `Description` | input | Internal description. |
  | `AppliesToAll` | checkbox | Applies to all shops. |
  | `ShopId` | select | Restrict to one shop. |
  | `IsActive` | checkbox | Active. |

### Tax classes — `/Admin/Tax` (`Tax/Index`, `Create`, `Deactivate`)
- **Model:** `TaxController.TaxListViewModel`
- **Forms:** two.
  1. **Create** (`Tax/Create`, POST) — `name` (input), `rate` (number, 0–1), `isExempt` (checkbox).
  2. **Deactivate** (`Tax/Deactivate`, POST) — `id` (hidden).

### Shops — `/Admin/Shops` (`Shops/Index`)
- **Model:** `IReadOnlyList<Shop>`
- **Forms:** four per shop row.
  1. **Approve** (`Shops/Approve`, POST) — `id` (hidden), `commissionPercent` (number, e.g. 10).
  2. **Reject** (`Shops/Reject`, POST) — `id` (hidden), `reason` (shown to the vendor).
  3. **Suspend** (`Shops/Suspend`, POST) — `id` (hidden), `reason` (shown to the vendor).
  4. **Reinstate** (`Shops/Reinstate`, POST) — `id` (hidden).
- The table also shows each shop's recorded **payout destination** (account name, bank, IBAN, or
  "Not provided") and a **history** link straight to `/Admin/Audit?entityType=Shop&entityId={id}`.

### Payouts list — `/Admin/Payouts` (`Payouts/Index`)
- **Model:** `AdminPayoutIndexViewModel`
- **Form:** one `POST` form (`Payouts/Create`) — `shopId` (hidden), `method` (input), `note` (input).
- The balances table shows what each shop is owed; the history table carries a **status** column
  (`Paid` / `Failed` / `Reversed` badges).

### Payout detail — `/Admin/Payouts/Details/{id}` (`Payouts/Details`)
- **Model:** `AdminPayoutDetailViewModel`.
- Read-only statement (reference, period, every ledger entry it settled), plus, **while the payout is
  still `Paid`**, two correction forms (both `POST`, `id` hidden):
  1. **Mark failed** (`Payouts/MarkFailed`) — `reason` (required, ≤ 500 chars); the bank refused.
     Releases every covered entry back to the unpaid balance.
  2. **Reverse** (`Payouts/Reverse`) — `note` (optional); recorded in error. The same release, with
     the row keeping its totals for the statement.
  A `Failed`/`Reversed` payout shows its reason, who handled it, and when — and no forms.

### Marketing — `/Admin/Marketing` (`Marketing/Index`)
- **Model:** `MarketingDashboardStats`. No forms (exports are GET links).

### Reviews moderation — `/Admin/Reviews` (`Reviews/Index`)
- **Model:** `AdminReviewListViewModel`
- **Forms:** five.
  1. **Filter** (`GET Reviews/Index`) — `status` (hidden), `ratingFilter` (select).
  2. Three **SetStatus** forms (`Reviews/SetStatus`, POST) — `id`, `status`, `filter`, `rating`, `page` (hidden).
  3. **Delete** (`Reviews/Delete`, POST) — `id`, `filter`, `rating`, `page` (hidden), confirm dialog.

### Audit trail — `/Admin/Audit` (`Audit/Index`, `Export`)
- **Model:** `IReadOnlyList<AuditLog>` (newest first, 200 rows per page).
- **Forms:** two.
  1. **Filter** (`GET Audit/Index`) — `actionName` (text with datalist suggestions derived from the
     data), `entityType` (datalist), `entityId` (number), `actor` (text).
  2. **Export** — GET links (`Audit/Export?actionName=…&entityType=……`, up to 500 rows,
     `?format=csv|excel`).
- Each row: timestamp, actor, dotted action (`shop.approved`, `refund.failed`, …), entity reference
  and the summary sentence. Per-entity links (e.g. from the shops list) land here pre-filtered.

### Emails outbox — `/Admin/Emails` (`Emails/Index`, `Details`, `Resend`, `SendTest`)
- **Model:** `AdminEmailListViewModel` (list), `EmailMessage` (detail).
- **Forms:**
  1. **Filter** (`GET Emails/Index`) — `status` (hidden), `kindFilter` (select).
  2. **Send test** (`Emails/SendTest`, POST) — `status` (hidden), `kind` (hidden), `page` (hidden), `testTo` (email).
  3. **Resend** (`Emails/Resend`, POST) — `id`, `status`, `kind`, `page` (hidden).
- Email detail page has a **Resend** form — `id` (hidden).

---

