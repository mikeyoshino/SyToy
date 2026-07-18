# M3 Storefront Design System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Use superpowers:test-driven-development for every behavior change, superpowers:verification-before-completion before checking a task, and superpowers:requesting-code-review before final handoff. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete M3-01 through M3-05 with a Thai-first, accessible, responsive storefront shell and reusable display/form components that preserve the visual language of `index.html` without copying its embedded images.

**Architecture:** Keep the design system in `ToyStore.Web` because it owns Razor presentation. Components accept presentation models and render independently of the database; future feature pages obtain those models through `ISender`. Application FluentValidation remains authoritative, while reusable Web helpers bind Thai validation failures to fields. Use CSS design tokens and component-scoped styles, with a deliberate custom select appearance instead of browser defaults.

**Tech Stack:** .NET 10 Blazor Web App (Global Interactive Server), Razor components, CSS custom properties, Noto Sans Thai variable WOFF2 from `@fontsource-variable/noto-sans-thai` 5.2.8 (OFL-1.1), xUnit v3, WebApplicationFactory.

**Product/UI decisions:** All visible copy is Thai. The storefront has no left rail. Storefront follows the bright monochrome + restrained lime reference in `index.html`; the previously approved Muted Ocean palette remains the Admin theme for its later phase. Repeated or behavior-heavy UI becomes shared components. Text, number, and select inputs share one field API; select CSS uses `appearance: none` and a design-system arrow. Reduced motion, visible focus, semantic landmarks, and 14px minimum customer body copy are mandatory.

**Repository note:** This workspace has no `.git` metadata. Do not claim commits. Each task ends with focused verification and `TASKS.md` tracking instead of commit steps.

---

## File map

### Font and global foundation

- `src/ToyStore.Web/wwwroot/fonts/noto-sans-thai-thai-wght-normal.woff2` — Thai glyph variable font, weights 100–900.
- `src/ToyStore.Web/wwwroot/fonts/noto-sans-thai-latin-wght-normal.woff2` — Latin glyph variable font for product/brand names.
- `src/ToyStore.Web/wwwroot/fonts/OFL-Noto-Sans-Thai.txt` — vendored license.
- `src/ToyStore.Web/wwwroot/css/fonts.css` — two unicode-ranged `@font-face` rules.
- `src/ToyStore.Web/wwwroot/css/tokens.css` — color, typography, space, radius, shadow, motion, z-index and layout tokens.
- `src/ToyStore.Web/wwwroot/css/site.css` — reset, global element rules, focus/reduced-motion and responsive container.
- `src/ToyStore.Web/Components/App.razor` — preload Thai WOFF2 and load design-system styles without Google Fonts.

### Shell and storefront components

- `src/ToyStore.Web/Components/Layout/MainLayout.razor(.css)` — skip link, shell, main region and Thai error UI.
- `src/ToyStore.Web/Components/Layout/StoreHeader.razor(.css)` — logo, desktop/mobile navigation, search/account/cart actions.
- `src/ToyStore.Web/Components/Layout/StoreFooter.razor(.css)` — Thai legal/help links and copyright.
- `src/ToyStore.Web/Components/Storefront/SectionHeader.razor`, `HeroShowcase.razor`, `ProductCard.razor`, `CollectionCard.razor`, `JournalFeature.razor`, `TrustBenefits.razor` with scoped CSS — database-independent storefront presentation.
- `src/ToyStore.Web/Components/Storefront/Models/*.cs` — small immutable presentation records used by the components.
- `src/ToyStore.Web/Components/Pages/Home.razor(.css)` — Thai component composition and static sample models until catalog queries exist.

### Shared form and feedback components

- `src/ToyStore.Web/Components/Forms/StoreTextField.razor`, `StoreNumberField.razor`, `StoreSelectField.razor`, `SelectOption.cs` — label/help/error capable fields.
- `src/ToyStore.Web/Components/Forms/FormValidationStore.cs` — maps FluentValidation property failures into an `EditContext` message store.
- `src/ToyStore.Web/Components/Feedback/StoreAlert.razor`, `StoreToast.razor`, `StoreDialog.razor`, `StoreDrawer.razor`, `StoreSkeleton.razor` — shared feedback primitives.
- `src/ToyStore.Web/Components/Feedback/StoreDialog.razor.js` — native-dialog show/close and focus return only.
- `src/ToyStore.Web/Components/Pages/DesignSystem.razor` — development/example route that renders controls without database dependencies.

