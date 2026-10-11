# RetailLab Project Intake Report

- **Prepared:** 2026-10-02
- **Repository inspected:** `D:\Development\RetailLab`
- **Audience:** Jarva (high-level planning), Baraka (project owner and final decision-maker)
- **Inspection mode:** Read-only, except for this sanctioned report file

## Executive summary

RetailLab is a public, educational .NET 10 retail application that demonstrates one business domain through three interfaces: a customer-facing Razor Pages website, a console lab application, and a Windows WPF staff application. They currently share a local SQLite database and reuse business rules in `RetailLab.Core`; the product is for customers browsing and placing simulated orders, staff managing products and stock, and developers or portfolio reviewers studying a layered, tested implementation.

The implemented local product is substantial: catalogue, customer Identity accounts, favourites, basket, idempotent simulated checkout and order history, console workflows, WPF product/inventory management, audited adjustments, archive/unarchive behavior, optimistic concurrency, migrations, and seed data all exist in source. The next architectural phase is desktop/server synchronization, but it is design-only and currently present as uncommitted documentation. No synchronization code, staff authentication, production deployment, or real payment processing exists.

The repository is on local branch `feature/sync-architecture` at commit `6b93282`, the same commit as `main` and `origin/main`, with no upstream configured for the current branch. The working tree is intentionally dirty: five documentation files are modified and `docs/SYNCHRONIZATION.md` is untracked. A fresh `dotnet test RetailLab.sln` check was attempted during this intake but produced no output and exceeded the two-minute limit; its spawned workers were terminated. Therefore the current build/test result is **UNKNOWN**, not failed. The README records an earlier observed result of **141 tests, 0 failed**, and static inspection finds 113 `[Fact]` methods plus 28 `[InlineData]` rows, consistent with 141 xUnit cases.

## 1. Project identity

- **Name:** RetailLab.
- **Repository:** `barakagitonga/RetailLab` — `https://github.com/barakagitonga/RetailLab`.
- **GitHub state:** public; default branch `main`; description: “Retail Web facing and standalone Desktop application.” Verified from GitHub repository metadata on 2026-10-02.
- **Purpose:** a learning and portfolio project for a small-business retail solution, explicitly not production-ready and not SaaS.
- **Users:** customers; store staff; and the developer/portfolio reviewer.
- **Commercial boundary:** ordering is simulated end to end; no real payment processing exists.

Evidence: `README.md`, `docs/PRODUCT.md`, `AGENTS.md`, Git remote configuration, and GitHub repository metadata.

## 2. Repository and Git state

| Item | Observed state |
| --- | --- |
| Current branch | `feature/sync-architecture` |
| Upstream | None configured |
| HEAD | `6b9328280e70c15a3c9c27c3c0b2723d5b3239af` (`6b93282`) |
| HEAD subject | `Merge pull request #8 from barakagitonga/feature/desktop-product-management` |
| HEAD date | 2026-09-30 16:00:54 +03:00 |
| Main relationship | Local `main`, `origin/main`, and `origin/HEAD` all point at the same HEAD |
| Modified files | `README.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/PRODUCT.md`, `docs/ROADMAP.md` |
| Untracked file | `docs/SYNCHRONIZATION.md` |
| Tags | None |
| Remote | `origin https://github.com/barakagitonga/RetailLab.git` |

Other local/remote branches:

- `feature/customer-accounts-favourites` at `6915143`, merged into `main`.
- `feature/web-ordering` at `80f444f`, merged into `main`.
- `feature/web-checkout` at `a395954`, local only, merged into `main` through later history.
- `feature/web-catalogue` local tip `952cdbf` is not itself an ancestor of `main`, although its feature commit `0b064d1` was merged by PR #1; the branch tip contains its own later merge commit.

The repository contains 22 commits, so the requested last ~50 commits means the complete available history. Important feature commits are summarized in the decisions section.

## 3. Technology stack

