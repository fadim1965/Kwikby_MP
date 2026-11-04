# Kwikby REST API over Stored Procedures — Implementation Plan

> Status: **Phase 0 + Phase 1 implemented and tested.** The delivered surface and
> operating instructions are in [`API_GUIDE.md`](API_GUIDE.md). Decisions are
> recorded in §19; the delivery summary is at the end of this document.
> Awaiting review before Phase 2 (write endpoints).
>
> Scope: add a versioned REST/JSON API (`Kwikby_MP.Api`, a separate ASP.NET Core
> Web API project) whose data access goes through **SQL Server stored
> procedures**, not EF Core.

---

## 1. Executive summary

The application today is an ASP.NET Core 9 MVC site. It talks to SQL Server
through EF Core (with a SQLite fallback for a fresh clone). A `Database/`
folder already contains a generated schema and a first set of stored
procedures, but **none of those procedures are deployed**, and the scripts use
syntax that the live server does not support.

This plan adds:

- a new **`Kwikby_MP.Api`** ASP.NET Core Web API project (separate process,
  port and deployment unit) serving `/api/v1/...`,
- a small **`Kwikby_MP.Shared`** class library for the password hasher and
  shared contracts,
- a Dapper-based data-access layer that only calls stored procedures,
- JWT authentication for API clients (the MVC site keeps cookie auth and is
  not modified),
- new 2014-compatible SQL scripts with the procedures the API needs,
- Swagger/OpenAPI, ProblemDetails errors, rate limiting, role/shop scoping,
- tests and a written API guide.

It is delivered in phases so each phase is independently testable.

---

## 2. What exists today (verified facts)

| Item | Finding |
|---|---|
| Runtime | .NET 9, ASP.NET Core MVC + Razor views |
| Auth | Cookie authentication (`AddCookie`), roles `Admin` / `Customer` |
| Data access | EF Core 9 (`ApplicationDbContext`) |
| SQL Server | `localhost` (`Data Source=.`), database **`MVStores`**, SQL Server **2014 SP2 Express**, compatibility level **120** |
| Tables | 42 tables already created (EF schema) |
| Stored procedures | **0 deployed** (`sys.procedures` is empty) |
| Existing SQL scripts | `Database/00_Schema.sql` (EF-generated), `02_StoredProcedures.sql`, `03_HeroEngine.sql` |
| Script compatibility | `02` and `03` use `CREATE OR ALTER`, which requires **SQL Server 2016 SP1+**. The live server is 2014 → these scripts **will fail as written**. |
| Hero tables | `PlatformSettings`, `VendorBanners`, `HeroSlots`, `CampaignBookings`, `HeroImpressionStaging` do **not** exist in `MVStores`. |
| Existing procedures (in files, not deployed) | `usp_SearchProducts`, `usp_GetProductDetail`, `usp_GetShopDashboard`, `usp_GetPlatformDashboard`, `usp_AdjustStock`, plus the Hero engine procedures |
| Cached NuGet packages | Dapper 2.1.66, Swashbuckle 9.0.4, JwtBearer 9.0.9, Microsoft.Data.SqlClient 5.1.6 & 6.1.1 |
| Global MVC filters | `AutoValidateAntiforgeryToken` on every controller |
| Routing | `MapControllerRoute` only — **no `MapControllers()`**, so attribute-routed controllers would not be reachable |
| Error/status pages | `UseExceptionHandler("/Home/Error")` and `UseStatusCodePagesWithReExecute("/Home/NotFound")` — these return **HTML** for API failures |

---

## 3. Goals and non-goals

### Goals

1. A REST/JSON API under `/api/v1` that external clients (mobile app, partner
   systems, internal tools) can call.
2. **All API reads and writes go through stored procedures** in `MVStores`.
3. Authenticate callers with JWT bearer tokens; authorize by role and by shop
   ownership.
4. Keep the existing MVC site working exactly as it does today.
5. Be safe by default: parameterised SQL, least privilege, rate limiting,
   ProblemDetails, audit trail, no secrets in source.
6. Ship a repeatable deployment script and an API guide.

### Non-goals (v1)

- Replacing EF Core in the MVC site. EF stays for the web UI.
- A separate microservice/deployment. The API is an area in the same app.
- A public developer portal or OAuth2/OpenID Connect server. JWT is issued
  directly by this API.
- Rewriting checkout/payment/ledger logic in T-SQL (see §7.6 for the trade-off).
- Multi-tenant database-per-shop isolation. Tenant scoping is by `ShopId`.

---

## 4. Key constraints and decisions

### 4.1 SQL Server 2014 compatibility (hard constraint)

The live server is **12.0 / compat 120**. The new SQL must therefore avoid:

| Not available on 2014 | Use instead |
|---|---|
| `CREATE OR ALTER` | `IF OBJECT_ID(...) IS NULL EXEC('CREATE ...')` + `GO` + `ALTER` |
| `DROP PROCEDURE IF EXISTS` | `IF OBJECT_ID(...) IS NOT NULL DROP PROCEDURE ...` |
| `STRING_AGG` | `FOR XML PATH('')` / client-side aggregation |
| `OPENJSON`, `JSON_VALUE`, `FOR JSON` | table-valued parameters (TVPs) or flat parameters |
| `STRING_SPLIT` | TVP or a numbers/tally table |
| `AT TIME ZONE` | store and compare UTC only |
| `TRIM`, `CONCAT_WS` | `LTRIM(RTRIM(...))`, `+` |
| `DROP ... IF EXISTS` for tables/views | `IF OBJECT_ID(...) IS NOT NULL` |
| `DATETRUNC`, `GREATEST`, `LEAST`, `GENERATE_SERIES` | explicit `DATEADD`/`CASE`/tally table |

**Two options:**

- **A (recommended):** rewrite `02`/`03` and write the new `04` in
  SQL Server 2014-compatible syntax. No server change needed; works today.
- **B:** upgrade the SQL Server instance to 2016 SP1+ (ideally 2019/2022).
  Cleaner long-term, but an infrastructure change outside this repository.

> The plan below assumes **A**. If you choose B, the SQL can keep
> `CREATE OR ALTER` and we save some script work.

