# RetailLab decision log

This log records established choices and distinguishes them from future work. Project-wide instructions in [AGENTS.md](../AGENTS.md) remain authoritative.

## D001 - Learning through small working features

Status: established.

Develop RetailLab as both a learning project and a portfolio demonstration using small, understandable vertical slices. Important responsibilities, data flow, alternatives, and failure cases must be explained and tested.

## D002 - Technology baseline

Status: established and partly implemented.

Use .NET 10, C#, ASP.NET Core with Razor Pages, WPF, Entity Framework Core, SQLite, xUnit, and Git. Tutorials 1 and 2 implement the console, Core, EF Core SQLite, migrations, and xUnit portions; Tutorial 3 adds the Razor Pages catalogue. WPF remains future work.

## D003 - Separate business rules, storage, and presentation

Status: established and implemented for Lab Prototype 1.

Core owns entities, business rules, services, and the `IRetailRepository` boundary. Data implements that boundary with EF Core. LabCli handles terminal input and output. Core references neither EF Core nor a UI framework.

## D004 - Data conventions and preservation

Status: established and implemented for Lab Prototype 1.

Use `decimal` for prices and UTC for persisted timestamps. Use migrations for schema changes. Startup applies migrations and seeds only an empty product table; it does not delete or recreate useful data.

The prototype database defaults to `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`. An optional `RETAILLAB_DATA_DIRECTORY` override supports isolated development runs.

## D005 - Framework-managed credentials and simulated commerce

Status: established; simulated ordering implemented.

Lab Prototype 1 uses the fixed label `customer-demo-001`, not an account. It stores no credentials or payment data. Later authentication must use the ASP.NET Core authentication framework. Orders remain demonstrations with no real payment processing.

## D006 - Lab Prototype 1 includes one persistent console retail flow

Status: approved and implemented; supersedes the earlier in-memory catalogue proposal.

The first prototype demonstrates persisted products, SKU, descriptions, decimal prices, inventory, bookmarks, simulated multi-line orders, stock reduction, insufficient-stock rejection, and order history through LabCli.

Reason: one end-to-end workflow demonstrates the intended separation between business behavior, persistence, and presentation while remaining small enough to inspect.

Consequences:

- Five fictional products are inserted only when the catalogue is empty.
- SKU is unique without regard to case.
- A bookmark is unique for a customer/product pair.
- Every order request is validated before stock changes.
- An order line snapshots SKU, description, and unit price.
- The selected currency remains unresolved, so the console shows two-decimal prices without a currency symbol.
- Multi-user concurrency protection is deferred because the prototype has one local console user.

## D007 - Defer synchronization design until local workflows exist

Status: established direction; design remains open.

Build demonstrated local workflows before selecting synchronization transport, server infrastructure, or conflict policies. Sharing Core models does not require the future server and desktop application to use one physical database.

## D008 - Staff product and inventory management in LabCli

Status: approved and implemented.

SKU is immutable after creation. Products are archived rather than deleted; archived products are hidden from customer-facing lists but retained with history. All stock changes are audited: order lines record adjustments with reason `Simulated order` and the customer identifier as actor, while staff corrections use `InventoryService` with an explicit signed delta, required reason, UTC timestamp, and actor identifier (`staff-demo-01` for the demonstration). No adjustment may be zero or drive stock negative. The staff workflow starts in LabCli; WPF remains a later slice. No new production packages were introduced. Existing data is preserved through an additive EF Core migration.

Consequences:

- `Product` carries `IsArchived` and `ArchivedAtUtc`; archive state is a business rule, not a delete.
- `InventoryAdjustment` is the single audit trail for stock movement.
- Order placement and staff adjustments share the same transactional stock-plus-audit pattern.
- Historic order-driven stock changes before this slice have no adjustment rows.

## D009 - Read-only Razor Pages customer catalogue

Status: approved and implemented (Tutorial 3).

Create `RetailLab.Web` with home, catalogue, and product-details pages over the existing Core and Data layers, sharing the same SQLite database and migration initializer as the console. `CatalogService` in Core owns the active-only lookup invariant; the Web-only `CatalogDisplayMapper` owns USD formatting and availability bands (`Out of stock` at 0, `Low stock` at 1-5, `In stock` above), keeping Core locale-free per D006. Razor views bind only to display models. Archived and unknown SKUs return the same 404. The home page shows a "catalogue preview" (first few products) since no featured-product rule exists. The single Core description is used honestly as the display name; no schema change was made for copy. No TestServer package: automated coverage is unit plus SQLite tests, with Razor compile errors caught by `dotnet build`. The test project references Web one-way for display tests. Runtime database files are git-ignored; the repository never stores them.

