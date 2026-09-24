# RetailLab architecture

This document describes the implemented repository and keeps implemented behavior separate from planned features. Project rules are defined in [AGENTS.md](../AGENTS.md).

## Current solution

All projects target .NET 10 with nullable reference checking and implicit imports enabled.

| Location | Current responsibility |
| --- | --- |
| `src/RetailLab.Core` | Retail entities, validation, business workflows, and the persistence boundary. It has no EF Core or UI dependency. |
| `src/RetailLab.Data` | EF Core SQLite context, mappings, repository, migration, and idempotent sample-data initialization. |
| `src/RetailLab.LabCli` | Interactive Lab Prototype 1 console interface and application startup. |
| `tests/RetailLab.Tests` | Core unit tests and SQLite integration tests. |

No Web or Desktop project exists yet.

## Project dependencies

```text
RetailLab.Core   -> no other projects
RetailLab.Data   -> RetailLab.Core, EF Core SQLite
RetailLab.LabCli -> RetailLab.Core, RetailLab.Data
RetailLab.Tests  -> RetailLab.Core, RetailLab.Data
```

Core therefore remains reusable by later Razor Pages and WPF interfaces without depending on those technologies or on EF Core.

## Lab Prototype 1 model

```text
Product 1 --------< Bookmark
   |
   +--------------< OrderLine >-------- 1 Order

CustomerIdentifier appears on Bookmark and Order.
```

- `Product` owns SKU, description, price, stock, and stock-reduction rules.
- `Bookmark` joins the fixed simulated customer to a product.
- `Order` owns its lines and calculates its total.
- `OrderLine` records product identifiers plus SKU, description, and unit-price snapshots.
- `BookmarkService` coordinates add/remove behavior.
- `OrderService` validates a complete request before changing stock, creates the order, and commits through `IRetailRepository`.
- `IRetailRepository` is the Core-owned persistence boundary implemented by `EfRetailRepository` in Data.

## Database

`RetailLabDbContext` maps four SQLite tables:

- `Products`, with a case-insensitive unique SKU and nonnegative price and stock constraints.
- `Bookmarks`, with `(CustomerIdentifier, ProductId)` as its composite primary key.
- `Orders`, indexed by customer identifier and placement time.
- `OrderLines`, with positive-quantity and nonnegative-price constraints.

The initial migration is stored in `RetailLab.Data/Migrations`. Application startup applies pending migrations, then inserts the fictional sample catalogue only if no products exist. It never recreates the database.

SQLite cannot translate ordering by `DateTimeOffset`. Order history is filtered in the database and sorted in memory after loading; this is acceptable for the deliberately small prototype. Persisted timestamps are UTC.

## Runtime data flow

```text
Console input
    -> Core service and business validation
    -> IRetailRepository
    -> EF Core DbContext
    -> SQLite
```

A successful order tracks stock changes, a new order, and all new lines in one DbContext. `SaveChangesAsync` persists them in one relational transaction. Invalid or insufficient-stock requests fail before any product is changed.

The database defaults to `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`. The `RETAILLAB_DATA_DIRECTORY` environment variable can select an isolated location for development.

## Deferred architecture

ASP.NET Core authentication, Razor Pages, WPF, multiple concurrent customers, inventory editing, and synchronization are not part of Lab Prototype 1. Shared models do not imply that future server and offline desktop applications will share one physical database file.