### Tests

- `tests/ToyStore.UnitTests/Web/StorefrontDesignContractTests.cs` — font, token, source-boundary, component and CSS contracts.
- `tests/ToyStore.UnitTests/Web/FormValidationStoreTests.cs` — FluentValidation-to-field mapping.
- `tests/ToyStore.IntegrationTests/Storefront/StorefrontRenderingTests.cs` — SSR Thai shell/home/design-system rendering and removal of demo pages.

---

## Task 1: Self-host Noto Sans Thai (M3-01)

**Files:** font assets, `fonts.css`, `App.razor`, `StorefrontDesignContractTests.cs`, `TASKS.md`

- [x] **Step 1: Write the failing font contract test**

Add a test that locates the repository root and asserts:

```csharp
[Fact]
public void NotoSansThaiIsSelfHostedAndPreloadedWithoutRuntimeFontCdn()
{
    var root = FindRepositoryRoot();
    var web = Path.Combine(root, "src", "ToyStore.Web");
    var app = File.ReadAllText(Path.Combine(web, "Components", "App.razor"));
    var fontsCss = File.ReadAllText(Path.Combine(web, "wwwroot", "css", "fonts.css"));

    Assert.True(new FileInfo(Path.Combine(web, "wwwroot", "fonts",
        "noto-sans-thai-thai-wght-normal.woff2")).Length > 20_000);
    Assert.True(new FileInfo(Path.Combine(web, "wwwroot", "fonts",
        "noto-sans-thai-latin-wght-normal.woff2")).Length > 20_000);
    Assert.True(File.Exists(Path.Combine(web, "wwwroot", "fonts", "OFL-Noto-Sans-Thai.txt")));
    Assert.Contains("font-weight: 100 900", fontsCss, StringComparison.Ordinal);
    Assert.Contains("rel=\"preload\"", app, StringComparison.Ordinal);
    Assert.Contains("noto-sans-thai-thai-wght-normal.woff2", app, StringComparison.Ordinal);
    Assert.DoesNotContain("fonts.googleapis.com", app, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("fonts.gstatic.com", app, StringComparison.OrdinalIgnoreCase);
}
```

- [x] **Step 2: Run RED**

Run:

```bash
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj \
  --filter FullyQualifiedName~StorefrontDesignContractTests
```

Expected: FAIL because `wwwroot/fonts` and `fonts.css` do not exist.

- [x] **Step 3: Vendor the pinned font assets and license**

Use an isolated temporary directory; do not add `package.json` or `node_modules`:

```bash
tmp_dir="$(mktemp -d)"
npm pack @fontsource-variable/noto-sans-thai@5.2.8 --pack-destination "$tmp_dir"
tar -xzf "$tmp_dir/fontsource-variable-noto-sans-thai-5.2.8.tgz" -C "$tmp_dir"
```

Copy with `apply_patch`/approved asset-copy tooling:

```text
package/files/noto-sans-thai-thai-wght-normal.woff2
  -> wwwroot/fonts/noto-sans-thai-thai-wght-normal.woff2
package/files/noto-sans-thai-latin-wght-normal.woff2
  -> wwwroot/fonts/noto-sans-thai-latin-wght-normal.woff2
package/LICENSE -> wwwroot/fonts/OFL-Noto-Sans-Thai.txt
```

Create `fonts.css` with two `@font-face` rules named `Noto Sans Thai`, `font-style: normal`, `font-display: swap`, `font-weight: 100 900`, and the Thai/Latin unicode ranges copied from the pinned package `wght.css`.

- [x] **Step 4: Load and preload the font**

Add before other site styles:

```razor
<link rel="preload"
      href="@Assets["fonts/noto-sans-thai-thai-wght-normal.woff2"]"
      as="font" type="font/woff2" crossorigin />
<link rel="stylesheet" href="@Assets["css/fonts.css"]" />
```

Do not preload every subset. Keep Thai as the one required first-paint preload; Latin loads through its unicode-ranged face when needed.

