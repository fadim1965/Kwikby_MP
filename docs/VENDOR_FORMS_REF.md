# Seller (vendor) form and field reference

## 6. Vendor area (`/Vendor`)

### Shop onboarding — `/Vendor/Onboarding/Register` (`Onboarding/Register`)
- **Model:** `ShopRegistration`
- **Form:** one `POST` form (`Onboarding/Register`).
  | Field | Type | Purpose |
  |---|---|---|
  | `Name` | input | Shop name. |
  | `Tagline` | input | Short tagline. |
  | `Description` | textarea | Shop description. |
  | `PickupLocation` | input | Where the courier collects parcels. |
  | `BankAccountName` | input | Payout destination — account holder. |
  | `BankName` | input | Payout destination — bank. |
  | `BankIban` | input | Payout destination — IBAN. |
  | `ThemeKey` | select | Storefront template. |
  | `AcceptTerms` | checkbox | Accept terms. |
- Reapplying a rejected application pre-fills every field, the payout details included.

### Vendor dashboard — `/Vendor/Dashboard` (`Dashboard/Index`)
- **Model:** `ShopDashboard`. No forms.

### Vendor products list — `/Vendor/Products` (`Products/Index`)
- **Model:** `AdminProductListViewModel` (shared)
- **Forms:** four (same as admin products list: filter, toggle published, restore, delete).

### Vendor product edit — `/Vendor/Products/Edit` (and Create) (`Products/Save`)
- **Model:** `ProductEditViewModel` (shared)
- **Form:** one `POST` form (`Products/Save`), `enctype="multipart/form-data"` — same fields as the admin product edit form.

### Vendor orders list — `/Vendor/Orders` (`Orders/Index`)
- **Model:** `IReadOnlyList<Order>`
- **Form:** one `GET` filter form — `status` (select), `q` (search).

### Vendor order detail — `/Vendor/Orders/Details/{id}` (`Orders/Details`)
- **Model:** `Order`. Read-only except for dispatch: the shop can register its own shipments
  through the simulator. Returns against the order appear in their own panel, linking to the RMA
  detail where the shop acts on them.
- **Forms:** two (`POST`) — **Ship** (`Orders/Ship`) and **Refresh tracking** (`Orders/RefreshTracking`),
  each just `id` (hidden). Status changes beyond shipping stay staff-only.

### Vendor returns — `/Vendor/Returns` (`Returns/Index`)
- **Model:** `IReadOnlyList<ReturnRequest>` (the shop's own only; another shop's is a `404`).
- **Form:** none — two filter links (`All` / `Open only`) and **Review** buttons into the detail.
- Columns: reference, order, requested, units, goods value, status badge.

### Vendor return detail — `/Vendor/Returns/Details/{id}` (`Returns/Details`)
- **Model:** `ReturnRequest`.
- **Purpose:** The worklist decision: approve or reject the request (with a note the shopper will
  see), then record the goods as received when the parcel arrives. Restocked units show up in the
  shop's stock ledger. The refund itself is staff-only and never shown here.
- **Forms:** three (`POST`).
  1. **Approve** (`Returns/Decide`) — `approve=true` (hidden), `note` (textarea).
  2. **Reject** (`Returns/Decide`) — same shape, `approve=false`.
  3. **Receive goods** (`Returns/Receive`) — no fields; restocks and closes the handing-back step.

### Vendor reviews — `/Vendor/Reviews` (`Reviews/Index`)
- **Model:** `VendorReviewListViewModel`
- **Forms:** two (both `Reviews/Reply`, POST).
  1. Write reply — `reviewId`, `stars`, `sort`, `page` (hidden), `reply` (textarea).
  2. Clear reply — `reviewId`, `reply` (hidden, empty), `stars`, `sort`, `page` (hidden).

### Vendor payouts — `/Vendor/Payouts` (`Payouts/Index`, `SaveBank`)
- **Model:** `VendorPayoutViewModel`.
- **Balance and settlements are read-only.** Every settlement shows its status badge (`Paid`,
  `Failed`, `Reversed`), and failed or reversed rows explain why (reason, who handled it, when).
- **Form:** one `POST` form (`Payouts/SaveBank`) — the payout destination the marketplace will use:
  | Field | Type | Limits |
  |---|---|---|
  | `bankAccountName` | input | ≤ 120 chars; account holder. |
  | `bankName` | input | ≤ 120 chars. |
  | `bankIban` | input | ≤ 40 chars. |

### Vendor payout detail — `/Vendor/Payouts/Details/{id}` (`Payouts/Details`)
- **Model:** `VendorPayoutDetailViewModel`. No forms; the statement plus its status — a `Failed` or
  `Reversed` settlement shows the reason and who marked it so.

### Vendor appearance — `/Vendor/Appearance` (`Appearance/Index`)
- **Model:** `ShopAppearanceViewModel`
- **Form:** one `POST` form (`Appearance/Index`).
  | Field | Type | Purpose |
  |---|---|---|
  | `themeKey` | radio | Chosen storefront template (one of six). |

---

