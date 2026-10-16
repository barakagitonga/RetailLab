# RetailLab architecture

This document describes the implemented repository and keeps implemented behavior separate from planned features. Project rules are defined in [AGENTS.md](../AGENTS.md).

## Current solution

All projects target .NET 10 with nullable reference checking and implicit imports enabled.

| Location | Current responsibility |
| --- | --- |
| `src/RetailLab.Core` | Retail entities, validation, business workflows, customer catalogue lookups, typed transport-independent synchronization contracts, and the persistence boundary. It has no EF Core or UI dependency. |
| `src/RetailLab.Data` | EF Core SQLite context, mappings, repository, migrations, and idempotent sample-data initialization. |
| `src/RetailLab.Sync` | UI-independent HTTP transport and orchestration for read-only catalogue bootstrap and incremental pull. |
| `src/RetailLab.LabCli` | Interactive Lab Prototype 1 console interface plus staff product and inventory workflow, application startup, and the headless `sync` command. |
| `src/RetailLab.Web` | Customer storefront (Razor Pages): home, catalogue, product details, Identity-backed accounts, per-customer favourites, a per-customer basket, and simulated basket checkout with order history over the same Core and Data layers. |
| `src/RetailLab.Desktop` | Windows WPF staff interface: exact local inventory, one audited stock-adjustment workflow, read-only adjustment-history viewing, product creation, explicit inventory refresh, detail editing, and archive controls over Core and Data. |
| `tests/RetailLab.Tests` | Core unit tests, Web display and redirect-policy tests, and SQLite integration tests (including standard Identity account flows, basket persistence conflicts, and checkout plus product-concurrency races). |

## Project dependencies

```text
RetailLab.Core   -> no other projects
RetailLab.Data   -> RetailLab.Core, EF Core SQLite
RetailLab.Sync   -> RetailLab.Core
RetailLab.LabCli -> RetailLab.Core, RetailLab.Data, RetailLab.Sync
RetailLab.Web    -> RetailLab.Core, RetailLab.Data
RetailLab.Desktop-> RetailLab.Core, RetailLab.Data
RetailLab.Tests  -> RetailLab.Core, RetailLab.Data, RetailLab.Sync, RetailLab.Web
```

Core therefore remains reusable by later Razor Pages and WPF interfaces without depending on those technologies or on EF Core. The test project references Web for mapper/redirect-policy tests and Sync for client transport/orchestration tests; both references point from Tests to production projects. Data references the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package for the standard Identity store.

## Desktop layer

`RetailLab.Desktop` is a Windows-only WPF presentation layer. `MainWindow` loads staff-facing `InventoryProductRow` display records, creates a fresh DbContext for each load or adjustment, and calls the existing `InventoryService`. It contains input parsing and status-message presentation, but no stock rule: zero changes, negative-result prevention, archived-product rejection, audit creation, and concurrency protection remain below the UI.

Adjustment-history viewing (Tutorial 6B) follows the same pattern without touching Core or Data: `MainWindow` opens a focused modal `AdjustmentHistoryWindow` owned by the main window for the selected product, active or archived. The history window receives the selected `InventoryProductRow` plus the existing DbContext factory, creates a fresh short-lived DbContext, loads through `InventoryService.GetHistoryAsync`, and maps the result to read-only `InventoryAdjustmentRow` display records. The repository returns oldest-first, so the window sorts newest-first in the presentation layer; an empty result shows a friendly empty state, and failures show safe staff-facing text. No schema, package, or business-rule change was needed.

Product creation and explicit refresh (Tutorial 6C) reuse Core and Data unchanged: a compact action area above the inventory grid offers New product and Refresh inventory. Refresh re-runs the existing load with a fresh DbContext, preserving the selected SKU when it still exists, so description, price, stock, and archive changes made through another interface become visible with correct button availability. New product opens a focused modal `CreateProductWindow` that validates input for immediate friendly feedback, then persists only through `ProductService.CreateAsync`; Core trims values, enforces lengths and nonnegative price/stock, preserves the entered SKU casing, and rejects case-insensitive duplicate SKUs. Initial stock is the product's starting value and creates no inventory-adjustment record. No schema, package, or business-rule change was needed.

Product editing and archive controls (Tutorial 6D) add the first Core change in the desktop series: backward-compatible expected-version overloads on `ProductService` plus an internal `Version` on `InventoryProductRow`. Desktop calls the overloads with the version captured in its selected row or open form; the service compares it against the freshly loaded product before mutating and throws `ProductConflictException` on mismatch without mutating or saving. This pre-check complements the existing EF save-time concurrency token: a fresh per-operation DbContext always loads the current version, so EF alone cannot see staleness that predates the operation. Archiving asks an explicit Yes/No confirmation explaining catalogue, favourites, basket, history, and reversibility effects; conflicts refresh the grid automatically with friendly retry guidance. No schema, package, or MVVM change was needed.

