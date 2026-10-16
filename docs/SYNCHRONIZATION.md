# RetailLab synchronization design (Tutorial 7A)

Status: **approved design direction, implemented through Tutorial 7C-ii.**
Sections 7B, 7C-i, and 7C-ii now describe working foundations, server feed/API,
and headless read-only client behavior. Uploads and later phases remain design.

## 1. Purpose and boundaries

This document began by answering one question before synchronization code existed:
when the desktop application keeps its own offline database and the website
keeps an authoritative server database, how do the two stay consistent
without losing data, applying one change twice, or silently overwriting
someone's work?

It covers:

- what the current implementation actually shares today (facts);
- the approved direction for a controlled sync protocol (decisions);
- what is deliberately left undecided (deferred choices);
- what the first sync slices will not attempt (non-goals).

It does not choose a cloud provider, a message broker, or a production
deployment topology. Implementation slices must stay
inside the architecture boundaries in `AGENTS.md`: Core must not depend on
WPF, ASP.NET Core, EF Core, or SQLite. Sync *contracts* live in Core,
transport/orchestration in `RetailLab.Sync`, storage in Data, and executable
composition in an interface project such as LabCli.

## 2. Current state versus target state

**Current state (verified in the repository).** The normal website, interactive
console, and desktop modes can still deliberately share one local SQLite file
through `RETAILLAB_DATA_DIRECTORY`. Separately, the 7C-ii LabCli `sync` command
can create a dedicated client SQLite database, pair it once with Web, and pull
the authoritative product feed incrementally. Desktop still uses local shared-
file coordination; uploads and bidirectional synchronization do not exist yet.

**Target state (approved direction).** The website uses an authoritative
server-side database. Each desktop installation keeps its own local SQLite
database and keeps working while offline. Staff actions apply locally first
and are queued; when connectivity is available, a push-then-pull cycle
uploads queued operations and downloads authoritative server changes. No
interface ever edits the other side's database file directly.

## 3. Initial single-store assumption

The first sync slices assume one store: one authoritative product catalogue,
one server database, and one or more desktop installations serving that same
store. There is no multi-store tenancy, no per-store partitioning, and no
store identifier in the protocol. If a second store is ever needed, identity
and ownership must be redesigned first — that work is explicitly out of scope
(see non-goals).

## 4. Data ownership

Ownership answers "who is allowed to change this, and who holds the final
answer when copies disagree?" Every synchronized record has exactly one
owner. The other side keeps a *projection* (a read copy plus queued
intentions), never a competing truth.

| Data | Owner (initial phase) | Notes |
| --- | --- | --- |
| Product (published catalogue and current state) | Server | Desktop holds an offline projection; staff edits travel as operations, not as row overwrites. |
| InventoryAdjustment | Server (authoritative log) | Desktop sends immutable adjustment operations (delta + reason); the server appends them. Adjustment rows are never edited or deleted. |
| Order and OrderLine | Server | Orders are created only by the server. Order lines already snapshot SKU, description, and price, so they survive later product changes. |
| Customer Identity data (`AspNet*` tables) | Server | Accounts and credentials live only on the server. Passwords and credential records are never synchronized. |
| Bookmark | Server | Per-customer favourites stay server-owned; desktop does not manage them in the initial phase. |
| BasketItem | Server | Per-customer baskets stay server-owned; desktop does not manage them in the initial phase. |
| Desktop outbox | Desktop (local only) | The queue of unsent staff operations. Never read directly by the server; it is uploaded, operation by operation. |
| Server inbox/receipt | Server | One durable receipt per terminally decided `OperationId` (accepted, conflict, or rejected). Replays return the recorded outcome, so retried uploads stay idempotent. |
| Server change feed | Server | The ordered, replayable log of authoritative changes desktops pull from. |

Customer accounts, credentials, bookmarks, baskets, and order placement
remain server-owned for the initial sync phase. Desktop staff workflows in
this phase cover product management (create, edit details, archive,
unarchive) and inventory adjustments only.

## 5. Stable identity: the GUID seeding blocker

**Fact.** `Product.Id` is a `Guid` assigned by `Guid.NewGuid()` in the
`Product` constructor, and `DatabaseInitializer` seeds the five demo products
by SKU with no fixed identifiers. Two independently initialized databases
therefore assign *different* GUIDs to the *same* seeded SKU. Today that is
harmless because every interface shares one file. The moment databases
separate, "which product is AUR-100?" has two different answers, and no sync
protocol can merge rows it cannot identify. **This is a synchronization
blocker and must be resolved during pairing (7B), before any data flows.**

**Approved direction.**

