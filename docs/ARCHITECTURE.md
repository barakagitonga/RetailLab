# RetailLab architecture

This document separates the inspected repository from the intended architecture in [AGENTS.md](../AGENTS.md). It does not describe unimplemented features as available.

## Current solution

`RetailLab.sln` contains four C# projects, all targeting `net10.0` with nullable reference checking and implicit imports enabled.

| Location | Current contents | Responsibility |
| --- | --- | --- |
| `src/RetailLab.Core` | An empty `Class1` class. | Business entities, rules, interfaces, and shared logic. |
| `src/RetailLab.Data` | An empty `Class1` class. | Database access and persistence implementation. |
| `src/RetailLab.LabCli` | A console program that prints `Hello, World!`. | Temporary experiments and learning exercises. |
| `tests/RetailLab.Tests` | An empty xUnit test without assertions. | Automated tests of implemented behavior. |

The test project references xUnit, Microsoft.NET.Test.Sdk, the Visual Studio xUnit runner, and Coverlet. No production NuGet packages, database context, migrations, or retail entities are present. The empty test provides no behavioral verification.

## Current project references

An arrow means that the project on the left references the project on the right.

```text
RetailLab.Core   -> no other projects
RetailLab.Data   -> RetailLab.Core
RetailLab.LabCli -> RetailLab.Core, RetailLab.Data
RetailLab.Tests  -> RetailLab.Core, RetailLab.Data
```

These references allow the outer projects to use the business model while keeping Core independent of storage and presentation.

## Intended project boundaries

### RetailLab.Core

Core defines what business objects mean and which operations are valid. It must not depend on ASP.NET Core, WPF, Entity Framework Core, or SQLite.

For example, a proposed product rule rejecting negative prices belongs here so every interface can reuse it. Introduce interfaces when a concrete need justifies them; do not add abstractions solely for possible future features.

### RetailLab.Data

Data implements persistence and may depend on Core. Entity Framework Core configuration and migrations belong here. A future database context will connect business objects to database tables; it is not itself the owner of business rules.

SQLite is the agreed development database and offline desktop storage technology. Persistent schema changes must use migrations once introduced. Useful test data must not be silently deleted or recreated.

### RetailLab.Web — planned, not created

The customer-facing website will use ASP.NET Core and Razor Pages. It may reference Core and Data. Page handling and presentation belong here; reusable business rules belong in Core.

### RetailLab.Desktop — planned, not created

The staff application will use WPF. It may reference Core and Data and will eventually work against local storage while offline. Shared models do not imply that the desktop and website will access the same physical database file. Synchronization transport and conflict handling are undecided.

### RetailLab.LabCli

The console application provides a small environment for learning and experiments. It may handle input, display output, and call shared code. Essential production architecture must not exist only here.

### RetailLab.Tests

Tests verify business behavior and, when persistence exists, database behavior. Tests of Core should not require a UI or database. Persistence tests should use isolated test data and never overwrite a useful development database.

## Proposed first data flow

```text
Console input -> parse menu and price -> Core product validation
              -> temporary collection -> console product listing
```

LabCli translates user input into values and displays failures clearly. Core enforces product rules. The temporary collection is held in LabCli for this experiment; Data has no role until the persistence slice.

The next proposed flow adds a Data implementation that saves validated products through EF Core to SQLite and reloads them for display. Exact storage interfaces and database configuration will be designed before that slice is implemented.

## Cross-cutting rules

- Use `decimal` for monetary values and UTC for persisted timestamps unless there is a specific reason otherwise.
- Authentication credentials must be managed by the authentication framework, never by password fields in business tables.
- Never hard-code or commit passwords, keys, tokens, or connection secrets.
- Keep presentation separate from business logic and provide understandable error feedback.
- Preserve the technology baseline unless the developer explicitly approves a change. Explain new production dependencies and reasonable alternatives before installation.

See [DECISIONS.md](DECISIONS.md) for the rationale and unresolved choices, and [ROADMAP.md](ROADMAP.md) for implementation order.
