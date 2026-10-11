# KCA UNIVERSITY
## FACULTY OF COMPUTING AND INFORMATION TECHNOLOGY
### UNDERGRADUATE / DIPLOMA PROJECT PROPOSAL

---

# RetailLab: An Audited, Multi-Interface Retail Management and E-Commerce System with Offline Desktop Operation and Synchronization Architecture

- **Student Name:** Baraka Gitonga
- **Email:** barakagitongam@gmail.com
- **Supervisor:** Merab Omondi
- **Institution:** KCA University — Faculty of Computing and Information Technology
- **Academic Year:** 2026 / 2027
- **Submission Date:** September / October 2026

---

## Executive Summary / Abstract

Small and medium-sized retail enterprises (SMEs) frequently operate in hybrid retail environments requiring both an online e-commerce storefront for customer discovery and an in-store Point-of-Sale (POS) or inventory workspace for staff. Existing commercial offerings are predominantly polarized: modern solutions require uninterrupted cloud connectivity, rendering physical store checkout vulnerable to internet dropouts, while traditional offline-first desktop systems remain siloed from online sales channels. Furthermore, high licensing fees of enterprise-grade Enterprise Resource Planning (ERP) systems make unified multi-channel inventory inaccessible to small retail businesses.

This project proposes **RetailLab**, a resilient, modular, and multi-interface retail software system built on .NET 10. RetailLab bridges physical and digital retail operations by sharing a single domain core across three front-ends: a responsive customer-facing web storefront (built with ASP.NET Core Razor Pages), an offline-capable desktop staff inventory workspace (built with Windows Presentation Foundation - WPF), and an administrative console workflow. The system implements transactional multi-line ordering, customer account management with ASP.NET Core Identity, strict audit logging with signed inventory adjustments, optimistic concurrency to prevent overselling, and an architectural foundation for asynchronous desktop-to-server synchronization over SQLite and Entity Framework Core. This proposal details the background, problem statement, research objectives, literature review, system architecture, methodology, implementation schedule, and project budget required for the complete lifecycle of the project.

---

## 1. Introduction and Background

### 1.1 Background of the Study
The retail sector forms the economic backbone of small and medium enterprises (SMEs) globally and across emerging economies like Kenya. In recent years, consumer expectations have shifted toward multi-channel experiences: customers demand the convenience of browsing catalogues, checking stock availability, maintaining wishlists/favourites, and placing orders online from mobile or desktop browsers. Concurrently, physical brick-and-mortar storefronts remain the primary revenue generator for most small businesses, requiring reliable, fast, and continuous in-person stock control.

Historically, small retailers have maintained separate tools: an independent website (or social media page) for online orders, and a standalone desktop ledger, cash register, or spreadsheets for counter inventory. When these systems operate in isolation, inventory figures diverge rapidly. When retailers attempt to deploy pure cloud-based web POS platforms, they face severe operational risks due to intermittent internet connectivity, power fluctuations, and broadband latency. When connectivity drops, physical store operations grind to a halt.

There is an urgent need for software architectures that offer the best of both worlds: robust online customer engagement coupled with independent, offline-tolerant desktop operation that synchronizes reliably when connectivity is present, all while enforcing strict auditability and preventing stock conflicts.

### 1.2 Problem Statement
Small retail businesses operating in environments with intermittent internet connectivity face critical operational bottlenecks:

1. **System Disconnect and Overselling:** Disconnected sales channels lead to discrepancies between physical stock and online catalogues. Without unified, real-time or synchronized stock controls, retailers risk selling goods online that have already been purchased at the physical counter, causing stockouts, order cancellations, and customer dissatisfaction.
2. **Fragility of Cloud-Only POS Systems:** Cloud-only Point of Sale (POS) and inventory tools fail when broadband access is disrupted or unstable. In retail environments, physical store transactions cannot wait for internet restoration without losing sales.
3. **Lack of Auditability and Accountability:** Informal inventory tracking and basic software systems often lack immutable audit trails. When stock levels decrease due to damages, stocktakes, supplier deliveries, or shrinkage, generic software allows direct count overwrites without capturing signed deltas, reasons, timestamps, or actor accountability.
4. **Prohibitive Cost and Complexity of Enterprise ERPs:** Enterprise solutions (such as SAP Business One or Microsoft Dynamics 365) offer offline sync and multi-channel inventory, but their capital cost, implementation overhead, and hardware demands exceed the budget and technical literacy of small business retailers.

### 1.3 Project Objectives

