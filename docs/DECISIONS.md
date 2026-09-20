# RetailLab decision log

This log records the baseline from [AGENTS.md](../AGENTS.md) and distinguishes it from proposals. An established decision below is inherited from those instructions; it does not mean the corresponding feature is implemented. Proposed choices remain open for developer review.

## D001 — Learning through small working features

Status: established by AGENTS.md.

Decision: develop RetailLab as both a learning project and a portfolio demonstration, using small vertical slices and clear, maintainable code.

Reason: the primary developer is a beginner and needs to understand the delivered system. Explain important concepts, class responsibilities, data flow, alternatives, and failure cases as they arise.

Consequence: present a plan and likely file changes before significant edits. Avoid large speculative abstractions and keep essential business logic outside LabCli.

## D002 — Technology baseline

Status: established by AGENTS.md; partly scaffolded.

Decision: use C#, .NET 10, ASP.NET Core with Razor Pages, WPF, Entity Framework Core, SQLite, xUnit, and Git.

Reason: this is the project's agreed stack for its web, Windows, persistence, and testing needs.

Consequence: baseline changes and replacement of major frameworks require explicit developer approval. Explain the purpose, necessity, and reasonable alternatives for each new production NuGet package before installation. Major infrastructure and additional frameworks require explanation before introduction.

Current implementation: the four projects target .NET 10 and the test project references xUnit. Web, WPF, EF Core, and SQLite are not yet implemented.

## D003 — Separate business rules, storage, and presentation

Status: established by AGENTS.md; initial project references exist.

Decision: Core owns business entities and rules; Data owns persistence; Web and Desktop own their interfaces. LabCli hosts experiments, and Tests verifies behavior.

Reason: the future interfaces need to reuse business behavior without coupling it to a UI framework or database.

Consequence: Core must not reference ASP.NET Core, WPF, EF Core, or SQLite. Data may reference Core; Web and Desktop may reference Core and Data. Keep EF configuration in Data. See [ARCHITECTURE.md](ARCHITECTURE.md) for actual references.

## D004 — Data conventions and preservation

Status: established by AGENTS.md; persistence is not implemented.

Decision: use `decimal` for money, UTC for persisted timestamps unless a specific exception is justified, and migrations for persistent schema changes once introduced. Preserve useful test data.

Reason: consistent value and time conventions support shared behavior, while controlled schema changes protect development data.

Consequence: do not silently delete or recreate useful databases. Currency, rounding, database location, and detailed schema design remain to be decided when needed.

## D005 — Framework-managed credentials and simulated commerce

Status: established by AGENTS.md; authentication and ordering are not implemented.

Decision: an authentication framework manages credentials; business tables must not store passwords. Never hard-code or commit secrets. Orders are simulated and do not process real payments.

Reason: RetailLab is an educational retail demonstration with explicit credential and payment boundaries.

Consequence: choose the authentication implementation during the accounts slice. Real payment processing requires an explicit later request.

## D006 — Start with a console catalogue held in memory

Status: proposed; not approved as an implementation task and not implemented.

Proposal: use LabCli to add and list products, with reusable name and price validation in Core and meaningful xUnit tests. A name must be nonblank and a price nonnegative, including zero.

Reason: this is a small observable retail workflow using the existing projects and test dependencies. It teaches entities, validation, input handling, and tests before persistence or UI frameworks are introduced.

Alternatives: starting with SQLite would demonstrate persistence immediately but adds database configuration and migrations; starting with Razor Pages would create a visible website but adds a new project and web concepts at the same time.

Consequence if selected: products disappear when the console exits. Data remains a placeholder until the next slice. No new production package is expected for this prototype. See [PRODUCT.md](PRODUCT.md) for acceptance criteria.

## D007 — Defer synchronization design until local workflows exist

Status: later synchronization is established by AGENTS.md; the design remains open.

Direction: support offline desktop work first, then design synchronization around demonstrated data and workflows.

Reason: conflict handling and data ownership depend on which changes staff can make offline and which changes also occur on the server.

Consequence: shared models and persistence code do not imply one shared database file. No transport, hosting platform, server database, conflict policy, or synchronization library has been selected.

## Open decisions

| Question | Resolve before |
| --- | --- |
| Demonstration retailer, currency, and price display conventions | Customer-facing catalogue |
| Product identifiers, stock units, and adjustment rules | Inventory implementation |
| Database location and migration workflow | Persistent catalogue |
| Authentication implementation and staff/customer permissions | Protected workflows |
| Order price snapshots, stock effects, and failure handling | Simulated ordering |
| Server storage, data ownership, synchronization transport, and conflict policy | Synchronization implementation |

## Maintaining this log

When a significant choice is made, record its status, reason, alternatives where relevant, and consequences. Update proposals when the developer selects or rejects them. Preserve the explanation of superseded choices when it helps future readers understand the architecture. The proposed delivery order is in [ROADMAP.md](ROADMAP.md).
