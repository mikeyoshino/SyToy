# High-Risk Feature Checklist

Use this checklist for checkout, inventory, order, payment, refund, webhook, and maintenance-command changes.

Read `docs/superpowers/specs/2026-07-17-commerce-platform-design.md` as the business/product source of truth. Product v1 has no variant; there is no pre-payment Order. UI remains Thai-first: completed Storefront uses monochrome/lime while Admin alone uses borderless Muted Ocean blue.

## Before implementation

- Identify the aggregate and transaction boundary.
- List valid states, invalid transitions, and the actor allowed to perform each action.
- Identify values that must be snapshotted instead of read later.
- Decide the idempotency key and duplicate-request behavior.
- Identify concurrency conflicts and the database mechanism that resolves them.
- Define audit events and ensure sensitive values are excluded.

## Handler behavior

- Validate request shape with FluentValidation.
- Re-read authoritative prices, stock, ownership, and current status from the server.
- Pass `CancellationToken` to every asynchronous operation.
- Keep all state changes that must succeed together in one transaction.
- Return explicit expected failures without exposing internal details.
- Dispatch external side effects only after durable state is known, or use an outbox when atomic coordination becomes necessary.

## Inventory and checkout

- Keep Product v1 free of variants and re-read its single conditional offer from the server.
- Keep Cart In-stock-only; Pre-order always enters direct checkout and must never be merged into or snapshotted from a cart.
- Assert `AvailableQuantity = OnHandQuantity - ActiveReservedQuantity`.
- Prevent negative on-hand, reserved, and available quantities.
- Lock or use optimistic concurrency around reservation updates.
- Create all reservations or none.
- Expire and release reservations exactly once.
- Create a durable `CheckoutAttempt` with authoritative item, price, address, shipping, provider-session and idempotency snapshots before payment; do not create a pending Order.
- After verified payment, consume reservations, record the payment and create the Order exactly once.
- Snapshot Product traceability, names, slug, `SaleType`, Category, Brand, Universe, image, offer amounts, quantity, shipping estimate and pre-order close/ETA/policy in Order items.

## Payment and webhooks

- Verify provider signature and reference.
- Reject mismatched amount, currency, order, or merchant data.
- Enforce uniqueness for provider event/reference identifiers.
- Treat repeated delivery as success without duplicating effects.
- Never mark paid from browser return parameters.
- Do not permit an Admin UI action to mark Stripe payment successful without provider verification.
- Record provider response safely without card or secret data.

## Tests

- Happy path and every invalid state transition.
- Zero, negative, boundary, and insufficient quantities.
- Two concurrent attempts for the last available item.
- Duplicate command, webhook, or maintenance execution.
- Transaction rollback after a mid-flow failure.
- Authorization and ownership failures.
- Reservation expiry, cancellation, and refund compensation.