- [x] **Step 5: Verify GREEN and record M3-01**

Run the focused test and CI build. Mark M3-01 complete only after both pass.

---

## Task 2: Create design tokens and responsive foundation (M3-02)

**Files:** `tokens.css`, `site.css`, `app.css`, `App.razor`, design contract tests, `TASKS.md`

- [x] **Step 1: Write failing token/accessibility tests**

Assert exact required tokens exist: `--color-bg`, `--color-surface`, `--color-ink`, `--color-muted`, `--color-accent`, `--color-danger`, `--color-focus`, `--content-max`, `--duration-fast`, `--duration-normal`, `--ease-standard`; assert global CSS contains `box-sizing: border-box`, 14px minimum body size, `:focus-visible`, `prefers-reduced-motion: reduce`, and responsive container gutters.

- [x] **Step 2: Run RED**

Expected: token stylesheet missing and old Helvetica/Bootstrap defaults still present.

- [x] **Step 3: Implement `tokens.css`**

Use the values in `docs/DESIGN_SPEC.md`, including:

```css
:root {
  --color-bg: #f8f8f6;
  --color-surface: #ffffff;
  --color-ink: #111111;
  --color-muted: #686864;
  --color-line: #dededb;
  --color-accent: #dfff29;
  --color-danger: #b42318;
  --color-focus: #2563eb;
  --content-max: 76.25rem;
  --duration-fast: 160ms;
  --duration-normal: 260ms;
  --ease-standard: cubic-bezier(.2, .8, .2, 1);
}
```

Complete spacing/radius/shadow/typography tokens rather than scattering literal values through components.

- [x] **Step 4: Implement reset and responsive foundation**

`site.css` must set Noto Sans Thai, body `font-size: 1rem`, semantic element defaults, `.store-container`, `.visually-hidden`, `.skip-link`, visible two-layer focus, reduced motion, and mobile/tablet/desktop gutters. Remove obsolete scaffold rules and base64 error image from `app.css`; retain only Blazor-specific status/error hooks in Thai styling.

- [x] **Step 5: Load styles in deterministic order**

Load `fonts.css`, `tokens.css`, `site.css`, then scoped component styles. Keep Bootstrap temporarily only until Task 5 migrates account forms, then remove its stylesheet link.

- [x] **Step 6: Verify GREEN and record M3-02**

Run design contract tests, format, and CI build.

---

## Task 3: Build StoreShell and StoreHeader (M3-03, closes M1-R06)

**Files:** `MainLayout`, `StoreHeader`, `StoreFooter`, `Routes`, `NotFound`, delete `NavMenu`, `Counter`, `Weather`, `Auth`, rendering tests, `TASKS.md`

- [x] **Step 1: Write failing SSR shell tests**

Create PostgreSQL-backed WebApplicationFactory tests that GET `/` and assert:

```csharp
Assert.Contains("<html lang=\"th\"", html, StringComparison.Ordinal);
Assert.Contains("<header", html, StringComparison.Ordinal);
Assert.Contains("<nav", html, StringComparison.Ordinal);
Assert.Contains("<main", html, StringComparison.Ordinal);
Assert.Contains("ข้ามไปยังเนื้อหา", html, StringComparison.Ordinal);
Assert.Contains("สินค้า", html, StringComparison.Ordinal);
Assert.Contains("เข้าสู่ระบบ", html, StringComparison.Ordinal);
Assert.DoesNotContain("sidebar", html, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("Hello, world!", html, StringComparison.Ordinal);
```

Also assert `/counter` and `/weather` no longer return their sample content.

- [x] **Step 2: Run RED**

Expected: old sidebar and English scaffold content are present.

- [x] **Step 3: Implement semantic shell/header/footer**

`MainLayout` renders a visible-on-focus skip link, `StoreHeader`, `<main id="main-content" tabindex="-1">`, `@Body`, and `StoreFooter`. `StoreHeader` uses desktop navigation plus a native `<details>` mobile menu, so keyboard and no-JS behavior work before adding optional motion. Include Thai links for home, products, pre-order, in-stock, brands, account, and cart. Use `AuthorizeView` for login/account/logout without leaking authorization decisions to presentation.

