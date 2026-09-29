# RetailLab architecture

This document describes the implemented repository and keeps implemented behavior separate from planned features. Project rules are defined in [AGENTS.md](../AGENTS.md).

## Current solution

All projects target .NET 10 with nullable reference checking and implicit imports enabled.

| Location | Current responsibility |
| --- | --- |
| `src/RetailLab.Core` | Retail entities, validation, business workflows, customer catalogue lookups, and the persistence boundary. It has no EF Core or UI dependency. |
| `src/RetailLab.Data` | EF Core SQLite context, mappings, repository, migrations, and idempotent sample-data initialization. |
| `src/RetailLab.LabCli` | Interactive Lab Prototype 1 console interface plus staff product and inventory workflow, and application startup. |
| `src/RetailLab.Web` | Customer storefront (Razor Pages): home, catalogue, product details, Identity-backed accounts, per-customer favourites, and a per-customer basket over the same Core and Data layers. |
| `tests/RetailLab.Tests` | Core unit tests, Web display and redirect-policy tests, and SQLite integration tests (including standard Identity account flows and basket persistence conflicts). |

No Desktop project exists yet.

## Project dependencies

```text
RetailLab.Core   -> no other projects
RetailLab.Data   -> RetailLab.Core, EF Core SQLite
RetailLab.LabCli -> RetailLab.Core, RetailLab.Data
RetailLab.Web    -> RetailLab.Core, RetailLab.Data
RetailLab.Tests  -> RetailLab.Core, RetailLab.Data, RetailLab.Web (for display-model tests only)
```

Core therefore remains reusable by later Razor Pages and WPF interfaces without depending on those technologies or on EF Core. The test project references Web only so mapper and redirect-policy tests compile against web models; the reference points one way and production code is unaffected. Data references the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package for the standard Identity store.

## Lab Prototype 1 model

```text
Product 1 --------< Bookmark
   |
   +--------------< BasketItem
   |
   +--------------< OrderLine >-------- 1 Order
   |
   +--------------< InventoryAdjustment
```

- `Product` owns SKU (immutable), description, price, stock, archive state, and stock-mutation rules.
- `Bookmark` joins the fixed simulated customer to a product.
- `BasketItem` joins a customer to a product with a positive quantity, timestamps, and a `Version` optimistic-concurrency token.
- `Order` owns its lines and calculates its total.
- `OrderLine` records product identifiers plus SKU, description, and unit-price snapshots.
- `InventoryAdjustment` records one stock change with signed quantity delta, resulting quantity, required reason, actor identifier, and UTC timestamp.
- `BookmarkService` coordinates add/remove behavior and rejects archived products for new bookmarks.
- `BasketService` owns basket rules (unknown and archived products, quantities, merge overflow) and reports customer-correctable failures as `BusinessRuleException`; expected persistence conflicts surface as the Core-owned `BasketConflictException`.
- `OrderService` validates a complete request before changing stock, rejects archived products, creates the order, records one `InventoryAdjustment` per line with reason `Simulated order`, and commits through `IRetailRepository`.
- `ProductService` coordinates product creation, detail updates, archiving, and unarchiving.
- `InventoryService` validates staff adjustments (including reason and actor) before mutating stock, appends the audit record, and exposes adjustment history.
- `CatalogService` owns customer-visible lookups: active-only product lists and single-product lookup returning null for missing or archived SKUs.
- `IRetailRepository` is the Core-owned persistence boundary implemented by `EfRetailRepository` in Data.

## Website layer

`RetailLab.Web` adds no business rules. Its PageModels (`Index`, `Products`, `Product`, `Error`) call `CatalogService`, then translate entities through `CatalogDisplayMapper` into display models (`ProductSummary`, `ProductDetails`) carrying pre-formatted USD prices and availability bands. Razor markup binds only to display models, never to entities or EF types. The single Core description is used honestly as the display name.

Startup resolves the SQLite path exactly like LabCli (`RETAILLAB_DATA_DIRECTORY` override, otherwise `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`), registers the DbContext scoped per request, and runs the shared `DatabaseInitializer` (migrate then idempotent seed). A startup database failure is logged and prevents startup with a plain console message; request-time failures render the friendly `/Error` page.

## Customer accounts and favourites

`Program.cs` registers authorization and the standard `AddIdentity<IdentityUser, IdentityRole>` with the Entity Framework store (unique email, confirmed accounts not required, default cookie paths). No external logins exist. `AddEntityFrameworkStores<RetailLabDbContext>` persists accounts to the standard framework-managed `AspNet*` tables; `UserManager` and `SignInManager` own hashing, validation, and cookie sign-in, so no credential logic lives in RetailLab code. The Identity user id is the `CustomerIdentifier` for bookmarks, which isolates favourites per account. Core never sees Identity types.

The Favourites PageModel is fully `[Authorize]` with `OnGetAsync`, `OnPostAddAsync`, and `OnPostRemoveAsync` handlers. Anonymous visitors never receive a form: catalogue and product pages render a "Sign in to save" link to `/Account/Login` with the current path as a local return URL. This matters because an anonymous POST would be challenged before its handler runs, and Identity's login return would replay only a GET — so the original POST must never be implied. After login the customer returns to the originating page and explicitly presses Add. Every POST ends in Post-Redirect-Get with a TempData notice rendered by the layout; return URLs pass through `Url.IsLocalUrl` via the unit-tested `FavouriteRedirects` policy (Add falls back to the product page or catalogue, Remove to `/Favourites`).