| Area | Technology | Version / status | Evidence |
| --- | --- | --- | --- |
| Language | C# | Compiler/language version not explicitly pinned; SDK default for target framework | all `.csproj`; no `LangVersion` |
| Runtime targets | .NET | `net10.0`; Desktop uses `net10.0-windows` | all `.csproj` |
| SDK observed on intake machine | .NET SDK | 10.0.401; MSBuild 18.9.11 | `dotnet --info` on 2026-10-02 |
| Shared runtimes observed | .NET / ASP.NET Core / Windows Desktop | 10.0.12 | `dotnet --info` |
| Web | ASP.NET Core Razor Pages | .NET 10 shared framework; no separate package pin | `RetailLab.Web.csproj`, `Program.cs` |
| Authentication | ASP.NET Core Identity with EF store and cookies | 10.0.12 | `RetailLab.Data.csproj`, `RetailLabDbContext`, `Program.cs` |
| Desktop | WPF | .NET 10 Windows Desktop | `RetailLab.Desktop.csproj` |
| Persistence | Entity Framework Core | 10.0.12 | `RetailLab.Data.csproj` |
| Database | SQLite via EF Core / Microsoft.Data.Sqlite | EF provider 10.0.12; SQLitePCLRaw 2.1.12 resolved transitively | package listing and Data project |
| Tests | xUnit | xUnit 2.9.3; runner 3.1.4; Test SDK 17.14.1 | `RetailLab.Tests.csproj` |
| Coverage collector | coverlet.collector | 6.0.4; no report or threshold checked in | `RetailLab.Tests.csproj` |
| Version control | Git / GitHub | no tags; public repository | Git state and GitHub metadata |
| Containers / cloud / brokers | None | intentionally excluded from current baseline | `AGENTS.md`, `README.md` |

Dependency notes:

- `RetailLab.Core` has no NuGet package references.
- `RetailLab.Data` directly references EF Core Design, EF Core SQLite, and Identity EF storage, all 10.0.12.
- Web, Desktop, and LabCli receive those packages transitively through Data.
- The test project directly references coverlet, Microsoft.NET.Test.Sdk, xUnit, and the Visual Studio xUnit runner.
- There is no `global.json`, central package file, or NuGet lockfile. SDK selection and transitive resolution are therefore not repository-pinned.

## 4. Architecture overview

### 4.1 Dependency shape

```text
RetailLab.Core   -> no project dependency
RetailLab.Data   -> RetailLab.Core + EF Core/SQLite/Identity
RetailLab.Web    -> RetailLab.Core + RetailLab.Data
RetailLab.Desktop-> RetailLab.Core + RetailLab.Data
RetailLab.LabCli -> RetailLab.Core + RetailLab.Data
RetailLab.Tests  -> RetailLab.Core + RetailLab.Data + RetailLab.Web
```

The intended boundary is clear and is respected by project references: business entities and workflows live in Core, EF/storage details live in Data, and each UI composes them. Tests reference Web only to test display mapping and redirect policy; production projects do not depend on Tests.

### 4.2 Top-level folders and modules

| Location | Responsibility |
| --- | --- |
| `.continue/rules` | Legacy copies of project guidance. These are materially stale compared with `docs/`; see risks. |
| `docs` | Current product, architecture, decision, roadmap, screenshots, and uncommitted synchronization design. |
| `src/RetailLab.Core` | Entities, validation, services, conflict exceptions, `IRetailRepository`. No EF or UI dependency. |
| `src/RetailLab.Data` | `RetailLabDbContext`, EF configurations, SQLite repository, migrations, database path helper, seed/migration initializer. |
| `src/RetailLab.Web` | Razor Pages storefront, Identity account pages, favourites, basket, checkout, order views, display models, CSS. |
| `src/RetailLab.Desktop` | Windows WPF inventory workspace and focused dialogs/windows for creation, editing, adjustment history, archive controls. |
| `src/RetailLab.LabCli` | Console composition root and interactive customer/staff menus. |
| `tests/RetailLab.Tests` | Core unit tests, web display/policy tests, Identity tests, and SQLite integration/concurrency tests. |

### 4.3 Domain model

```text
Product 1 --------< Bookmark
   |
   +--------------< BasketItem
   |
   +--------------< OrderLine >-------- 1 Order
   |
   +--------------< InventoryAdjustment
```