- [x] **Step 4: Remove scaffold navigation and pages**

Delete `NavMenu.razor(.css)`, `Counter.razor`, `Weather.razor`, and `Auth.razor`. Translate `PageTitle`, not-found/error/reconnect/error-UI copy to Thai. Do not retain the storefront left rail.

- [x] **Step 5: Add responsive/focus behavior**

Desktop header becomes condensed below 1050px; mobile details menu appears below 900px. Every action is at least 44px on mobile. Motion uses tokenized transform/opacity and is disabled by reduced motion.

- [x] **Step 6: Verify GREEN and record M3-03/M1-R06**

Run rendering tests, existing account endpoint tests, format and CI build.

---

## Task 4: Build reusable storefront components (M3-04)

**Files:** `Components/Storefront/**`, `Home.razor(.css)`, rendering/contract tests, `TASKS.md`

- [x] **Step 1: Write failing component-source and home rendering tests**

Assert the seven component files exist and contain semantic/accessibility contracts. GET `/` and require Thai hero copy, section headings, product accessible names, pre-order/in-stock labels, empty/loading hooks, journal, trust benefits, and no `data:image`/remote image URL.

- [x] **Step 2: Run RED**

Expected: components and Thai home composition do not exist.

- [x] **Step 3: Define immutable presentation models**

Create records such as:

```csharp
public sealed record ProductCardModel(
    string Name,
    string Brand,
    string PriceLabel,
    string TypeLabel,
    string? ImageUrl,
    string ProductUrl,
    bool IsAvailable);
```

Add similarly small `CollectionCardModel` and `JournalStoryModel`. No Domain entities, EF types, or database calls enter these files.

- [x] **Step 4: Implement components and states**

Each component has focused parameters, useful Thai defaults only where appropriate, and scoped CSS. `ProductCard` supports normal, pre-order, in-stock, unavailable, disabled and loading skeleton states; its button/link has an accessible Thai name. `SectionHeader` supports heading level and optional action. Cards use CSS placeholder art when `ImageUrl` is absent—never prototype base64.

- [x] **Step 5: Compose the Thai home page**

Use static presentation models as design-system examples until M4 replaces them with query results. Copy remains clearly representative, does not invent stock/payment truth, and contains no database dependency. Add touch-friendly horizontal behavior only where it improves small screens; product grid remains responsive per spec.

- [x] **Step 6: Verify GREEN and record M3-04**

Run focused unit/integration tests plus account regression tests.

---

## Task 5: Build reusable form and feedback components (M3-05)

**Files:** `Components/Forms/**`, `Components/Feedback/**`, `DesignSystem.razor`, account pages, tests, docs, `TASKS.md`

- [x] **Step 1: Write failing form CSS/component contracts**

Assert shared text/number/select files exist; all expose label, help text, required/disabled and `ValueExpression`; CSS contains:

```css
.store-select select {
  appearance: none;
  -webkit-appearance: none;
  background: var(--color-surface);
}
.store-select::after { pointer-events: none; }
```

Assert focus/error/disabled selectors use tokens and no select rule restores browser-native appearance.

- [x] **Step 2: Write failing FluentValidation mapping tests**

Given an `EditContext` and failures for `Email` and `Quantity`, verify `FormValidationStore.Display(...)` adds Thai field messages, `Clear()` removes them, and property names that are not fields go to the model-level summary without throwing.

- [x] **Step 3: Run RED**

Expected: components and mapper are absent.

- [x] **Step 4: Implement shared fields**

Use Blazor `InputText`, `InputNumber<TValue>` and `InputSelect<TValue>` internally. Forward `Value`, `ValueChanged`, `ValueExpression`, `AdditionalAttributes`, `disabled`, `aria-invalid`, and one generated stable ID. Each component renders label, required marker, optional help, and `ValidationMessage<TValue>`. `StoreNumberField` forwards `min`, `max`, `step`, and `inputmode`; server validation remains authoritative.

- [x] **Step 5: Implement custom select presentation**

Wrap the native select in `.store-select` and draw the arrow with a CSS pseudo-element. Set inline-end padding, logical properties, focus-visible, invalid and disabled styles. Test the stylesheet contract and render it on `/design-system`; do not depend on Bootstrap `.form-select`.