The first slice intentionally uses XAML plus focused code-behind rather than adding an MVVM framework. This keeps the learning surface small while the code-behind acts only as a presentation coordinator. If the desktop application gains several screens or richer state, a dedicated view-model layer can be introduced based on demonstrated need.

`RetailLabSqliteDatabase` in Data owns local SQLite connection-string and DbContext construction. LabCli, Web, and Desktop use it from their composition roots, while each interface still decides how to obtain its configured data directory. Desktop startup runs the shared migration-and-seed initializer before showing the window.

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

- `Product` owns SKU (immutable), description, price, stock, archive state, stock-mutation rules, a database-local `Version` optimistic-concurrency token, a separate authoritative non-negative `ServerRevision`, and support for prescribed non-empty GUID construction alongside the default `Guid.NewGuid()`.
- `Bookmark` joins the fixed simulated customer to a product.
- `BasketItem` joins a customer to a product with a positive quantity, timestamps, and a `Version` optimistic-concurrency token.
- `Order` owns its lines and calculates its total.
- `OrderLine` records product identifiers plus SKU, description, and unit-price snapshots.
- `InventoryAdjustment` records one stock change with signed quantity delta, resulting quantity, required reason, actor identifier, and UTC timestamp.
- `BookmarkService` coordinates add/remove behavior and rejects archived products for new bookmarks.
- `BasketService` owns basket rules (unknown and archived products, quantities, merge overflow) and reports customer-correctable failures as `BusinessRuleException`; expected persistence conflicts surface as the Core-owned `BasketConflictException`.
- `OrderService` validates a complete request before changing stock, rejects archived products, creates the order, records one `InventoryAdjustment` per line with reason `Simulated order`, and commits through `IRetailRepository`. Its internal `StageAsync` (validate-all-then-stage, optional prescribed order id, no save) is shared by console ordering and web checkout.
- `CheckoutService` converts one customer's basket into an order through the shared staging, removes that customer's basket lines, and commits everything in one save; the checkout-attempt identifier doubles as the order id for idempotent resubmission.
- `ProductService` coordinates product creation, detail updates, archiving, and unarchiving.
- `InventoryService` validates staff adjustments (including reason and actor) before mutating stock, appends the audit record, and exposes adjustment history.
- `CatalogService` owns customer-visible lookups: active-only product lists and single-product lookup returning null for missing or archived SKUs.
- `IRetailRepository` is the Core-owned persistence boundary implemented by `EfRetailRepository` in Data.
- Expected persistence races surface as Core-owned conflicts: `BasketConflictException` (basket saves), `CheckoutConflictException` (checkout saves), `ProductConflictException` (staff and console product writes). Presentation layers catch these, never EF exception types.

## Website layer

`RetailLab.Web` adds no business rules. Its PageModels (`Index`, `Products`, `Product`, `Error`) call `CatalogService`, then translate entities through `CatalogDisplayMapper` into display models (`ProductSummary`, `ProductDetails`) carrying pre-formatted USD prices and availability bands. Razor markup binds only to display models, never to entities or EF types. The single Core description is used honestly as the display name.

Startup resolves the SQLite path exactly like LabCli (`RETAILLAB_DATA_DIRECTORY` override, otherwise `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`), registers the DbContext scoped per request, and runs the shared `DatabaseInitializer` (migrate then idempotent seed). A startup database failure is logged and prevents startup with a plain console message; request-time failures render the friendly `/Error` page.

## Customer accounts and favourites

`Program.cs` registers authorization and the standard `AddIdentity<IdentityUser, IdentityRole>` with the Entity Framework store (unique email, confirmed accounts not required, default cookie paths). No external logins exist. `AddEntityFrameworkStores<RetailLabDbContext>` persists accounts to the standard framework-managed `AspNet*` tables; `UserManager` and `SignInManager` own hashing, validation, and cookie sign-in, so no credential logic lives in RetailLab code. The Identity user id is the `CustomerIdentifier` for bookmarks, which isolates favourites per account. Core never sees Identity types.

