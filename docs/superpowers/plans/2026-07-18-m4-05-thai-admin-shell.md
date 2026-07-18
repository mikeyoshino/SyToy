# M4-05 Thai Admin Shell Plan

**Goal:** Deliver a Thai-first, server-authorized Admin shell using the approved borderless Muted Ocean design, reusable responsive navigation and reusable Admin list/modal/state primitives that later catalog, inventory, order and reporting slices can compose without duplicating controls.

**Architecture:** M4-05 is a Web/presentation and authorization-shell milestone. Application owns the stable `CanAccessAdmin` policy name; Web configures the policy and owns Razor layouts/components/routes. Existing shared Store form, validation, dialog, drawer, alert, toast and skeleton components remain the single cross-theme behavior boundary. There is no domain use case, query handler, database access, migration, dashboard metric, actionable-count query, upload or CRUD in this milestone.

**Locked decisions:**

- All visible shell, navigation, placeholder, state and accessibility copy is Thai-first. English is limited to proper names or technical identifiers.
- Admin uses exact Muted Ocean tokens: accent `#3f91b8`, accent-strong `#2f789b`, accent-soft `#eaf6fb`, background `#f5f8fa`, surface `#ffffff`, ink `#15212a`, muted `#667782`. Storefront lime/theme/layout must not change.
- Every normal-size text/background pairing must pass WCAG AA (at least 4.5:1), and focus/non-text indicators must pass their applicable 3:1 requirement. Base accent `#3f91b8` must not carry small white text; use accent-strong `#2f789b` with white or dark ink `#15212a` on the base/soft accent according to the verified pairing contract.
- Hierarchy comes from whitespace, surface contrast and restrained shadows. Do not add harsh/heavy borders around layout, rail, cards, buttons or modal surfaces. Semantic status never relies on color alone.
- Define `PolicyNames.CanAccessAdmin` as the route-shell policy: Admin role and absence of the `MustChangePassword=true` claim. Keep granular `CanManageProducts`, `CanManageOrders`, `CanVerifyPayments` and `CanManageUsers` for later actions.
- Every routed `/admin` component explicitly declares `[Authorize(Policy = PolicyNames.CanAccessAdmin)]`; menu visibility is never the authorization boundary.
- Unauthorized routing is deterministic across two distinct paths. Direct/static SSR uses the cookie authorization challenge/forbidden result: anonymous users receive a `302` to `/Account/Login` with a validated local path-and-query return URL, an Admin carrying `MustChangePassword=true` receives a `302` to `/Account/Manage/ChangePassword`, and other authenticated users receive a `302` to `/Account/AccessDenied`. Enhanced navigation uses authentication-aware `AuthorizeRouteView.NotAuthorized` behavior with the same destinations. Avoid redirect loops and never pass an absolute return URL.
- Replace the temporary `/account/admin-probe` regression target with `/admin`; remove the probe page only after all Identity regressions use the real protected route.
- Global rail destinations are Dashboard, Catalog, Inventory, Orders, Notifications, Sales Reports and Settings plus an antiforgery-protected POST logout. Do not show invented badge counts. Badge API may accept only a real optional actionable count supplied later.
- Desktop rail has stable layout width, icon/collapsed mode and pin state; hover/focus expansion must not move page content. Collapsed entries retain Thai accessible names and tooltips. At widths below 900 px reuse `StoreDrawer` with its native focus trap, explicit close button and Escape close. Escape/manual close returns focus to the trigger; route-navigation close must suppress old-trigger restoration so `FocusOnNavigate` owns focus on the new page `h1`. Backdrop-click close is not required in M4-05.
- Context pills never duplicate the global rail. Catalog pills are Products/Brands/Universes. Orders pills are All/In-stock/Pre-order/Ready to ship/Shipped/Cancelled and preserve query URLs. Dashboard metrics/tabs are deferred until real queries exist.
- Protected placeholder pages make every menu destination usable and non-404, but contain honest Thai next-step/empty copy only. Do not add Category management or fake dashboard/order/catalog data.
- Reuse `StoreFieldShell`, `StoreTextField`, `StoreNumberField`, `StoreSelectField`, `SelectOption<T>`, `FormValidationStore`, `StoreAlert`, `StoreToast`, `StoreDialog`, `StoreDrawer` and `StoreSkeleton`. Do not create Admin-specific text/number/select components or a second dialog/drawer JS module.
- New Admin primitives stay presentation-only and use primitive values/render fragments or small immutable Web view models; they do not reference Domain entities, EF, Infrastructure or `DbContext`.
- Motion uses existing 160/260 ms tokens with transform/opacity and an effective `prefers-reduced-motion: reduce` override. Targets are at least 44×44 px; focus is visible and never clipped.
- SSR must render meaningful protected-page `h1` and content before hydration. No new UI/icon/runtime package, remote font/image, base64 asset or JavaScript library is allowed. Inline SVG icons live in one reusable component.