- `Product` owns immutable SKU, description, price, stock, archive state, archive timestamp, and a local optimistic-concurrency `Version`.
- `Bookmark` and `BasketItem` are scoped by customer identifier; basket items also carry a concurrency version.
- `Order` owns snapshot `OrderLine` records so historical SKU, description, and price survive product changes.
- `InventoryAdjustment` is the stock audit record: signed delta, resulting quantity, reason, actor, and UTC timestamp.
- `CustomerIdentifier` normalizes/validates customer identifiers without introducing an Identity dependency into Core.

### 4.4 Core workflows

- `CatalogService`: customer-visible active products and single active-product lookup.
- `BookmarkService`: per-customer add/remove/list with archived-product protection.
- `BasketService`: add/update/remove/list, quantity and overflow checks, archive behavior, friendly business errors.
- `OrderService`: validate-all-then-stage multi-line ordering, stock reduction, snapshots, and audit records.
- `CheckoutService`: idempotent basket checkout, order lookup/history, and atomic basket-to-order persistence.
- `ProductService`: create, edit, archive, and unarchive, including expected-version overloads for stale WPF forms/actions.
- `InventoryService`: audited signed stock adjustment and history lookup.

Expected persistence races are translated into Core-owned `BasketConflictException`, `CheckoutConflictException`, and `ProductConflictException`, keeping EF exception types out of presentation layers.

### 4.5 Persistence and startup

`RetailLabDbContext` derives from `IdentityDbContext<IdentityUser>` and maps six business tables plus the seven standard Identity tables. Five EF Core migrations exist:

1. `20260924121108_InitialCreate`
2. `20260928101031_ProductArchiveAndAdjustments`
3. `20260928173434_IdentitySchema`
4. `20260928204200_BasketItems`
5. `20260929121628_ProductVersion`

Each application resolves a data directory, constructs `retaillab.db`, applies pending migrations, and seeds five fictional products only when the product table is empty. Startup does not recreate or wipe existing data.

Default database path:

```text
%LOCALAPPDATA%\RetailLab\LabPrototype1\retaillab.db
```

### 4.6 Runtime data flows

Read flow:

```text
Browser / console / WPF
  -> presentation handler
  -> Core service
  -> IRetailRepository
  -> EfRetailRepository / RetailLabDbContext
  -> SQLite
  -> display model or UI row
```

Web mutation flow:

```text
Authenticated POST + antiforgery token
  -> authorized PageModel handler
  -> Core workflow and validation
  -> one repository save / relational transaction
  -> TempData notice
  -> redirect (Post-Redirect-Get)
```

Checkout flow:

```text
Customer basket + server-generated checkoutAttemptId
  -> CheckoutService
  -> shared OrderService staging
  -> order + snapshot lines + stock reductions + audit rows + basket deletion
  -> one SaveCheckoutChangesAsync transaction
  -> /Orders/{id}
```

Desktop flow:

```text
WPF window/dialog
  -> fresh short-lived DbContext
  -> ProductService or InventoryService
  -> local SQLite
  -> explicit refresh / friendly conflict message
```

## 5. Current project state

### 5.1 Implemented behavior

| Capability | State | Evidence |
| --- | --- | --- |
| Persistent console catalogue, bookmarks, ordering, history | Implemented | `RetailLab.LabCli`; commit `64f70b1` |
| Staff product and inventory management in CLI | Implemented | `ProductService`, `InventoryService`; commit `c5f6bf6` |
| Razor Pages home/catalogue/details | Implemented | `RetailLab.Web`; commit `0b064d1` |
| Customer registration/login/logout | Implemented with standard Identity | account pages, `Program.cs`; commit `6915143` |
| Per-customer favourites | Implemented | Favourites page/service; commit `6915143` |
| Persistent per-customer basket | Implemented | Basket page/service/migration; commit `80f444f` |
| Simulated idempotent web checkout and order history | Implemented | Checkout service/order pages; commit `a395954` |
| Product and basket optimistic concurrency | Implemented | version migration/configuration and conflict translation; commits `a395954`, `0295293` |
| WPF exact-stock list and audited adjustment | Implemented | Desktop project; commit `496e6ba` |
| WPF adjustment history | Implemented | history window; commit `cb68fdc` |
| WPF create and refresh | Implemented | create dialog/refresh; commit `b1378dc` |
| WPF edit/archive/unarchive and stale-form protection | Implemented | edit dialog/version checks; commit `0295293` |
| EF migrations and idempotent five-product seed | Implemented | Data migrations and `DatabaseInitializer` |
| Desktop/server synchronization | Design only; not implemented | dirty docs, especially untracked `docs/SYNCHRONIZATION.md` |

