# RetailLab development roadmap

This is a proposed sequence, not an implementation commitment or schedule. Only the repository skeleton currently exists. Each milestone should be delivered through small vertical slices: one observable behavior implemented through the layers it needs.

The intended product is described in [PRODUCT.md](PRODUCT.md); project boundaries are described in [ARCHITECTURE.md](ARCHITECTURE.md).

## Starting point

- Core, Data, LabCli, and Tests are included in the solution and target .NET 10.
- Project references establish the intended dependency direction.
- Source files are placeholders; there are no retail features or meaningful automated tests.
- Web, Desktop, EF Core configuration, and database migrations have not been created.

## 1. Console product catalogue

Outcome: a user can add and list products during one console session.

- Introduce a product model and validation in Core.
- Add a small add/list/exit interaction to LabCli using an in-memory collection.
- Replace the empty test with meaningful product validation tests.
- Document how to run the prototype and its temporary storage limitation.

Likely files: Core's placeholder and new `Product.cs`, LabCli's `Program.cs`, the test placeholder and new `ProductTests.cs`, and usage documentation. No new package is expected.

Completion check: demonstrate valid products, an empty catalogue, invalid numeric input, blank names, negative prices, and clean exit. The solution must build and tests must pass. Detailed behavior is proposed in [PRODUCT.md](PRODUCT.md).

## 2. Persistent product catalogue

Outcome: products can be saved and listed after restarting the console application.

- Explain the required EF Core SQLite package and migration tooling before adding them, including alternatives.
- Introduce a database context, product mapping, and an initial migration in Data.
- Connect LabCli to persistence while retaining business validation in Core.
- Document database location, migration steps, and how to preserve development data.

Completion check: save products, restart, reload them, and verify persistence with isolated database tests. Consider an unavailable or unwritable database and ensure failures are understandable.

## 3. Customer product catalogue on the web

Outcome: customers can browse persisted products in a Razor Pages website.

- Create RetailLab.Web using the agreed ASP.NET Core baseline.
- Start with a product list; add product details as a separate slice if useful.
- Settle the sample catalogue and currency display convention.
- Reuse Core and Data with an accessible, responsive layout.

Completion check: inspect populated and empty catalogues, keyboard navigation, narrow screens, and missing products if detail pages are added.

## 4. Local staff inventory application

Outcome: staff can inspect and update inventory in a WPF application using local SQLite storage.

- Agree on stock identifiers, units, and adjustment rules before implementation.
- Create RetailLab.Desktop and start with a product list.
- Add one stock adjustment workflow with shared rules in Core.
- Verify that the selected workflow works without network access and persists after restart.

Completion check: test valid and invalid stock adjustments, restart persistence, and offline use. Sharing models at this stage does not synchronize website and desktop data.

## 5. Customer accounts and favourites

Outcome: a customer can register, sign in, and maintain their own saved products.

- Select and configure framework-managed authentication within ASP.NET Core.
- Deliver registration/login first, then favourites in a separate slice.
- Define access rules and keep authentication credentials out of business tables.

Completion check: verify login failure feedback, protected actions, and isolation between two customers' favourites.

## 6. Simulated orders

Outcome: a customer can place a simulated order and view its confirmation.

- Decide order contents, price snapshots, stock effects, and insufficient-stock behavior before coding.
- Implement shared order rules and persistence with a minimal web workflow.
- Clearly identify the demonstration as simulated; no real payment processing.

Completion check: verify calculated amounts, invalid quantities, stock rules, duplicate submission handling, and consistency when saving an order fails.

## 7. Desktop and server synchronization

Outcome: selected offline desktop changes can be synchronized when connectivity returns.

- Define data ownership, stable identifiers, conflict policy, and the synchronization boundary.
- Explain and review any required new infrastructure or major technology before introducing it.
- Start with one synchronized entity or operation before expanding.

Completion check: demonstrate reconnecting, retrying interrupted work, repeated submissions, and conflicts without silent data loss. The mechanism is intentionally undecided until earlier workflows clarify the requirements.

## Working agreement for each implementation slice

1. Inspect the repository and explain the current state, objective, proposed approach, and likely files before significant edits.
2. Make the smallest maintainable change and explain important classes, relationships, data flow, alternatives, and failure cases.
3. Run `dotnet build RetailLab.sln` and `dotnet test RetailLab.sln` after meaningful code changes; report actual results and warnings.
4. Identify manual verification steps and update documentation when behavior or architecture materially changes.
5. Leave commits to the developer. Recommend a checkpoint commit before a large refactor and do not rewrite history without an explicit request.

A slice is complete when its intended behavior exists, the solution builds, relevant tests pass, important failure paths are considered, and the implementation and manual verification are explained. Dates and effort estimates should be set only when the next slice is selected.