The Favourites PageModel is fully `[Authorize]` with `OnGetAsync`, `OnPostAddAsync`, and `OnPostRemoveAsync` handlers. Anonymous visitors never receive a form: catalogue and product pages render a "Sign in to save" link to `/Account/Login` with the current path as a local return URL. This matters because an anonymous POST would be challenged before its handler runs, and Identity's login return would replay only a GET — so the original POST must never be implied. After login the customer returns to the originating page and explicitly presses Add. Every POST ends in Post-Redirect-Get with a TempData notice rendered by the layout; return URLs pass through `Url.IsLocalUrl` via the unit-tested `FavouriteRedirects` policy (Add falls back to the product page or catalogue, Remove to `/Favourites`).

## Customer basket

The Basket PageModel follows the same shape as Favourites but is fully `[Authorize]` with `OnGetAsync`, `OnPostAddAsync`, `OnPostUpdateAsync`, and `OnPostRemoveAsync` handlers. Catalogue cards post quantity 1, the product page posts the customer's chosen quantity, and the basket page posts quantity edits and removals; anonymous visitors get a "Sign in to add to basket" link instead of forms, for the same never-replay reason. The service owns every rule, so handlers only bind, invoke `BasketService`, map `BusinessRuleException` messages and `BasketConflictException` retries to TempData notices, and redirect. Return URLs pass through the PageModel's private `RedirectToLocal` (`Url.IsLocalUrl`, never the favourites-named helper): Add falls back to the product page or catalogue, Update and Remove to `/Basket`.

Basket lines render through the `BasketLine` display model (unit price, line total, availability band, archived flag) mapped by `CatalogDisplayMapper`, keeping USD formatting out of Core. Archived lines show "No longer available" with removal as the only action. The basket reserves nothing: out-of-stock products stay addable and retained, and a note says stock and prices are confirmed when ordering.

`SaveBasketChangesAsync` is the only basket commit path: it translates EF `DbUpdateException` failures — concurrent first-add key collisions and stale-`Version` losses — into `BasketConflictException`, so simultaneous requests retry with a friendly notice instead of corrupting the basket or surfacing HTTP 500. There are no idempotency keys on basket edits: Post-Redirect-Get stops refresh resubmission, but two rapid Add clicks can add twice; checkout enforces stock, so no oversell follows.

## Web checkout

The basket page mints a fresh server-generated checkout-attempt identifier per render and posts it with the "Place simulated order" form to the fully `[Authorize]` Basket page. `CheckoutService` validates the attempt id, returns the original order when the id already exists for the customer, rejects an empty basket, and otherwise stages through the shared `OrderService.StageAsync` (one implementation for web and console: combined requests, per-line existence/archive/stock checks against current data, current-price snapshots, stock reduction, one `Simulated order` adjustment per line). Basket-line removal joins the same tracked unit of work, and a single `SaveCheckoutChangesAsync` commits order, lines, stock, adjustments, and basket deletion in one relational transaction — or rolls everything back.

Duplicate handling needs no extra table: the attempt identifier *is* the new order's id. A sequential repeat hits the scoped pre-check; a concurrent same-id race collides on the `Orders` primary key, translates to `CheckoutConflictException`, and the service re-reads committed state to return the winner. A forged id belonging to another customer resolves to nothing at either lookup, so it can only produce a generic retry, never another customer's order.

Stale data fails safe: `Product.Version` (bumped by details, archive, and stock changes) and `BasketItem.Version` are concurrency tokens, so a price change, archive, stock move, basket edit, or racing checkout after load fails the save and the customer retries against current state. Two customers cannot both buy the final unit: the loser's product update matches zero rows.

Every POST ends in Post-Redirect-Get: success redirects to `/Orders/{id}`, rule and conflict failures redirect to `/Basket` with TempData notices. `/Orders` and `/Orders/{id}` are fully `[Authorize]`; the details page returns 404 for unknown and foreign ids alike. Orders render through `OrderSummary`/`OrderDetails` display models with USD formatting owned by `CatalogDisplayMapper`, and every order surface states that no payment was processed.

## Database

`RetailLabDbContext` maps eighteen SQLite tables: six RetailLab business tables, seven framework-managed Identity tables, plus five synchronization persistence tables.

- `Products`, with a case-insensitive unique SKU, nonnegative price and stock constraints, plus `IsArchived`, nullable `ArchivedAtUtc`, `Version` as an optimistic-concurrency token, and `ServerRevision` with default 0.
- `Bookmarks`, with `(CustomerIdentifier, ProductId)` as its composite primary key. Customer bookmark queries exclude archived products.
- `BasketItems`, with `(CustomerIdentifier, ProductId)` as its composite primary key, a positive-quantity check constraint, and `Version` as an optimistic-concurrency token. Customer basket queries include products in one query with no archived filter.
- `Orders`, indexed by customer identifier and placement time.
- `OrderLines`, with positive-quantity and nonnegative-price constraints.
- `InventoryAdjustments`, with nonzero quantity-change and nonnegative resulting-quantity constraints, required reason and actor, indexed by product and creation time, referencing products with restricted deletes.

