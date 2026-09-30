# RetailLab product brief

RetailLab is a learning and portfolio project demonstrating a small business retail software solution. Development should produce understandable working features while teaching their design and implementation.

Project rules are defined in [AGENTS.md](../AGENTS.md). Architecture and implementation status are described in [ARCHITECTURE.md](ARCHITECTURE.md).

## Intended users

| User | Needs |
| --- | --- |
| Customer | Browse products, register and sign in, save favourites, and place simulated orders. |
| Store staff | Manage inventory through a Windows desktop application, including during offline operation. |
| Developer and portfolio reviewer | Understand the architecture, run demonstrations, and see evidence of tested behavior. |

## Lab Prototype 1: persistent console retail workflow

Status: implemented in `RetailLab.LabCli`.

The prototype demonstrates one simulated customer browsing a persistent catalogue, maintaining bookmarks, placing simulated orders, and viewing order history and remaining inventory. It uses Entity Framework Core and SQLite through `RetailLab.Data`; reusable entities and rules remain in `RetailLab.Core`.

The fixed customer identifier is `customer-demo-001`. It is only a label and is not authentication. The prototype stores no password or payment information and performs no real payment processing.

### Available actions

1. Display products with SKU, description, price, and stock.
2. Bookmark a product by SKU.
3. View bookmarked products.
4. Remove a bookmark by SKU.
5. Place a simulated multi-line order.
6. View previous simulated orders and their lines.
7. View remaining inventory (staff view, includes archived products).
8. Create product (staff).
9. Update product details (staff).
10. Archive or unarchive product (staff).
11. Adjust stock (staff).
12. View stock adjustment history (staff).

### Business behavior

- SKU and description are required.
- Product price and stock cannot be negative; a zero price is allowed.
- A product SKU cannot be changed after creation.
- Products are archived rather than permanently deleted. Archived products are hidden from customer-facing lists but retained with their history.
- Order quantities must be positive whole numbers.
- Repeated occurrences of the same SKU in one order are combined.
- Every line is checked before stock changes. If one line is invalid, archived, or lacks stock, the entire order is rejected and no stock is reduced.
- Successful orders reduce stock and save the order, its lines, and inventory updates together. Each order line also records an inventory adjustment with reason `Simulated order`.
- Order lines retain SKU, description, and unit-price snapshots for historical display.
- A customer cannot bookmark the same product more than once. Archived products cannot be bookmarked, and bookmarks of newly archived products disappear from customer bookmark lists.
- Stock changes outside order placement are represented by explicit inventory adjustments carrying a signed quantity change, a required reason, a UTC timestamp, and an actor identifier. The demonstration staff actor is `staff-demo-01`.
- An adjustment must not be zero and must not cause stock to become negative. Invalid reasons or actors fail before stock changes.
- Prices use `decimal`. No currency symbol is shown because a demonstration currency has not been chosen.

### Seed catalogue

The database receives five fictional products only when its product table is empty:

- Aurora insulated travel mug (`AUR-100`)
- Brindle recycled canvas tote (`BRI-210`)
- Cirrus compact desk lamp (`CIR-320`)
- Dovetail bamboo notebook (`DOV-430`)
- Emberline wireless speaker (`EMBER-540`)

Existing product, bookmark, order, and inventory data is not deleted or reset at startup.

### Running the prototype

From the repository root:

```powershell
dotnet run --project src/RetailLab.LabCli
```

By default the database is stored at:

```text
%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db
```

For an isolated development run, set `RETAILLAB_DATA_DIRECTORY` to another directory before starting the application.

## Tutorial 3: customer catalogue website

Status: implemented in `RetailLab.Web` (ASP.NET Core Razor Pages).

Customers visit a polished home page under the temporary store name **RetailLab**, browse active products, and open product details. The website shares the same SQLite database and migration initializer as the console, so both deliberately use one local demonstration database via `RETAILLAB_DATA_DIRECTORY`.

- Routes: `/` (home with a catalogue preview of the first few products), `/Products` (full catalogue), `/Products/{sku}` (details).
- Prices format as USD (for example `$24.95`).
- Availability shows customer-friendly bands instead of exact counts: `In stock`, `Low stock` (5 or fewer), `Out of stock`.
- Archived products are absent from lists and return 404 when requested directly, identical to an unknown SKU.
- The single Core description serves as the product display name; this slice invents no extra product copy.
- Responsive, accessible server-rendered HTML with locally maintained CSS. No JavaScript framework.