### 4.2 Where the API lives — **confirmed: separate project**

**Confirmed: a new `Kwikby_MP.Api` ASP.NET Core Web API project.**

- The API is its own process, port and deployment unit. It does **not** run the
  MVC pipeline, so the MVC antiforgery filter, `MapControllerRoute`, session,
  tenant-theme middleware and HTML error pages are irrelevant to it.
- It has its own `Program.cs`, `appsettings*.json`, DI container, Swagger and
  health checks.
- A small `Kwikby_MP.Shared` class library holds the few pieces both apps need
  (the PBKDF2 `IPasswordHasher` and shared enums/contracts). The hasher source
  is **linked** into the shared project rather than copied, so there is one
  source of truth.
- Phase 0–1 uses **no EF Core and no reference to the MVC project**.
- Phase 3 (hybrid money workflows) will add a reference to the MVC services
  (`IOrderService`, payment/ledger) or move those services into `Shared`.
  That is a deliberate later step, not part of Phase 0–1.

> Why not reference `Kwikby_MP.csproj` directly from the API? It works, but it
> drags the whole MVC/EF dependency graph and the web `appsettings.json` into
> the API output and creates config-collision risk. The small shared library is
> cleaner and keeps the API publish small.

### 4.3 SQL Server is required for the API

The API's stored procedures only exist on SQL Server. If no SQL Server
connection string is configured (fresh clone / CI without SQL Server):

- the API returns **503 Service Unavailable** with a ProblemDetails body
  explaining that `ConnectionStrings:MultiVendorsStoresConn` is required;
- the MVC site continues to run on SQLite.

This keeps development unblocked and makes the failure explicit.

### 4.4 Data-access library

- **Dapper 2.1.66** — light, fast, maps result sets to DTOs, no change tracker.
- **Microsoft.Data.SqlClient 5.1.6** — matches the version EF Core SqlServer
  9.0.9 already uses, avoiding assembly-binding conflicts. (`6.1.1` is cached
  but not used unless we also bump EF.)
- No EF in the API request path.

---

## 5. Architecture

```
┌─────────────────────────────┐        ┌─────────────────────────────┐
│  Kwikby_MP (existing MVC)   │        │  Kwikby_MP.Api (new)        │
│  localhost:5268             │        │  localhost:5269             │
│  cookie auth, Razor, EF     │        │  JWT auth, JSON, Dapper     │
└──────────────┬──────────────┘        └──────────────┬──────────────┘
               │ EF Core                               │ stored procedures
               └───────────────┬───────────────────────┘
                               ▼
                    SQL Server 2014 (MVStores)
                    ├─ 42 EF tables
                    └─ dbo.usp_* procedures

Kwikby_MP.Shared (class library, no EF)
  └─ IPasswordHasher / Pbkdf2PasswordHasher, shared enums/contracts
```

API request flow:

```
Client (mobile / partner / Postman / Swagger)
        │  HTTPS + JWT Bearer
        ▼
Kwikby_MP.Api pipeline
  ├─ security headers, HTTPS, CORS
  ├─ rate limiter (api policy)
  ├─ authentication: JWT bearer
  ├─ authorization: role + shop-scope policies
  └─ routing: /api/v1/... → ApiControllers (attribute routing)
        │
        ▼
ApiControllerBase
  ├─ model validation → ProblemDetails 400
  ├─ calls IStoredProcedureGateway (never DbContext)
  └─ maps SQL errors → 400/403/404/409/500
        │
        ▼
StoredProcedureGateway (Dapper)
  ├─ IDbConnectionFactory → new SqlConnection(MultiVendorsStoresConn)
  ├─ CommandType = StoredProcedure
  ├─ QueryAsync / QueryMultipleAsync / ExecuteScalarAsync
  └─ cancellation + command timeout
        │
        ▼
SQL Server 2014 (MVStores)
  └─ dbo.usp_* procedures (set-based, transactional, audited)
```

---

## 6. Project / file layout

Two new projects in `Kwikby_MP.sln`; the existing MVC project is not modified.

```
Kwikby_MP.Api/
  Kwikby_MP.Api.csproj              (Microsoft.NET.Sdk.Web, net9.0)
  Program.cs
  appsettings.json                  (Api section, empty secrets)
  appsettings.Development.json
  Properties/launchSettings.json    (http://localhost:5269)
  Controllers/
    ApiControllerBase.cs
    AuthController.cs
    ProductsController.cs
    CategoriesController.cs
    ShopsController.cs
    DashboardController.cs
    (later: Inventory, Orders, Customers, Coupons, Reviews, Returns, Payouts)
  Models/
    Requests/                       (CreateProductRequest, ...)
    Responses/                      (ProductSummary, PagedResponse<T>, ...)
  Filters/
    ShopScopeAttribute.cs           (vendor can only touch own shop)
    ApiExceptionFilter.cs           (ProblemDetails mapping)
  Mapping/
    ApiMappings.cs

  Infrastructure/
    ApiOptions.cs
    JwtOptions.cs
    SqlConnectionFactory.cs
    IStoredProcedureGateway.cs
    StoredProcedureGateway.cs
    SqlErrorTranslator.cs
    ServiceCollectionExtensions.cs
    PipelineExtensions.cs
    JwtTokenService.cs
    ApiRateLimitPolicies.cs

  Repositories/
    IProductApiRepository.cs / ProductApiRepository.cs
    ICategoryApiRepository.cs / CategoryApiRepository.cs
    IShopApiRepository.cs / ShopApiRepository.cs
    IAuthRepository.cs / AuthRepository.cs
    IDashboardApiRepository.cs / DashboardApiRepository.cs

Kwikby_MP.Shared/
  Kwikby_MP.Shared.csproj           (Microsoft.NET.Sdk, net9.0, no packages)
  Security/PasswordHasher.cs        (linked from ../Services/PasswordHasher.cs)
  Contracts/                        (shared enums/contracts, if needed)

Database/
  02_StoredProcedures.sql          (rewritten for 2014)
  03_HeroEngine.sql                (rewritten for 2014; optional)
  04_ApiStoredProcedures.sql       (new — everything the API calls)
  05_Deploy.ps1                    (runs 02/03/04 in order, prints results)

docs/
  API_IMPLEMENTATION_PLAN.md       (this file)
  API_GUIDE.md                     (endpoint reference for consumers)
  api-smoke.ps1                    (end-to-end smoke test)

Tests/
  Kwikby_MP.Api.Tests/             (new test project)
    GatewayTests.cs
    AuthTests.cs
    ErrorMappingTests.cs
    SqlContractTests.cs            (skipped without SQL Server)
    IntegrationTests.cs            (skipped without SQL Server)
```