### 5.2 Known incomplete scope

- No staff login, staff role model, or device identity. CLI/Desktop use fixed demo actor labels.
- No actual desktop/server synchronization, server API, outbox, inbox receipt table, change feed, cursor, pairing, or sync UI.
- No production server database or deployment topology.
- No real payment processing.
- No search, pagination, multi-store tenancy, SaaS tenancy, or background synchronization.
- No email sender or email verification; demo registration immediately confirms the email.
- No fixed web port or `launchSettings.json`.
- No CI configuration, container configuration, release tags, changelog, license file, SDK pin, or dependency lockfile.
- No HTTP end-to-end/TestServer suite; web coverage focuses on PageModel-related behavior, display mapping, redirects, Identity, Core, and SQLite integration. Razor compilation depends on solution build.
- WPF layout and interaction require manual Windows testing; automated tests cover the underlying Core/Data behavior rather than UI automation.

### 5.3 Test and build evidence

- `README.md` records: **141 tests, 0 failed**.
- Static test shape: 113 `[Fact]` methods, 10 `[Theory]` methods, and 28 `[InlineData]` rows. Under normal xUnit discovery, the facts plus inline rows yield 141 cases.
- Coverage spans product rules, services, persistence, catalogue display, redirect safety, Identity account behavior, two-customer isolation, basket conflicts, checkout idempotency/rollback/races, product concurrency, order display, and SQLite path/migration behavior.
- Fresh verification on 2026-10-02: `dotnet test RetailLab.sln --nologo --verbosity minimal` produced no output, spawned many idle .NET workers, and exceeded the two-minute inspection limit. The workers created by the attempt were terminated. No pass/fail result was produced, so current build and test status is **UNKNOWN**. This observation is not evidence of a source-code failure.

## 6. Build and run

### 6.1 Required environment

- .NET 10 SDK.
- Windows for `RetailLab.Desktop` because it targets WPF (`net10.0-windows`).
- The web and console projects target `net10.0`; the repository documents Windows/PowerShell paths, while their source does not otherwise impose a Windows-only API.
- Writable access to the selected data directory.
- Network access is needed only for first NuGet restore if packages are not cached.

Repository-specific required environment variables: **none**.

Optional environment variable:

- `RETAILLAB_DATA_DIRECTORY` — selects the folder containing `retaillab.db`; use the same value to share a demonstration database or a fresh folder for isolation.

No `.env.example` exists. `.env` and SQLite runtime files are git-ignored.

### 6.2 Exact commands

From `D:\Development\RetailLab`:

```powershell
dotnet restore RetailLab.sln
dotnet build RetailLab.sln
dotnet test RetailLab.sln
```

Run the console application:

```powershell
dotnet run --project src/RetailLab.LabCli
```

Run the website:

```powershell
dotnet run --project src/RetailLab.Web
```

Open the URL printed by ASP.NET Core. Exact ports are **UNKNOWN** because the repository contains no launch profile or explicit URL binding.

Run the WPF staff application on Windows:

```powershell
dotnet run --project src/RetailLab.Desktop
```

Example isolated PowerShell session:

```powershell
$env:RETAILLAB_DATA_DIRECTORY = 'D:\RetailLabDemoData'
dotnet run --project src/RetailLab.Web
```

On first application startup, pending migrations are applied and the catalogue is seeded if the product table is empty.

### 6.3 Seed/demo data

The initializer creates these products only when no products exist:

- `AUR-100` — Aurora insulated travel mug
- `BRI-210` — Brindle recycled canvas tote
- `CIR-320` — Cirrus compact desk lamp
- `DOV-430` — Dovetail bamboo notebook
- `EMBER-540` — Emberline wireless speaker