### Running the website

From the repository root:

```powershell
dotnet run --project src/RetailLab.Web
```

Then open the printed `http://localhost:XXXX` URL in a browser. To share a demonstration database with the console, set `RETAILLAB_DATA_DIRECTORY` to the same directory for both processes.

## Tutorial 4: customer accounts and favourites

Status: implemented in `RetailLab.Web` with auth storage in `RetailLab.Data`.

Customers register and sign in with an email and password. Authentication is framework-managed: ASP.NET Core Identity (`UserManager`/`SignInManager`) owns password hashing and cookie sign-in, and accounts persist in the standard framework-managed Identity tables (`AspNetUsers`, `AspNetRoles`, claims, logins, and tokens). No credentials live in RetailLab business tables. Each account is identified by its Identity user id, so favourites are strictly isolated per customer.

- Routes: `/Account/Register`, `/Account/Login`, `/Account/Logout`, `/Favourites` (all favourite mutations), plus favourite controls on `/Products` and `/Products/{sku}`.
- Anonymous visitors see a "Sign in to save" link carrying a safe local return URL. After login they land back on the originating catalogue or product page and explicitly press Add; signing in never creates a favourite by itself.
- Signed-in customers see Add/Remove forms posting to the fully `[Authorize]` Favourites page. Every POST redirects (Post-Redirect-Get) with a friendly TempData notice, so refreshing never resubmits.
- Submitted return URLs are validated with `Url.IsLocalUrl`. Add falls back to the matching product page (or catalogue); Remove falls back to `/Favourites`. Direct anonymous visits to `/Favourites` challenge and return there after login; forged anonymous POSTs are challenged before any handler runs and are never replayed.
- Registration confirms the email immediately (no email sender exists yet) and applies the default Identity password policy. Failed sign-ins count toward the default Identity lockout.

### Trying two accounts

Run the website, register `anna@example.com`, save a favourite, then register `bob@example.com` in a private window: Bob's favourites start empty while Anna's remain intact. To re-run the demonstration from scratch, point `RETAILLAB_DATA_DIRECTORY` at a fresh empty folder before starting.

## Tutorial 5A: customer basket

Status: implemented in `RetailLab.Web` with basket storage in `RetailLab.Data`.

Signed-in customers keep a persistent per-account basket. Catalogue cards add one item per click, the product page offers a quantity field, and the basket page edits quantities, removes lines, and shows line totals plus a basket total with the existing availability bands.

- Routes: `/Basket` (all basket mutations), plus add controls on `/Products` and `/Products/{sku}`.
- Anonymous visitors see a "Sign in to add to basket" link carrying a safe local return URL. After login they land back on the originating page and explicitly press Add; signing in never adds anything by itself.
- Signed-in customers post to the fully `[Authorize]` Basket page. Every POST redirects (Post-Redirect-Get) with a friendly TempData notice, so refreshing never resubmits.
- Submitted return URLs are validated with `Url.IsLocalUrl`. Add falls back to the matching product page (or catalogue); Update and Remove fall back to `/Basket`.
- Updating a quantity to zero is rejected with a message pointing at the separate Remove action.
- The basket reserves no inventory: out-of-stock products can be added and retained, and a neutral note explains that stock and prices are confirmed when ordering.
- Archived products already in the basket stay visible with a "No longer available" note; only removal works for them.
- Simultaneous requests never corrupt the basket: a conflicting change shows "Your basket changed; please try again." instead of an error page.

### Trying the basket

Run the website, register an account, add products from the catalogue and a product page, then open Basket from the header: edit a quantity, try zero to see the friendly rejection, and remove a line. To re-run from scratch, point `RETAILLAB_DATA_DIRECTORY` at a fresh empty folder before starting.

## Tutorial 5B: web checkout

Status: implemented in `RetailLab.Web` with ordering in `RetailLab.Core` and storage in `RetailLab.Data`.

Signed-in customers convert their basket into a simulated order from the basket page. The basket carries a "Place simulated order" button, and every order surface states plainly that no payment is processed.

