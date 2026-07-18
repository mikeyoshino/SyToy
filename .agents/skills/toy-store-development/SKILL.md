---
name: toy-store-development
description: Build, change, review, or diagnose the Toy Store Blazor application while preserving its Clean Architecture, vertical-slice conventions, commerce invariants, Thai Noto Sans UI system, PostgreSQL persistence, and local Docker workflow. Use for Toy Store features in Razor, C#, EF Core, MediatR, FluentValidation, Identity, catalog, cart, checkout, orders, inventory, payments, shipping, tests, migrations, or project documentation.
---

# Toy Store Development

Implement Toy Store work through the smallest complete vertical slice while keeping domain, use-case, infrastructure, and UI responsibilities separate.

## Establish context

1. Read `AGENTS.md` at the repository root.
2. Read `TASKS.md`, confirm the active milestone, and update the matching task rather than creating an untracked parallel plan.
3. Inspect the existing solution and nearby feature before designing a new pattern.
4. Load only the task-specific source of truth:
   - UI/component: `docs/DESIGN_SPEC.md` and `index.html`
   - domain/use case: `docs/DOMAIN_RULES.md` and `docs/ARCHITECTURE.md`
   - persistence/local environment: `docs/ARCHITECTURE.md` and `docs/LOCAL_DEVELOPMENT.md`
   - catalog, inventory, cart, pre-order, checkout, Order, payment, fulfillment, notification, dashboard or Admin: `docs/superpowers/specs/2026-07-17-commerce-platform-design.md`
5. Read [references/feature-checklist.md](references/feature-checklist.md) before implementing checkout, inventory, order, payment, refund, or webhook behavior.

If the repository has not been scaffolded, follow the target structure in `docs/ARCHITECTURE.md`. Do not invent project or namespace layouts that conflict with it.

## Classify the change

- Put invariants and valid state transitions in Domain.
- Put command/query contracts, validation, orchestration, authorization requirements, and external interfaces in Application.
- Put EF mappings, Identity, provider integrations, migrations, local file storage, caching, and telemetry in Infrastructure.
- Put Razor components, routes, UI state, and dependency composition in Web.
- Put domain tests in UnitTests and real database/handler tests in IntegrationTests.

When a change crosses layers, implement from the center outward: Domain -> Application -> Infrastructure -> Web -> tests.

## Implement a vertical slice

1. Define observable behavior and failure cases.
2. Add or adjust the Domain model without framework dependencies.
3. Create one action folder containing its request, handler, validator, and result/response as needed.
4. Use `IApplicationDbContext` directly for ordinary queries and commands. Add an aggregate-specific repository only for persistence logic that cannot be expressed safely through the context boundary.
5. Add Infrastructure implementation or EF configuration behind an Application interface.
6. Connect the Razor page/component through `ISender`; never access `DbContext` directly from UI.
7. Add focused tests for the rule and integration tests for transaction or provider boundaries.
8. Build and run the relevant tests before handoff.

Keep one handler per action. Avoid generic repositories, large service classes, hidden database writes, and request handlers that mix unrelated use cases.

## Implement UI

- Recreate patterns from `index.html` as reusable Razor components; treat the HTML as reference, not production source.
- Use Thai copy and Noto Sans Thai by default across Storefront, Account and Admin.
- Preserve the completed Storefront lime/monochrome visual language. Admin alone uses the borderless Muted Ocean blue direction, global rail and contextual top pills.
- Implement loading, empty, error, disabled, and success states.
- Provide semantic HTML, keyboard access, visible focus, Thai alt text, and reduced-motion support.
- Keep business calculations and authorization enforcement on the server.

## Protect commerce flows

- Keep Product v1 free of variants. Model exactly one conditional `InStock` or `PreOrder` offer per Product.
- Re-read current price and availability on the server at checkout.
- Create a durable `CheckoutAttempt` and stock/capacity reservation atomically before payment; there is no pre-payment Order.
- Required sequence: `CheckoutAttempt -> verified Stripe/provider evidence -> Payment + Order exactly once`.
- Create the Order only after verified Stripe/provider evidence.
- Verify Stripe payment, then consume the reservation, record payment and create the Order exactly once.
- Store immutable Order-item, address, shipping-estimate and pre-order-policy snapshots.
- Only verified Stripe/provider evidence may mark payment successful; browser completion and an Admin UI action are never payment evidence.
- Make webhook, payment confirmation, reservation expiry, cancellation, and refund effects idempotent.
- Use concurrency protection for stock and order transitions.
- Record audit information without logging secrets or sensitive payment data.
- Treat Application vertical-slice FluentValidation as authoritative input validation and map failures to Thai field messages/summary.
- Persist instants as UTC; format and display business dates, close boundaries and dashboard periods with `th-TH` in `Asia/Bangkok`.

## Work with local infrastructure

Run only PostgreSQL in Docker for local development:

```bash
docker compose up -d postgres
dotnet run --project src/ToyStore.Web
```

Do not add the Web project to local Compose. Use user secrets for the host application's connection string. Keep `.env` limited to container initialization values.

Use EF Core Code First. Keep migrations in Infrastructure, commit the migration and model snapshot, and review an idempotent SQL script. Web startup must call `Database.MigrateAsync()` in a service scope before serving requests; a connection or migration failure must stop startup. Never use `EnsureCreated` or invoke migrations from an HTTP request.

Keep production deployable on one Linux server: Caddy, one Web process, one PostgreSQL container, one local media directory, and local backup staging. Do not introduce Redis, Cloudflare R2, background workers, schedulers, Hangfire, or Quartz. Handle expired reservations through idempotent synchronous commands invoked by checkout or an authorized Admin action.

## Validate

Use the strongest checks available for the current repository state:

```bash
dotnet restore ToyStore.sln
dotnet build ToyStore.sln --no-restore
dotnet test ToyStore.sln --no-build
docker compose config
```

For migrations, inspect the generated files and an idempotent SQL script. For payment, order, and inventory changes, test retries, concurrency, rollback, and invalid transitions.

Update the relevant source-of-truth document when changing a public rule, design token, architecture boundary, or local command.

Update `TASKS.md` after implementation: mark work complete only after validation, record blockers inline, and set Current Focus to the next actionable task.