Solution / CI changes:

- `dotnet sln Kwikby_MP.sln add Kwikby_MP.Api/Kwikby_MP.Api.csproj Kwikby_MP.Shared/Kwikby_MP.Shared.csproj`
- `.github/workflows/ci.yml`: build the solution (or both projects) and run both
  test projects.
- `Dockerfile`: optionally add a second stage/target for the API in Phase 4.
  Phase 0–1 can run the API with `dotnet run --project Kwikby_MP.Api`.

Local configuration: the API reads
`ConnectionStrings:MultiVendorsStoresConn` and `Api:Jwt:SigningKey` from
`Kwikby_MP.Api/appsettings.Local.json` (gitignored) or environment variables.
The SQL password is not duplicated in source.

---

## 7. Stored-procedure strategy

### 7.1 Conventions

Every procedure follows the same shape:

```sql
IF OBJECT_ID(N'dbo.usp_Product_Get', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_Product_Get AS BEGIN SET NOCOUNT ON; END');
GO
ALTER PROCEDURE dbo.usp_Product_Get
    @ProductId INT,
    @Actor     NVARCHAR(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- set-based query
END;
GO
```

Rules:

- Name: `dbo.usp_<Area>_<Action>` (e.g. `usp_Product_List`).
- `SET NOCOUNT ON` always; `SET XACT_ABORT ON` for writes.
- Reads return one or more result sets (Dapper `QueryMultiple`).
- Writes return the created/updated row (or at least `Id`) so the API can
  respond with the resource, not just `204`.
- Writes take `@Actor NVARCHAR(256)` (the JWT `sub`/email) and write an
  `AuditLogs` row.
- Timestamps are `SYSUTCDATETIME()`; all times are UTC.
- No dynamic SQL, no string concatenation of user input.
- Pagination: `@Page INT = 1`, `@PageSize INT = 20`, capped at 100;
  `COUNT(*) OVER()` for total count.
- Sort: whitelist inside `CASE` (never concatenate a sort column).
- Errors: `THROW 52xxx, N'...', 1;` with a documented code table (§7.4).
- Permissions: the app's SQL login gets `EXECUTE` on `dbo` only; the API never
  needs direct table `SELECT/INSERT/UPDATE/DELETE` (defence in depth).
  (The deployment script grants `EXECUTE` to a configurable role/login.)

### 7.2 Deployment order

1. `00_Schema.sql` — already applied (42 tables). Not re-run.
2. `02_StoredProcedures.sql` — rewrite for 2014, deploy.
3. `03_HeroEngine.sql` — rewrite for 2014, deploy (only if Hero is in scope).
4. `04_ApiStoredProcedures.sql` — new procs, deploy.
5. `05_Deploy.ps1` — runs 2–4 with `sqlcmd`, stops on first error, prints a
   summary. Parameterised by connection string/environment variable; no
   password in the script.

### 7.3 Procedure catalogue

**A. Reuse from `02` (after 2014 rewrite)**

| Procedure | Purpose |
|---|---|
| `usp_SearchProducts` | storefront search/browse (paged) |
| `usp_GetProductDetail` | product + variants + images + rating (4 result sets) |
| `usp_GetShopDashboard` | vendor KPIs |
| `usp_GetPlatformDashboard` | admin KPIs |
| `usp_AdjustStock` | atomic stock change + movement row |

**B. New in `04` — auth**

| Procedure | Purpose |
|---|---|
| `usp_Auth_GetUserForLogin` | user by email + password hash + roles |
| `usp_Auth_GetUserClaims` | roles + shop memberships for token claims |

**C. New in `04` — catalogue**

| Procedure | Purpose |
|---|---|
| `usp_Category_List` / `_Get` / `_Create` / `_Update` / `_Delete` | category CRUD |
| `usp_Product_List` | admin/vendor list with filters + paging |
| `usp_Product_Get` | admin/vendor detail |
| `usp_Product_Create` / `_Update` / `_SoftDelete` | product CRUD |
| `usp_Product_SetPublished` / `_SetModeration` | publish/moderation |
| `usp_Variant_List` / `_Create` / `_Update` / `_Delete` | variants |
| `usp_Image_List` / `_Add` / `_Delete` / `_Reorder` | images |
| `usp_Shop_List` / `_Get` / `_Create` / `_Update` / `_SetStatus` | shops |
| `usp_Shop_User_List` / `_Add` / `_Remove` | team members (vendor) |

**D. New in `04` — inventory**

| Procedure | Purpose |
|---|---|
| `usp_Stock_List` | stock levels by shop |
| `usp_StockMovements_List` | movement history |
| (`usp_AdjustStock`) | reuse for adjustments |

**E. New in `04` — commerce**

| Procedure | Purpose |
|---|---|
| `usp_Order_List` / `_Get` / `_UpdateStatus` | order read + status transitions |
| `usp_Order_Create` | transactional order placement (see §7.6) |
| `usp_Customer_List` / `_Get` | admin customer directory |
| `usp_Coupon_List` / `_Get` / `_Create` / `_Update` / `_Delete` / `_Validate` | coupons |
| `usp_Review_List` / `_Get` / `_Create` / `_SetStatus` / `_Delete` | reviews |
| `usp_Return_List` / `_Get` / `_Create` / `_SetStatus` | returns |
| `usp_Ledger_List` | vendor ledger |
| `usp_Payout_List` / `_Create` / `_SetStatus` | payouts |
| `usp_Audit_List` | audit log (admin) |

**F. New in `04` — settings / reporting (Phase 4)**

| Procedure | Purpose |
|---|---|
| `usp_Setting_List` / `_Get` / `_Update` | site settings |
| `usp_Translation_List` / `_Upsert` | translations |
| `usp_Report_Sales` / `_TopProducts` / `_LowStock` | reporting |

