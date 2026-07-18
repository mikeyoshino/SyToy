# Toy Store Agent Guide

## Project intent

Build a Thai collectible toy commerce application for Art Toy and Gundam products with roughly 100 daily users. Keep the first deployment simple while preserving clear seams for future growth.

Use this baseline unless an approved architecture decision says otherwise:

- .NET 10 Blazor Web App
- Global Interactive Server rendering
- ASP.NET Core Identity with cookie authentication
- Modular monolith with Clean Architecture and vertical slices
- MediatR commands and queries with FluentValidation
- EF Core Code First with PostgreSQL
- One application process and one database
- PostgreSQL in Docker for local development
- Run the web application directly with `dotnet run`; do not containerize it for local development

## Read before changing code

Read only the documents relevant to the task:

- Planning or starting new work: `TASKS.md`
- UI or component work: `docs/DESIGN_SPEC.md` and `index.html`
- Domain or use-case work: `docs/DOMAIN_RULES.md` and `docs/ARCHITECTURE.md`
- Database or infrastructure work: `docs/ARCHITECTURE.md` and `docs/LOCAL_DEVELOPMENT.md`
- Catalog, inventory, cart, pre-order, checkout, Order, payment, fulfillment, notification, dashboard or Admin work: `docs/superpowers/specs/2026-07-17-commerce-platform-design.md`
- New feature work: `.agents/skills/toy-store-development/SKILL.md`

Treat `index.html` as the visual reference, not the production implementation. Rebuild its patterns as reusable Blazor components. Do not copy embedded base64 images into Razor files.

## Architecture boundaries

Keep dependency direction strict:

```text
ToyStore.Web -> ToyStore.Application
ToyStore.Web -> ToyStore.Infrastructure (composition root only)
ToyStore.Application -> ToyStore.Domain
ToyStore.Infrastructure -> ToyStore.Application + ToyStore.Domain
ToyStore.Domain -> no other project
```

- Keep business rules in Domain entities, aggregates, and value objects.
- Keep use-case orchestration in Application handlers.
- Keep EF Core, Identity, local file storage, payment, email, shipping, and provider code in Infrastructure.
- Keep Razor components focused on input, presentation, UI state, authorization display, and `ISender.Send(...)`.
- Never reference Infrastructure implementations from Application.
- Do not add generic repositories over EF Core. Add an aggregate-specific repository only when it protects complex persistence rules.

## Feature organization

Organize Application code by action, not by technical type:

```text
Products/CreateProduct/
  CreateProductCommand.cs
  CreateProductHandler.cs
  CreateProductValidator.cs
  CreateProductResult.cs
```

- Use one handler per action.
- Use commands for state changes and queries for reads.
- Keep read and write models separate when their shapes differ, while using the same PostgreSQL database.
- Avoid large `*Service` classes that collect unrelated operations.
- Pass `CancellationToken` through async calls.
- Return explicit `Result<T>` failures for expected business errors; reserve exceptions for exceptional or invariant-breaking conditions.
- Use FluentValidation validators in the Application vertical slice as the authoritative input-validation rules. Map failures to Thai field messages and a validation summary; UI hints may mirror rules for immediate feedback but must not replace or diverge from FluentValidation.

## UI rules

- Storefront, Customer Account and Admin copy is Thai by default.
- Use Noto Sans Thai as the primary font.
- Storefront follows the layout, visual hierarchy, lime accent, monochrome palette, rounded cards and motion language in `index.html`; Admin uses the separate borderless Muted Ocean direction in the approved commerce spec.
- Preserve readable text sizes; body text must not be smaller than 14px on customer pages.
- Support desktop, tablet, and mobile layouts.
- Provide visible focus states, keyboard access, semantic markup, useful alt text, and reduced-motion behavior.
- Keep the storefront free of the left rail shown in the original inspiration image.
- Extract repeated or behavior-heavy UI into shared Razor components. Text, number, and select fields must share labels, help/error states, accessibility, and design tokens; select/dropdown components must use explicit cross-browser styling and must not fall back to the browser-default appearance.

## Commerce invariants

- Never trust prices, totals, payment status, stock, roles, or authorization decisions from the browser.
- Product ไม่มี variant ใน v1; store immutable Order-item snapshots for ProductId, names/slug, SaleType, Category, Brand, Universe, primary image, price/deposit/balance, quantity, close/ETA/policy and shipping data.
- Calculate available stock as on-hand minus reserved stock.
- Before payment, create a durable `CheckoutAttempt` and reservation atomically; do not create a pending Order.
- Required sequence: `CheckoutAttempt -> verified Stripe/provider evidence -> Payment + Order exactly once`.
- Create the Order exactly once only after verified Stripe payment. Browser completion and an Admin UI action are not payment evidence.
- Make payment webhooks and other retryable commands idempotent.
- Persist instants as UTC; interpret/display business dates, close boundaries and dashboard periods using `Asia/Bangkok` and `th-TH`.
- Do not log passwords, tokens, full payment data, or sensitive personal data.

## Local workflow

Use the commands documented in `docs/LOCAL_DEVELOPMENT.md`. The expected loop is:

```bash
docker compose up -d postgres
dotnet restore ToyStore.sln
dotnet build ToyStore.sln --no-restore
dotnet test ToyStore.sln --no-build
dotnet run --project src/ToyStore.Web
```

Before handing off a change:

- Update the matching task status and Current Focus in `TASKS.md`.
- Build the solution.
- Run focused tests, then the full relevant test suite.
- Add or update tests for domain rules and important handlers.
- Confirm Code First migrations are intentional, committed and have reviewed SQL, especially for destructive changes.
- Apply pending migrations during application startup before serving requests; migration failure must stop startup. Never use `EnsureCreated` or trigger migration from an HTTP request.
- Update documentation when changing architecture, domain invariants, local setup, or UI tokens.

## Scope discipline

The complete platform must remain deployable on one Linux server. Redis, Cloudflare R2, background workers, schedulers, and job frameworks are outside the architecture; do not introduce them. Also avoid microservices, Kubernetes, an event bus, Elasticsearch, JWT, separate read/write databases, or a generic repository. Prefer the simplest design that preserves current boundaries.
