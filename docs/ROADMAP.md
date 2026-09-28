# RetailLab development roadmap

The roadmap uses small vertical slices. Implemented behavior is described in [PRODUCT.md](PRODUCT.md), architecture in [ARCHITECTURE.md](ARCHITECTURE.md), and significant choices in [DECISIONS.md](DECISIONS.md).

## 1. Lab Prototype 1: persistent console retail flow

Status: implemented.

Outcome: the fixed simulated customer can browse five seeded products, add and remove bookmarks, place multi-line simulated orders, view order history, and inspect remaining stock. EF Core and SQLite persist the data across runs.

Implemented safeguards include Core-owned validation, full-order stock checks before mutation, transactional persistence, price snapshots, unique bookmarks and SKUs, database migrations, idempotent seed data, and automated Core and SQLite tests.

## 2. Product and inventory management slice

Status: implemented in `RetailLab.LabCli` (Tutorial 2).

Outcome: a staff workflow can create products, update descriptions and prices, archive and unarchive products, apply explicit audited stock adjustments, and view adjustment history. SKU is immutable, products archive instead of deleting, and no adjustment may be zero or drive stock negative. Order placement records matching adjustments so every stock change is audited.

Implemented safeguards include Core-owned archive and adjustment rules, reason and actor validation before stock mutation, transactional stock-plus-audit persistence, archived-product exclusion from customer views, an additive migration preserving existing data, and automated Core and SQLite tests.

## 3. Customer product catalogue on the web

Proposed outcome: customers can browse persisted products in an accessible Razor Pages website.

- Create `RetailLab.Web` using ASP.NET Core Razor Pages.
- Start with list and detail pages.
- Select the retailer theme and currency convention.
- Reuse Core and Data rather than duplicating rules.

## 4. Customer accounts and favourites

Proposed outcome: customers can register, sign in, and maintain isolated favourites.

- Configure framework-managed authentication.
- Replace the fixed customer label with authenticated identity mapping.
- Keep credentials outside RetailLab business tables.

## 5. Web simulated ordering

Proposed outcome: an authenticated customer can submit an order and view confirmation and history.

- Reuse the Core ordering behavior demonstrated by Lab Prototype 1.
- Add duplicate-submission and concurrent-stock protection.
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