#### 1.3.1 General Objective
To design, develop, test, and document **RetailLab**, an audited, multi-interface retail management software solution that enables offline-tolerant desktop inventory management, web-based customer commerce, and asynchronous synchronization over a shared business domain.

#### 1.3.2 Specific Objectives
1. **Design and implement a customer-facing e-commerce web application** utilizing ASP.NET Core Razor Pages and Identity to allow user registration, catalogue browsing with availability bands (`In stock`, `Low stock`, `Out of stock`), shopping basket management, and simulated checkout.
2. **Develop an offline-capable Windows Desktop (WPF) application** for store staff that allows real-time local stock tracking, product management (creation, editing, archiving/unarchiving), and audited stock adjustments.
3. **Establish an audited inventory and transactional domain core** that enforces business rules, records signed inventory deltas with required reasons and timestamps, and uses optimistic concurrency tokens (`Version`) to prevent race conditions and overselling.
4. **Architect an asynchronous synchronization mechanism** (incorporating pairing, local SQLite outbox/inbox queues, and cursor tracking) to enable offline desktop nodes to synchronize catalog updates and stock movements with the central web platform.
5. **Verify and validate the system** through automated unit, integration, and concurrency testing, as well as usability evaluations against defined functional and non-functional requirements.

### 1.4 Research & Design Questions
- How can a clean domain architecture be structured in .NET 10 so that web, desktop, and console clients share identical business validation without duplicate code?
- How can offline desktop inventory modifications and concurrent web checkouts be reconciled to prevent negative stock and duplicate order submission?
- What data structures and synchronization patterns (such as outbox feeds and revision cursors) best support eventual consistency between a local SQLite store and a central database?
- How can audit trails for inventory movements be maintained immutably across both automated sales and manual staff adjustments?

### 1.5 Justification and Rationale
- **Economic Relevance for SMEs:** RetailLab demonstrates a cost-effective, maintainable retail architecture tailored for small businesses that cannot afford multi-tenant SaaS ERP subscriptions or cloud-only reliability risks.
- **Architectural Resilience:** By using local SQLite databases and an offline-tolerant design, retail staff can continue ringing up sales and adjusting stock even during complete network blackouts.
- **Academic & Portfolio Rigor:** The project provides an in-depth exploration of software engineering principles: clean layering, Domain-Driven Design (DDD) concepts, transactional consistency, optimistic concurrency control, and automated testing in modern .NET 10.

### 1.6 Scope and Delimitations

#### In Scope:
- **Core Domain:** Business entities (`Product`, `Order`, `OrderLine`, `Bookmark`, `BasketItem`, `InventoryAdjustment`), business rules, conflict exceptions, and domain services.
- **Web Storefront:** Customer registration/login via ASP.NET Core Identity, product catalogue display with stock bands, customer favourites, shopping basket, and simulated checkout.
- **Staff Desktop Application:** Windows WPF application with local inventory list, product creation dialog, product edit modal with stale-form concurrency protection, soft-archive/unarchive workflows, and stock adjustment audit history.
- **Persistence & Migration:** EF Core with SQLite, additive database migrations, idempotent seed data, and connection pooling.
- **Synchronization Design:** Detailed architectural specification for push-then-pull synchronization, outbox/inbox schema, and revision tracking.
- **Automated Testing:** Extensive xUnit test coverage spanning unit, persistence, and concurrency integration tests.

#### Out of Scope (Delimitations):
- **Real Payment Processing:** Payment transactions are simulated end-to-end; no live credit card or mobile money (e.g., M-Pesa) API integration is included in the initial prototype to avoid PCI-DSS compliance overhead.
- **External Shipping / Logistics Tracking:** Order fulfillment stops at order generation and inventory reduction; courier integration is deferred to future commercial iterations.
- **Cross-Platform Mobile Application:** Focus is restricted to responsive Web and native Windows Desktop (WPF).

### 1.7 Significance of the Study
- **For Retailers:** Demonstrates how SMEs can safeguard in-store operations against internet downtime while maintaining a modern digital storefront.
- **For Software Practitioners:** Serves as a reference implementation demonstrating clean separation between business logic (`RetailLab.Core`), persistence (`RetailLab.Data`), and diverse presentation technologies (`Web`, `Desktop`, `CLI`).
- **For the Student:** Meets the academic requirements of KCA University Faculty of Computing and Information Technology, demonstrating mastery of software engineering, system design, testing, and modern C#/.NET development.

---

## 2. Literature Review