- The Product GUID remains the permanent distributed identity once pairing
  establishes it. GUIDs are stable, globally unique, and already the primary
  key everywhere (products, adjustments, orders, lines).
- SKU is a business key, not the identity. It may assist a deliberate
  one-time pairing process (matching "these two rows mean the same product"),
  but it must not become the permanent distributed identity: matching by SKU
  on every sync would be fragile and slow, and it could not distinguish a
  renamed product from two different products.
- A new or empty desktop database is bootstrapped from the server, and the
  server Product GUIDs are preserved locally, so both sides agree from day
  one.
- Independently initialized existing databases (same SKUs, different GUIDs)
  require an explicit migration or pairing strategy later. This design does
  not propose silently rewriting a database that contains useful orders,
  baskets, adjustments, or related data.
- A future implementation slice will need a controlled import/rehydration
  path for prescribed Product IDs — internal and narrowly scoped, following
  the existing precedent of the internal `Order(Guid id, ...)` constructor
  used for checkout idempotency — rather than a publicly writable ID.

## 6. Versions are local: the revision warning

**Fact.** `Product.Version` is a database-local optimistic-concurrency
counter: it starts at 0, every successful mutation bumps it, and EF Core
compares it at save time so a stale writer loses instead of overwriting.
Tutorial 6D added expected-version pre-checks that compare a UI-captured
version against the freshly loaded row.

**Warning.** Once the databases separate, local version numbers can diverge:
the server may be at version 9 while an offline desktop is at version 4 for
the same product, and both numbers are "correct" locally. **Do not design
synchronization as if the existing `Product.Version` automatically becomes a
global version.** Comparing a desktop-local 4 against a server-local 9 is
meaningless.

**Approved direction.** A future slice must introduce or define separate
server revision (or base-version) metadata for synchronization:

- Desktop UI concurrency keeps using the local `Version` exactly as today
  (expected-version overloads, EF token at save time).
- Each queued sync operation additionally retains the last authoritative
  server revision the desktop observed for that product, or names its
  predecessor when it extends a local causal chain (section 8).
- The server compares the operation's base revision against its own
  authoritative revision to detect stale edits. Implemented in 7B:
  `Product.ServerRevision` is a separate non-negative `long` column on `Product`
  representing the last authoritative server state (independent of `Version`),
  while global change-feed entries carry an independent monotonic `Sequence`.

## 7. Desktop pairing and bootstrap

Pairing connects one desktop database to the server for the first time, and
bootstrap fills it with the authoritative catalogue.

**Approved direction for new or empty desktop databases.**

1. The desktop authenticates to the server (see section 14 — required before
   any state-changing sync is exposed).
2. The desktop declares itself empty (no local products, no outbox entries).
3. The server returns the full authoritative product set with server GUIDs,
   current details, stock, archive state, and server revisions, plus the
   current change-feed cursor.
4. The desktop imports the rows preserving server GUIDs, stores the cursor,
   and only then offers staff workflows.

**Independently initialized databases** (a desktop that was seeded on its own
and now has different GUIDs for the same SKUs, possibly with its own orders
or adjustments) are explicitly out of the first pairing slice. They need a
deliberate migration or pairing strategy — SKU-assisted matching with human
confirmation is the expected shape — designed later. Silent GUID rewriting is
forbidden because it would orphan or misattribute existing orders, baskets,
bookmarks, and adjustments.

## 8. Local outbox: remembering intentions offline

An **outbox** is a durable local queue of things the desktop did while it
could not (or did not yet) tell the server. Each staff action that must
eventually reach the server is stored locally first — applied to the local
database so staff see their own work immediately — and appended to the
outbox as one **immutable operation**: once written, an operation is never
edited, only uploaded and then marked sent (or conflicted).

Why an outbox instead of "just send it live"? Staff must keep working with
no network, and networks fail mid-request. An outbox turns "did the server
get it?" from a guess into a query: anything still marked pending has not
been acknowledged and can be safely retried, because every operation carries
a stable GUID `OperationId` and the server keeps idempotency receipts
(section 9).

Initial operation set (product management only):

- create product (client-generated Product GUID, SKU, description, price,
  initial stock);
- update details (Product GUID, new description, new price, base server
  revision);
- archive / unarchive (Product GUID, base server revision);
- inventory adjustment (Product GUID, signed delta, reason, actor as
  *claimed* context only — the server re-resolves the actor from
  authentication).

Inventory sync sends the immutable adjustment operation — "add 5 for this
reason" — never "set stock to 42". Absolute stock values from a stale device
would silently erase checkouts and adjustments that happened elsewhere.

