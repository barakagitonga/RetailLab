# RetailLab product brief

RetailLab is a learning and portfolio project demonstrating a small business retail software solution. Development should produce understandable working features while teaching their design and implementation.

This document describes intended behavior. The repository currently contains starter projects only; none of the retail workflows below is implemented. Project rules are defined in [AGENTS.md](../AGENTS.md).

## Intended users

| User | Needs |
| --- | --- |
| Customer | Browse products, register and sign in, save favourites, and place simulated orders. |
| Store staff | Manage inventory through a Windows desktop application, including during offline operation. |
| Developer and portfolio reviewer | Understand the architecture, run demonstrations, and see evidence of tested behavior. |

## Intended scope

- A customer-facing e-commerce website with a product catalogue.
- Customer registration, login, and bookmarked or favourite products.
- Simulated order placement without real payments.
- Store inventory management through a Windows desktop application.
- Local desktop storage for offline work.
- Shared business models and persistence design, with online synchronization introduced later.

Interfaces should be clean, consistent, accessible, responsive where appropriate, and understandable without technical knowledge. Business rules should behave consistently across interfaces.

## Proposed first prototype: console product catalogue

Status: proposed, not implemented. This is a temporary learning interface using the existing `RetailLab.LabCli` project.

Objective: enter a product name and price, then list the products entered during the current run.

### Acceptance criteria

- The user can choose to add a product, list products, or exit.
- A product has a nonblank name and a nonnegative `decimal` price; zero is allowed in this proposal.
- Invalid menu choices, blank names, invalid numeric input, and negative prices produce understandable feedback without crashing the application.
- Listing products shows their names and prices, or a clear message when the catalogue is empty.
- Products remain available during the current run and disappear when the application closes; this limitation is documented.
- Core owns product validation, and automated tests cover valid and invalid product values.

This prototype needs no database, authentication, stock quantities, orders, web project, or desktop project. Persistence is the proposed next slice. The currency and pricing display convention must be settled before a customer-facing price display is implemented.

## Scope boundaries

Real payment processing is excluded unless explicitly requested in a later phase. Deployment infrastructure, advanced inventory workflows, and synchronization mechanisms have not been selected. Add complexity only when a concrete feature needs it.

## Questions to resolve as features approach

- What type of retailer, sample products, and currency should the demonstration use?
- Which product identifiers and stock units are needed?
- When does a simulated order affect stock, and what happens when stock is insufficient?
- What permissions do customers and staff need?
- Which desktop actions must work offline, and how should conflicting changes be resolved?

See [ROADMAP.md](ROADMAP.md) for the proposed delivery sequence and [DECISIONS.md](DECISIONS.md) for decision status.