## Customer basket

The Basket PageModel follows the same shape as Favourites but is fully `[Authorize]` with `OnGetAsync`, `OnPostAddAsync`, `OnPostUpdateAsync`, and `OnPostRemoveAsync` handlers. Catalogue cards post quantity 1, the product page posts the customer's chosen quantity, and the basket page posts quantity edits and removals; anonymous visitors get a "Sign in to add to basket" link instead of forms, for the same never-replay reason. The service owns every rule, so handlers only bind, invoke `BasketService`, map `BusinessRuleException` messages and `BasketConflictException` retries to TempData notices, and redirect. Return URLs pass through the PageModel's private `RedirectToLocal` (`Url.IsLocalUrl`, never the favourites-named helper): Add falls back to the product page or catalogue, Update and Remove to `/Basket`.

Basket lines render through the `BasketLine` display model (unit price, line total, availability band, archived flag) mapped by `CatalogDisplayMapper`, keeping USD formatting out of Core. Archived lines show "No longer available" with removal as the only action. The basket reserves nothing: out-of-stock products stay addable and retained, and a neutral note says stock and prices are confirmed when ordering. There is no checkout button and no header basket count yet.

`SaveBasketChangesAsync` is the only basket commit path: it translates EF `DbUpdateException` failures — concurrent first-add key collisions and stale-`Version` losses — into `BasketConflictException`, so simultaneous requests retry with a friendly notice instead of corrupting the basket or surfacing HTTP 500. There are no idempotency keys: Post-Redirect-Get stops refresh resubmission, but two rapid Add clicks can add twice; checkout (Tutorial 5B) enforces stock, so no oversell follows.

## Database

`RetailLabDbContext` maps thirteen SQLite tables: six RetailLab business tables plus seven framework-managed Identity tables.

- `Products`, with a case-insensitive unique SKU, nonnegative price and stock constraints, plus `IsArchived` and nullable `ArchivedAtUtc`.
- `Bookmarks`, with `(CustomerIdentifier, ProductId)` as its composite primary key. Customer bookmark queries exclude archived products.
- `BasketItems`, with `(CustomerIdentifier, ProductId)` as its composite primary key, a positive-quantity check constraint, and `Version` as an optimistic-concurrency token. Customer basket queries include products in one query with no archived filter.
- `Orders`, indexed by customer identifier and placement time.
- `OrderLines`, with positive-quantity and nonnegative-price constraints.
- `InventoryAdjustments`, with nonzero quantity-change and nonnegative resulting-quantity constraints, required reason and actor, indexed by product and creation time, referencing products with restricted deletes.

- `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserRoles`, and `AspNetUserTokens`: the standard framework-managed Identity schema. Business tables hold no credentials and no foreign keys to these tables.

Migrations are stored in `RetailLab.Data/Migrations`. Application startup applies pending migrations, then inserts the fictional sample catalogue only if no products exist. It never recreates the database. Existing data survives the product-archive and adjustments migration unchanged; historic order-driven stock changes before Tutorial 2 have no adjustment rows.

The runtime database file (`retaillab.db` plus SQLite WAL/shared-memory sidecars) is local demonstration data and is git-ignored; it must never live inside the repository.

SQLite cannot translate ordering by `DateTimeOffset`. Order and adjustment history are filtered in the database and sorted in memory after loading; this is acceptable for the deliberately small prototype. Persisted timestamps are UTC.

## Runtime data flow

Console:

```text
Console input
    -> Core service and business validation
    -> IRetailRepository
    -> EF Core DbContext
    -> SQLite
```

Website:

```text
Browser GET
    -> PageModel -> CatalogService (Core active-only rules)
    -> IRetailRepository -> EF Core DbContext
    -> SQLite
    -> display-model mapping
    -> Razor HTML + local CSS
```

Browser POST (a signed-in favourite change):

```text
Browser POST + antiforgery token
    -> auth middleware ([Authorize] already passed)
    -> Favourites PageModel -> BookmarkService (Core rules, user id scoped)
    -> IRetailRepository -> EF Core DbContext
    -> SQLite
    -> TempData notice + 302 redirect (validated return URL or fallback)
```

Browser POST (a signed-in basket change):

```text
Browser POST + antiforgery token
    -> auth middleware ([Authorize] already passed)
    -> Basket PageModel -> BasketService (Core rules, user id scoped)
    -> IRetailRepository.SaveBasketChangesAsync -> EF Core DbContext
    -> SQLite (key collision or stale Version -> BasketConflictException)
    -> TempData notice + 302 redirect (validated return URL or fallback)
```

A successful order tracks stock changes, inventory adjustments, a new order, and all new lines in one DbContext. `SaveChangesAsync` persists them in one relational transaction. A staff adjustment tracks the stock change and its audit record the same way. Invalid or insufficient-stock requests fail before any product is changed; invalid reasons or actors fail before stock is mutated.

The database defaults to `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`. The `RETAILLAB_DATA_DIRECTORY` environment variable can select an isolated location for development, and both the console and the website honor it so they can deliberately share one local database.

## Deferred architecture

Web checkout (including checkout idempotency), staff administration and roles, WPF, synchronization, search, pagination, deployment, and SaaS tenancy are not implemented yet. Shared models do not imply that future server and offline desktop applications will share one physical database file.
