# RetailLab

RetailLab is a learning and portfolio project demonstrating the design and development of a small-business retail software solution in .NET. It is currently in a website-first phase: a persistent console workflow, a staff inventory workflow, and a customer-facing catalogue website share one set of business rules over a local SQLite database.

RetailLab is a demonstration and teaching codebase. It is not production-ready and not a SaaS product.

## Features

- **Persistent CLI retail workflow** — browse a seeded catalogue, bookmark products, place simulated multi-line orders, and review order history and remaining stock from the console.
- **Staff product and inventory management** — create products, update descriptions and prices, and archive or unarchive products instead of deleting them. SKUs are immutable after creation.
- **Inventory adjustment audit history** — every stock change is recorded with a signed quantity delta, a required reason, a UTC timestamp, and an actor identifier. Order lines record matching adjustments, so the full movement of stock is traceable.
- **Responsive Razor Pages catalogue** — a read-only website with a home page, a product catalogue, and product-details pages, styled with locally maintained CSS and no JavaScript framework.
- **Product details and availability** — prices formatted as USD with customer-friendly availability (`In stock`, `Low stock`, `Out of stock`) instead of exact inventory counts. Archived products are hidden from lists and return 404 when requested directly.

## Technology stack

| Area | Choice |
| --- | --- |
| Language / runtime | C# on .NET 10 |
| Web | ASP.NET Core Razor Pages |
| Data access | Entity Framework Core with SQLite |
| Desktop (planned) | WPF (not yet implemented) |
| Tests | xUnit |
| Version control | Git |

No JavaScript frameworks, cloud infrastructure, containers, or message brokers are used.

## Architecture

```text
Browser / Console
    |
    +-- RetailLab.Web (Razor Pages + display models) --+
    +-- RetailLab.LabCli (console menus) --------------+
                                                       v
                                              RetailLab.Core
                              (entities, rules, services, IRetailRepository)
                                                       |
                                              RetailLab.Data
                                    (EF Core mappings, repository,
                                     migrations, seed data)
                                                       v
                                                SQLite (.db file)
```

## Projects

| Project | Role |
| --- | --- |
| `src/RetailLab.Core` | Business entities (`Product`, `Order`, `Bookmark`, `InventoryAdjustment`), validation, and services (`OrderService`, `ProductService`, `InventoryService`, `BookmarkService`, `CatalogService`). Has no dependency on UI frameworks or Entity Framework Core. |
| `src/RetailLab.Data` | EF Core `DbContext`, entity mappings, the `EfRetailRepository` implementation, migrations, and idempotent seed data. |
| `src/RetailLab.LabCli` | Console application hosting the persistent retail workflow and the staff product/inventory workflow. |
| `src/RetailLab.Web` | Read-only Razor Pages catalogue website. PageModels call `CatalogService` and render web-only display models; markup never touches entities. |
| `tests/RetailLab.Tests` | xUnit suite: Core unit tests, Web display tests, and SQLite integration tests. |

## Getting started

Prerequisite: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
# Restore, build, and test (from the repository root)
dotnet build RetailLab.sln
dotnet test RetailLab.sln

# Run the console application
dotnet run --project src/RetailLab.LabCli

# Run the website, then open the printed http://localhost:XXXX URL in a browser
dotnet run --project src/RetailLab.Web
```

## Demonstration database

Both the console and the website use a local SQLite file. By default it lives at `%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db`. Set the `RETAILLAB_DATA_DIRECTORY` environment variable to point both applications at the same folder to deliberately share one demonstration database, or at a fresh empty folder for an isolated run (the schema migrates and the sample catalogue seeds automatically on startup). Runtime database files are git-ignored and must never be committed.

## Limitations

- Commerce is simulated: no real ordering checkout, no real payment processing.
- No authentication: the console uses fixed demo labels (`customer-demo-001`, `staff-demo-01`) and the website is read-only.
- No synchronization between installations.
- SQLite is a development and offline-demonstration choice, not a multi-user server database.

## Roadmap

Completed: persistent console retail flow, staff product and inventory management with audit history, and the customer catalogue website. Planned next slices: customer accounts and favourites, web simulated ordering, a local WPF staff application with offline storage, and desktop/server synchronization. Details live in [docs/ROADMAP.md](docs/ROADMAP.md); decisions are logged in [docs/DECISIONS.md](docs/DECISIONS.md).

## Testing

`dotnet test RetailLab.sln` — observed result: **59 tests, 0 failed** (Core unit tests, Web display tests, and SQLite integration tests, including migration, archive-visibility, and audit-history coverage).

## Screenshots

> Clean screenshots are still to be captured; the images below will appear once saved at the listed paths.

![RetailLab home page](docs/images/home.png)
![RetailLab product catalogue](docs/images/catalogue.png)
![RetailLab product details](docs/images/product-details.png)

## Learning and engineering approach

RetailLab is built in small vertical slices: one working feature at a time, each backed by automated tests and an EF Core migration that preserves existing data. Business rules live in `Core`, storage details in `Data`, and presentation in the console and web projects, so each layer can be understood and reviewed independently. AI-assisted development is used for delivery speed, and every AI-produced change is human-reviewed, built, tested, and manually verified before it is kept.