- Routes: `/Basket` (the checkout POST), `/Orders` (order history), `/Orders/{id}` (confirmation and details).
- Checkout revalidates every line against current data: the product must still exist, must not be archived, and must have enough stock; the order snapshots the current description and price.
- One atomic save creates the order and its snapshot lines, reduces stock, writes one `Simulated order` inventory adjustment per line, and clears only that customer's basket lines. Any failure rolls everything back.
- Duplicate submissions are idempotent: each basket render mints a server-generated checkout-attempt identifier that doubles as the new order's id, so double-clicks and back-button re-POSTs land on the original confirmation instead of creating another order.
- Simultaneous checkouts, basket edits, and staff product changes resolve through optimistic-concurrency retries: the loser sees a friendly "review and try again" notice instead of an error page, and stock can never go negative.
- Order lookups are scoped to the signed-in customer. A forged order id renders the same 404 as an unknown one and never leaks another customer's order.
- Web and console ordering share one `OrderService` staging implementation, so both enforce identical rules.

### Trying checkout

Run the website, register an account, add products to the basket, and press "Place simulated order": the confirmation shows snapshot lines and the order total with an explicit no-payment notice, and `/Orders` lists the new order. Press the browser Back button and submit again to see the same confirmation rather than a second order. To re-run from scratch, point `RETAILLAB_DATA_DIRECTORY` at a fresh empty folder before starting.

## Tutorial 6A: local desktop inventory adjustment

Status: implemented in `RetailLab.Desktop`.

Store staff can run a Windows WPF workspace over the same local SQLite database while offline. The first deliberately small workflow shows exact stock for every product and records audited stock corrections.

- Active and archived products are listed with SKU, description, price, exact stock, and state.
- Selecting an active product enables a signed quantity change and required free-text reason. Positive values add stock; negative values remove stock.
- Archived products remain visible but cannot be adjusted until they are unarchived through an existing staff workflow.
- Successful adjustments refresh the selected row and show the resulting quantity.
- The fixed demonstration actor is `staff-desktop-01`; staff authentication and roles are a later slice.
- Existing Core rules reject zero changes, blank or overlong reasons, archived products, and changes that would produce negative stock.
- Validation messages are staff-friendly and do not expose programmer exception details.
- Optimistic concurrency reports a friendly retry message if another process changes the product first.
- Startup applies existing migrations and seed behavior without recreating or deleting useful data.

### Trying the desktop workflow

From Windows, run `dotnet run --project src/RetailLab.Desktop`, select an active product, enter `5` and a reason such as `Delivery received`, then apply the adjustment. Confirm the row increases by five. Try `0`, a blank reason, and a negative change larger than current stock to see friendly validation without a stock change. Shorten the window and use the adjustment panel's vertical scrollbar to reach the lower controls and Apply button. Set `RETAILLAB_DATA_DIRECTORY` before launch to use an isolated database.

## Tutorial 6B: desktop adjustment-history viewing

Status: implemented in `RetailLab.Desktop`.

Store staff can select any product, including an archived product, and open a focused modal window showing that product's complete stock-adjustment history.

- A `View adjustment history` button sits in the selected-product area. It is disabled when no product is selected or while the main window is busy, and enabled for both active and archived products.
- The history window is modal and owned by the main window; closing it returns to the unchanged inventory workspace.
- The heading shows the selected product clearly (SKU, description, and archived state when applicable).
- A read-only grid shows Time (UTC), signed Change, Resulting stock, Reason, and Actor, sorted newest first.
- Products with no adjustments show a friendly empty state instead of an empty grid.
- Load failures show safe staff-facing text without raw programmer exception details.
- The existing Apply-adjustment control is unchanged and remains disabled for archived products.
- Long reasons wrap inside the grid, and the grid scrolls within a sensibly sized window.

### Trying history viewing

From Windows, run `dotnet run --project src/RetailLab.Desktop`, select a product with existing adjustment history, and press `View adjustment history`. Confirm the newest records appear first with correct change, resulting stock, reason, actor, and UTC time. Select a product with no history and confirm the friendly empty state, then open history for an archived product to confirm it remains viewable. Close the window and confirm the workspace is unchanged, then apply a small adjustment to an active product to confirm the existing workflow still works. Set `RETAILLAB_DATA_DIRECTORY` before launch to use an isolated database.

## Scope boundaries

Desktop product creation/editing, staff authentication and roles, synchronization, search, pagination, deployment, and SaaS tenancy are not implemented yet. Ordering is simulated end to end: the website and console share one ordering implementation with no payment processing. Simultaneous checkouts and staff edits resolve through optimistic-concurrency retries rather than locking.
