# RetailLab development roadmap

The roadmap uses small vertical slices. Implemented behavior is described in [PRODUCT.md](PRODUCT.md), architecture in [ARCHITECTURE.md](ARCHITECTURE.md), and significant choices in [DECISIONS.md](DECISIONS.md).

## 1. Lab Prototype 1: persistent console retail flow

Status: implemented.

Outcome: the fixed simulated customer can browse five seeded products, add and remove bookmarks, place multi-line simulated orders, view order history, and inspect remaining stock. EF Core and SQLite persist the data across runs.

Implemented safeguards include Core-owned validation, full-order stock checks before mutation, transactional persistence, price snapshots, unique bookmarks and SKUs, database migrations, idempotent seed data, and automated Core and SQLite tests.

## 2. Product and inventory management slice

Status: implemented in `RetailLab.LabCli` (Tutorial 2).

Outcome: a staff workflow can create products, update descriptions and prices, archive and unarchive products, apply explicit audited stock adjustments, and view adjustment history. SKU is immutable, products archive instead of deleting, and no adjustment may be zero or drive stock negative. Order placement records matching adjustments so every stock change is audited. Bookmarks of archived products are excluded from customer bookmark results while the stored rows are preserved.

Implemented safeguards include Core-owned archive and adjustment rules, reason and actor validation before stock mutation, transactional stock-plus-audit persistence, archived-product exclusion from customer views, an additive migration preserving existing data, and automated Core and SQLite tests.

## 3. Customer product catalogue on the web

Status: implemented in `RetailLab.Web` (Tutorial 3).

Outcome: customers can visit a polished home page, browse active products, and open product details. The site reuses Core and Data over the same SQLite database and migration initializer, formats prices as USD, shows In stock / Low stock / Out of stock bands instead of exact counts, hides archived products (404 when requested directly), and serves responsive accessible HTML with locally maintained CSS and no JavaScript framework.

Implemented safeguards include a Core-owned active-only catalogue service, web display models that keep business rules out of markup, startup-vs-request failure separation, tests that need no new packages, and documentation updates.

## 4. Customer accounts and favourites

Status: implemented in `RetailLab.Web` with auth storage in `RetailLab.Data` (Tutorial 4).

Outcome: customers register and sign in through standard ASP.NET Core Identity with the Entity Framework store, and each account keeps isolated favourites keyed by Identity user id.

- Standard Identity authentication: `AddIdentity` with `AddEntityFrameworkStores`; credentials live only in the framework-managed `AspNet*` tables.
- Anonymous catalogue/product pages show "Sign in to save" with a safe local return URL; signed-in customers get Add/Remove POST forms on the fully `[Authorize]` Favourites page with Post-Redirect-Get notices.
- Safeguards: `[Authorize]` challenges before any mutation, `Url.IsLocalUrl` on every return URL with known fallbacks, forged POSTs never replayed, plus automated isolation, redirect-policy, and standard Identity account tests.

## 5. Web simulated ordering

### 5A. Customer basket

Status: implemented in `RetailLab.Web` with basket storage in `RetailLab.Data` (Tutorial 5A).

Outcome: a signed-in customer can add catalogue products to a persistent per-account basket (one per catalogue card, chosen quantities on the product page), edit quantities and remove lines on the basket page, and see line totals, a basket total, and the existing availability bands.

- Core-owned rules: `BasketService` validates unknown and archived products, quantities, and merge overflow as `BusinessRuleException`; `BasketItem` carries a `Version` optimistic-concurrency token.
- No inventory reservation: out-of-stock products stay addable and retained; checkout enforces stock in 5B. No checkout button yet — a neutral note says stock and prices are confirmed when ordering.
- Persistence conflicts (concurrent first-add key collisions, stale `Version` losses) translate inside Data to the Core-owned `BasketConflictException`, shown as a friendly retry notice; Web never sees EF exception types.
- Safeguards: fully `[Authorize]` basket page, account isolation, archived lines visible but only removable, `Url.IsLocalUrl` return-URL checks with product/catalogue/basket fallbacks, Post-Redirect-Get with TempData notices, antiforgery on every form, an additive migration, and automated Core, SQLite (including deterministic conflict-translation), and display tests.

### 5B. Web checkout

Proposed outcome: an authenticated customer can submit the basket as an order and view confirmation and history.

- Reuse the Core ordering behavior demonstrated by Lab Prototype 1.
- Enforce stock and prices at order time, with duplicate-submission and idempotency protection.
- Continue to exclude real payment processing.

## 6. Local staff inventory application

Proposed outcome: staff can use a WPF application with local SQLite storage while offline.

- Create `RetailLab.Desktop` only when this slice is approved.
- Reuse Core rules and Data patterns.
- Demonstrate one clear inventory workflow before expanding it.

## 7. Desktop and server synchronization

Proposed outcome: selected offline changes synchronize safely when connectivity returns.

Define ownership, stable identifiers, transport, retry behavior, and conflict policy only after the local and server workflows clarify the actual requirements.

## Working agreement

For every slice: inspect first, explain the objective and likely files, make the smallest maintainable change, run `dotnet build RetailLab.sln` and `dotnet test RetailLab.sln`, report actual results, identify manual checks, update documentation, and leave commits to the developer.