### 7.4 Error-code table

| Code | Meaning | HTTP |
|---|---|---|
| 52001 | validation / zero quantity | 400 |
| 52002 | unknown enum value | 400 |
| 52003 | entity not found | 404 |
| 52004 | insufficient stock | 409 |
| 52005 | duplicate slug / SKU / email | 409 |
| 52006 | invalid state transition | 409 |
| 52007 | forbidden (shop/role scope) | 403 |
| 52008 | concurrency conflict | 409 |
| 52999 | unhandled database error | 500 |

`SqlProblemDetails` maps these to RFC 7807 bodies with `traceId` and the
procedure name (never the raw SQL or connection string).

### 7.5 Security of the SQL layer

- Parameterised commands only.
- The API login is a dedicated SQL login, not `sa`. It gets `EXECUTE` on
  `dbo` and `SELECT` on nothing else.
- `DB_CHAINING` off; procedures run as caller (no `EXECUTE AS OWNER`).
- No user input is ever used as an identifier (table/column/sort name).
- Connection string only from configuration/environment; never logged.

### 7.6 Checkout / payment / ledger trade-off (decision)

The existing `Database/README.md` deliberately keeps order placement, payments
and the vendor ledger in C# services because they are unit-tested and complex.

Two options:

- **Recommended (hybrid):** v1 reads and all simple CRUD use procedures; the
  three complex workflows call the existing C# services. This avoids
  duplicating money logic in T-SQL and keeps the tested behaviour.
- **Strict:** add `usp_Order_Create`, `usp_Payment_*`, `usp_Ledger_*` and move
  all money logic into transactions. Higher risk and more work; the C# services
  become dead code or must be kept in sync.

The rest of this plan assumes the hybrid. If you want **all** writes in
procedures, say so and I will add a Phase 3b with the money procedures.

---

## 8. API surface (v1)

Base: `/api/v1`. All responses are JSON (`application/json`); errors are
`application/problem+json`. All list endpoints are paged.

Legend: **A** = Admin, **V** = Vendor (own shop), **C** = Customer, **P** =
public/anonymous.

### Auth

| Method | Path | Access | Procedure |
|---|---|---|---|
| POST | `/auth/token` | P | `usp_Auth_GetUserForLogin` + `usp_Auth_GetUserClaims` |
| GET | `/auth/me` | any token | `usp_Auth_GetUserClaims` |

### Catalogue

| Method | Path | Access | Procedure |
|---|---|---|---|
| GET | `/products` | P | `usp_SearchProducts` |
| GET | `/products/{id}` | P | `usp_GetProductDetail` |
| POST | `/products` | V/A | `usp_Product_Create` |
| PUT | `/products/{id}` | V/A | `usp_Product_Update` |
| DELETE | `/products/{id}` | V/A | `usp_Product_SoftDelete` |
| PATCH | `/products/{id}/publish` | V/A | `usp_Product_SetPublished` |
| PATCH | `/products/{id}/moderation` | A | `usp_Product_SetModeration` |
| GET | `/products/{id}/variants` | P | `usp_Variant_List` |
| POST | `/products/{id}/variants` | V/A | `usp_Variant_Create` |
| PUT | `/products/{id}/variants/{variantId}` | V/A | `usp_Variant_Update` |
| DELETE | `/products/{id}/variants/{variantId}` | V/A | `usp_Variant_Delete` |
| GET | `/products/{id}/images` | P | `usp_Image_List` |
| POST | `/products/{id}/images` | V/A | `usp_Image_Add` |
| DELETE | `/products/{id}/images/{imageId}` | V/A | `usp_Image_Delete` |
| GET | `/categories` | P | `usp_Category_List` |
| GET | `/categories/{id}` | P | `usp_Category_Get` |
| POST | `/categories` | A | `usp_Category_Create` |
| PUT | `/categories/{id}` | A | `usp_Category_Update` |
| DELETE | `/categories/{id}` | A | `usp_Category_Delete` |

### Shops / vendors

| Method | Path | Access | Procedure |
|---|---|---|---|
| GET | `/shops` | P | `usp_Shop_List` |
| GET | `/shops/{id}` | P | `usp_Shop_Get` |
| POST | `/shops` | C | `usp_Shop_Create` |
| PUT | `/shops/{id}` | V/A | `usp_Shop_Update` |
| POST | `/shops/{id}/approve` | A | `usp_Shop_SetStatus` |
| POST | `/shops/{id}/reject` | A | `usp_Shop_SetStatus` |
| POST | `/shops/{id}/suspend` | A | `usp_Shop_SetStatus` |
| GET | `/shops/{id}/dashboard` | V/A | `usp_GetShopDashboard` |
| GET | `/admin/dashboard` | A | `usp_GetPlatformDashboard` |
| GET | `/shops/{id}/team` | V/A | `usp_Shop_User_List` |
| POST | `/shops/{id}/team` | V/A | `usp_Shop_User_Add` |
| DELETE | `/shops/{id}/team/{userId}` | V/A | `usp_Shop_User_Remove` |

### Inventory

| Method | Path | Access | Procedure |
|---|---|---|---|
| GET | `/inventory` | V/A | `usp_Stock_List` |
| POST | `/inventory/{variantId}/adjust` | V/A | `usp_AdjustStock` |
| GET | `/inventory/{variantId}/movements` | V/A | `usp_StockMovements_List` |

### Orders

| Method | Path | Access | Procedure |
|---|---|---|---|
| GET | `/orders` | C/V/A | `usp_Order_List` (scoped) |
| GET | `/orders/{id}` | C/V/A | `usp_Order_Get` (scoped) |
| PATCH | `/orders/{id}/status` | V/A | `usp_Order_UpdateStatus` |
| POST | `/orders` | C | `usp_Order_Create` or `IOrderService` (§7.6) |

### Customers / coupons / reviews / returns / payouts