## Task 1: RED authorization contract and real Admin policy

**Files:**

- Modify: `src/ToyStore.Application/Common/Authorization/PolicyNames.cs`
- Modify: `src/ToyStore.Web/Identity/IdentityWebConfiguration.cs`
- Modify: `src/ToyStore.Web/Components/Routes.razor`
- Create/modify: account redirect components under `src/ToyStore.Web/Components/Account/Shared`
- Modify: `tests/ToyStore.UnitTests/Web/IdentityCompositionTests.cs`
- Modify: `tests/ToyStore.UnitTests/Application/Accounts/AccountHandlerTests.cs`
- Modify: `tests/ToyStore.IntegrationTests/Identity/AccountEndpointTests.cs`
- Create: `tests/ToyStore.IntegrationTests/Admin/AdminAuthorizationTests.cs`

- [x] RED policy tests prove anonymous, Customer, forced-password Admin and valid Admin outcomes for `CanAccessAdmin`.
- [x] RED direct/static SSR HTTP tests use `AllowAutoRedirect=false` and assert exact `302` relative locations for `/admin` and every protected placeholder route: anonymous → Login with local path/query return URL, Customer → AccessDenied, forced Admin → ChangePassword and valid Admin → 200 Thai shell. Add loop tests for Login, AccessDenied and ChangePassword destinations.
- [x] RED enhanced-navigation/renderer tests exercise the authentication-aware `NotAuthorized` branch separately and prove it selects the same three destinations without using an absolute URI.
- [x] GREEN add the policy without weakening granular management policies.
- [x] GREEN configure the server cookie authorization redirect/result behavior for direct requests and make `AuthorizeRouteView.NotAuthorized` authentication/claim-aware for enhanced navigation; keep server policy metadata authoritative.
- [x] Move Admin probe regressions to `/admin`, then delete `AdminProbe.razor` and prove no `/account/admin-probe` dependency remains.
- [x] Update the stable policy-name contract in `AccountHandlerTests` for `CanAccessAdmin`.
- [x] Render the real Admin logout form, extract its antiforgery token, POST successfully, verify sign-out and a subsequent Admin challenge, and retain missing/invalid-token rejection tests.

## Task 2: RED design/source contracts and Admin theme boundary

**Files:**

- Modify: `src/ToyStore.Web/wwwroot/css/tokens.css`
- Create: `src/ToyStore.Web/wwwroot/css/admin.css`
- Modify: `src/ToyStore.Web/Components/App.razor`
- Create: `tests/ToyStore.UnitTests/Web/Admin/AdminShellDesignContractTests.cs`

- [x] RED exact-token tests lock Muted Ocean colors, Noto Sans Thai, 44 px targets, 160/260 ms motion, reduced-motion behavior and no lime token use inside Admin selectors.
- [x] RED contrast/pairing contracts compute or otherwise prove WCAG thresholds for normal/muted text, links, buttons/CTA, badges and focus indicators; explicitly reject white normal text on base accent `#3f91b8`.
- [x] RED source contracts reject remote assets, base64 images, new icon/UI packages, heavy layout/card/button borders and duplicated select/number/dialog/drawer implementations.
- [x] GREEN add scoped Admin token aliases so existing shared controls and feedback components inherit Muted Ocean under `.admin-shell` without changing Storefront root tokens.
- [x] Keep global CSS limited to the Admin theme/top-layer dialog/drawer needs that CSS isolation cannot safely reach.