### 2.1 Overview of Retail Information Systems in Small Businesses
Information systems in the retail industry have evolved from standalone mechanical cash registers and disconnected desktop spreadsheets to integrated Point-of-Sale (POS) and Enterprise Resource Planning (ERP) networks. Research demonstrates that multi-channel retail operations—where merchants interact with customers across both physical and digital touchpoints—generate higher customer retention and average transaction volume than single-channel operations (Verhoef et al., 2015). However, SMEs often fail to implement multi-channel systems due to technological fragmentation, implementation complexity, and prohibitive recurring SaaS fees.

### 2.2 Analysis of Existing Retail Solutions

| System Category | Representative Examples | Key Strengths | Critical Shortcomings for Local SMEs |
| :--- | :--- | :--- | :--- |
| **Cloud-Only POS & E-Commerce** | Shopify POS, Vend, Lightspeed | Centralized real-time catalogue; accessible from any browser or tablet. | Complete operational failure during network blackouts; ongoing subscription costs in foreign currency; latency on slow connections. |
| **Legacy Desktop POS** | QuickBooks POS, legacy Access/FoxPro POS | Runs locally without internet; high UI responsiveness; one-time licensing. | Siloed from modern web storefronts; difficult remote synchronization; lacks web customer self-service. |
| **Enterprise ERPs** | SAP Business One, Microsoft Dynamics 365 | Comprehensive multi-channel integration; offline store sync engines. | Prohibitive licensing and implementation costs; complex configuration requiring specialized consultants; excessive resource overhead. |

**Synthesis:** A clear market gap exists for a lightweight, modular software architecture that provides the reliability of a local offline desktop application alongside a clean web storefront, utilizing efficient local database engines and decoupled synchronization.

### 2.3 Distributed Data Consistency and Offline Synchronization
Managing data consistency across disconnected nodes is a classic computer science problem addressed by the CAP theorem (Consistency, Availability, Partition tolerance) (Brewer, 2012). In offline retail systems, availability and partition tolerance are paramount: a shop cashier cannot be prevented from recording sales or adjustments when the network is unavailable.

To resolve data conflicts in disconnected nodes, modern distributed system design favors:
1. **Delta-Based Stock Adjustments:** Rather than sending absolute inventory counts (e.g., "Product X has 15 units"), systems record signed differential events (e.g., "Add +5 units", "Deduct -2 units"). This allows mathematical commutativity during reconciliation.
2. **Outbox and Inbox Patterns:** Modifications made while offline are staged in a persistent local outbox table. When connectivity is restored, the changes are sent sequentially to the server inbox using a transactional cursor mechanism.
3. **Optimistic Concurrency Control:** Using version tokens or row versions prevents stale client screens from silently overwriting recent changes made by concurrent users.

### 2.4 Technology Baseline Justification
- **.NET 10 & C#:** Provides enterprise-grade type safety, high runtime performance, asynchronous programming (`async`/`await`), and cross-layer code sharing.
- **ASP.NET Core Razor Pages:** Emphasizes page-focused development for server-rendered HTML, providing fast load times, excellent security (built-in anti-forgery, input sanitization), and low overhead without demanding complex JavaScript build toolchains.
- **Windows Presentation Foundation (WPF):** The industry standard for high-performance, native Windows business applications, ideal for desktop POS and warehouse management with rich data-binding.
- **Entity Framework Core with SQLite:** EF Core provides expressive object-relational mapping, transactional integrity, and automated schema migrations. SQLite delivers an embedded, serverless, self-contained database engine ideal for zero-configuration local desktop installations and offline environments.
- **xUnit Test Framework:** Industry-standard automated unit and integration testing engine ensuring high regression safety and test reproducibility.

### 2.5 Conceptual Framework

```text
+-------------------------------------------------------------+
|                     PRESENTATION LAYER                       |
|  [Customer Web - Razor Pages]     [Staff Desktop - WPF]     |
|   - Product Catalogue              - Exact Inventory Grid    |
|   - Identity Auth & Favourites     - Stock Adjustment Dialog |
|   - Basket & Simulated Checkout    - Stale-Form Edit Protection |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
|                     BUSINESS DOMAIN CORE                     |
|                      (RetailLab.Core)                       |
|   - Business Entities (Product, Order, Basket, Audit)       |
|   - Services (Catalog, Basket, Checkout, Inventory, Product) |
|   - Validation & Conflict Exceptions                         |
|   - Repository Contracts (IRetailRepository)                |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
|                     PERSISTENCE LAYER                        |
|                      (RetailLab.Data)                       |
|   - EF Core DbContext & Mappings                            |
|   - Optimistic Concurrency Engine (Version tokens)           |
|   - ASP.NET Core Identity EF Stores                         |
|   - Schema Migrations & Idempotent Seeder                   |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
|                     DATA STORAGE LAYER                       |
|          Local SQLite Database (retaillab.db)                |
+-------------------------------------------------------------+
```