Demo identifiers include `customer-demo-001` for the console customer and fixed staff actor labels for console/Desktop. They are labels, not authentication.

## 7. Decisions log: present and past

| Decision | State and rationale | Evidence |
| --- | --- | --- |
| Small vertical slices for learning and portfolio quality | Established; prioritize understandable, tested features over broad abstraction. | `docs/DECISIONS.md` D001; `AGENTS.md`; initial docs commit `e9bc11b` |
| .NET 10 / C# / Razor Pages / WPF / EF Core / SQLite / xUnit | Established baseline. | D002; project files; initial scaffold `eb8e7a9`, EF package commit `fcfbd77` |
| Separate Core, Data, and presentation | Implemented; Core owns rules and repository boundary, Data owns EF, UIs coordinate. | D003; solution references; prototype commit `64f70b1` |
| Preserve data through migrations and seed only an empty product table | Implemented; no startup reset. | D004; `DatabaseInitializer.cs`; migrations |
| Framework-managed credentials; simulated commerce only | Implemented for customer Identity; payment remains out of scope. | D005/D010; commit `6915143` |
| Persistent console prototype supersedes earlier in-memory catalogue proposal | Earlier in-memory proposal abandoned in favor of end-to-end persistent flow. | D006; commit `64f70b1` |
| Archive products rather than delete; audit every stock change | Implemented. | D008; commit `c5f6bf6` |
| Razor Pages, server-rendered HTML, local CSS, no JS framework | Implemented. | D009; commit `0b064d1` |
| Identity user ID is the Core customer identifier | Implemented to isolate favourites/baskets/orders without Core depending on Identity. | D010; commit `6915143` |
| Sign-in-first anonymous UX instead of replaying POST after login | Implemented for favourites and basket; local return URLs are validated. | D010/D011; web pages and tests |
| Basket does not reserve stock | Implemented; checkout validates current stock and price. | D011; commit `80f444f` |
| Translate EF persistence conflicts at the Data boundary | Implemented with Core-owned exceptions. | D011-D013; `EfRetailRepository.cs` |
| Checkout attempt ID doubles as Order ID | Implemented idempotency without an extra table. | D012; commit `a395954` |
| Product integer version is an EF concurrency token | Implemented; stale writes lose rather than overwrite. | D013; migration `20260929121628_ProductVersion`; commit `a395954` |
| First WPF slice is local inventory adjustment | Implemented to reuse a proven Core workflow without premature UI framework complexity. | D014; commit `496e6ba` |
| Focused WPF code-behind rather than an MVVM framework | Retained through current screens; revisit only when UI state complexity demonstrates need. | D014-D017; commits `496e6ba` through `0295293` |
| Desktop stale forms require an expected-version pre-check in addition to EF save-time concurrency | Implemented. | D017; commit `0295293` |
| Defer synchronization until local workflows clarify requirements | Past decision fulfilled at the design level. | D007; local workflow commits |
| Server-authoritative, push-then-pull synchronization using desktop outbox, server inbox receipts, change feed, and cursor | Approved design direction, not code. Conflicts require human review; no silent last-write-wins. | dirty D018 and untracked `docs/SYNCHRONIZATION.md` |
| No automatic renames/restructures in history | No Git renames were found. Placeholder `Class1.cs` and `UnitTest1.cs` files were deleted when the real prototype arrived. | Git history; commit `64f70b1` |

### Synchronization design already agreed in the working tree

Although uncommitted, the current design is specific:

- Server owns published products, authoritative inventory log, orders, customer Identity, favourites, and baskets.
- Desktop owns only its local projection and immutable outbox.
- New/empty desktops bootstrap server GUIDs; SKU is a business key, not permanent distributed identity.
- Existing random seed GUIDs differ between independently created databases and are a pairing blocker.
- Existing `Product.Version` is database-local and must not be treated as a global sync revision.
- Push precedes pull; stable operation IDs plus inbox receipts make uploads idempotent.
- A monotonic server sequence and transactional local cursor make downloads resumable.
- Product/stock conflicts are retained for human attention; inventory sync sends deltas, never absolute stock.
- State-changing sync is gated on a staff-device authentication/authorization design.