- `AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserRoles`, and `AspNetUserTokens`: the standard framework-managed Identity schema. Business tables hold no credentials and no foreign keys to these tables.

- `SyncClientStates`, a singleton table (`Id = 1`) storing client device identity, paired server identifier, monotonic cursor (`LastPulledSequence`), pairing timestamp, and last successful sync timestamp.
- `SyncOutboxEntries`, durable local operations ordered by database-generated monotonic `LocalSequence`, unique `OperationId`, indexed `ProductId`, operation kind, serialized payload, base revision or predecessor id, processing state, and recorded result.
- `SyncInboxReceipts`, terminal server decision receipts keyed by `OperationId`, recording accepted/conflict/rejected outcome and result details for idempotent upload retries.
- `SyncChangeFeedEntries`, authoritative server change entries sequenced by database-generated monotonic `Sequence`, indexed `ProductId`, change kind, product server revision, payload, and UTC timestamp.
- `SyncServerProperties`, a server-only singleton identity row whose GUID remains stable when a deployment URL or port changes.

Migrations are stored in `RetailLab.Data/Migrations`. Application startup applies pending migrations and never recreates the database. Web migrates through the base context, seeds through `ServerRetailDbContext` so fresh products publish, then backfills any revision-zero products and ensures the stable server identity before serving requests. Desktop and LabCli keep their existing base-context initialization. Existing data survives migrations unchanged: `ProductArchiveAndAdjustments` preserved products, `ProductVersion` added `Version`, `SynchronizationFoundations` added `ServerRevision` and four foundation tables, and `SyncServerIdentity` adds the server singleton plus the unique `(ProductId, ServerRevision)` feed invariant.

The runtime database file (`retaillab.db` plus SQLite WAL/shared-memory sidecars) is local demonstration data and is git-ignored; it must never live inside the repository.

SQLite cannot translate ordering by `DateTimeOffset`. Order, adjustment, and outbox history rely on explicit sequences or monotonic keys for deterministic ordering; persisted timestamps are UTC.

## Synchronization foundations (Tutorial 7B)

Tutorial 7B introduces the typed contracts and persistence schema needed for later desktop-to-server synchronization, without introducing network transport or UI changes:

- **Identity & Revisions:** `Product` supports prescribed GUID identity (for future bootstrap imports) and keeps `Version` (local optimistic concurrency token) strictly separate from `ServerRevision` (authoritative server version). Normal mutations bump `Version` without altering `ServerRevision`. `ApplyServerRevision` allows applying authoritative server state while rejecting negative numbers and revisions that regress.
- **Causal Chains:** Transport-independent Core contracts represent client operations (`CreateProductOperation`, `UpdateProductDetailsOperation`, `ArchiveProductOperation`, `UnarchiveProductOperation`). Creation carries neither base revision nor predecessor; mutating operations require exactly one of `BaseServerRevision` (based on server state) or `PredecessorOperationId` (chained behind unsent local operations).
- **Snapshots & Change Feed:** `ProductSnapshot` captures authoritative product state at a specific server revision; `ServerChangeEntry` models ordered feed items; and `SyncOperationResult` models server evaluation outcomes (`Accepted`, `Conflict`, `Rejected`).
- **Persistence Boundary:** Serialization payloads, singleton client state, outbox queues, inbox receipts, and change feed rows live strictly in `RetailLab.Data`. Core remains completely free of HTTP, JSON, EF Core, and SQLite dependencies.

## Server feed and read-only API (Tutorial 7C-i)

- **Explicit server context:** only `ServerRetailDbContext` publishes. Its asynchronous save derives authoritative revisions and feed snapshots before one atomic EF Core save; its synchronous save path throws. A failed save restores the speculative revisions and detaches derived rows before rethrowing.
- **Existing-database transition:** fresh Web seed data publishes normally. A base-context startup service backfills revision-zero products exactly once while preserving GUIDs; the database unique index prevents duplicate per-product revisions during racing startups.
- **Stable identity:** `SyncServerProperties` stores one `ServerInstanceId`. Pairing returns that ID, current UTC time, and the feed head; the URL is not identity.
- **Read boundary:** `ISyncFeedReader` converts persistence rows into Core `ServerChangeEntry` contracts. `GET /api/sync/v1/pull` pages by global sequence, reports the head and `hasMore`, rejects negative or beyond-head cursors, and rejects a mismatched server identity.
- **Development trust boundary:** pair and pull require `RETAILLAB_SYNC_READ_KEY` through the `X-RetailLab-Sync-Key` header. The key is configuration-only and is never logged. This is not staff-device authentication, which remains required before uploads.