**Chained offline operations.** Staff may act on the same product several
times before syncing: two edits against server revision 5, or a create
followed by an edit, archive, or adjustment of the still-unsent product.
Operations affecting the same product preserve their local causal order in
the outbox, and a later operation may identify its predecessor operation (or
equivalent immutable causal-chain metadata) instead of pretending it
independently observed a newer server revision. The server accepts the next
operation in a chain only when its predecessor was accepted and no
intervening conflicting operation changed the authoritative product; a
genuine intervening server/other-device change still produces a conflict.
The server returns the new authoritative revision for each accepted
operation, and retrying any chain member stays safe through `OperationId`
receipts. Chains never rewrite an operation's original business intent —
create-then-edit and multiple-edit-before-sync flows work because each step
keeps its own immutable record linked to the step before it. Implemented in 7B:
Core contracts enforce that mutating operations carry exactly one of
`BaseServerRevision` (based directly on server state) or `PredecessorOperationId`
(chained behind an unsent local outbox operation). Product creation has neither.

## 9. Server inbox and idempotency receipts

An **inbox receipt** is the server's durable memory that it already handled
a given `OperationId`, plus the outcome (accepted, conflict, or rejected).
When the desktop retries an upload after a network failure, the server looks
up the receipt first: if one exists, it returns the recorded outcome without
re-applying anything. This is **idempotency** — doing the same request twice
has the same effect as doing it once — and it is what makes retries safe.
Every terminal server decision for an `OperationId` — accepted, conflict, or
rejected — is durably recorded, and a replay returns that same recorded
outcome; "already processed" is the replay response for an existing receipt,
not a second business mutation. A failure that never reached a terminal
committed decision leaves no receipt, so the operation remains retryable.

The repository already proves the pattern twice: web checkout replays a
`checkoutAttemptId` to the original order instead of double-charging stock,
and `Order` accepts a prescribed id through an internal constructor. The
sync inbox generalizes that idea from orders to every uploaded operation.

## 10. Server change feed and monotonic cursor

A **change feed** is the server's ordered, replayable log of authoritative
changes: product created/updated/archived/unarchived, stock moved by an
accepted adjustment or a checkout, each entry stamped with a **monotonic
server sequence number**. "Monotonic" means the numbers always go up and
never repeat or go backwards, so a desktop can say "give me everything after
37" and know it missed nothing and can resume exactly where it stopped.

Why a sequence and not timestamps? Timestamps remain useful audit
information, but clocks on different machines can disagree, go backwards
across daylight-saving changes, or tick at different rates. A server-issued
sequence is the distributed ordering mechanism; timestamps must never decide
"which change wins" or "what did I miss."

Each desktop durably stores its **last successfully applied server cursor**
(the sequence number it has fully processed). Pull means "send changes after
my cursor, in order"; the desktop applies a page and advances the cursor in
the same local transaction (section 12), so a crash can only resume, never
skip or double-apply.

## 11. Push-then-pull synchronization cycle

The approved cycle pushes local intentions before pulling authoritative
truth, so the server sees the desktop's work before the desktop rewrites its
projection around newer server state:

1. Authenticate the desktop/device (required; see section 14).
2. Push pending outbox operations in recorded order, preserving each product's causal chain.
3. Receive a durable result for each operation: accepted (with its new
   authoritative server revision), conflict, or rejected; replays of an
   already-recorded `OperationId` return the same recorded outcome with an
   "already processed" replay status.
4. Preserve conflicts for human review (section 13); never auto-resolve.
5. Pull authoritative server changes after the stored cursor.
6. Apply each downloaded page locally and advance the cursor transactionally.
7. Repeat until the server reports no further changes (caught up).
8. Report the resulting status to the user (section 15).

Push before pull matters: uploading first lets the server judge each
operation against the freshest authoritative state and return precise
conflict snapshots; pulling first would refresh the projection and strand
already-typed work against revisions the desktop no longer displays.

## 12. Transaction boundaries

Each side keeps its own transactions small and local. There are no
distributed transactions spanning desktop and server; the outbox, receipts,
and cursor make coarser guarantees from small atomic steps.

- **Local page apply.** Applying one downloaded page of server changes and
  advancing the local cursor must be one local transaction. Crash before
  commit and the page replays; crash after commit and the cursor proves it.
- **Server operation apply (accepted).** Applying one uploaded operation, writing its
  inbox receipt, and publishing the corresponding server change-feed entry
  must be one server transaction.
- **Server conflict/rejection record.** A conflict or rejection atomically
  records its receipt plus a diagnostic snapshot or details, with no business
  mutation and no authoritative change-feed entry unless a future design
  explicitly requires one. No receipt for an `OperationId` means no terminal
  decision was committed, so the operation stays retryable; transient
  failures that never reach a terminal decision create no misleading
  successful receipt.