## D010 - Customer accounts on the standard Identity EF store, and sign-in-first favourites

Status: approved and implemented (Tutorial 4).

Use ASP.NET Core Identity's `UserManager`/`SignInManager` with cookie sign-in and the standard Entity Framework store over the framework-managed `AspNet*` tables. The Identity user id maps to Core's `CustomerIdentifier`, so favourites isolate per account while Core keeps no Identity dependency. Registration confirms email immediately (no mail sender exists); confirmed accounts are not required, failed sign-ins count toward the default lockout, and the default password policy applies.

Reason: the standard store keeps all security-sensitive persistence inside the maintained framework instead of RetailLab code. It adds one well-understood package (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) and the full Identity schema, which future roles or external logins can reuse unchanged.

Anonymous favourite flow correction: an anonymous POST to the `[Authorize]` Favourites page would be challenged before its handler runs, and Identity's login return would come back as GET, so the original POST must never be implied or replayed. Catalogue and product pages therefore render a "Sign in to save" link (local return URL, validated with `Url.IsLocalUrl`) for anonymous visitors, and Add/Remove POST forms only for signed-in customers. Every POST uses Post-Redirect-Get with TempData notices and validated return URLs (Add falls back to the product page or catalogue, Remove to `/Favourites`).

Consequences:

- `Data` references the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package; no custom store or manual account mapping exists.
- The `AspNet*` tables arrive through an additive migration; existing business data is untouched.
- Two-customer isolation, redirect fallbacks, and the standard Identity account flows (hashing, sign-in, generic credential errors, lockout) are covered by automated tests.

## D011 - Customer basket without reservation, with persistence-conflict translation

Status: approved and implemented (Tutorial 5A).

Each signed-in customer keeps a basket of product lines keyed by Identity user id. `BasketItem` carries a positive quantity plus a `Version` optimistic-concurrency token under the composite key `(CustomerIdentifier, ProductId)`. `BasketService` owns every basket rule — unknown and archived products, quantities, and integer overflow when merging — and reports all customer-correctable failures as `BusinessRuleException`, so PageModels catch one business type plus the conflict type. `EfRetailRepository.SaveBasketChangesAsync` translates expected `DbUpdateException` failures (concurrent first-add key collisions, and stale `Version` losses arriving as `DbUpdateConcurrencyException`) into the Core-owned `BasketConflictException`; the web layer shows "Your basket changed; please try again." and never references EF exception types.

Reason: simultaneous requests must never corrupt the basket or surface HTTP 500. A stale update or delete loses its `Version` check instead of silently overwriting another request, and a concurrent first add collides on the key instead of duplicating the line; both losers retry against the current basket.

Consequences:

- The basket reserves no inventory: out-of-stock products can be added and retained with the existing availability bands, and checkout (Tutorial 5B) enforces stock and prices.
- Catalogue cards add one item; quantity selection lives on the product page and quantity editing on the basket page. Updating to zero is rejected with a message pointing at the separate Remove action.
- Archived lines stay visible with a "No longer available" note; only removal works for them, through the all-products lookup rather than the active-only catalogue.
- Tutorial 5A basket edits have no idempotency keys: Post-Redirect-Get prevents refresh resubmission, but two rapid Add clicks can add twice. This is honest basket behavior for this slice — stock enforcement at checkout means no oversell.
- No new packages were introduced. The `BasketItems` table arrives through an additive migration; existing business data is untouched.

## D012 - Web checkout through shared staging with Order.Id idempotency

Status: approved and implemented (Tutorial 5B).

`OrderService` keeps its public `PlaceAsync` behavior and signature, but its validate-all-then-stage work moves into an internal `StageAsync` (optional prescribed order id, no save) shared by console ordering and the new `CheckoutService`. Web checkout stages through it, removes the customer's tracked basket lines, and commits order, lines, stock, adjustments, and basket deletion in a single `SaveCheckoutChangesAsync`, so a completed order can never strand an uncleared basket.