## Task 3: TDD Admin layout, centralized navigation and route matching

**Files:**

- Create: `src/ToyStore.Web/Components/Admin/Layout/AdminLayout.razor`
- Create: `src/ToyStore.Web/Components/Admin/Layout/AdminLayout.razor.css`
- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminNavigationItem.cs`
- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminNavigation.cs`
- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminRouteMatcher.cs`
- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminIcon.razor`
- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminRail.razor`
- Create/modify focused Unit/renderer tests under `tests/ToyStore.UnitTests/Web/Admin`

- [x] RED navigation model tests lock Thai labels, exact destinations, unique icons, route groups and no Category destination.
- [x] RED route-matcher tests cover exact path, nested Catalog routes, query-sensitive contextual URLs, trailing slash, absolute navigation URI and false-prefix attacks such as `/administrator`.
- [x] GREEN layout emits skip link, global navigation landmark and stable `main#admin-main`; subscribes/unsubscribes from navigation safely.
- [x] GREEN rail supports collapsed/pinned state, active route-group state, tooltip/accessibility text, optional real badge and antiforgery POST logout.
- [x] Keep icon SVG centralized, decorative paths hidden and icon-only controls explicitly named in Thai.

## Task 4: TDD responsive drawer and contextual navigation

**Files:**

- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminMobileNavigation.razor`
- Create: `src/ToyStore.Web/Components/Admin/Navigation/AdminContextNav.razor`
- Modify/reuse: `src/ToyStore.Web/Components/Feedback/StoreDrawer.razor`
- Modify/reuse: `src/ToyStore.Web/Components/Feedback/StoreDialog.razor`
- Modify/reuse: `src/ToyStore.Web/Components/Feedback/StoreDialog.razor.js`
- Create/modify renderer/behavior tests under `tests/ToyStore.UnitTests/Web/Admin`

- [x] RED tests prove mobile menu button state/name, close-on-navigation, native drawer focus/Escape contract and no duplicate global/context destination. Browser/JS regressions distinguish Escape/manual close (restore trigger) from navigation close (do not restore old trigger; new `h1` receives focus).
- [x] GREEN use `StoreDrawer` below 900 px; desktop rail remains hidden from the accessibility tree on mobile and mobile controls remain absent from desktop layout flow.
- [x] Extend the one shared dialog/drawer module with the smallest explicit programmatic close mode needed to suppress focus return during navigation; preserve the existing default focus return for all Storefront/manual/Escape closes and do not add a second JS module.
- [x] GREEN context pills expose Thai navigation labels, `aria-current`, keyboard-visible focus and a local horizontal scroller that never causes body overflow.
- [x] Catalog pages receive Products/Brands/Universes pills; Orders receives approved filter URLs; other sections omit meaningless pills.

## Task 5: Micro-TDD reusable Admin page/list/state primitives

**Files:**

- Create components/models under `src/ToyStore.Web/Components/Admin/Shared/`
- Create focused tests under `tests/ToyStore.UnitTests/Web/Admin/`

- [x] `AdminPageHeader`: one page title/description plus optional actions; semantic heading ownership stays with the page.
- [x] `AdminStatusBadge`: neutral/info/success/warning/danger tone plus visible text/icon, never color-only.
- [x] `AdminContentState`: Loading/Empty/Error/Ready; skeleton/live status, Thai next action, alert/retry button and no content layout shift.
- [x] `AdminDataTable`: required accessible caption and local horizontal overflow; caller supplies header/rows/empty content through render fragments.
- [x] `AdminFilterBar`: GET/URL-oriented form slots, Thai landmark/name and optional clear link; no business filter model.
- [x] `AdminPagination`: primitive current/total/URL factory API, Thai label, `aria-current`, disabled first/previous/next/last boundaries and compact ellipsis without invalid page links.
- [x] `AdminModal`: wraps `StoreDialog`, large desktop surface and full-screen mobile presentation; no duplicate JS or focus implementation.
- [x] Renderer/API tests prove Thai states, semantic markup, disabled boundaries, render-fragment composition and absence of Domain/Infrastructure coupling.

## Task 6: Protected Thai placeholder destinations

**Files:**

- Create pages under `src/ToyStore.Web/Components/Admin/Pages/`
- Modify: `src/ToyStore.Web/Components/_Imports.razor` only for stable Admin namespaces
- Remove: `src/ToyStore.Web/Components/Account/Pages/AdminProbe.razor`
- Create/modify page contract and HTTP tests

- [x] Add `/admin`, `/admin/products`, `/admin/brands`, `/admin/universes`, `/admin/inventory`, `/admin/orders`, `/admin/notifications`, `/admin/reports` and `/admin/settings`.
- [x] Every page explicitly sets `@layout AdminLayout`, `PageTitle`, exactly one meaningful Thai `h1` and `CanAccessAdmin` authorization metadata.
- [x] Dashboard and every list destination render only an honest neutral Thai “available in the next phase” placeholder with no invented records, loading, empty-dataset, error or ready state. Loading/Empty/Error/Ready variants are exercised only in Task 5 renderer/test-host tests until a real query owns those states.
- [x] Catalog and Orders pages show the correct contextual pills and active state; all global menu destinations resolve without 404.
- [x] No `/admin/categories` route or Category management affordance exists.

## Task 7: Responsive, keyboard and browser verification

**Files:**

- Modify component-scoped/Admin CSS as findings require
- Add browser/source regression tests only where stable

- [x] Verify 390 px and 768 px: mobile drawer, 44 px targets, no body overflow, table/pills scroll locally and Admin modal is full-screen.
- [x] Verify 900–1199 px: compact icon rail, hover/focus/pin behavior does not shift main content and tooltips are not clipped.
- [x] Verify at least 1200 px: stable expanded/pinned rail, main content does not overlap and placeholder routes remain readable.
- [x] Keyboard smoke: skip link, every rail/pill/action, drawer Escape/manual-close focus return, navigation-close focus on the new `h1`, logout form and pagination focus/`aria-current`.
- [x] Reduced-motion smoke: transitions become effectively instant without hiding content; SSR output retains Thai heading/content before hydration.
- [x] Contrast audit verifies every production Admin text/background, link/button/badge and focus pairing at its rendered size; small white text never appears on base accent `#3f91b8`.
- [x] Confirm shared select still uses `appearance: none` and the Admin token aliases do not regress Storefront controls.