- [x] **Step 6: Implement validation mapping**

`FormValidationStore` owns `ValidationMessageStore`, accepts `IEnumerable<ValidationFailure>`, clears stale messages, maps property names through `EditContext.Field`, and calls `NotifyValidationStateChanged`. It does not log expected validation failures.

- [x] **Step 7: Implement feedback primitives**

`StoreAlert` uses `role="alert"` only for urgent errors; `StoreToast` uses `aria-live="polite"`; `StoreSkeleton` is hidden from assistive technology; `StoreDialog` uses native `<dialog>` with an accessible title and its JS module only for `showModal`, close, focus return; `StoreDrawer` reuses the dialog behavior with placement styling. Confirmation content/actions are render fragments so business pages control wording and commands.

- [x] **Step 8: Add database-independent examples and migrate repeated account fields**

`/design-system` renders every state in Thai. Replace repeated Register/Login/ChangePassword text-field markup with shared controls while preserving exact `FormName`, SSR POST field names, antiforgery, FluentValidation-aligned presentation rules and existing account flows. Remove Bootstrap stylesheet after no production component depends on it.

- [x] **Step 9: Verify GREEN and record M3-05**

Run form/unit tests, storefront/account integration tests, format and CI build. Confirm rendered select markup has the shared class and CSS arrow contract.

---

## Task 6: M3 full verification, visual smoke, review and handoff

**Files:** `TASKS.md`, `docs/DESIGN_SPEC.md` only if implementation reveals an intentional token/API change

- [x] **Step 1: Run automated verification**

```bash
dotnet restore ToyStore.sln
dotnet format ToyStore.sln --verify-no-changes
dotnet build ToyStore.sln --no-restore -p:CI=true
dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --no-build
dotnet test tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj --no-build
dotnet list ToyStore.sln package --vulnerable --include-transitive
docker compose config >/dev/null
```

- [x] **Step 2: Run local visual/accessibility smoke**

Start PostgreSQL and Web, then inspect `/`, `/design-system`, `/Account/Login` at mobile/tablet/desktop widths. Verify keyboard-only header/details/dialog/select use, focus return, Thai copy, 14px minimum body, no horizontal overflow, no default browser select appearance, and reduced-motion behavior. Use screenshots only as evidence; do not generate bitmap UI assets.

- [x] **Step 3: Request code review**

Review M3-01 through M3-05 against `AGENTS.md`, `docs/DESIGN_SPEC.md`, `index.html`, the approved commerce spec, architecture boundaries, Thai-first copy, reusable component API, CSS browser behavior, accessibility and no-remote-font requirement. Fix every Critical/Important finding with focused RED/GREEN tests.

- [x] **Step 4: Update task tracking**

Mark M3-01 through M3-05 complete only after fresh green verification. Set Current Focus to M4-01 and Next Task to M4-02. Record exact test counts and review verdict.

---

## Self-review result

- Spec coverage: font hosting, tokens, responsive shell, no storefront rail, all M3 storefront/form/feedback components, Thai copy, reduced motion, keyboard/focus, custom select styling and FluentValidation mapping are assigned to explicit tasks.
- Architecture: all components are presentation-only; no component receives Domain/EF types or queries a database.
- Asset discipline: only pinned WOFF2/OFL files are vendored; prototype base64 and remote runtime assets are forbidden by tests.
- Type consistency: `ProductCardModel`, `SelectOption<TValue>`, and `FormValidationStore` names/signatures are used consistently.
- Placeholder scan: no TBD/TODO/future implementation placeholders remain; M4 replacement of static presentation samples is an explicit task boundary, not missing M3 work.

## Completion evidence

- Completed: 2026-07-17
- Automated verification: format clean; CI build 0 warnings/errors; Unit 167/167; Integration 51/51; NuGet vulnerability scan clean; Compose config valid.
- Browser verification: 4/4 Chrome smoke checks passed for `/`, `/design-system` and `/Account/Login` at mobile, tablet and desktop widths, including no horizontal overflow, custom select appearance, native Escape close and focus return.
- Review: M3-05 spec review and independent quality review both approved with no Critical/Important findings.