## 8. Open questions and risks

### Highest priority

1. **The synchronization design is not safely in repository history.** Five docs are modified and `docs/SYNCHRONIZATION.md` is untracked. A clean clone at `origin/main` does not contain D018 or the design artifact. Review and commit it before planning implementation.
2. **Fresh build/test state is unknown.** The documented 141/0 result is plausible and matches static discovery, but this intake’s bounded run timed out without output. Re-run build and tests in a normal developer terminal and record the actual output before declaring the branch green.
3. **Staff/device authentication is a hard gate for write synchronization.** The code has customer Identity only. Do not expose 7D/7E write endpoints until roles, enrollment, credential rotation, and server-side actor resolution are designed.
4. **Distributed identity is unresolved for existing databases.** Independently seeded databases assign different random GUIDs to the same SKU. New empty desktops can bootstrap server IDs; existing populated databases require explicit, human-safe pairing/migration.
5. **Local versions are not global revisions.** A dedicated server revision/base-revision design is required; comparing desktop `Product.Version` with server `Product.Version` would be invalid after separation.

### Documentation and repository hygiene

6. `.continue/rules/*` are tracked, stale duplicates frozen around the early prototype. They still say synchronization is open and omit current web/desktop details. Tools that read them may receive conflicting guidance. Remove, regenerate, or clearly mark them as non-authoritative.
7. `docs/DECISIONS.md` D002 still says “WPF remains future work,” despite four implemented WPF slices. This is a concrete stale statement in the primary decision log.
8. The public repository has no `LICENSE`; public visibility alone does not grant reuse rights. Choose a license if portfolio viewers are intended to clone or contribute.
9. There is no changelog, release/tag scheme, CI pipeline, SDK pin (`global.json`), or dependency lockfile. Reproducibility currently depends on the installed SDK and restored package graph.

### Product and operational decisions

10. Permanent retailer brand and multi-currency policy are open; the site currently formats USD at the web edge while the console intentionally shows no symbol.
11. Staff roles/permissions and stock-adjustment reason taxonomy are open.
12. Production database, hosting, deployment topology, secrets management, and observability are open.
13. Sync API/transport, payload versioning, revision metadata, causal predecessor representation, retry/backoff, page/batch sizes, conflict-resolution UI, background scheduling, and existing-database pairing are open.
14. The initial sync design assumes a single store. Multi-store tenancy would require an identity/ownership redesign.
15. Shared-file SQLite is appropriate for the local demo but not a multi-user server database. Multiple local interfaces can contend for one file; current protection is explicit refresh plus optimistic concurrency.
16. Application startup automatically applies migrations. Useful for this lab, but production rollout would need controlled migration/backup policy.
17. No fixed web port is committed. This is fine for local development but needs explicit configuration for repeatable demos/deployment.
18. Test count is documented, but coverage percentage, coverage artifact, and quality threshold are absent.
19. WPF UX is manually verified only; no UI automation or accessibility audit was found.

## 9. Recommended next planning steps

1. Review, correct, and commit the current synchronization documentation as a design-only checkpoint.
2. Resolve documentation drift (`.continue/rules` and D002) in the same or a separate documentation-only change.
3. Re-run `dotnet build RetailLab.sln` and `dotnet test RetailLab.sln` in the normal development environment and capture the actual result.
4. Before 7B code, decide the exact server revision and causal-chain metadata and write acceptance tests for GUID-preserving bootstrap.
5. Before any write upload (7D), decide staff/device authentication, roles, enrollment, credential storage, rotation, revocation, and server-side actor attribution.
6. Add a minimal CI check once the local build is confirmed, then consider SDK/package reproducibility and a repository license.

## Appendix A — Pruned file tree

Excluded: `.git`, `bin`, `obj`, test result/build output, runtime databases, and generated migration designer files. Related files are grouped where that keeps the tree readable.