| Method | Path | Access | Procedure |
|---|---|---|---|
| GET | `/customers` | A | `usp_Customer_List` |
| GET | `/customers/{id}` | A | `usp_Customer_Get` |
| GET | `/coupons` | A | `usp_Coupon_List` |
| GET | `/coupons/{id}` | A | `usp_Coupon_Get` |
| POST | `/coupons` | A | `usp_Coupon_Create` |
| PUT | `/coupons/{id}` | A | `usp_Coupon_Update` |
| DELETE | `/coupons/{id}` | A | `usp_Coupon_Delete` |
| POST | `/coupons/validate` | C | `usp_Coupon_Validate` |
| GET | `/products/{id}/reviews` | P | `usp_Review_List` |
| POST | `/products/{id}/reviews` | C | `usp_Review_Create` |
| GET | `/reviews` | A | `usp_Review_List` |
| PATCH | `/reviews/{id}/status` | A | `usp_Review_SetStatus` |
| DELETE | `/reviews/{id}` | A | `usp_Review_Delete` |
| GET | `/returns` | C/V/A | `usp_Return_List` |
| GET | `/returns/{id}` | C/V/A | `usp_Return_Get` |
| POST | `/returns` | C | `usp_Return_Create` |
| PATCH | `/returns/{id}/status` | V/A | `usp_Return_SetStatus` |
| GET | `/shops/{id}/ledger` | V/A | `usp_Ledger_List` |
| GET | `/payouts` | A/V | `usp_Payout_List` |
| POST | `/payouts` | A | `usp_Payout_Create` |
| PATCH | `/payouts/{id}/status` | A | `usp_Payout_SetStatus` |
| GET | `/audit` | A | `usp_Audit_List` |

### Ops

| Method | Path | Access | Notes |
|---|---|---|---|
| GET | `/health/live` | P | existing endpoint (process up) |
| GET | `/health/ready` | P | existing endpoint (DB reachable) |
| GET | `/swagger` | dev only | Swagger UI with JWT auth |

---

## 9. Data-access layer

### Interfaces

```csharp
public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken ct);
    bool IsConfigured { get; }
}

public interface IStoredProcedureGateway
{
    Task<IReadOnlyList<T>> QueryAsync<T>(string proc, object? parameters, CancellationToken ct);
    Task<T?> QuerySingleAsync<T>(string proc, object? parameters, CancellationToken ct);
    Task<SqlMapper.GridReader> QueryMultipleAsync(string proc, object? parameters, CancellationToken ct);
    Task<T?> ExecuteScalarAsync<T>(string proc, object? parameters, CancellationToken ct);
    Task ExecuteAsync(string proc, object? parameters, CancellationToken ct);
}
```

- Implemented once in `StoredProcedureGateway`; all calls set
  `CommandType.StoredProcedure`, `CommandTimeout` from `Api:CommandTimeoutSeconds`.
- Controllers depend on **typed repositories** (e.g. `IProductApiRepository`),
  not on raw strings, so parameter names live in one place.
- `QueryMultipleAsync` returns a `GridReader`; repositories read result sets in
  order and map to response DTOs. The reader is disposed with the connection.
- `CancellationToken` flows from the request (`HttpContext.RequestAborted`).
- A small `SqlErrorTranslator` turns `SqlException.Number` (52xxx) into
  `ApiException` with an HTTP status and a safe message.

### Repository examples

```csharp
public sealed class ProductApiRepository : IProductApiRepository
{
    private readonly IStoredProcedureGateway _db;
    public ProductApiRepository(IStoredProcedureGateway db) => _db = db;

    public Task<PagedResult<ProductSummary>> SearchAsync(ProductQuery q, CancellationToken ct) =>
        _db.QueryPagedAsync<ProductSummary>("usp_SearchProducts", q, ct);

    public async Task<ProductDetail?> GetAsync(int id, CancellationToken ct)
    {
        using var grid = await _db.QueryMultipleAsync("usp_GetProductDetail",
            new { ProductId = id }, ct);
        var product = await grid.ReadSingleOrDefaultAsync<ProductRow>();
        if (product is null) return null;
        var variants = await grid.ReadAsync<VariantRow>();
        var images   = await grid.ReadAsync<ImageRow>();
        var rating   = await grid.ReadSingleAsync<RatingRow>();
        return ApiMappings.ToDetail(product, variants, images, rating);
    }
}
```

---

## 10. Authentication and authorization

### Token issuing

`POST /api/v1/auth/token`

```json
{ "email": "admin@libanpost.example", "password": "…" }
```

Flow:

1. `usp_Auth_GetUserForLogin @Email` returns the user row with the stored
   password hash (and lockout flags if present).
2. `IPasswordHasher.Verify` (the existing PBKDF2 implementation, linked into
   `Kwikby_MP.Shared`) checks it. The API also calls
   `usp_Auth_RecordFailedLogin` / `usp_Auth_RecordLogin` so lockout behaviour
   matches the MVC site.
3. `usp_Auth_GetUserClaims @UserId` returns roles and shop memberships.
4. `JwtTokenService` signs an HS256 token with claims:
   `sub`, `email`, `name`, `role` (one per role), `shop_id` (one per shop),
   `jti`, `iat`, `exp`.
5. Response: `{ accessToken, tokenType: "Bearer", expiresIn }`.

### Token validation

- `AddJwtBearer` with issuer/audience/signing key from `Api:Jwt`.
- Signing key **must** be at least 32 bytes; production guard fails fast if it
  is missing or short (same pattern as the existing webhook-secret guard).
- Clock skew 2 minutes; `ValidateIssuer`, `ValidateAudience`,
  `ValidateLifetime`, `ValidateIssuerSigningKey` all true.
- `NameClaimType = "email"`, `RoleClaimType = "role"`.

### Policies

The existing app has only two role names, `Admin` and `Customer`; vendor
access is expressed by **shop membership** (`ShopUsers`), not a role.

| Policy | Requirement |
|---|---|
| `ApiAuthenticated` | valid JWT |
| `ApiAdmin` | `role` claim contains `Admin` |
| `ApiVendor` | authenticated and at least one `shop_id` claim, or `Admin` |
| `ApiCustomer` | `role` claim contains `Customer`, or `Admin` |

`ShopScopeAttribute` / a filter checks that the `{shopId}` or the resource's
`ShopId` matches a `shop_id` claim unless the caller is `Admin`. This prevents
a vendor from reading another shop's orders by guessing an id.