---

## 3. System Architecture and Methodology

### 3.1 Architectural Decomposition
The solution strictly separates concerns across modular projects:

1. **`RetailLab.Core` (Class Library):**
   - Independent of all UI frameworks and database drivers.
   - Enforces business invariants: non-negative inventory, immutable SKUs, required audit reasons, price snapshotting on checkout, and optimistic concurrency version validation.
2. **`RetailLab.Data` (Class Library):**
   - Contains EF Core configuration, entity mappings, migrations, and repository implementation.
   - Houses the ASP.NET Core Identity store for secure credential handling.
3. **`RetailLab.Web` (ASP.NET Core Web Application):**
   - Customer-facing website with Razor Pages.
   - Implements Post-Redirect-Get (PRG), antiforgery tokens, TempData notifications, and customer-scoped authorization.
4. **`RetailLab.Desktop` (WPF Desktop Application):**
   - Native Windows desktop client for retail staff.
   - Operates against local SQLite, providing fast data grids, modal dialogs, and instant validation feedback.
5. **`RetailLab.LabCli` (Console Application):**
   - Terminal-based interactive tool for rapid administrative debugging and CLI workflows.
6. **`RetailLab.Tests` (xUnit Test Project):**
   - Automated unit, integration, persistence, and concurrency tests.

### 3.2 Software Development Methodology
The project follows an **Agile Vertical Slice Development Methodology**. Rather than building horizontal tiers in isolation (e.g., building all database tables first, then all services, then all UIs), the project is engineered in complete, end-to-end vertical slices. Each slice introduces a single working feature:
1. **Requirement & Domain Modeling:** Identify the entities and business invariants.
2. **Core Logic & Unit Tests:** Implement services and write unit tests.
3. **Persistence & Migration:** Configure EF mappings and generate an additive database migration.
4. **UI Implementation:** Build the web or desktop user interface.
5. **Verification & Regression Testing:** Compile the entire solution (`dotnet build`) and execute the automated test suite (`dotnet test`).

---

## 4. Project Implementation Plan and Schedule

The project is structured across a 14-week university schedule spanning two trimesters / project phases:

```text
Week 1 - 2:   Ideation, Problem Formulation & Supervisor Allocation
Week 3 - 4:   Proposal Preparation, Literature Review & Milestone Submission (Current Milestone)
Week 5:       Proposal Defense / Faculty Presentation
Week 6 - 7:   System Requirements Specification (SRS) & System Design Specification (SDS)
Week 8 - 9:   Core Domain & Persistent Console Slice (RetailLab.Core, RetailLab.Data, CLI)
Week 10 - 11: Web Catalogue, Customer Identity & Favourites Slice (RetailLab.Web)
Week 12 - 13: Customer Basket & Simulated Web Checkout Slice
Week 14:      Mid-Project Review & First Trimester Wrap-Up

Week 15 - 16: WPF Desktop Staff Inventory & Audited Adjustment Workflows (RetailLab.Desktop)
Week 17 - 18: Desktop Product Creation, Editing & Optimistic Concurrency Protection
Week 19 - 20: Offline Synchronization Architecture & Change-Feed Foundation
Week 21 - 22: Integration Testing, Concurrency Race Condition Validation & Bug Fixing
Week 23:      User Manual Preparation & Final Spiral-Bound Documentation
Week 24:      Final System Compilation, Working Software Demonstration & Final Presentation
```

### Milestone Deliverables Summary Table

| Week | Milestone / Deliverable | Status / Target | Faculty Mark Weight |
| :---: | :--- | :--- | :---: |
| W4 | **Project Proposal Document** | **Completed / Submitted** | **10%** |
| W5 | **Proposal Presentation** | Scheduled | **10%** |
| W7 | **System Requirements Specifications (SRS)** | In Preparation | **10%** |
| W8 | **System Design Specifications (SDS)** | Scheduled | **10%** |
| W14-20 | **System Implementation (Core, Web, Desktop)** | In Progress / Passing Tests | **10%** |
| W21-22 | **System Testing & Evaluation** | 141 Automated Tests Passing | **5%** |
| W23 | **User Manual** | Scheduled | **5%** |
| W23 | **Final Project Documentation** | Scheduled | **10%** |
| W24 | **Compiled System & Code Artifacts** | Ready for Demonstration | **10%** |
| W24 | **Final Project Presentation & Defense** | Scheduled | **20%** |
| **Total** | | | **100%** |