- **Existing boundaries stay.** Fine-grained repository saves
  (`SaveProductChangesAsync`, `SaveCheckoutChangesAsync`, and friends) keep
  their one-save-one-transaction semantics; sync adds new tables and flows
  around them rather than stretching them across the network.

## 13. Retries, idempotency, and conflict handling

**Retries.** Network failures — timeouts, dropped connections, crashed
processes mid-request — may be retried safely at any point, because receipts
make uploads idempotent and the cursor makes downloads resumable. Retry
policies (how many attempts, how long to wait, backoff shape) are deferred;
the design only requires that retrying never corrupts data.

**Conflicts.** A business conflict means the server understood the operation
but cannot honestly apply it: the product changed since the desktop last saw
it, the SKU is now taken, or the delta would drive stock negative. Conflicts
are data to show a human, not errors to retry forever and never excuses for
silent last-write-wins. Each conflicted operation keeps its receipt (outcome:
conflict) plus the authoritative server snapshot, and the desktop surfaces
it as "needs attention" until staff resolve it deliberately.

**Initial conflict rules (proposed policies for implementation slices).**

- *Product creation:* the server may accept the client-generated Product
  GUID when unused and the SKU is unique. An existing different product with
  the same SKU requires explicit resolution (rename-and-retry, or pair with
  the existing product through the pairing strategy). Edits chained behind a
  pending create name that create as their predecessor.
- *Description, price, and archive changes:* compare the operation's last
  observed server revision against the current authoritative revision, except
  that a chained operation names its accepted predecessor instead of
  re-asserting a stale base (section 8). A stale edit becomes
  "needs attention" and returns the authoritative server
  snapshot so staff can re-apply their intent against current values.
- *Inventory adjustment:* apply its delta exactly once against the
  authoritative current stock. Reject it if it would produce negative stock
  or violate another business rule; the rejection names the rule so staff
  understand what happened.
- *Server checkout:* checkout keeps reducing authoritative stock on the
  server and publishes the resulting product/adjustment changes for desktops
  to pull like any other change.
- *No hard deletes* in the initial protocol; product archive state is
  synchronized instead, so nothing disappears without a reversible,
  auditable record.

## 14. Authentication and trust boundaries

**Fact.** The repository has customer authentication only: standard ASP.NET
Core Identity with unique emails, framework-managed password hashing, and
cookie sign-in. Staff interfaces use fixed demo actor labels
(`staff-console`, `staff-desktop`); there is no staff login, no device
identity, and no authorization model for devices.

**Approved direction.** The server owns a trust boundary around every sync
endpoint: it authenticates the desktop/device, authorizes the requested
operation, and **revalidates everything** from its own authoritative state.
It must not trust client-provided actor identity, resulting stock,
authoritative version, timestamps, or authorization claims — those fields
are, at best, hints for error messages. Credentials must never be
hard-coded or committed (per `AGENTS.md` security rules).

**Gate.** State-changing synchronization must not be exposed before an
authentication/authorization design for staff devices exists. Designing that
mechanism (which credentials, which roles, how devices enroll and rotate
secrets) is deferred work, but it is a prerequisite for 7D/7E, not an
afterthought. Read-only bootstrap/pull design (7C) must state what identity,
if any, it requires before it is built.

## 15. Desktop synchronization UX

Start with an explicit **Sync button**, not an invisible background service:
a visible action with visible outcomes is easier to learn, test, and trust.
Local work must remain fully available when the network is unavailable —
offline is a normal state, not an error.

Recommended states (plain words, no jargon):

- **Offline** — no network; local work continues, operations queue.
- **Syncing** — a push-then-pull cycle is running; controls that would
  interfere disable while busy (following the existing `SetBusy` pattern).
- **Up to date** — no pending operations and the cursor matches the server.
- **Pending changes** — outbox holds unsent operations.
- **Needs attention** — one or more conflicted or rejected operations await
  human review.
- **Sync failed** — the last cycle could not complete (network or server
  problem); nothing was lost and retrying is safe.

Also show the pending-operation count and the last successful sync time in
UTC. Background scheduling, badges, and richer conflict-resolution screens
arrive later, after the explicit flow is proven.

## 16. Failure recovery

- *Crash mid-push:* receipts make every upload replayable; the next cycle
  re-sends pending operations and receives recorded outcomes.
- *Crash mid-pull:* the cursor only advances inside the page-apply
  transaction, so the next cycle replays from the last committed cursor.
- *Server restart mid-apply:* the single server transaction guarantees the
  receipt and the change commit together or not at all.