## Headless bootstrap and incremental pull (Tutorial 7C-ii)

- **Separated orchestration:** `RetailLab.Sync` depends only on Core. `HttpSyncTransport` owns HTTP, authentication headers, web JSON, timeouts, and server-error mapping; `SyncOrchestrator` owns pair-once and ordered paging policy.
- **Local storage boundary:** `EfLocalSyncStore` in Data creates a fresh base `RetailLabDbContext` per operation. It never uses the publishing server context. A pulled page, product projections, and `LastPulledSequence` commit in one SQLite transaction or all roll back.
- **Safe first pairing:** only a database with no products and no outbox rows may pair. The stable server GUID is stored once and is never silently replaced. Client startup migrates the schema but deliberately skips sample-data seeding.
- **Conservative pull:** pending local outbox work blocks download; feed entries must be contiguous and product revisions must advance exactly once. Server identity changes, beyond-head cursors, malformed responses, and gaps stop without moving the cursor.
- **Headless composition:** `RetailLab.LabCli sync` requires explicit `--server` and `--data-directory` values plus `RETAILLAB_SYNC_READ_KEY`. There are no automatic retries, uploads, background scheduling, or WPF integration in this slice.

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

Desktop adjustment:

```text
WPF selection + signed quantity + reason
    -> InventoryService (Core validation and audit creation)
    -> IRetailRepository.SaveProductChangesAsync
    -> EF Core DbContext
    -> local SQLite (product stock + InventoryAdjustment in one save)
    -> refreshed staff display + status message
```

Desktop history:

```text
WPF product selection
    -> AdjustmentHistoryWindow -> InventoryService.GetHistoryAsync (Core lookup)
    -> IRetailRepository.GetAdjustmentsAsync
    -> EF Core DbContext
    -> local SQLite (read-only product history)
    -> newest-first staff display rows + empty/error states
```

Desktop product creation:

```text
WPF creation dialog (SKU, description, price, initial stock)
    -> ProductService.CreateAsync (Core validation + duplicate check)
    -> IRetailRepository.SaveProductChangesAsync
    -> EF Core DbContext
    -> local SQLite (new product row; no adjustment record)
    -> refreshed inventory grid with the new product selected
```

Desktop product edit:

```text
WPF edit dialog (description, price + captured version)
    -> ProductService.UpdateDetailsAsync with expected version (Core pre-check)
    -> EF save-time concurrency token (post-check races)
    -> local SQLite (updated details, bumped version)
    -> refreshed inventory grid with the SKU reselected, or conflict refresh
```

Desktop archive/unarchive:

```text
WPF grid action (Yes/No confirmation for archive only)
    -> ProductService.ArchiveAsync/UnarchiveAsync with expected version
    -> EF save-time concurrency token (post-check races)
    -> local SQLite (flipped archive state, bumped version)
    -> refreshed grid, updated button availability, or conflict refresh
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

Browser POST (a signed-in checkout):

```text
Browser POST + antiforgery token + checkout-attempt id
    -> auth middleware ([Authorize] already passed)
    -> Basket PageModel -> CheckoutService (idempotent pre-check, shared staging, basket removal)
    -> IRetailRepository.SaveCheckoutChangesAsync -> EF Core DbContext
    -> SQLite, one transaction (stale Version or duplicate order id -> CheckoutConflictException)
    -> TempData notice + 302 redirect (/Orders/{id} on success, /Basket on failure)
```

A successful order tracks stock changes, inventory adjustments, a new order, and all new lines (and, for web checkout, basket deletions) in one DbContext. `SaveChangesAsync` persists them in one relational transaction. A staff adjustment tracks the stock change and its audit record the same way. Invalid or insufficient-stock requests fail before any product is changed; invalid reasons or actors fail before stock is mutated.

The database defaults to `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`. The `RETAILLAB_DATA_DIRECTORY` environment variable can select an isolated location for development, and the console, website, and desktop application honor it so they can deliberately share one local database.

## Deferred architecture

Staff authentication and roles, uploads, Desktop synchronization UI, background scheduling, search, catalogue pagination, deployment, and SaaS tenancy are not implemented yet. Web publishes an authoritative read-only feed and the headless LabCli client can consume it into a dedicated SQLite projection. Desktop remains local-only until push-then-pull is safe. Shared Core models never imply one shared physical file.