---

## 5. Project Resource Plan and Budget

The budget is prepared based on prevailing standard commercial and development resource rates in Kenya (KES) with US Dollar equivalents. Because RetailLab prioritizes open-source, non-proprietary frameworks (.NET 10, SQLite, Razor Pages, WPF), recurring software licensing costs are effectively zero.

| Item Category | Item Description | Quantity / Duration | Cost (KES) | Cost (USD) | Source / Justification |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Hardware** | Development & Test Laptop (Core i7, 16GB RAM, 512GB SSD) | 1 unit | KES 85,000 | $650 | Required for .NET 10 SDK, IDEs, and WPF compilation |
| **Hardware** | Mobile Testing Device / Secondary Display | 1 unit | KES 20,000 | $150 | Testing responsive web layout on mobile viewports |
| **Software & Tools** | Microsoft Visual Studio Community 2026 / VS Code | 1 license | Free / Open Source | $0 | Standard academic/community developer license |
| **Software & Tools** | Git Version Control & GitHub Repository Hosting | 1 repository | Free Tier | $0 | Public repository source control |
| **Software & Tools** | SQLite Database Engine & EF Core Tools | Open Source | Free / Open Source | $0 | Embedded runtime database, zero licensing fees |
| **Connectivity** | High-Speed Internet Access for Research & Documentation | 6 months | KES 18,000 | $140 | Continuous research, package restoration, Git sync |
| **Documentation** | Printing, Binding, & Proposal Documentation Materials | Spiral / Hardbound copies | KES 4,500 | $35 | Submission of academic copies to university panel |
| **Contingency** | Miscellaneous Emergency Expenses / Backup Power Storage | 10% Contingency | KES 12,500 | $95 | Unforeseen hardware repair, testing consumables |
| **Total Project Budget** | | | **KES 140,000** | **$1,070** | |

---

## 6. Expected Outcomes and Deliverables

Upon completion of the project, the following deliverables will be formally produced and submitted:
1. **Fully Functional, Layered Software Solution:**
   - Responsive ASP.NET Core Razor Pages storefront with customer account management, favourites, basket, and checkout.
   - WPF desktop staff workspace supporting offline inventory views, audited adjustments, history inspection, and safe product editing.
   - Shared pure C# business domain library (`RetailLab.Core`) and data access layer (`RetailLab.Data`).
2. **Automated Test Suite:**
   - Over 140 comprehensive xUnit tests covering business rules, optimistic concurrency, SQLite migrations, and security isolation.
3. **Formal Documentation Suite:**
   - Approved Project Proposal.
   - System Requirements Specification (SRS) and System Design Specification (SDS).
   - User Manual detailing installation, operation, and staff administration.
   - Final spiral-bound academic project report.
4. **Demonstrable Software Release:**
   - Compiled source code and executable build ready for presentation before the Faculty Examination Panel.

---

## 7. References

1. Brewer, E. (2012). Push the limits of the CAP theorem. *IEEE Computer*, 45(2), 23-29.
2. Fowler, M. (2002). *Patterns of Enterprise Application Architecture*. Addison-Wesley Professional.
3. Freeman, A. (2022). *Pro ASP.NET Core 6: Develop Cloud-Ready Web Applications Using MVC, Blazor, and Razor Pages* (9th ed.). Apress.
4. Gamma, E., Helm, R., Johnson, R., & Vlissides, J. (1994). *Design Patterns: Elements of Reusable Object-Oriented Software*. Addison-Wesley.
5. Microsoft. (2026). *Entity Framework Core Documentation and Concurrency Tokens*. Microsoft Learn.
6. Microsoft. (2026). *Windows Presentation Foundation (WPF) Desktop Guide*. Microsoft Learn.
7. Troelsen, A., & Japikse, P. (2022). *Pro C# 10 with .NET 6: Foundational Principles and Practices in Programming*. Apress.
8. Verhoef, P. C., Kannan, P. K., & Inman, J. J. (2015). From multi-channel retailing to omni-channel retailing: Introduction to the special issue on multi-channel retailing. *Journal of Retailing*, 91(2), 174-181.
9. Vernon, V. (2013). *Implementing Domain-Driven Design*. Addison-Wesley.