- *Lost or wiped desktop database:* re-pair and re-bootstrap as a new
  database. Unsynced outbox operations on a destroyed device are unrecoverable
  by design — a reason to sync promptly, documented in UX wording, not a
  reason for silent server-side guessing.
- *Clock skew:* harmless to ordering (sequences decide), visible only in
  audit timestamps.

## 17. Phased implementation roadmap

Each slice stays narrow and independently verifiable:

- **7A: synchronization boundaries and architecture** — this document.
  Design only; no behavior changes.
- **7B: stable identity, pairing metadata, sync contracts, and schema
  foundations** — implemented: `Product.ServerRevision`, prescribed GUID identity,
  causal chain contracts (`BaseServerRevision` vs `PredecessorOperationId`),
  `ProductSnapshot`, `ServerChangeEntry`, `SyncOperationResult`, plus
  `SyncClientState`, `SyncOutboxEntry`, `SyncInboxReceipt`, and
  `SyncChangeFeedEntry` persistence tables via `SynchronizationFoundations`
  migration.
- **7C-i: server-side feed publication and read-only API** — implemented:
  server-only atomic revision/feed publication, revision-zero backfill,
  persisted server identity, and development-key-protected pair/pull endpoints.
- **7C-ii: headless catalogue bootstrap and incremental pull client** —
  implemented: a Core-only transport/storage boundary, UI-independent HTTP
  transport and orchestrator, empty-database pair-once gate, pending-outbox
  block, atomic page/cursor application, strict protocol checks, and a LabCli
  command using an explicit server and dedicated data directory.
- **7D: authenticated desktop outbox upload for product management
  operations** — requires the staff-device authentication design from
  section 14; create/update/archive/unarchive upload with receipts.
- **7E: inventory adjustment upload and server checkout changes flowing back
  to desktop** — delta operations up, authoritative stock and adjustment log
  down.
- **Later:** conflict-resolution UI, background scheduling, existing-database
  pairing/migration strategy, production hardening, and deployment decisions.

(The repository roadmap keeps its own numbering; this list defines the
dependency order, not marketing names.)

## 18. Deferred decisions and explicit non-goals

**Resolved in Tutorial 7B.**

- Server revision metadata shape: separate non-negative `ServerRevision` column on `Product` alongside monotonic global change-feed `Sequence`.
- Causal chain representation: `BaseServerRevision` (for operations based on server state) vs `PredecessorOperationId` (for chained operations extending unsent local operations).

**Deferred (recorded honestly, decided later with evidence).**

- Staff-device authentication mechanism, roles, enrollment, and rotation.
- Upload transport shape and protocol version negotiation beyond the implemented
  versioned read-only `/api/sync/v1/pair` and `/api/sync/v1/pull` endpoints.
- Retry policy details (attempt counts, waits, backoff).
- Batch limits for push. Pull currently defaults to 100 entries and permits an
  explicit client page size from 1 through 500.
- Existing-database pairing/migration strategy for divergent GUIDs.
- Conflict-resolution UI beyond "needs attention" plus authoritative
  snapshot display.
- Background scheduling policy.
- Production database choice and deployment topology.

**Non-goals for the first synchronization slices.**

- Selecting a cloud provider.
- Replacing SQLite merely for this tutorial.
- Containers.
- Message brokers.
- Microservices.
- Real-time synchronization.
- Automatic last-write-wins conflict resolution.
- Multi-store tenancy.
- Real payment processing.
- Synchronizing Identity password data.
- A giant general-purpose sync framework.

## 19. Glossary for beginners

- **Outbox:** the desktop's durable to-do list of operations the server has
  not acknowledged yet. Lets staff work offline and makes retries safe.
- **Inbox receipt:** the server's durable note that it already handled one
  `OperationId`, and what it decided. Stops double-applying retried uploads.
- **Idempotency:** the property that repeating a request is harmless. Built
  from stable operation ids plus receipts, and already proven by checkout
  replay in this repository.
- **Cursor:** the desktop's bookmark in the server change feed — "I have
  processed everything up to sequence 37." Advances only inside the
  page-apply transaction.
- **Predecessor (causal chain):** the earlier outbox operation a later
  operation builds on (for example, the create behind an edit). Lets the
  server accept ordered same-product work without mistaking it for stale,
  conflicting edits.
- **Conflict:** the server understood an operation but cannot honestly apply
  it (stale base, taken SKU, negative stock). Surfaced for human review,
  never silently resolved or retried forever.
- **Projection:** the desktop's local copy of server-owned data. Good for
  offline reading and for capturing intentions; never the source of truth.