The basket page mints a server-generated checkout-attempt identifier per render that doubles as the new order's id: no idempotency table, no extra column, no cleanup. A sequential repeat returns the pre-checked order; a concurrent same-id race collides on the `Orders` key and the service re-reads committed state to return the winner (proven by a two-context SQLite test on the loser's own context). Every order lookup is scoped by customer id, so forged ids render 404 and leak nothing. Success follows Post-Redirect-Get to `/Orders/{id}`; `/Orders` and `/Orders/{id}` are fully `[Authorize]`; every order surface states that no payment was processed.

Consequences:

- Console ordering behavior and tests are unchanged; both ordering paths enforce identical rules from one implementation.
- `CheckoutConflictException` (Core-owned) covers stale versions and duplicate inserts; Web catches it and shows a friendly retry notice, never EF exception types.
- Order history and confirmation render through web display models with USD formatting at the edge, keeping Core locale-free.

## D013 - Product optimistic concurrency with a versioned migration

Status: approved and implemented (Tutorial 5B).

`Product` carries an integer `Version` token, initialized to zero and bumped after every successful `UpdateDetails`, `Archive`, `Unarchive`, and stock mutation, configured as an EF Core concurrency token. Two customers cannot both buy the final unit, and a concurrent price, archive, or stock change invalidates a stale checkout, which rolls back entirely. An additive `ProductVersion` migration backfills existing rows with zero; no data is rewritten or lost.

Consequences:

- `SaveProductChangesAsync` translates stale product versions into the Core-owned `ProductConflictException` for `ProductService`, `InventoryService`, and console `OrderService.PlaceAsync`; the console prints its friendly message. Staff and console conflicts are never labeled as checkout conflicts.
- `BookmarkService` keeps the raw save: bookmarks carry no concurrency token, and its pre-existing duplicate-add behavior is unchanged.
- Version races are covered by SQLite integration tests (staff update-vs-update, adjust-vs-adjust, console order-vs-order, final-unit checkout race, price/archive-after-load rollback); the in-memory test double enforces no concurrency by design.

## D014 - First WPF slice is local inventory adjustment

Status: approved and implemented (Tutorial 6A).

Create `RetailLab.Desktop` as a .NET 10 Windows WPF application whose first vertical slice lists exact inventory and applies audited stock adjustments. It uses the same local SQLite database, migrations, `InventoryService`, repository implementation, optimistic-concurrency behavior, and fixed demonstration-data conventions as the console. The desktop actor label is `staff-desktop-01`; it is not authentication.

Reason: inventory adjustment is already a complete, well-tested Core workflow and gives the desktop application a meaningful offline task without prematurely adding navigation, roles, synchronization, or a UI framework. XAML owns layout, a small display record owns staff-facing labels, and focused code-behind coordinates contexts and services without implementing business rules.

Consequences:

- Active and archived products are visible, but Core continues to reject adjustments to archived products.
- A new `RetailLabSqliteDatabase` Data helper centralizes local SQLite connection-string and DbContext construction; no production package was added.
- Product creation, detail editing, archive/unarchive controls, and adjustment-history UI remain later desktop slices.
- Automated tests continue to cover Core and SQLite behavior; XAML compilation is covered by the solution build, and layout/interaction require a Windows manual check.

## D015 - Desktop adjustment history in a focused window, still without MVVM

Status: approved and implemented (Tutorial 6B).

Open history from the inventory workspace through a `View adjustment history` button that is enabled for the selected product whether active or archived (disabled only when nothing is selected or the main window is busy). The button opens a focused modal `AdjustmentHistoryWindow` owned by the main window; the existing Apply-adjustment control keeps its archived-product restriction unchanged.

Reason: history is a read-only companion to the adjustment workflow, not a new workflow: a separate window keeps the adjustment panel small, shows the selected product and its complete UTC audit trail (newest first) without navigation state, and leaves archived-product history viewable while archived products stay unadjustable. XAML plus focused code-behind remains sufficient: the window only coordinates a fresh DbContext, the existing `InventoryService.GetHistoryAsync`, and display-row mapping, with newest-first sorting as a presentation concern. Introducing an MVVM framework for two windows would add learning surface without demonstrated need; revisit when the desktop application gains several screens or richer shared state.