```text
RetailLab/
├── .continue/
│   └── rules/
│       ├── ARCHITECTURE.md
│       ├── DECISIONS.md
│       ├── PRODUCT.md
│       └── ROADMAP.md
├── .gitignore
├── AGENTS.md
├── README.md
├── RetailLab.sln
├── docs/
│   ├── ARCHITECTURE.md
│   ├── DECISIONS.md
│   ├── PRODUCT.md
│   ├── ROADMAP.md
│   ├── SYNCHRONIZATION.md              [untracked at intake]
│   └── images/
│       ├── README.md
│       ├── home.png
│       ├── catalogue.png
│       └── product-details.png
├── src/
│   ├── RetailLab.Core/
│   │   ├── RetailLab.Core.csproj
│   │   ├── Product.cs
│   │   ├── Bookmark.cs
│   │   ├── BasketItem.cs
│   │   ├── Order.cs
│   │   ├── OrderLine.cs
│   │   ├── OrderItemRequest.cs
│   │   ├── InventoryAdjustment.cs
│   │   ├── CustomerIdentifier.cs
│   │   ├── IRetailRepository.cs
│   │   ├── CatalogService.cs
│   │   ├── BookmarkService.cs
│   │   ├── BasketService.cs
│   │   ├── OrderService.cs
│   │   ├── CheckoutService.cs
│   │   ├── ProductService.cs
│   │   ├── InventoryService.cs
│   │   └── *Exception.cs               [4 conflict/rule exception types]
│   ├── RetailLab.Data/
│   │   ├── RetailLab.Data.csproj
│   │   ├── RetailLabDbContext.cs
│   │   ├── RetailLabDbContextFactory.cs
│   │   ├── RetailLabSqliteDatabase.cs
│   │   ├── EfRetailRepository.cs
│   │   ├── DatabaseInitializer.cs
│   │   ├── Configurations/
│   │   │   ├── ProductConfiguration.cs
│   │   │   ├── BookmarkConfiguration.cs
│   │   │   ├── BasketItemConfiguration.cs
│   │   │   ├── OrderConfiguration.cs
│   │   │   ├── OrderLineConfiguration.cs
│   │   │   └── InventoryAdjustmentConfiguration.cs
│   │   └── Migrations/
│   │       ├── 20260924121108_InitialCreate.cs
│   │       ├── 20260928101031_ProductArchiveAndAdjustments.cs
│   │       ├── 20260928173434_IdentitySchema.cs
│   │       ├── 20260928204200_BasketItems.cs
│   │       ├── 20260929121628_ProductVersion.cs
│   │       └── RetailLabDbContextModelSnapshot.cs
│   ├── RetailLab.Web/
│   │   ├── RetailLab.Web.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Models/                      [basket, product, order display records]
│   │   ├── Services/
│   │   │   ├── CatalogDisplayMapper.cs
│   │   │   └── FavouriteRedirects.cs
│   │   ├── Pages/
│   │   │   ├── Index.*
│   │   │   ├── Products.*
│   │   │   ├── Product.*
│   │   │   ├── Favourites.*
│   │   │   ├── Basket.*
│   │   │   ├── Error.*
│   │   │   ├── Account/{Register,Login,Logout}.*
│   │   │   ├── Orders/{Index,Details}.*
│   │   │   └── Shared/_Layout.cshtml
│   │   └── wwwroot/css/site.css
│   ├── RetailLab.Desktop/
│   │   ├── RetailLab.Desktop.csproj
│   │   ├── App.xaml*
│   │   ├── MainWindow.xaml*
│   │   ├── CreateProductWindow.xaml*
│   │   ├── EditProductWindow.xaml*
│   │   ├── AdjustmentHistoryWindow.xaml*
│   │   ├── InventoryProductRow.cs
│   │   └── InventoryAdjustmentRow.cs
│   └── RetailLab.LabCli/
│       ├── RetailLab.LabCli.csproj
│       ├── Program.cs
│       └── ConsoleRetailApp.cs
└── tests/
    └── RetailLab.Tests/
        ├── RetailLab.Tests.csproj
        ├── *ServiceTests.cs             [catalogue/bookmark/basket/order/checkout/product/inventory]
        ├── *PersistenceTests.cs         [basket/checkout/staff/general SQLite]
        ├── ProductConcurrencyTests.cs
        ├── IdentityAccountTests.cs
        ├── *DisplayTests.cs
        ├── FavouriteRedirectTests.cs
        ├── SqliteDatabaseTests.cs
        ├── ProductTests.cs
        ├── InMemoryRetailRepository.cs
        └── TestTimeProvider.cs
```

