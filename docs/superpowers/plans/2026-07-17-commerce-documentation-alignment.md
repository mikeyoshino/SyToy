# Commerce Source-of-Truth Alignment Plan

**Goal:** Align every implementation-facing document and the executable backlog with the approved commerce platform design before M4 code begins, so later agents cannot accidentally build Product variants, pre-payment Orders, editable categories, or the wrong Admin experience.

**Authority:** `docs/superpowers/specs/2026-07-17-commerce-platform-design.md` is the approved business/product source of truth. `AGENTS.md` remains authoritative for architecture and workflow. Existing completed M0-M3 history must be preserved.

**Scope:** Documentation and documentation contract tests only. Do not implement M4 production code or mark M4 tasks complete.

**Process:** TDD for drift-prone contracts, focused verification, full unit verification, independent spec review, independent quality review.

## Task 1: Lock the approved catalog and checkout semantics with RED tests

**Files:**

- Modify: `tests/ToyStore.UnitTests/Architecture/DocumentationAlignmentTests.cs`

- [x] Add repository-document tests that require:
  - `AGENTS.md` routes catalog, checkout, order and Admin work to the approved commerce spec.
  - Product v1 has no variant and Orders are created only after verified payment.
  - Product uses `SaleType` (`InStock`, `PreOrder`) and lifecycle status (`Draft`, `Published`, `Archived`).
  - Categories are seeded `ArtToy` and `Gundam` and have no v1 Admin management page.
  - Brand, Universe and Character rules are present.
  - `CheckoutAttempt`, `BalancePaymentRequest`, `NotificationDelivery` and the immutable Thai address catalog appear in architecture/backlog documents.
  - TASKS contains executable Admin shell, catalog, media, inventory/cart, pre-order, checkout/Stripe, post-payment Order, fulfillment/notification, dashboard and production milestones.
  - TASKS no longer instructs ProductVariant, pending-Order-before-payment, category hierarchy/Admin CRUD, manual bank-transfer-first payment, or deferred sales reporting.
  - Storefront and Admin themes are explicitly separated: completed storefront keeps its approved bold monochrome/lime language; Admin uses borderless Muted Ocean blue.
- [x] Run the focused tests and record the expected RED failures against the stale documents.

## Task 2: Align agent guidance and domain rules

**Files:**

- Modify: `AGENTS.md`
- Modify: `docs/DOMAIN_RULES.md`
- Modify: `.agents/skills/toy-store-development/SKILL.md`
- Modify: `.agents/skills/toy-store-development/references/feature-checklist.md`
- Modify: `docs/superpowers/specs/2026-07-17-commerce-platform-design.md`

- [x] Add the approved commerce spec to relevant-document routing for catalog, inventory, checkout, order, notification, dashboard and Admin work.
- [x] Correct the approved spec's UI-scope transcription error with an explicit amendment: completed Storefront remains monochrome/lime; Admin owns the borderless Muted Ocean direction. Correct §18 and §23 without rewriting historical M3 evidence.
- [x] Replace variant/pending-order guidance with the approved no-variant Product model and durable `CheckoutAttempt`/reservation before payment.
- [x] State that Order creation occurs exactly once only after verified Stripe payment.
- [x] Document conditional In-stock and Pre-order offer invariants, Bangkok close-date conversion, capacity/MaxPerCustomer, draft/publish/archive lifecycle, seeded category and Brand/Universe/Character rules.
- [x] Align inventory, cart, pre-order cancellation/deposit forfeiture, payment/fulfillment states and immutable snapshot rules without weakening the no-oversell or server-authority invariants.
- [x] Apply the same routing, theme boundary, pre-payment `CheckoutAttempt`, no-variant and snapshot rules to the project skill and its feature checklist so later sessions receive the corrected guidance automatically.

## Task 3: Align architecture, UI and roadmap

**Files:**

- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/DESIGN_SPEC.md`
- Modify: `docs/ROADMAP.md`

- [x] Remove `ProductVariants` and generic `PreOrders` from the v1 persistence map; add catalog relations and the approved checkout/payment/order/notification records.
- [x] Add `IFileStorage`, `IThaiAddressCatalog`, `IPaymentGateway`, `ITransactionalEmailSender` and notification provider boundaries where relevant.
- [x] Record the pinned local Thai address dataset as validated at startup, immutable/frozen and consumed through Application queries.
- [x] Clarify that the storefront retains the completed M3 visual language while Admin uses the approved Muted Ocean blue, soft surfaces, no harsh borders, global rail and contextual top pills.
- [x] Replace stale variant/manual-payment/OpenTelemetry roadmap language with approved vertical delivery: Catalog/Admin → Inventory/In-stock cart → Pre-order → Address/Checkout/Stripe → Orders/Fulfillment/Notifications → Dashboard → Production.

## Task 4: Rebaseline the executable backlog

**Files:**

- Modify: `TASKS.md`

- [x] Preserve every completed M0-M3 checkbox and verification record.
- [x] Rebuild unstarted milestones so each task is small enough for one TDD/review cycle and dependencies match the approved design.
- [x] Include Admin shell, Product/Brand/Universe/Character, seeded Category, local staged media, catalog persistence/admin/storefront, inventory audit, anonymous In-stock cart, Pre-order direct flow/capacity, Thai addresses, `CheckoutAttempt`, Stripe Embedded Checkout/webhooks, post-payment Order creation, balance payment, cancellation/refund/forfeiture, shipping, email/LINE delivery, dashboard analytics and launch readiness.
- [x] Keep free shipping in v1, no background worker/scheduler, synchronous idempotent maintenance and one-server constraints explicit.
- [x] Set Current Focus to the first M4 task and Next Task to the second M4 task without marking either complete.

## Task 5: GREEN verification and review

- [x] Run focused `DocumentationAlignmentTests`.
- [x] Run `dotnet format ToyStore.sln --verify-no-changes`.
- [x] Run `dotnet build ToyStore.sln --no-restore -p:CI=true`.
- [x] Run the full Unit suite.
- [x] Search all implementation-facing documents for stale ProductVariant, pre-payment Order, category CRUD, manual-bank-transfer-first and deferred-dashboard instructions; contextual historical references in completed plans are allowed only when clearly historical.
- [x] Obtain independent spec review, fix all gaps, then independent quality review and fix every Critical/Important finding.
- [x] Record exact evidence here and in `TASKS.md`; proceed immediately to the separate M4 implementation plan.

## Completion evidence

- RED: focused `DocumentationAlignmentTests` initially failed 5/6 contracts because the implementation-facing documents still contained missing routing, the variant/pending-Order model, inverted theme scope and the old M4–M9 backlog.
- Initial GREEN: focused `DocumentationAlignmentTests` passed 6/6.
- Spec-review RED: 6 new focused contracts failed while exposing 7 ordering/authority gaps (provider-only payment evidence, M4 roadmap exit, In-stock/Pre-order delivery order, notification persistence/provider/retry order, refund-aware dashboard dependencies and configurable delivery-estimate default).
- Review-fix GREEN: focused `DocumentationAlignmentTests` pass 12/12.
- Final contract mutation proof: temporarily changed the M6-05 dependency from M6-04 to M6-03; the focused suite failed 1/12 on the new storefront-ordering assertion. Restoring the approved M6-04 dependency returned the focused suite to 12/12, with no mutation left in `TASKS.md`.
- Quality-review RED: semantic contracts failed 5/16 on reversed state tuples, missing cross-cutting Skill constraints, an incomplete section-scoped checkout chain and the M4/M5 media boundary; the initial theme marker was also narrowed to §18 rather than the amendment preface.
- Mutation proof for already-correct ownership/dependency behavior: swapping the §18 Storefront lime token to Admin blue and removing the M8-09 → M8-08 edge produced exactly 2 focused failures; both mutations were restored before final verification.
- Quality-fix GREEN: section-scoped theme/checkout contracts, dependency-edge parsing, durable completed-history/milestone ordering, tuple convention and cross-cutting guidance pass 16/16.
- Final semantic RED: the sentence-scoped after-verification rule and UTC-persistence/Bangkok-display direction contracts failed 2/16 until the Skill and Domain made those directions explicit.
- Final semantic mutation proof: changing the scoped AGENTS Order rule from after→before and swapping the Skill UTC/Bangkok direction produced 2/16 focused failures; after restoring those, changing the Domain Redis/worker/scheduler prohibition to a positive directive produced 1/16 focused failure. All mutations were restored.
- Final semantic GREEN: scoped checkout flow/after-only/provider-only rules plus prohibition and time-direction semantics pass 16/16.
- `dotnet format ToyStore.sln --verify-no-changes` passes.
- `dotnet build ToyStore.sln --no-restore -p:CI=true` passes with 0 warnings and 0 errors.
- Fresh quality-fix verification: format clean; CI build 0 warnings/errors; full Unit suite passes 183/183.
- Focused stale-instruction search returns no active ProductVariant, pre-payment-Order creation, Category hierarchy/Admin, manual-bank-transfer-first, OpenTelemetry roadmap or deferred-sales-report instruction.
- Independent spec review: APPROVED after capacity/storefront dependency contracts were added.
- Independent quality review: APPROVED after state-axis normalization and mutation-proven semantic contracts for payment ordering, infrastructure prohibitions and UTC/Bangkok responsibility.
- Final status: complete; proceed to the separate M4 implementation plan.