### Separation from MVC (confirmed separate project)

- The API has its **own** `Program.cs` and pipeline. It does not use cookie
  auth, antiforgery, sessions, Razor views, tenant-theme middleware or the MVC
  error pages.
- The MVC site is untouched and keeps its current behaviour.
- `ApiControllerBase` declares
  `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`
  and `[ApiController]`.
- No `[IgnoreAntiforgeryToken]`, no `MapControllers()` change, no MVC pipeline
  edits are needed — those concerns only existed for the in-project option.
- **Optional API key** for server-to-server calls (`X-Api-Key`), hashed in
  config, constant-time compare. Phase 4.

---

## 11. Validation, errors, paging, versioning

- Request DTOs use DataAnnotations + explicit checks; `[ApiController]`
  automatically returns 400 ProblemDetails.
- Unknown/invalid enum names → 400.
- `page` ≥ 1, `pageSize` 1–100 (clamped), `sortBy` whitelisted.
- Response envelope for lists:

```json
{ "items": [ … ], "page": 1, "pageSize": 20, "totalCount": 137, "totalPages": 7 }
```

- Single resources return the object directly.
- Created resources: `201 Created` + `Location` header.
- Deletes: `204 No Content`.
- Errors: RFC 7807 `ProblemDetails` with `type`, `title`, `status`, `detail`,
  `instance`, `traceId`, and `errors` for validation.
- Versioning: URL segment `/api/v1`. A future `/api/v2` can coexist.
- JSON: camelCase, enums as strings, nulls omitted, `DateTime` in UTC ISO 8601.

---

## 12. Configuration

`Kwikby_MP.Api/appsettings.json` (values overridden by
`appsettings.Development.json`, `appsettings.Local.json` or environment
variables):

```json
"ConnectionStrings": {
  "MultiVendorsStoresConn": ""
},
"Api": {
  "Enabled": true,
  "CommandTimeoutSeconds": 30,
  "SlowCommandMs": 1000,
  "Jwt": {
    "Issuer": "kwikby_MP",
    "Audience": "kwikby_MP.Api",
    "SigningKey": "",
    "AccessTokenMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "RateLimit": {
    "PermitLimit": 100,
    "WindowSeconds": 60
  }
}
```

- `SigningKey` is **never** committed. For local development it goes in
  `Kwikby_MP.Api/appsettings.Local.json`; in production it comes from an
  environment variable or secret store. The app fails fast if it is missing or
  shorter than 32 bytes in Production.
- `ConnectionStrings:MultiVendorsStoresConn` is the only SQL connection the
  API uses. The MVC `Database:Provider`/SQLite fallback is irrelevant here:
  if the connection string is empty, the API returns 503 (see §4.3).
- Do **not** copy the SQL password into source. Locally, either put it in the
  gitignored `Kwikby_MP.Api/appsettings.Local.json` or set
  `ConnectionStrings__MultiVendorsStoresConn`.

---

## 13. API pipeline (new project — no MVC changes)

Because the API is its own project, the MVC pipeline is **not touched**. The
API's `Program.cs` contains only:

1. Configuration + options (`Api`, `Api:Jwt`, connection string).
2. `AddControllers()` with JSON options (camelCase, string enums), a global
   `ApiExceptionFilter`, and `AddProblemDetails()`.
3. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`
   plus the policies in §10.
4. `AddAuthorization`.
5. `AddCors("ApiCors")` from `Api:Cors:AllowedOrigins`.
6. `AddRateLimiter` with an `api` policy (per token/IP) returning ProblemDetails
   on 429.
7. `AddSwaggerGen` + JWT security definition; Swagger UI enabled in
   Development only (or behind auth in production).
8. `AddHealthChecks().AddSqlServer(...)`.
9. `MapControllers()`, `MapHealthChecks("/health/live", "/health/ready")`,
   `UseSwagger`/`UseSwaggerUI` (dev), `UseCors`, `UseRateLimiter`,
   `UseAuthentication`, `UseAuthorization`, `MapControllers`.

Security headers (`X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy`, HSTS) are applied by a tiny middleware in the API project,
reusing the same values as the MVC site.

No `AutoValidateAntiforgeryToken`, no Razor, no session, no tenant themes.

---

## 14. Logging, audit, observability

- Every request gets a `traceId` (`HttpContext.TraceIdentifier`) returned in
  ProblemDetails and logged.
- API writes pass `@Actor` to the procedure, which inserts into `AuditLogs`
  in the same transaction.
- Log slow procedures (> `Api:SlowCommandMs`, default 1000 ms) with the
  procedure name and duration (no parameters containing secrets).
- Reuse the existing `/health` endpoints; add a SQL Server connectivity check
  tagged `api` if needed.
- Optional: OpenTelemetry later; not in v1.

---

## 15. Testing strategy

| Layer | What | How |
|---|---|---|
| Unit | DTO validation, paging clamp, sort whitelist, SQL error → HTTP mapping | xUnit + Moq |
| Unit | Controllers with a fake `IStoredProcedureGateway` | xUnit |
| Contract | Every `usp_*` named in code exists in the DB; parameter names/types match | query `sys.procedures` / `sys.parameters` with a test connection; skipped if no SQL Server |
| Integration | Apply `02`/`04` to a scratch DB, seed minimal rows, call API via `WebApplicationFactory` | xUnit + `WebApplicationFactory<Program>` |
| Smoke | Token → list products → create category → adjust stock → read back | `docs/api-smoke.ps1` with `Invoke-RestMethod` |
| Security | 401 without token, 403 for wrong role, 403 for other shop's order, 400 for bad input, 429 after limit | integration tests |

SQL Server integration tests are skipped unless
`ConnectionStrings:MultiVendorsStoresConn` (or `API_TEST_SQL`) is set, so the
normal `dotnet test` run still passes on a machine without SQL Server.

---

## 16. Phased delivery

| Phase | Deliverable | Est. |
|---|---|---|
| **0. Foundations** | `Kwikby_MP.Shared` + `Kwikby_MP.Api` projects, solution/CI wiring, packages, options, connection factory, gateway, JWT, Swagger, ProblemDetails, API pipeline, health | 1–2 days |
| **1. Read API** | `02` 2014 rewrite + `04` auth/read procs; auth token/me, products search/detail, categories list, shops list/get, admin + shop dashboards | 1–2 days |
| **2. Catalogue writes** | `04` procs + product/variant/image/category/shop CRUD, inventory, shop scoping | **Done** |
| **3. Commerce** | orders list/get/status, customers, coupons, reviews, returns, payouts, audit; hybrid money workflows via existing services | **In progress** (read/status done; order placement via IOrderService pending) |
| **3b. Optional** | money procedures (`usp_Order_Create`, payments, ledger) if strict mode is chosen later | 3–5 days |
| **4. Polish** | settings/translations/reports/hero, API key auth, load test, security review, Docker, docs, smoke script | 2–3 days |

**Phase 0 + 1 detail (the approved starting point):**

Phase 0:
- Create `Kwikby_MP.Shared` (net9.0 class library; linked password hasher) and
  `Kwikby_MP.Api` (net9.0 Web SDK), add both to `Kwikby_MP.sln`.
- Add packages: Dapper 2.1.66, Microsoft.Data.SqlClient 5.1.6,
  Swashbuckle.AspNetCore 9.0.4, Microsoft.AspNetCore.Authentication.JwtBearer
  9.0.9.
- Implement `ApiOptions`, `JwtOptions`, `SqlConnectionFactory`,
  `IStoredProcedureGateway`/`StoredProcedureGateway`, `SqlErrorTranslator`,
  `JwtTokenService`, `ApiControllerBase`, ProblemDetails, Swagger, CORS,
  rate limiting, security headers, health checks.
- Add `Kwikby_MP.Api/appsettings.json` + `appsettings.Development.json` +
  `Properties/launchSettings.json` (port 5269).
- Add `Database/05_Deploy.ps1` and rewrite `Database/02_StoredProcedures.sql`
  for SQL Server 2014.
- Add `Kwikby_MP.Api.Tests` with gateway/error-mapping/auth unit tests.

Phase 1:
- Add `Database/04_ApiStoredProcedures.sql` with `usp_Auth_*`,
  `usp_Category_List`, `usp_Shop_List`, `usp_Shop_Get` (the product/dashboard
  procs already exist in `02`).
- Implement `AuthController` (`POST /auth/token`, `GET /auth/me`).
- Implement `ProductsController` (`GET /products`, `GET /products/{id}`).
- Implement `CategoriesController` (`GET /categories`).
- Implement `ShopsController` (`GET /shops`, `GET /shops/{id}`).
- Implement `DashboardController` (`GET /admin/dashboard`,
  `GET /shops/{id}/dashboard`).
- Add DTOs, mappings, repository classes, Swagger annotations.
- Add `docs/API_GUIDE.md` and `docs/api-smoke.ps1`.
- Verify against the live `MVStores` database in a scratch database first;
  do not run write procs against production data in Phase 1.

Each phase ends with: both projects build, tests green, the SQL deployed to a
scratch DB, and a short changelog entry.

---

## 17. Deliverables

- `Kwikby_MP.Api/` — new Web API project (controllers, DTOs, filters,
  mappings, repositories, infrastructure).
- `Kwikby_MP.Shared/` — small shared library (linked password hasher,
  shared enums/contracts).
- `Kwikby_MP.sln` — both projects added.
- `Database/02_StoredProcedures.sql` — 2014-compatible rewrite.
- `Database/03_HeroEngine.sql` — 2014-compatible rewrite (if Hero in scope).
- `Database/04_ApiStoredProcedures.sql` — all new procedures.
- `Database/05_Deploy.ps1` — idempotent deploy runner.
- `Kwikby_MP.Api/appsettings.json` + `appsettings.Development.json` — `Api`
  section with empty secrets.
- `Kwikby_MP.Api/Properties/launchSettings.json` — dev port 5269.
- `.github/workflows/ci.yml` — build both projects and run both test projects.
- `docs/API_GUIDE.md` — endpoints, auth, examples, error codes.
- `docs/api-smoke.ps1` — end-to-end smoke test.
- `Tests/Kwikby_MP.Api.Tests/` — unit + integration + contract tests.

The existing `Kwikby_MP.csproj` / MVC code is **not modified** in Phase 0–1.

---

## 18. Risks and mitigations

| Risk | Mitigation |
|---|---|
| SQL Server 2014 lacks `CREATE OR ALTER` | 2014-compatible script pattern; `05_Deploy.ps1` verified on a scratch DB before the live one |
| Duplicating money logic in T-SQL | Hybrid mode (§7.6): reuse tested C# services for checkout/payment/ledger |
| API accidentally exposed without auth | `[Authorize]` on `ApiControllerBase`; a test asserts 401 for every non-public route |
| Vendor reads another shop's data | `ShopScope` filter + SQL `WHERE ShopId = @ShopId`; tests for cross-shop 403 |
| Two apps / duplicate configuration | API has its own `appsettings*.json`; only the SQL connection + JWT key are shared; documented in `API_GUIDE.md` |
| Shared code drift between MVC and API | password hasher is linked (single source) into `Kwikby_MP.Shared`; a test verifies a known hash |
| JWT secret committed | empty in `appsettings.json`; production guard; `.gitignore` already excludes `appsettings.Local.json` |
| EF and Dapper version conflicts | pin `Microsoft.Data.SqlClient` 5.1.6 to match EF 9.0.9 |
| Schema drift between EF and procs | procedures reference EF column names; contract test checks proc existence and parameters |
| `sa` used by the app | create a dedicated least-privilege login for the API; document the grant script |
| SQL Server Express limits | 10 GB DB / no SQL Agent; fine for current size; note for growth |
| Phase 3 service reuse | add a project reference or move the needed services into `Shared`; decide at the start of Phase 3 |

---

## 19. Decisions (confirmed)

| # | Decision | Choice |
|---|---|---|
| 1 | API location | **New `Kwikby_MP.Api` project** (separate process) |
| 2 | SQL Server version | **Keep 2014; write compatible SQL** |
| 3 | Auth | **JWT bearer only** for the API |
| 4 | Money workflows | **Hybrid** — procedures for reads/CRUD/stock, existing services for checkout/payment/ledger |
| 5 | Scope | **Phase 0 + Phase 1 first**, then review |
| 6 | Hero engine | **Out of Phase 0–1**; revisit in Phase 4 |
| 7 | Database name | Keep live **`MVStores`**; update `Database/README.md` |

### Resolved during Phase 0

| Item | Resolution |
|---|---|
| SQL login | A dedicated least-privilege login is documented in `API_GUIDE.md` §5.5. The live server has **not** been changed. |
| Dev port | API uses `5269`; the MVC site keeps `5268`, so both run side by side. |
| Swagger | Development-only. Production startup rejects the public dev JWT key. |

---

## 20. Delivery status (Phase 0 + Phase 1)

Delivered:

- `Kwikby_MP.Api` (ASP.NET Core 9, Dapper, stored procedures, JWT only) and
  `Kwikby_MP.Shared` (links the existing `Services/PasswordHasher.cs`).
- Twelve procedures deployed to the scratch database `MVStores_ApiTest`:
  `usp_Auth_GetUserForLogin`, `usp_Auth_GetUserClaims`, `usp_Auth_RecordLogin`,
  `usp_Auth_RecordFailedLogin`, `usp_Category_List`, `usp_Shop_List`,
  `usp_Shop_Get`, `usp_SearchProducts`, `usp_GetProductDetail`,
  `usp_GetShopDashboard`, `usp_GetPlatformDashboard`, `usp_AdjustStock`.
- Endpoints: `POST /api/v1/auth/token`, `GET /api/v1/auth/me`,
  `GET /api/v1/products`, `GET /api/v1/products/{id}`,
  `GET /api/v1/categories`, `GET /api/v1/shops`, `GET /api/v1/shops/{id}`,
  `GET /api/v1/admin/dashboard`, `GET /api/v1/shops/{id}/dashboard`.
- Health checks at `/health/live` and `/health/ready`; RFC 7807 errors with
  `traceId` on every problem response; rate limiting; CORS; security headers;
  slow-procedure logging; `Api:Enabled` maintenance switch.
- `Database/05_Deploy.ps1` (batch-aware, idempotent) and
  `Database/06_SeedApiTest.ps1` (refuses any catalog other than
  `MVStores_ApiTest`).
- Tests: **45/45 API tests** (unit + SQL contract against the scratch DB),
  **440/440 MVC tests**, and the real-process `docs/api-smoke.ps1`.
- Documentation: `docs/API_GUIDE.md`, `Database/README.md`, CI updated to build
  the solution and run both test projects.

Deliberate deviations from the plan:

| Plan item | Delivered | Why |
|---|---|---|
| `Api:RoutePrefix` setting | Removed; routes are fixed at `/api/v1` | The setting was validated but unused. A dead setting is worse than a fixed, documented prefix. |
| `WebApplicationFactory` pipeline tests | Replaced by `docs/api-smoke.ps1` | The only cached `Mvc.Testing` is 8.0.0, whose TestHost response writer lacks `PipeWriter.UnflushedBytes`, which .NET 9 JSON serialization requires. |
| `Api:Enabled` / `Api:SlowCommandMs` | Implemented | They were present in the plan; both now have real behaviour (503 maintenance response; slow-procedure warning). |
| Scratch seed | Added `06_SeedApiTest.ps1` | Makes the smoke test reproducible and keeps the live database untouched. |
| MVC test project | Excludes `Tests/Kwikby_MP.Api.Tests/**` | The SDK glob would otherwise compile the API tests and their generated assembly attributes into the MVC test project. |
| One stale MVC test | Updated `Received_goods_restock_the_shops_listing_pool` | It still expected the pre-`StockSync` behaviour (listing-only restock). The service now restocks the variant and refreshes the listing total, which the adjacent test already asserted. |

Not done, by design:

- No SQL was applied to the live `MVStores` database.
- No Phase 2 write endpoints (product/category/shop CRUD, moderation, stock
  adjustment endpoint, order transitions).
- No Phase 3 checkout/payment/ledger work.
- No refresh token or revocation list; access tokens last 60 minutes.

Review commands:

```powershell
dotnet build Kwikby_MP.sln
dotnet test Tests\WebDS_Test.Tests.csproj -p:RemoveTestHostAppHost=true
$env:API_TEST_SQL = 'Data Source=.;Initial Catalog=MVStores_ApiTest;Integrated Security=True;TrustServerCertificate=True'
dotnet test Tests\Kwikby_MP.Api.Tests\Kwikby_MP.Api.Tests.csproj -p:RemoveTestHostAppHost=true
./docs/api-smoke.ps1 -StartApi
```

Phase 2 starts only after this review.

---

## 21. Phase 2 + Phase 3 delivery status

Phase 2 (catalogue writes) and the read/status half of Phase 3 (commerce) are
implemented.

Additional procedures deployed to `MVStores_ApiTest`:
`usp_Category_Get/Create/Update/Delete`, `usp_Product_List/Get/Create/Update/SoftDelete/
SetPublished/SetModeration`, `usp_Variant_List/Create/Update/Delete`,
`usp_Image_List/Add/Delete`, `usp_Shop_Create/Update/SetStatus`,
`usp_Shop_User_List/Add/Remove`, `usp_Stock_List`, `usp_StockMovements_List`,
`usp_Order_List/Get/UpdateStatus`, `usp_Customer_List/Get`, `usp_Coupon_List/Get/
Create/Update/Delete`, `usp_Review_List/SetStatus/Delete`,
`usp_Return_List/SetStatus`, `usp_Payout_List/SetStatus`, `usp_Audit_List`.

Additional endpoints: see `API_GUIDE.md` �16.

Not yet done:
- Order placement via `IOrderService` (hybrid). The API now references the MVC
  project, so the service is available; the checkout DTO and transactional flow
  still need to be wired.
- Reviews creation by customers (`usp_Review_Create` not yet added).
- Phase 4 (settings, translations, reports, hero, API keys, load tests, security
  review, Docker).

Verification: `dotnet build Kwikby_MP.sln` succeeds; API tests 45/45; MVC tests
440/440; smoke passes.