## Appendix B — Key configuration files verbatim

### `src/RetailLab.Core/RetailLab.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="RetailLab.Tests" />
  </ItemGroup>

</Project>
```

### `src/RetailLab.Data/RetailLab.Data.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\RetailLab.Core\RetailLab.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />
    <!-- Standard Identity EF store: framework-managed account tables. -->
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.12" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

### `src/RetailLab.Web/RetailLab.Web.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\RetailLab.Core\RetailLab.Core.csproj" />
    <ProjectReference Include="..\RetailLab.Data\RetailLab.Data.csproj" />
  </ItemGroup>

</Project>
```

### `src/RetailLab.Desktop/RetailLab.Desktop.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\RetailLab.Core\RetailLab.Core.csproj" />
    <ProjectReference Include="..\RetailLab.Data\RetailLab.Data.csproj" />
  </ItemGroup>

</Project>
```

### `src/RetailLab.LabCli/RetailLab.LabCli.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\RetailLab.Core\RetailLab.Core.csproj" />
    <ProjectReference Include="..\RetailLab.Data\RetailLab.Data.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

### `tests/RetailLab.Tests/RetailLab.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\RetailLab.Core\RetailLab.Core.csproj" />
    <ProjectReference Include="..\..\src\RetailLab.Data\RetailLab.Data.csproj" />
    <!-- Needed so mapper/display-model tests compile against the Web project.
         The reference points one way (Tests -> Web); production code is unaffected. -->
    <ProjectReference Include="..\..\src\RetailLab.Web\RetailLab.Web.csproj" />
  </ItemGroup>

</Project>
```

### `src/RetailLab.Web/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## Appendix C — Complete commit history (newest first)

```text
6b93282  2026-09-30  Merge pull request #8 from barakagitonga/feature/desktop-product-management
0295293  2026-09-30  feat: add desktop product editing and archive controls
7424763  2026-09-30  Merge pull request #7 from barakagitonga/feature/desktop-product-creation
b1378dc  2026-09-30  feat: add desktop product creation and refresh
ba64a04  2026-09-30  Merge pull request #6 from barakagitonga/feature/desktop-adjustment-history
cb68fdc  2026-09-30  feat: add desktop adjustment history
e49d70a  2026-09-30  Merge pull request #5 from barakagitonga/feature/desktop-inventory
496e6ba  2026-09-30  feat: add offline desktop inventory adjustment
a395954  2026-09-30  feat: add simulated web checkout and order history
dd54b37  2026-09-29  Merge pull request #4 from barakagitonga/feature/web-ordering
80f444f  2026-09-29  feat: add persistent customer basket
1ed7968  2026-09-28  Merge pull request #2 from barakagitonga/feature/customer-accounts-favourites
6915143  2026-09-28  feat: add customer accounts and favourites
b046d9f  2026-09-28  Merge pull request #1 from barakagitonga/feature/web-catalogue
4f11466  2026-09-28  docs: remove stale screenshot note
a252826  2026-09-28  docs: add project overview and screenshots
0b064d1  2026-09-28  feat: add customer web catalogue
c5f6bf6  2026-09-28  feat: add staff product and inventory management
64f70b1  2026-09-24  feat: add first retail domain prototype
fcfbd77  2026-09-21  build: add Entity Framework Core SQLite
e9bc11b  2026-09-21  docs: define RetailLab product and architecture
eb8e7a9  2026-09-20  docs: add Codex project instructions
```

---

**Bottom line:** RetailLab has a coherent and unusually well-documented local architecture for its size. The implementation is through Tutorial 6D; Tutorial 7A is an uncommitted design checkpoint. The next safe move is to preserve that design in Git, correct stale duplicate documentation, re-establish a fresh green build/test result, and then make the unresolved identity/revision/authentication decisions before writing synchronization code.
