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

## Scope boundaries

No desktop UI, ordering, staff administration, synchronization, search, pagination, deployment, or SaaS tenancy is implemented yet. The website has customer accounts and favourites but no checkout. Concurrency protection for several simultaneous customers is deferred until a multi-user ordering interface is introduced.
