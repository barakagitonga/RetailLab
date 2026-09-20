# RetailLab — Codex Project Instructions

## Project mission

RetailLab is a learning and portfolio project demonstrating the design and development of a small business retail software solution.

The system will eventually contain:

- a customer-facing e-commerce website;
- customer registration and login;
- a product catalogue;
- bookmarked/favourite products;
- simulated order placement;
- store inventory management;
- a Windows desktop application for staff;
- offline desktop operation;
- shared business models and database architecture;
- later online synchronization between desktop and server.

This project is educational and demonstrative. It does not process real payments.

## Developer profile

The primary developer is a beginner software developer.

Treat development as both software delivery and guided learning.

Do not merely generate large amounts of code.

When introducing an important concept:

- explain what it is;
- explain why the project needs it;
- explain where it belongs in the architecture;
- explain important alternatives when relevant.

Prefer clear and maintainable solutions over clever or highly abstract solutions.

## Technology baseline

Unless the developer explicitly approves a change, use:

- .NET 10
- C#
- ASP.NET Core
- Razor Pages for the initial website
- WPF for the initial Windows desktop application
- Entity Framework Core
- SQLite during development and for offline desktop storage
- xUnit for automated tests
- Git for version control

Do not introduce JavaScript frameworks, cloud infrastructure, containers, message brokers, microservices, or other major technologies without first explaining why they are needed.

## Architecture

Keep responsibilities separated.

### RetailLab.Core

Contains business entities, business rules, interfaces, and logic that should not depend on UI frameworks or Entity Framework Core.

Core must not depend on:

- ASP.NET Core
- WPF
- Entity Framework Core
- SQLite

### RetailLab.Data

Contains database and persistence implementation.

It may depend on RetailLab.Core.

Entity Framework Core configuration belongs here.

### RetailLab.Web

Contains the customer-facing web interface.

It may depend on RetailLab.Core and RetailLab.Data.

### RetailLab.Desktop

Contains the Windows staff application.

It may depend on RetailLab.Core and RetailLab.Data.

### RetailLab.LabCli

Contains temporary console-based experiments and learning exercises.

Do not place essential production architecture only in this project.

### RetailLab.Tests

Contains automated tests.

## Development approach

Build the system using small vertical slices.

For a significant task:

1. Inspect the existing repository first.
2. Explain what currently exists.
3. State the objective.
4. Propose a short implementation plan.
5. Identify which files are likely to change.
6. Make the smallest reasonable implementation.
7. Build the solution.
8. Run relevant automated tests.
9. Report any warnings or failures.
10. Summarize what changed.
11. Explain what the developer should manually test.

For architecture changes or tasks affecting several files, present the plan before editing.

Small obvious corrections may be implemented directly.

## AI-assisted learning rules

Do not hide important implementation decisions from the developer.

When generating important code, explain:

- the role of each class;
- major relationships;
- how data flows through the application;
- failure cases worth knowing about.

Avoid generating enormous files when smaller focused classes are appropriate.

When possible, teach through one working feature at a time.

## Dependency rules

Do not install a new production NuGet package without explaining:

- what package is being introduced;
- why it is necessary;
- what problem it solves;
- whether the same result can reasonably be achieved without it.

Do not replace existing major libraries or frameworks without explicit approval.

## Database rules

Use migrations for persistent schema changes once migrations are introduced.

Do not silently delete or recreate databases containing useful test data.

Use decimal rather than floating-point types for monetary values.

Use UTC for persisted timestamps unless there is a specific reason otherwise.

Do not store passwords in RetailLab business tables.

Authentication credentials must be managed through the authentication framework.

## Security

Never hard-code:

- passwords;
- API keys;
- access tokens;
- connection secrets;
- private credentials.

Never commit secrets to Git.

The e-commerce workflow is simulated. Do not add real payment processing unless explicitly requested in a later project phase.

## Git safety

Do not automatically commit changes.

The developer decides when a Git commit is created.

Before a large refactor, recommend creating a checkpoint commit.

Do not rewrite Git history unless explicitly requested.

## UI and UX

The project is also a UI/UX portfolio exercise.

Interfaces should therefore aim to be:

- clean;
- consistent;
- accessible;
- responsive where appropriate;
- understandable without technical knowledge.

Separate business logic from visual presentation.

## Testing

After meaningful code changes, run:

`dotnet build RetailLab.sln`

When automated tests exist, also run:

`dotnet test RetailLab.sln`

Do not claim that code works simply because it was generated.

State what was actually compiled, tested, or manually verified.

## Definition of done

A development task is complete when:

- its intended behavior exists;
- the solution builds;
- relevant automated tests pass;
- important failure paths are considered;
- the implementation is explained;
- manual verification steps are identified;
- documentation is updated when architecture or behavior materially changes.