Consequences:

- Core and Data are unchanged: no schema migration, no business-rule change, no new package.
- `InventoryAdjustmentRow` keeps WPF formatting (UTC and signed-change display) out of Core and carries no rules.
- History failures show safe staff-facing text without raw exception details; an empty history shows a friendly empty state.
- XAML compilation is covered by the solution build; layout and interaction require a Windows manual check.

## D016 - Desktop product creation plus explicit refresh; edit/archive deferred

Status: approved and implemented (Tutorial 6C).

Give the inventory workspace a compact action area with `New product` and `Refresh inventory`. Creation opens a focused modal `CreateProductWindow` (SKU, description, price, initial stock) that validates for immediate friendly feedback and persists only through `ProductService.CreateAsync`; success closes the dialog, refreshes the grid, and selects the new SKU. Refresh re-runs the existing inventory load with a fresh DbContext and preserves the selected SKU when it still exists.

Reason: creation is the next self-contained Core workflow after adjustment and history: one service call, no navigation state, and duplicate-SKU protection already tested. Pairing it with Refresh answers a real multi-interface problem observed in manual testing — the console can change a product while Desktop is open, and Core's concurrency protection keeps data safe but leaves the desktop row stale until reloaded. Editing and archive/unarchive stay deferred because they need harder UX design: stale-form concurrency (another interface changing the product while the form is open) and destructive-action confirmation deserve their own slice (proposed 6D).

Consequences:

- Core and Data are unchanged: no schema migration, no business-rule change, no new package, no MVVM framework.
- The window passes values through untouched; Core trims, enforces lengths and nonnegative price/stock, preserves SKU casing, and rejects case-insensitive duplicates (NOCASE collation plus a pre-check).
- Initial stock is a starting value, not an adjustment: creation writes no inventory-adjustment record, matching the existing console behavior.
- UI prechecks are friendly feedback only; Core remains the final authority, and service-thrown validation maps back to fields without raw messages.
- XAML compilation is covered by the solution build; creation, duplicate, validation, and refresh flows require a Windows manual check.

## D017 - Expected-version stale-form protection plus explicit archive confirmation

Status: approved and implemented (Tutorial 6D).

Teach `ProductService` optional expected-version checks: `UpdateDetailsAsync`, `ArchiveAsync`, and `UnarchiveAsync` gain overloads taking the version the caller saw, while the existing versionless signatures keep working for the console. Desktop carries the internal `Version` on `InventoryProductRow` (never displayed) and passes it from the selected row or open form. The service loads the current product, compares versions before mutating, throws `ProductConflictException` on mismatch without mutating or saving, and still relies on the EF concurrency token for races between the check and the save. Archiving asks an explicit Yes/No confirmation (default No) in plain language; unarchiving needs none.

Reason: EF's save-time token alone cannot protect a stale form. Desktop creates a fresh DbContext per operation, so the operation always loads the current version and the token always passes — the staleness lives outside the DbContext, in the displayed row or the values typed against it. Comparing the UI-captured version before mutating closes that gap with one shared workflow per operation (thin overloads over a private core), no schema change, and no caller-visible storage details. The confirmation exists because archiving changes what customers see; its text states the catalogue, favourites, basket, history, and reversibility effects explicitly so staff never mistake archive for delete.

Consequences:

- Versionless LabCli calls and all existing race tests behave exactly as before; four new Core tests prove match-success plus stale update/archive/unarchive rejection without mutation or save.
- Conflicts never overwrite the winner and never retry automatically: the grid refreshes and staff review current values before acting again.
- Archive stays reversible: stock, orders, baskets, and history are retained, and unarchiving restores customer visibility under existing web rules.
- XAML compilation is covered by the solution build; edit, confirmation, stale-form, and stale-action flows require a Windows manual check.

## Open decisions

| Question | Resolve before |
| --- | --- |
| Permanent retailer brand and multi-currency support (Tutorial 3 uses temporary name RetailLab and USD-at-edge) | Storefront hardening |
| Stock adjustment reason taxonomy (free text vs enum) and actor roles | Protected web workflows |
| Staff permissions and roles (customer password sign-in is done) | Protected web workflows |
| Server storage, data ownership, synchronization transport, and conflict policy | Synchronization implementation |