## Task 8: Full verification and independent reviews

- [x] Run focused Admin policy, route, renderer, navigation, primitive and source-contract tests.
- [x] Run focused Identity regressions after removing AdminProbe.
- [x] Run format, warnings-as-errors CI build, full Unit and Integration suites, vulnerability scan and Compose validation.
- [x] Verify no EF model/migration change, no Domain/Infrastructure dependency from Admin components, no new package/remote asset/base64 image and no fake metrics/counts.
- [x] Obtain independent specification/accessibility review and fix every gap with RED/GREEN tests.
- [x] Obtain independent code-quality/authorization review and fix every Critical/Important finding.
- [x] Mark M4-05 complete only after fresh root verification; set Current Focus M4-06 and Next Task M4-07.

## Explicit non-goals

- No dashboard analytics query/chart, sales aggregation, operational queue or Product performance data.
- No Product, Brand, Universe, Character, Inventory, Order, Notification, Report or Settings handler/query/CRUD.
- No upload/reorder/autocomplete implementation and no business FluentValidation form in this shell milestone.
- No schema/migration, database query from Razor, repository/service abstraction or Infrastructure change.
- No persisted per-user rail preference, real badge count, chart package, icon package or separate Admin form-control family.

## Completion evidence

Completed and verified 2026-07-17 (Asia/Bangkok). Fresh root verification passed format, warnings-as-errors build, Unit 538/538, Integration 104/104, vulnerability scan, Compose validation and EF pending-model gate. The retained real-Chrome report passes 30/30 assertions at 390, 768, 900, 1199 and 1200 px, including full-screen Admin modal behavior, keyboard focus modes, Thai tooltip metadata, overflow, stable rail layout and reduced motion. Independent specification/accessibility and authorization/code-quality re-reviews approved the final implementation with no findings.
