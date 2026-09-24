# RetailLab decision log

This log records established choices and distinguishes them from future work. Project-wide instructions in [AGENTS.md](../AGENTS.md) remain authoritative.

## D001 - Learning through small working features

Status: established.

Develop RetailLab as both a learning project and a portfolio demonstration using small, understandable vertical slices. Important responsibilities, data flow, alternatives, and failure cases must be explained and tested.

## D002 - Technology baseline

Status: established and partly implemented.

Use .NET 10, C#, ASP.NET Core with Razor Pages, WPF, Entity Framework Core, SQLite, xUnit, and Git. Lab Prototype 1 implements the console, Core, EF Core SQLite, migrations, and xUnit portions. Web and WPF remain future work.

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

## Open decisions

| Question | Resolve before |
| --- | --- |
| Demonstration retailer, currency, and price display conventions | Customer-facing catalogue |
| Product creation/editing, stock adjustment audit, and deletion/archive rules | Staff inventory implementation |
| Authentication implementation and staff/customer permissions | Protected web workflows |
| Concurrency strategy and duplicate submission handling | Multi-user ordering |
| Server storage, data ownership, synchronization transport, and conflict policy | Synchronization implementation |
