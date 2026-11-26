# Tutorial 7F Gate A supplement — adjustment-sign ruling

From: Baraka (CEO), recorded with Codex  
Date: 26 October 2026  
Status: Gate A planning clarification only. Gate B remains ungranted.

## D7. Sync-managed adjustment quantity and direction

This is an owner ruling for the refined Tutorial 7F design. Muse should treat
it as decided input and incorporate it into the concrete implementation plan,
acceptance criteria, automated test matrix, and manual verification steps.

### Sync-managed mode

1. The adjustment form presents exactly the four Core-owned reasons:
   `restock`, `damage`, `shrinkage`, and `correction`.
2. Staff enter a **positive whole-number quantity magnitude**. They do not type
   a signed delta in sync-managed mode.
3. The selected reason determines direction where its meaning is unambiguous:
   - `restock` means **Add stock** and produces a positive delta;
   - `damage` means **Remove stock** and produces a negative delta;
   - `shrinkage` means **Remove stock** and produces a negative delta.
4. `correction` can validly add or remove stock, so selecting it reveals a
   required **Correction direction** choice with exactly two options:
   **Add stock** and **Remove stock**.
5. Correction direction has no default. Staff must make an explicit choice.
   Changing to `correction` resets any earlier correction-direction choice so
   stale intent is not silently reused.
6. When another reason is selected, the correction-direction control is
   hidden or disabled and has no effect on the generated operation.
7. Before saving, the form states the resolved action in plain language, for
   example `Add 5 units — restock` or `Remove 3 units — correction`.
8. Zero, negative magnitude input, non-whole-number input, and a correction
   without a direction are rejected with field-specific, staff-friendly
   messages before capture is attempted.
9. The UI derives the signed delta once from magnitude, reason, and optional
   correction direction. `SynchronizedAdjustmentReasons.Validate` remains the
   final Core authority; WPF does not replace or weaken the business rule.
10. A removal that exceeds the current local stock is rejected before capture,
    while the server remains authoritative and may still return
    `insufficient_stock` if stock changed elsewhere.

### Direct mode

Direct-mode adjustment behavior is unchanged:

- staff enter the existing signed whole-number quantity;
- staff enter the existing free-text reason;
- the adjustment is applied immediately through the existing direct service;
- the applied adjustment appears immediately in history.

### Accessibility and presentation

- Reason and correction-direction controls need visible labels and accessible
  names.
- Keyboard focus moves to the first invalid field.
- The resolved Add/Remove summary is readable by assistive technology and is
  updated when quantity, reason, or direction changes.
- Colour is not the only indication of direction or validation failure.

### Required test coverage

The 7F plan must include tests proving:

- restock plus magnitude `5` captures delta `+5`;
- damage plus magnitude `5` captures delta `-5`;
- shrinkage plus magnitude `5` captures delta `-5`;
- correction/Add plus magnitude `5` captures delta `+5`;
- correction/Remove plus magnitude `5` captures delta `-5`;
- correction without a direction is refused;
- zero, negative, fractional, and nonnumeric magnitudes are refused;
- switching to correction clears an earlier correction-direction choice;
- a non-correction reason ignores any stale direction value;
- direct mode retains signed quantity and free-text reason behavior;
- every valid sync-managed adjustment still travels through the synchronized
  command-routing seam and creates an `AdjustStock` outbox operation.

Pure magnitude-to-delta resolution should be isolated from WPF controls in a
small deterministic type so its complete matrix can be unit tested without UI
automation. This adds no protocol, database schema, operation kind, or new
production package.

## Effect on the Gate A plan

The correction-sign issue is no longer open. Muse should revise the Gate A
implementation plan so that:

- the dual-mode adjustment section uses this ruling;
- the exact file/type plan identifies the pure resolver and its tests;
- acceptance criteria include explicit correction direction and the resolved
  action summary;
- manual verification covers all five reason/direction outcomes above;
- no other approved 7F scope is reopened.

Whether 7F manual evidence also discharges the parked 7E owner verification
remains a separate decision for Baraka. This supplement does not accept 7E and
does not grant Gate B.
