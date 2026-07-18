# Toy Store Delivery Tasks

ไฟล์นี้คือ backlog และ progress tracker หลักของโปรเจกต์ อัปเดต checkbox และ Current Focus ทุกครั้งที่เริ่มหรือจบงานสำคัญ

## Status legend

```text
[ ] Not started
[-] In progress
[x] Completed and verified
[!] Blocked — add reason directly below the task
```

## Current focus

- 2026-07-18: ทำ Catalog filter แบบ collapsible ใต้ 64rem เพื่อลดความยาวหน้า mobile/tablet โดย default ย่อ, แสดงจำนวน filter ที่ใช้อยู่, รองรับ keyboard/ARIA และคง desktop sticky sidebar เปิดเต็มตลอด
- 2026-07-18: เพิ่ม Product image derivative สำหรับ storefront: ทุกภาพที่อัปโหลดใหม่สร้าง WebP thumbnail สูงสุด 960px quality 84 ใน staging/commit/compensation เดียวกับต้นฉบับ, persist key/url แยก, list/search/home/cart ใช้ thumbnail พร้อม fallback รูปเดิมสำหรับ legacy product และ lazy decode ใน shared ProductCard
- 2026-07-18: เปลี่ยน M10-04 เป็น full production Docker Compose: manual GitHub Actions รับ branch (default `main`) → Release build/migration review → build/push non-root Web image ไป GHCR → pinned SSH ส่ง immutable image digest → root-owned backup/Compose activation/readiness/image rollback; นำ test suite ออกจาก deployment workflow ตาม operational choice โดย local validation ล่าสุดยังผ่าน 1,296/1,296 เหลือ clean Ubuntu VPS deploy/restore verification ก่อนปิด task
- 2026-07-18: ออกแบบโลโก้ SY TOYS ใหม่เป็น wordmark แนวนอนสีดำ/เขียว lime ตัดพื้นที่ว่างบนล่าง ทำ shared `BrandLogo` สำหรับ Storefront/Admin และอัปเดต favicon ให้ใช้ภาษาภาพเดียวกัน
- 2026-07-18: แก้ Account Order search ทำให้ Blazor circuit ล้ม เพราะ shared `StoreTextField` render `ValidationMessage` นอก `EditForm`; ให้ standalone GET/search field ไม่สร้าง validation component เมื่อไม่มี cascading `EditContext` พร้อม rendering regression test
- 2026-07-18: เพิ่ม Order history search จากเลขคำสั่งซื้อ/ชื่อสินค้าใน URL `q`, PostgreSQL ownership-scoped ILIKE พร้อม escape wildcard, reset/clamp page, pagination แบบเลขหน้า 5 ปุ่มพร้อม first/last และ Thai empty/count states
- 2026-07-18: ทำ reusable storefront ProductCard ให้สูงเท่ากันในแต่ละ grid row และตรึงราคา/CTA ที่ฐานเดียวกันทั้งหน้าแรกและ Catalog แม้ชื่อสินค้ามีจำนวนบรรทัดต่างกัน
- 2026-07-18: รวม `/account/orders`, Order detail และ `/Account/Manage/ChangePassword` ให้ใช้ `CustomerAccountLayout` + `CustomerAccountNav` ชุดเดียว ลบ Manage layout/nav รุ่นเก่า และแก้ scoped `NavLink` selector ด้วย `::deep` เพื่อห้าม underline ทุก state จริง
- 2026-07-18: ปรับ Customer Account navigation จากแถบบนเป็น sticky sidebar ด้านซ้ายใน order history/detail, ลด font weight 700 → 500 (active 600), ลบ underline ทุก interaction state และใช้ 2-column top navigation เฉพาะจอเล็ก
- 2026-07-18: ทำ M8-01 Customer Order account ครบ history/detail ที่ `/account/orders`: server-side pagination, Thai Payment/Fulfillment status, immutable item/address/pre-order/payment snapshots, responsive empty/loading/error states, checkout success deep link และ PostgreSQL ownership proof ที่ cross-customer list/detail อ่านไม่ได้
- 2026-07-18: เพิ่ม dismissible backdrop behavior ให้ shared dialog/drawer โดยคลิกพื้นที่นอก surface แล้วเล่น slide-out, ปิด drawer และคืน focus; ระหว่าง cart busy จะไม่อนุญาตให้ปิด
- 2026-07-18: ลด quantity stepper และปุ่มนำออกใน cart drawer เป็น control 36px/13px บน desktop-tablet โดยคง touch target 44px บน mobile
- 2026-07-18: ลดชื่อสินค้าใน cart drawer เป็น body 14px น้ำหนัก bold ไม่มี underline และใช้ muted hover โดยคง visible keyboard focus
- 2026-07-18: แก้ storefront cart icon ไม่เปิด drawer จาก StoreDialog hydration race และ preserved-layout state propagation; ให้ StoreCartDrawer subscribe coordinator โดยตรง และใช้ Web Animations API บน shared StoreDrawer เพื่อบังคับ slide เข้า/ออกจากขวาที่ native dialog top layer แสดงผลได้แน่นอน พร้อม reduced-motion และ guest localStorage cart
- 2026-07-18: เพิ่ม Stripe sandbox/local E2E runbook สำหรับตั้ง test keys, Stripe CLI webhook listener, ทดสอบ In-stock/Pre-order ด้วย Card/PromptPay, ตรวจ CheckoutAttempt → verified webhook → Payment/Order exactly once และ troubleshooting provider errors
- 2026-07-18: ให้ Stripe Checkout prefill `customer_email` จาก ASP.NET Identity ฝั่ง server ทั้ง In-stock/Pre-order เพื่อลูกค้าไม่ต้องกรอกอีเมลซ้ำ โดยยังไม่สร้างหรือ persist Stripe Customer
- 2026-07-18: แก้ Stripe Checkout Session ให้ส่ง `ui_mode=embedded_page` ตาม Stripe API/Stripe.net 52.1.1 ปัจจุบันทั้ง In-stock และ Pre-order แทนค่า `embedded` ที่ provider เลิกรองรับ
- 2026-07-18: ทำ M7-01 immutable Thai address catalog จาก `kongvut/thai-province-data` commit `326c2ebe...` แบบ embedded/Frozen Singleton พร้อม fail-fast integrity validation, Application lookup query, reusable cascading จังหวัด → อำเภอ/เขต → ตำบล/แขวง → รหัสไปรษณีย์ และ authoritative checkout relation validation ทั้ง In-stock/Pre-order
- 2026-07-18: ปิด race หลัง Login โดยให้ `/checkout` รอ anonymous browser cart merge สำเร็จก่อนอ่าน customer cart จึงไม่แสดงตะกร้าว่างชั่วคราวระหว่าง full-page authentication handoff
- 2026-07-18: Implement In-stock checkout จริงครบเส้นทาง Login/merge cart → ที่อยู่ → atomic multi-item stock reservation → Stripe Embedded full payment → verified webhook → consume stock + Payment + Order exactly once; เพิ่ม checkout expiry webhook คืน stock, preserved Pre-order snapshots migration และ PostgreSQL concurrency/expiry/replay tests แล้ว เหลือ Stripe test-mode E2E กับ saved Thai address catalog/Admin maintenance ใน M7
- 2026-07-18: แก้ cart และ Pre-order login handoff ให้หน้า Account ซึ่งใช้ static SSR โหลดด้วย full document request แทน interactive routing ที่อาจตก Not Found; In-stock `/checkout` ยังรอ M7-07 และต้องไม่รับเงินก่อนมี atomic stock reservation
- 2026-07-18: เริ่ม direct Pre-order checkout จริง: modal ไปหน้าที่อยู่, สร้าง CheckoutAttempt + capacity reservation แบบ atomic, Stripe Embedded deposit, verified webhook และสร้าง Payment/Order ครั้งเดียว; ยังต้อง verify ด้วย Stripe test mode และทำ Thai address catalog/saved address/expiry maintenance ให้ครบ M7
- 2026-07-18: ตัดจำนวน capacity คงเหลือออกจากผลตรวจพรีออเดอร์ และยืนยันช่องว่างก่อนสั่งจริง: ระบบยังขาด CheckoutAttempt, ที่อยู่, Stripe, webhook, Payment และ Order ใน M7
- 2026-07-18: แก้ปุ่มยืนยันตรวจสอบสิทธิ์พรีออเดอร์ใน modal ไม่ทำงาน โดยเชื่อม external submit button กับ HTML form id ที่ตรงกัน
- 2026-07-18: ซ่อน global focus ring เฉพาะ storefront h1 ที่ Blazor FocusOnNavigate โฟกัสอัตโนมัติหลัง refresh โดยคง visible focus ของ interactive controls
- 2026-07-18: ลด Home latest-product hero เป็น highlight banner แนวนอนสูง 20–22rem และใช้ horizontal snap list บน mobile แทนการซ้อนภาพในแนวตั้ง
- 2026-07-18: ลบ static marketing copy/CTA จาก Home hero และขยาย latest-product mosaic เต็มความกว้าง พร้อม skeleton และไม่จองพื้นที่เมื่อไม่มีสินค้า
- 2026-07-18: เปลี่ยน Home hero art เป็น product mosaic จากสินค้า Published ล่าสุด 5 ชิ้น โดยชิ้นล่าสุดเด่นและทุกช่องมี badge พรีออเดอร์/พร้อมส่ง
- 2026-07-18: กำหนด ProductGallery responsive 4 ระดับให้ชัดเจน: small 1, medium 2, large 3 และ larger 4 คอลัมน์ โดย Catalog large-card คง 3 คอลัมน์บนจอใหญ่
- 2026-07-18: เพิ่ม large-card layout ให้ reusable ProductGallery และใช้ Catalog 3 คอลัมน์บน desktop เพื่อให้ thumbnail ใหญ่ขึ้นหลังเพิ่ม filter sidebar
- 2026-07-18: ปรับ spacing และ focus state ของ searchable dropdown ให้เหลือเส้น focus บางชั้นเดียว และไม่ highlight option แรกก่อนผู้ใช้เลือก
- 2026-07-18: เพิ่ม autocomplete-style search ภายใน custom Brand และ Character dropdown ของ Catalog filter เพื่อรองรับตัวเลือกจำนวนมาก
- 2026-07-18: ปรับ Catalog desktop เป็น filter sidebar ด้านซ้ายและ product results ด้านขวา โดย mobile ยังเรียงแนวตั้งตามพื้นที่จอ
- 2026-07-18: จัด Product Detail ใหม่ให้ gallery และรายละเอียดอยู่คอลัมน์ซ้ายเดียวกันเพื่อไม่ให้เส้นคั่นตัดผ่าน purchase column และลดชื่อสินค้าใน purchase column เป็น 1rem
- 2026-07-18: ลดข้อมูลหน้า Pre-order Product Detail โดยซ่อนยอดคงเหลือ จำนวนที่ยังรับได้ สูงสุดต่อคน และจำนวน capacity จากข้อความสถานะ โดยยังตรวจทั้งหมดฝั่ง server ตามเดิม
- 2026-07-18: แยก Product Detail และข้อมูล Brand/Category/Universe/Character ออกจาก purchase column เป็น section เต็มความกว้างใต้ gallery และข้อมูลราคา
- 2026-07-18: ปรับ reusable storefront ProductCard ที่หน้าแรก รายการสินค้า และผลค้นหาใช้ร่วมกัน ให้พรีออเดอร์แสดงเฉพาะราคามัดจำ และเพิ่ม thumbnail บน desktop ด้วย grid 4 คอลัมน์
- 2026-07-18: ทำ action ของสินค้าให้มองเห็นเสมอด้วยคอลัมน์จัดการแบบ sticky และอธิบาย Draft -> Published ในข้อความสำเร็จ เพื่อให้ Admin รู้ว่าสินค้าจะแสดง storefront หลัง “เผยแพร่หน้าร้าน” เท่านั้น
- 2026-07-18: แก้ Admin product list ทำให้ Blazor circuit ล้มหลังสร้างสินค้ารายการแรก โดยนำ React-style empty fragment ออกจาก Razor และกำหนด query splitting behavior แบบ explicit สำหรับ Product queries ที่โหลด Images กับ Characters
- 2026-07-18: แก้อาการบันทึกสินค้าแล้วค้างระหว่างอ่านรูปใน Blazor Server โดย buffer `IBrowserFile` ให้เสร็จก่อนส่ง product command และกำหนด timeout พร้อม validation ภาษาไทยที่ช่องรูปภาพ เพื่อไม่ให้ transaction ผูกกับ browser stream หรือปุ่ม loading ค้างไม่สิ้นสุด
- 2026-07-18: แก้ปัญหาแก้ไขรูปพรีออเดอร์ (Update Draft) โดยอนุญาต retain ภาพเดิมร่วมกับไฟล์ใหม่ใน validator

```text
Milestone: M7 — Checkout, Stripe and Order creation
Active task: M7-06/M7-09 Stripe test-mode E2E and provider failure verification
Next task: M7-02 saved shipping addresses
```

## Project constraints

- Deploy production บน Linux server เครื่องเดียว
- PostgreSQL รันใน Docker; local Web รันด้วย `dotnet run`
- ใช้ EF Core Code First และ apply pending migration ตอน application startup ก่อนรับ request
- ไม่ใช้ Redis, Cloudflare R2, distributed cache หรือ application job scheduler
- รูปสินค้าเก็บใน local media directory
- UI ภาษาไทยและใช้ Noto Sans Thai
- `index.html` เป็น visual reference
- รักษา Clean Architecture, Modular Monolith และ Vertical Slice

---

## M0 — Specification and repository foundation

เป้าหมาย: มีเอกสารและกฎที่เพียงพอสำหรับเริ่ม implementation โดยไม่ตีความ architecture ใหม่ทุกครั้ง

- [x] **M0-01** Create visual HTML reference
  - Source: `index.html`
  - Verified: responsive layout, Thai copy, Noto Sans Thai, transitions

- [x] **M0-02** Document architecture and domain rules
  - Sources: `docs/ARCHITECTURE.md`, `docs/DOMAIN_RULES.md`

- [x] **M0-03** Document UI design system
  - Source: `docs/DESIGN_SPEC.md`

- [x] **M0-04** Document local and production environments
  - Sources: `docs/LOCAL_DEVELOPMENT.md`, `docs/DEPLOYMENT.md`

- [x] **M0-05** Create repository agent guidance and project Skill
  - Sources: `AGENTS.md`, `.agents/skills/toy-store-development/`

- [x] **M0-06** Add PostgreSQL Compose and repository defaults
  - Sources: `compose.yaml`, `.editorconfig`, `Directory.Build.props`, `global.json`

Exit criteria: documentation links and Skill validation pass; PostgreSQL Compose configuration is valid.

---

## M1 — Application foundation

เป้าหมาย: solution build/test ได้ เชื่อม PostgreSQL ได้ และมี application shell พร้อมพัฒนา vertical slice

- [x] **M1-01** Scaffold .NET 10 solution
  - Create `ToyStore.sln`
  - Create `src/ToyStore.Web`
  - Create `src/ToyStore.Application`
  - Create `src/ToyStore.Domain`
  - Create `src/ToyStore.Infrastructure`
  - Create `tests/ToyStore.UnitTests`
  - Create `tests/ToyStore.IntegrationTests`
  - Acceptance: `dotnet build ToyStore.sln` succeeds
  - Verified: local restore/build, Web startup and HTTP 200 smoke test
  - Review note: generated `UnitTest1` files are empty placeholders and are not quality evidence

### M1 scaffold review findings

- [x] **M1-R01** Remove generated SQLite provider, migration and `Data/app.db`
  - Severity: High
  - Reason: architecture requires PostgreSQL and the resolved SQLite native package has a High-severity advisory
  - Owner: M1-03 and M2-01
  - Verified: SQLite packages, Web-owned migrations and `Data/app.db` are absent; vulnerability scan is clean

- [x] **M1-R02** Make a clean CI build pass with warnings treated as errors
  - Severity: Medium
  - Current failures: CA1848, CA1859 and CA1873 in generated Identity support code
  - Owner: M1-05 or earlier when the affected Identity code is moved
  - Verified: `dotnet build ToyStore.sln --no-restore -p:CI=true` completes with 0 warnings and 0 errors

- [x] **M1-R03** Replace empty scaffold tests with architecture and foundation tests
  - Severity: Medium
  - Owner: M1-02 and M1-04
  - Verified: placeholder tests removed; architecture, Application, diagnostics, deployment and health behavior are covered

- [x] **M1-R04** Move Identity persistence and migrations out of Web
  - Severity: Medium
  - Reason: generated `ApplicationDbContext`, user and migrations currently violate the target layer ownership
  - Owner: M2-01 and M2-02
  - Verified: Identity user/context and migration assembly ownership are in Infrastructure; generated Web migrations were removed

- [x] **M1-R05** Replace no-op email behavior before enabling confirmed-account registration in production
  - Severity: Medium
  - Owner: M2-02
  - Verified: v1 explicitly does not require confirmed email; no no-op email sender or unsupported confirmation surface remains

- [x] **M1-R06** Remove English demo pages, Bootstrap navigation and sample content
  - Severity: Low for foundation; required before storefront delivery
  - Owner: M3-01 through M3-04
  - Verified: generated sidebar/navigation and demo routes are removed; active shell, fallback and reconnect customer copy is Thai

- [x] **M1-02** Configure dependency direction
  - Depends on: M1-01
  - Add only approved project references from `docs/ARCHITECTURE.md`
  - Add assembly marker classes and layer DI extensions
  - Add architecture tests for forbidden project dependencies
  - Acceptance: Domain has no project dependency; architecture tests pass
  - Verified: exact project-reference graph, isolated Domain, focused CI build, 9 architecture tests and 10 full-solution tests

- [x] **M1-03** Install baseline packages
  - Depends on: M1-01
  - EF Core, Npgsql, Identity, MediatR, FluentValidation, Serilog and health checks
  - Review MediatR license terms before selecting the production version
  - Remove generated SQLite provider/database and its vulnerable transitive dependency; replace with Npgsql in M2-01
  - Avoid optional infrastructure packages
  - Acceptance: package restore has no warnings requiring action
  - Verified: package contract tests pass; MediatR 12.5.0 license decision is documented; restore and vulnerability scan are clean

- [x] **M1-04** Create Application common primitives
  - Depends on: M1-02
  - `Result` and `Result<T>`
  - Error model and pagination model
  - `IApplicationDbContext`
  - Validation and logging pipeline behaviors
  - Transaction behavior for commands
  - Acceptance: unit tests cover success/failure result behavior
  - Verified: 28 focused tests cover result, pagination, logging, validation and transaction behavior

- [x] **M1-05** Configure application startup
  - Depends on: M1-02, M1-03
  - Layer registration through `AddApplication()` and `AddInfrastructure()`
  - Global exception handling with safe Thai customer messages
  - Serilog structured console/file logging
  - Correlation ID
  - Acceptance: application starts and unexpected errors do not expose stack traces
  - Verified: safe RFC 7807 and correlation tests pass; explicit trusted Caddy proxy forwarding is covered; CI build has 0 warnings

- [x] **M1-06** Add health endpoints
  - Depends on: M1-05, M2-01
  - `/health`, `/health/live`, `/health/ready`
  - Readiness checks PostgreSQL; liveness does not depend on external providers
  - Acceptance: documented curl commands return expected status
  - Verified: unavailable-database integration tests pass; live, ready and combined endpoints return 200 with local PostgreSQL

Exit criteria: clean clone can restore, build, test and start the application using the documented local workflow.

---

## M2 — PostgreSQL and Identity

เป้าหมาย: database และ authentication พร้อมสำหรับ feature จริง

- [x] **M2-01** Implement EF Core persistence foundation
  - Depends on: M1-02, M1-03
  - Create `ApplicationDbContext`
  - Implement `IApplicationDbContext`
  - Configure Npgsql and snake_case naming decision
  - Configure migration assembly
  - Acceptance: Web connects to PostgreSQL container through user secrets
  - Verified: Npgsql registration tests pass, Web connects to the local PostgreSQL container, and Infrastructure owns persistence

- [x] **M2-02** Configure ASP.NET Core Identity
  - Depends on: M2-01
  - Cookie authentication for Interactive Server
  - Roles: Customer, Admin
  - Policies: product, order, payment and user management require Admin
  - Admin with `MustChangePassword` cannot use management policies until changing the temporary password
  - Thai-first email/password registration and login; no confirmation link in v1
  - Persist Data Protection keys to configured directory
  - Acceptance: register, login, logout and protected-route tests pass
  - Verified: Thai register/login/change-password/logout, lockout, antiforgery, local redirects, Customer/Admin policies, forced password change and persistent-cookie restart behavior pass against PostgreSQL

- [x] **M2-03** Create initial Code First migration
  - Depends on: M2-02
  - Include Identity tables only plus confirmed foundation schema
  - Generate and review idempotent SQL
  - Apply pending migrations during Web startup before serving requests
  - Fail startup when database connection or migration fails; never use `EnsureCreated`
  - Acceptance: empty and existing databases migrate idempotently on startup; migration failure prevents the application from listening
  - Verified: reviewed idempotent SQL contains Identity v2 plus `MustChangePassword` only; empty/existing/conflicting/unreachable database tests pass and production source contains no `EnsureCreated`

- [x] **M2-04** Create safe role/admin seeding flow
  - Depends on: M2-02
  - Seed roles idempotently
  - Create first Admin only through `--bootstrap-admin`; read temporary credentials from configuration, never hard-code or pass the password as a command argument
  - Bootstrap migrates and seeds roles, creates at most one Admin, requires a password change and exits without listening
  - Acceptance: repeated startup does not duplicate roles or users
  - Verified: role seeding is idempotent; PostgreSQL advisory-lock concurrency test creates exactly one first Admin; existing-email and role-assignment rollback cases pass

- [x] **M2-05** Establish integration-test database strategy
  - Depends on: M2-01
  - Use a separate PostgreSQL database from development
  - Reset data safely between tests
  - Use a throwaway Testcontainers PostgreSQL database with a database-name safety guard; never reset the development `toystore` database
  - Upgrade deprecated xUnit v2 packages and runner to xUnit v3 as part of the test-harness change
  - Acceptance: destructive integration tests cannot target the development database
  - Verified: xUnit v3, PostgreSQL 17 Testcontainers, Respawn role reseeding, database-name guard and 41 integration tests pass; development database cannot be reset

Exit criteria: users authenticate, authorization policies work, and integration tests use PostgreSQL safely.

Verification: format clean; CI build 0 warnings/errors; Unit 110/110; Integration 41/41; NuGet vulnerability scan clean; Compose config valid; independent code review approved with no Critical/Important findings.

---

## M3 — Storefront design system

เป้าหมาย: Blazor UI มี shell และ reusable components ตาม `index.html` ก่อนเพิ่มหน้าธุรกิจจำนวนมาก

- [x] **M3-01** Self-host Noto Sans Thai
  - Add optimized `.woff2` files and font-face declarations
  - Define font preload only for required weights
  - Acceptance: storefront has no Google Fonts runtime dependency
  - Verified: pinned Thai/Latin variable WOFF2 subsets and OFL license are self-hosted; focused contract test, format verification and CI build pass

- [x] **M3-02** Create design tokens and responsive foundation
  - Depends on: M3-01
  - Colors, typography, spacing, radius, shadow, motion and breakpoints
  - Reduced-motion and focus-visible rules
  - Verified: focused storefront contracts 5/5, Unit tests 115/115, format verification and CI build pass with 0 warnings
  - Acceptance: tokens match `docs/DESIGN_SPEC.md`

- [x] **M3-03** Build StoreShell and StoreHeader
  - Depends on: M3-02
  - Brand, navigation, search/account/cart actions
  - Desktop/tablet/mobile navigation
  - No left navigation rail
  - Acceptance: keyboard navigation and responsive checks pass
  - Verified: SSR shell 5/5, storefront source contracts 8/8, account endpoints 9/9, Unit 118/118, format clean and CI build 0 warnings/errors

- [x] **M3-04** Build reusable storefront components
  - Depends on: M3-02
  - Hero, ProductCard, CollectionCard, SectionHeader, JournalFeature, TrustBenefits and Footer
  - Loading, empty, error and disabled states
  - Acceptance: component examples render without database dependencies
  - Verified: reusable database-independent display components and Thai SSR home composition; focused source/render tests 23/23, storefront/account tests 18/18, Unit 133/133, format clean and CI build 0 warnings/errors

- [x] **M3-05** Build reusable form and feedback components
  - Depends on: M3-02
  - Shared field wrapper, text input, number field, select/dropdown, validation message, alert, toast, dialog, confirmation and skeleton
  - Use FluentValidation in Application slices as the authoritative rules and map failures to Thai field errors/summary
  - Select/dropdown uses explicit cross-browser appearance, arrow, focus, disabled and error styling instead of browser-default CSS
  - Extract repeated behavior-heavy modal, drawer, pagination, table-state, badge, upload/reorder and autocomplete patterns as shared components
  - Acceptance: keyboard/focus behavior, responsive select styling and Thai FluentValidation copy verified
  - Verified: focused design/form/feedback tests 50/50; storefront/account integration 19/19; Unit 167/167; Integration 51/51; format clean and CI build 0 warnings/errors
  - Maintenance 2026-07-18: standardized semantic actions on shared `StoreButton`; operation-scoped busy groups show a spinner on the active action and disable sibling actions. Web compile succeeded with 0 warnings/errors; tests intentionally not run for this UI refinement.
  - Maintenance 2026-07-18: simplified shared validation visuals to a single thin field border and compact error summary panel; Web compile succeeded with 0 warnings/errors.
  - Maintenance 2026-07-18: replaced plain image-picker button presentation with responsive upload-card controls for single and multi-image fields; Web compile succeeded with 0 warnings/errors.
  - Maintenance 2026-07-18: replaced native select popups with the shared app-owned accessible listbox so menus use Thai application styling and anchor directly below their field; Web compile succeeded with 0 warnings/errors.
  - Maintenance 2026-07-18: moved hidden `InputFile` styling to the global form layer so only the clickable upload cards are visible.
  - Maintenance 2026-07-18: normalized shared single-line control height and field grid alignment, with clearer spacing around inline errors and validation summaries.
  - Maintenance 2026-07-18: removed the oversized visual focus ring from programmatically focused Admin page headings while preserving screen-reader focus behavior.
  - Maintenance 2026-07-18: standardized Admin and Storefront filter layouts with responsive auto-fit field grids, separated right-aligned action rows and full-width mobile actions.

Exit criteria: visual shell is reusable, accessible and consistent across target viewport sizes.

Verification: format clean; CI build 0 warnings/errors; Unit 167/167; Integration 51/51; NuGet vulnerability scan clean; Compose config valid; Chrome browser smoke 4/4 across mobile/tablet/desktop with no horizontal overflow, custom select appearance and native dialog Escape/focus-return behavior; independent M3-05 spec and quality reviews approved with no Critical/Important findings.

Commerce documentation alignment (before M4): initial focused contract RED failed 5/6 then GREEN passed 6/6; spec-review contracts failed 6/12 then passed 12/12; quality-review semantic contracts failed 5/16 then passed 16/16 with theme/dependency/payment-order/infrastructure/time-direction mutation proofs restored cleanly. Final format clean; CI build 0 warnings/errors; full Unit 183/183; stale active-instruction scan clean; independent spec and quality reviews APPROVED with no remaining Critical/Important findings.

---

## M4 — Catalog foundation, reference data, media and Admin catalog shell

เป้าหมาย: catalog schema/reference/media/Admin foundation พร้อมสำหรับ Product management โดยไม่มี variant หรือ Category CRUD

- [x] **M4-01** Model Product aggregate and conditional offers
  - Depends on: M1-04
  - Common Product fields, ordered ProductImage, `SaleType` (`InStock`, `PreOrder`) and lifecycle (`Draft`, `Published`, `Archived`)
  - Conditional In-stock/Pre-order offer invariants, UTC timestamps and Bangkok close-date conversion
  - Acceptance: domain tests cover forbidden cross-type fields, amounts, close/ETA, capacity, `MaxPerCustomer`, publish and archive rules
  - Verified: focused Product Domain 83/83; Unit 266/266; Integration 51/51; format clean; CI build 0 warnings/errors; vulnerability scan clean; Compose valid; no migration/schema change; independent spec and quality reviews APPROVED
  - Quality fixes: reorder snapshots adversarial collections once and fails atomically; generated slug rejects trailing newline/control characters; Pre-order Product creation rechecks `CloseAtUtc > createdAtUtc`

- [x] **M4-02** Model catalog reference entities
  - Depends on: M1-04
  - Seed required Category `ArtToy` and `Gundam`; no v1 Category management page
  - Brand, Universe seeds `Marvel`/`DC`/`Unknown`, Character unique within Universe and ProductCharacter relation
  - Acceptance: domain tests cover required relation, archive rules and scoped Character uniqueness
  - Implemented: deterministic Form-KC name normalization/shared absolute-end slug grammar; immutable ArtToy/Gundam and Marvel/DC/Unknown literal seeds; auditable Brand/Universe lifecycle and media eligibility; scoped Character identity; Product-owned Draft character links with atomic audit
  - Verified: focused Catalog + Product Domain 160/160; Unit 343/343; Integration 51/51; format clean; CI build 0 warnings/errors; vulnerability scan clean; Compose valid; no migration/schema change; independent spec and quality reviews APPROVED

- [x] **M4-03** Configure catalog persistence, uniqueness and slug allocation
  - Depends on: M4-01, M4-02, M2-01
  - EF mappings, ordered images/relations, normalized unique constraints, transaction-safe deterministic slug suffixes, indexes and seed data
  - Commit Code First migration/snapshot and review idempotent SQL for destructive changes
  - Acceptance: PostgreSQL persistence, duplicate/concurrency and startup-migration integration tests pass
  - Implemented: explicit EF mappings for seven catalog tables; exact shared seeds; normalized uniqueness/FKs/checks; lossless `numeric` Money; transaction-safe Product image reorder/remove persistence; deterministic Product/Brand/Universe slug allocation under scope advisory locks; startup Code First migration and test-only seed restoration
  - Migration: `20260716235231_AddCatalogFoundation` plus an explicit idempotent `InitialIdentity` → catalog delta SQL; 7 tables, 19 indexes, 16 named checks and 5 literal seed rows; destructive/Identity-DDL scan clean; SQL applied twice successfully to an Identity-only PostgreSQL database
  - Verified: focused Unit 189/189; focused PostgreSQL 43/43; full Unit 360/360; full Integration 89/89; format clean; CI build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; independent spec and quality/migration re-reviews APPROVED

- [x] **M4-04** Implement staged local media storage
  - Depends on: M1-04
  - `IFileStorage`/`LocalFileStorage`, configurable persistent root, safe public endpoint and backup procedure
  - JPEG/PNG/WebP signature+MIME validation, 5 MB/image, max 8 ordered/primary images, generated keys, traversal protection and staging/commit/orphan cleanup
  - Acceptance: storage tests cover invalid signature/size/path, ordering, move/delete, partial failure and cleanup
  - Implemented: provider-neutral immutable contracts; streamed JPEG/PNG/WebP signature+MIME validation; inclusive 5 MiB/eight-file defenses; CSPRNG 16-byte staged keys and retained ownership markers; non-overwriting batch commit; idempotent lifecycle/read/retention cleanup; canonical-root/symlink/traversal protection; crash-recoverable startup directory-rename probe; immutable anonymous GET/HEAD media endpoint with range/conditional support
  - Verified: focused media Unit 110/110; focused endpoint/startup Integration 13/13; full Unit 462/462; full Integration 102/102; format clean; CI build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; no worker/object storage/`EnsureCreated`/public static media path; storage temp roots clean; independent spec and security/code-quality re-reviews APPROVED with no Critical/Important/Minor findings

- [x] **M4-05** Build the Thai Admin shell
  - Depends on: M2-02, M3-05
  - Borderless Muted Ocean global rail, contextual top pills, collapsed tooltips/badges and mobile drawer
  - Shared Admin page header, list/modal, table state, filter, pagination and badge patterns; server-side Admin policy
  - Acceptance: route authorization, responsive keyboard/focus/reduced-motion and Thai navigation tests pass
  - Implemented: server-side `CanAccessAdmin` policy and auth-aware redirects; borderless Muted Ocean responsive rail/mobile drawer/context navigation; Thai placeholder routes; shared Admin header, state, table, filter, pagination, badge and modal primitives; focus-return and route-navigation coordination through the shared dialog module
  - Verified: full Unit 538/538; full Integration 104/104; format clean; build 0 warnings/errors; reproducible authenticated real-Chrome responsive/modal/focus/overflow/tooltip/reduced-motion smoke 30/30; vulnerability scan clean; Compose valid; EF has no pending model changes; no fake dashboard data, new package, migration, category route or forbidden layer coupling; independent specification/accessibility and authorization/code-quality re-reviews APPROVED with no findings

- [x] **M4-06** Implement Brand and Universe Admin slices/UI
  - Depends on: M4-03, M4-04, M4-05
  - List + create/edit modal + archive with required image/logo, authoritative FluentValidation and typed duplicate errors
  - Acceptance: happy/failure/duplicate/reference/authorization and Thai modal-state tests pass
  - Implemented: operation-scoped Brand/Universe list/create/update/archive slices; granular Admin policies; FluentValidation and typed duplicate/stale/system Results; advisory-lock slug allocation; versioned PostgreSQL concurrency; staged local media coordination with commit-outcome reconciliation and persisted cleanup ledger; Thai same-page list/editor/archive UI using reusable styled controls, URL filters, canonical pagination, neutral terminal states and deterministic dialog focus sequencing
  - Verified: full Unit 708/708; full Integration 137/137; retained real Chrome 48/48 across 390/768/900/1199/1200 px including Thai SSR, URL history, keyboard/focus, blob lifecycle, duplicate/stale/commit-unknown and mutation success; format clean; warnings-as-errors build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; migration SQL destructive scan clean; independent specification/accessibility and authorization/concurrency/media re-reviews APPROVED

- [x] **M4-07** Implement Character search and inline creation
  - Depends on: M4-03, M4-05
  - Searchable multi-select query and Universe-scoped inline create command; no Character management page
  - Acceptance: normalized search/create/duplicate/authorization and reusable autocomplete tests pass
  - Implemented: Active-Universe normalized Character search and inline create slices; fresh no-tracking readers and once-only mutation sessions with Universe row locks, exact duplicate classification and commit-outcome reconciliation; generic Thai controlled multi-select autocomplete with FluentValidation/EditContext, ARIA, IME, cancellation-safe keyboard/pointer behavior; thin authorized Admin adapter and fake-data design-system specimen
  - Verified: full Unit 785/785; full Integration 150/150; retained real Chrome 26/26 across keyboard/pointer/inline-create/busy/focus/IME and 390/768/1200 px; format clean; build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; no migration, Character route, Product UI, native dropdown or forbidden layer coupling; independent specification/accessibility and authorization/concurrency/persistence re-reviews APPROVED

Exit criteria: catalog schema/reference/media/Admin foundation is ready; Category has seeded values only and Product has no variant.

---

## M5 — Inventory, Product management, Storefront and In-stock cart

เป้าหมาย: Admin publish In-stock Product ที่ stock audit ได้และ anonymous customer เพิ่มลง drawer ได้

- [x] **M5-01** Model auditable Inventory
  - Depends on: M4-01
  - InventoryItem, immutable StockMovement and StockReservation with `AvailableQuantity = OnHandQuantity - ActiveReservedQuantity`
  - Non-negative/all-or-nothing invariants and optimistic/atomic concurrency behavior
  - Acceptance: unit tests cover zero, invalid adjustment, expiry and insufficient quantity boundaries
  - Implemented: separate auditable `InventoryItem` aggregate with authoritative fail-closed held quantity, immutable movement/reservation evidence, exact-expiry availability semantics, checked receive/adjust/reserve/release/expire/consume transitions, one aggregate version and typed invariant/concurrency/evidence failures
  - Verified: focused Inventory/architecture 23/23; full Unit 808/808; full Integration 150/150; format clean; build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; independent domain-invariant and persistence/concurrency-seam reviews APPROVED

- [x] **M5-02** Persist Inventory and implement Admin stock slices
  - Depends on: M5-01, M4-03, M2-01
  - ReceiveStock/AdjustStock with quantity, reason, reference, actor; movement history and availability queries
  - Product stock cannot be edited directly; Admin policies and typed errors
  - Acceptance: every durable quantity change has exactly one movement and competing PostgreSQL operations cannot oversell
  - Implemented: Code First `InventoryItems`/immutable `StockMovements`/controlled `StockReservations`; fresh once-only PostgreSQL `FOR UPDATE` mutation sessions and evidence-first commit reconciliation; idempotent authorized Receive/Adjust commands; fresh no-tracking authorized availability/history readers with physical-vs-effective reservation semantics and deterministic paging
  - Migration: `20260717075735_AddInventoryFoundation`; idempotent SQL reviewed with no destructive schema/data operation and applied twice after Identity+Catalog migration
  - Verified: focused Inventory Unit 29/29 and PostgreSQL 9/9; full Unit 851/851; full Integration 193/193; format clean; CI build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; independent plan/invariant, persistence/concurrency, authorization and code-quality reviews APPROVED

- [x] **M5-03** Implement In-stock Product management slices
  - Depends on: M4-01, M4-03, M4-04, M5-02
  - Create/update/publish/archive In-stock Product transaction including InitialStock movement, staged media commit and typed duplicate errors
  - Pre-order Admin fields/actions remain disabled until M6 capacity persistence is complete
  - Authoritative FluentValidation, Thai field/summary mapping and Admin policy
  - Media coordination commits new files before database save/commit and compensates new media with non-cancelled cleanup when save/commit fails; delete replaced media only after durable database commit
  - Post-commit deletion failure logs/records an orphan for PostgreSQL-aware age-graced reconciliation and returns the already-committed success; never signal false rollback/retry or claim filesystem/PostgreSQL atomicity
  - Acceptance: create/update/publish/archive rollback, duplicate, stock/media and unauthorized PostgreSQL tests pass
  - Acceptance: tests prove database failure compensation and post-commit delete failure does not invite a duplicate client retry
  - Implemented: versioned Draft -> Published -> Archived In-stock lifecycle, atomic Product + InitialStock creation, complete draft replacement, authoritative Publish readiness, terminal Archive, typed Thai FluentValidation/results, staged batch media coordination and explicit system-invariant propagation
  - Implemented: Product uploads create a 960px WebP thumbnail derivative; original/thumbnail references participate together in commit, rollback, verification and cleanup, while legacy rows fall back to the original image
  - Migration: `20260717095755_AddProductVersion`; additive Version default/check constraints reviewed and applied idempotently
  - Migration: `20260718152759_AddProductImageThumbnails`; additive nullable derivative references, paired-value check and filtered unique storage-key index reviewed
  - Verified: focused lifecycle Unit 103/103 and Product PostgreSQL 27/27; full Unit 935/935; full Integration 227/227; format clean; CI/Release build 0 warnings/errors; vulnerability scan clean; Compose valid; EF has no pending model changes; independent lifecycle, persistence/media, plan and authorization/code-quality reviews APPROVED

- [x] **M5-04** Build In-stock Product management UI
  - Depends on: M4-05, M4-07, M5-03
  - Product list + large create/edit modal (full-screen mobile), In-stock fields and draft/publish/archive actions
  - Shared image preview/drag reorder/primary indicator and Character autocomplete
  - Acceptance: Thai responsive/keyboard/focus/error flows pass without browser-default form controls
  - Implemented: Thai authorized Product list with canonical search/status/category/Brand/Universe filters and pagination, same-page create/edit modal, reusable fields, character autocomplete, retryable multi-image preview/reorder/primary controls, and publish/archive dialogs
  - Verified: full Unit 944/944; full Integration 234/234; focused UI/query and media tests passed; ProductManagementReader PostgreSQL projection/filter test passed; format clean; CI build 0 warnings/errors; independent UI/accessibility, architecture-boundary and plan reviews APPROVED

- [x] **M5-05** Implement published In-stock Storefront catalog queries/UI
  - Depends on: M5-03, M3-04
  - Home sections, all products, Brand route, search and Product detail expose Published In-stock Products only in this milestone
  - URL filters: SaleType, Category, Brand, Character, Universe and price range; server pagination and supporting indexes
  - Preserve completed monochrome/lime Storefront; responsive cards/gallery/swipe, Thai states, metadata and semantic/SEO content
  - Acceptance: Draft/Archived never leak; pricing/filter/page and mobile gallery/accessibility tests pass
  - Implemented: Published+In-stock hard-gated list/detail readers with reservation-aware availability, canonical URL filters and paging, Brand/search routes, live Home featured/catalog sections, Thai retry/error/not-found states, swipe/keyboard gallery and absolute SEO metadata
  - Migration: `20260717115849_AddStorefrontCatalogPriceIndex`; additive price index SQL reviewed and startup migration count updated
  - Verified: focused Unit/catalog/UI 25/25; PostgreSQL reader+SSR 12/12; full Unit 953/953; full Integration 236/236; format clean; CI build 0 warnings/errors; independent architecture, query/persistence, UI/accessibility and plan reviews APPROVED

- [x] **M5-06** Model anonymous In-stock cart and customer cart
  - Depends on: M4-01
  - Anonymous browser cart is untrusted; customer cart enforces ownership and quantity shape
  - Cart accepts only Published In-stock Product and promises neither final price nor stock
  - Acceptance: domain tests reject Pre-order, invalid quantities and cross-customer access
  - Implemented: browser-only anonymous cart item shape and customer-owned Cart aggregate with Published+In-stock gate, quantity 1–99, duplicate combine bounds, ownership/version/audit rules, and EF optimistic concurrency
  - Migration: `20260717121838_AddCustomerCartFoundation`; Carts/CartItems constraints, FKs and unique owner/product indexes reviewed with idempotent SQL
  - Verified: full Unit 960/960; full Integration 239/239; focused Cart Unit 7/7 and PostgreSQL 9/9; format clean; CI build 0 warnings/errors; EF no pending model changes; independent domain/persistence/plan reviews APPROVED

- [x] **M5-07** Implement cart and login-merge slices
  - Depends on: M5-06, M2-01
  - Get/add/change/remove/clear and deterministic anonymous-to-customer merge through `ISender`
  - Re-read Product state and current constraints; typed Thai failures and FluentValidation
  - Acceptance: integration tests cover merge, retries, ownership, unavailable/archived Product and quantity clamping/rejection
  - Implemented: authorized MediatR cart CRUD and deterministic anonymous-to-customer merge with actor-supplied ownership, product re-read, typed Thai validation, optimistic concurrency and durable CartOperation idempotency snapshots
  - Migration: `20260717123444_AddCartOperationIdempotency`, `20260717124452_AddCartOperationResultReplay`; legacy outcome backfill and replay constraints reviewed
  - Verified: full Unit 970/970; full Integration 246/246; focused cart/startup 16/16; format clean; CI build 0 warnings/errors; EF no pending model changes; independent authorization, persistence/idempotency and plan reviews APPROVED

- [x] **M5-08** Build the right-side cart drawer
  - Depends on: M3-05, M5-07
  - Header count, items, quantity/remove, display total, Continue shopping and Checkout; no forced navigation after add
  - Anonymous user may add; login is required only when Checkout begins
  - Acceptance: responsive drawer focus trap/return, keyboard controls, reduced motion and Thai loading/error/empty states pass
  - Implemented: right-side Thai drawer with anonymous browser cart and customer ISender cart, live count, add/quantity/remove, merge outcomes, login-only checkout gate, focus trap/return, keyboard/reduced-motion states and authoritative current price/availability preview
  - Verified: full Unit 980/980; full Integration 247/247; focused cart/SSR 17/17; format clean; CI build 0 warnings/errors; independent UI/security/accessibility and plan reviews APPROVED

Exit criteria: Admin publishes an auditable In-stock Product and anonymous customer can browse/filter it and add it to the right-side drawer.

---

## M6 — Pre-order

เป้าหมาย: ลูกค้าเริ่ม Pre-order โดยตรงจาก Product detail โดยไม่มี cart และระบบบังคับเวลา/capacity/limit อย่างถูกต้อง

- [x] **M6-01** Model Pre-order capacity, movement and reservation rules
  - Depends on: M4-01
  - Capacity movement/history and reserve/consume/release/cancel compensation rules
  - Cancellation after close date never silently reopens the original sale round
  - Acceptance: non-negative, expiry, duplicate consume/release/cancel and movement-history tests pass
  - Implemented: PreOrderCapacity aggregate with held/committed/retired accounting, immutable movement evidence, exact close/expiry boundaries, idempotent reserve/release/expire/consume/cancel transitions, and deposit disposition policy
  - Verified: focused PreOrderCapacity Unit 16/16; CI build 0 warnings/errors; independent invariant and plan reviews APPROVED

- [x] **M6-02** Implement concurrency-safe Pre-order capacity slices
  - Depends on: M6-01, M4-03
  - Persist capacity/movements/reservations and query effective remaining capacity
  - PostgreSQL concurrency protection and customer aggregate purchased/reserved quantity query
  - Acceptance: two customers competing for final capacity and one customer exceeding MaxPerCustomer are rejected without negative capacity
  - Implemented: customer-authorized reserve and lifecycle transition commands with server-owned 32-minute expiry, FOR UPDATE mutation session, MaxPerCustomer aggregate checks, typed ownership/idempotency errors and PostgreSQL conflict mapping
  - Verified: full Unit 998/998; full Integration 255/255; focused command/concurrency suite passed; format clean; CI build 0 warnings/errors; EF no pending model changes; independent persistence/concurrency and plan reviews APPROVED

- [x] **M6-03** Extend Product Admin for Pre-order
  - Depends on: M6-02, M5-04
  - Enable conditional Pre-order create/update/publish fields only after capacity persistence is available
  - Wire initial capacity movement, close/ETA, MaxPerCustomer, BalancePaymentDays and policy through authoritative FluentValidation
  - Acceptance: Admin UI/handler tests prove Pre-order publish creates durable capacity and rollback leaves no partial Product/capacity/media state
  - Implemented: conditional Thai Admin create/edit fields, shared Bangkok-aware close/ETA FluentValidation, atomic publish with initial capacity movement, defensive typed field errors and truthful Pre-order lifecycle guidance
  - Verified: CI build 0 warnings/errors; full Unit 1004/1004; focused PostgreSQL 15/15; direct media cleanup/preservation and Product/capacity/movement rollback tests; independent quality and plan reviews APPROVED

- [x] **M6-04** Enforce Pre-order close, ETA and customer-limit validation
  - Depends on: M6-03, M4-01, M2-02
  - Deposit/balance/full price, close at 23:59:59 `Asia/Bangkok`, ETA month/year, required `MaxPerCustomer`, `BalancePaymentDays` default 7 and effective closed state
  - Login-required server query validates Published state, requested quantity, remaining capacity and aggregate customer limit
  - Authoritative FluentValidation + typed Thai `PreOrderClosed`/capacity/customer-limit errors
  - Acceptance: amount/date/month/exact-close/capacity/limit/UTC conversion and retry tests pass
  - Implemented: Customer-authorized actor-free eligibility query, authoritative Product/capacity/allocation snapshot, exact UTC close boundary, remaining-capacity and aggregate customer-limit checks, typed Thai failures and fail-closed persistence coherence validation
  - Verified: format clean; CI build 0 warnings/errors; focused Unit 18/18; PostgreSQL 8/8; combined plan-review Unit 43/43; retry/no-write, UTC, amount/ETA and terminal-allocation evidence; independent quality and plan reviews APPROVED

- [-] **M6-05** Build Pre-order storefront and direct-checkout entry
  - Depends on: M5-05, M6-04, M3-05
  - Extend published catalog/detail queries and UI from In-stock to Pre-order only after M6 capacity persistence and Admin support are complete
  - Product detail → login → direct quantity/policy confirmation; never add Pre-order to cart
  - Show deposit, balance, full price, close date, ETA, remaining capacity, MaxPerCustomer and non-refundable policy
  - Acceptance: Thai responsive/keyboard states and disabled closed/over-limit actions pass while server remains authoritative
  - Implemented in progress: eligibility confirmation now continues to `/checkout/preorder/{productId}` with quantity and Thai address form instead of ending at Close

Exit criteria: Pre-order has no cart, eligibility is server-authoritative and capacity/customer limits remain correct under concurrency and compensation.

---

## M7 — Checkout, Stripe and Order creation

เป้าหมาย: In-stock และ Pre-order จ่ายผ่าน Stripe Embedded Checkout โดยมี durable CheckoutAttempt ก่อนจ่าย และสร้าง Order ครั้งเดียวหลัง verified payment

- [x] **M7-01** Add the immutable Thai address catalog
  - Depends on: M1-04
  - Pin reviewed `kongvut/thai-province-data` JSON locally and implement `IThaiAddressCatalog`
  - Validate Province → District → Sub-district → postal-code relations at startup, load frozen/immutable Singleton and fail startup on corruption
  - Acceptance: dataset integrity/startup failure tests and Application lookup-query tests pass without runtime network access

- [ ] **M7-02** Implement saved shipping addresses
  - Depends on: M7-01, M2-01, M2-02
  - Customer owns at most 5 addresses and one default; cascading selectors/autofill through `ISender`
  - Acceptance: ownership, limit/default, malformed relation and Thai shared-field UI tests pass

- [x] **M7-03** Model CheckoutAttempt and authoritative snapshots
  - Depends on: M5-01, M6-02, M7-02
  - Durable attempt/items, item/price/address/free-shipping/delivery-estimate snapshots, stock/capacity reservations, provider session and idempotency key
  - Payment window 30 minutes + 2-minute safety grace; before payment there is no Order
  - Acceptance: expiration, duplicate identity and immutable snapshot tests pass
  - Verified: shared multi-item CheckoutAttemptItem snapshots preserve In-stock and Pre-order item/price/relation/image/reservation evidence with 32-minute expiry and unique provider/idempotency identities

- [x] **M7-04** Model post-payment Order, OrderItem and Payment
  - Depends on: M7-03
  - Separate payment/fulfillment statuses and immutable Product/relation/image/offer/pre-order-policy/address/shipping snapshots
  - Payment purposes `Full`, `Deposit`, `Balance`, `Refund`; valid initial state differs by SaleType
  - Acceptance: totals, snapshots, valid/invalid initial states and duplicate-effect invariants pass unit tests
  - Verified: In-stock starts Paid + ReadyToShip with Full Payment; Pre-order remains DepositPaid + AwaitingPreOrderArrival, both using immutable OrderItems

- [x] **M7-05** Configure checkout, reservation, Order and Payment persistence
  - Depends on: M7-03, M7-04, M2-01
  - EF mappings, unique Stripe event/session/provider references, Order number, indexes and concurrency constraints
  - Commit migration/snapshot and review idempotent SQL
  - Acceptance: PostgreSQL constraints, rollback, concurrency and startup-migration tests pass
  - Verified: `GeneralizeCheckoutItems` preserves existing Pre-order snapshot rows before removing legacy single-item columns; startup migration and pending-model check pass

- [-] **M7-06** Integrate Stripe Embedded Checkout
  - Depends on: M7-05
  - `IPaymentGateway` boundary, Stripe Checkout Sessions, card/PromptPay configuration and safe provider metadata
  - JS module mounts/unmounts embedded payment; CSP, secret/publishable values and webhook secret are separated
  - Acceptance: Application has no Stripe SDK dependency; session request and component lifecycle tests pass
  - Implemented: separate In-stock full-payment and Pre-order deposit sessions, card/PromptPay metadata, embedded return attempt identity and signed webhook routing; external Stripe test-mode E2E remains

- [x] **M7-07** Begin In-stock checkout atomically
  - Depends on: M7-05, M5-07, M7-06
  - Require login; re-read cart Product publication/price/address/stock, compute free shipping and create all CheckoutAttempt reservations or none
  - Acceptance: rollback, retry and concurrent-last-item PostgreSQL tests prove no oversell and no pre-payment Order
  - Verified: deterministic `FOR UPDATE` inventory locking, all-or-none durable reservation, server price/cart re-read, concurrent last-stock one-winner test and no pre-payment Order

- [ ] **M7-08** Begin direct Pre-order deposit checkout atomically
  - Depends on: M7-05, M6-04, M7-06
  - Re-read Product/close/capacity/MaxPerCustomer/address, compute deposit total and reserve capacity without cart
  - Acceptance: exact-close, over-limit, retry and concurrent-final-capacity tests prove no oversell and no pre-payment Order

- [-] **M7-09** Verify Stripe fulfillment and create Order exactly once
  - Depends on: M7-07, M7-08
  - Verify signature/timestamp/merchant/session metadata/amount/currency/payment status; browser return cannot mark paid
  - Consume reservation, record Full/Deposit Payment and create Order atomically; duplicate/concurrent webhook/session fulfillment has one durable effect
  - Acceptance: invalid/mismatch/provider-failure, retry/concurrency and immutable-history PostgreSQL tests pass
  - Implemented: signed purpose/session/amount/currency/paid checks, atomic reservation consumption, Full/Deposit Payment and exactly-once Order; In-stock replay/mismatch PostgreSQL tests pass, broader provider failure matrix remains

- [-] **M7-10** Implement expiration and synchronous maintenance
  - Depends on: M7-06, M7-09
  - `checkout.session.expired`, related pre-checkout cleanup and authorized Admin inspect/retry/release
  - Retrieve Stripe session before uncertain release and fail closed when provider is unavailable; no worker/scheduler
  - Acceptance: maintenance is idempotent and never releases paid stock/capacity
  - Implemented: signed `checkout.session.expired` releases In-stock/Pre-order holds idempotently and never changes completed checkout; provider retrieval and authorized Admin maintenance remain

- [-] **M7-11** Build Thai address, summary, embedded-payment and result UI
  - Depends on: M7-02, M7-07, M7-08, M7-09
  - Saved/new cascading address, order/deposit summary, processing/failure/retry and limited server polling
  - Success modal shows Order number/items/price/free shipping or ETA and detail link; refresh never reopens completed payment
  - Acceptance: responsive/keyboard/focus/Thai states and server-authoritative totals pass
  - Implemented: authenticated `/checkout` and direct Pre-order address/summary/payment/processing/expiry/success states; immutable Thai catalog, saved address selectors and Order-detail link remain

Exit criteria: no Order exists before verified payment; concurrent/retried checkout cannot oversell or duplicate Payment/Order.

---

## M8 — Orders, fulfillment and notifications

เป้าหมาย: ลูกค้า/Admin จัดการ post-payment Order, balance, cancellation/refund, shipment และ notification ตาม state machine ที่ตรวจสอบย้อนหลังได้

- [x] **M8-01** Implement customer Order queries and detail UI
  - Depends on: M7-09, M4-01
  - History/detail checks ownership and shows Thai payment/fulfillment status plus immutable item/address/policy/shipping snapshots
  - Acceptance: cross-customer access fails and historical display survives catalog/settings changes
  - Implemented: authenticated history/detail routes, server-side pagination, account/checkout navigation, snapshot-only presentation and PostgreSQL ownership integration coverage

- [ ] **M8-02** Implement combined Admin Order management
  - Depends on: M8-01, M4-05
  - Combined list filters SaleType, payment/fulfillment status, date and order/customer/tracking search
  - Full-route detail shows snapshots, payment history, balance request, shipment, audit and notification history
  - Enforce actor/current-state rules for both status axes; Acceptance: authorization/filter/invalid-transition and Thai responsive tests pass

- [ ] **M8-03** Implement Admin In-stock cancellation and refund
  - Depends on: M8-02, M7-06
  - Admin may cancel/refund `Paid + ReadyToShip` with reason/audit/provider reference; Shipped cannot cancel and customer has no self-cancel
  - Stripe refund and inventory compensation are idempotent
  - Acceptance: provider retry/failure, rollback and invalid-state tests pass

- [ ] **M8-04** Implement Pre-order arrival and BalancePaymentRequest
  - Depends on: M8-02
  - Admin `MarkPreOrderArrived` creates one request; due = action time + snapshotted `BalancePaymentDays`
  - Default is 7 days and Product-specific value is snapshotted; arrival transitions to AwaitingBalancePayment
  - Acceptance: duplicate arrival, state, due calculation and audit tests pass

- [ ] **M8-05** Implement balance Stripe payment and overdue forfeiture
  - Depends on: M8-04, M7-06
  - Authenticated route validates ownership/state/deadline and creates a 30-minute Stripe session only when opened
  - Verified Stripe webhook records `Balance` Payment and moves Order to `Paid + ReadyToShip` exactly once
  - Overdue UI/query is immediate; synchronous Admin/customer-touch maintenance persists `DepositForfeited + Cancelled` idempotently
  - Acceptance: duplicate webhook/session, exact due race, ownership and overdue forfeiture tests pass

- [ ] **M8-06** Implement Pre-order cancellation and capacity compensation
  - Depends on: M8-02, M6-02
  - Customer cancellation forfeits deposit; Admin/supplier cancellation refunds; each records reason/actor/audit and capacity movement
  - Cancellation after close date does not reopen the original round; refund/compensation is idempotent
  - Acceptance: customer/Admin/overdue/provider retry and compensation rollback tests pass

- [ ] **M8-07** Implement shipment and tracking
  - Depends on: M8-02
  - Thailand Post, Flash, Kerry, J&T or Other; required tracking and carrier templates, validated HTTPS URL for Other
  - Confirmation summary then durable Shipment/audit and `Shipped` transition
  - Acceptance: carrier/format/transition/idempotency tests and customer tracking UI pass

- [ ] **M8-08** Persist NotificationDelivery idempotency foundation
  - Depends on: M7-09, M8-02
  - Persist type, recipient key, idempotency key, pending/status, attempts, safe provider response and timestamps before dispatching email/LINE
  - Provide provider-agnostic after-commit delivery orchestration; duplicate idempotency key cannot create or send a second delivery
  - Acceptance: PostgreSQL uniqueness, after-commit, provider-failure and duplicate-dispatch tests pass without rolling back Order/Payment

- [ ] **M8-09** Implement transactional email delivery
  - Depends on: M8-03, M8-04, M8-05, M8-06, M8-07, M8-08
  - `ITransactionalEmailSender`, local capture sender and templates: confirmations, balance request/confirmation, cancellation/refund/forfeiture and shipment
  - Production provider/from/credential config warning is a launch blocker
  - Acceptance: send occurs after commit, safe Thai template snapshots are correct and provider failure never rolls back Order/Payment

- [ ] **M8-10** Implement LINE Official Account notifications
  - Depends on: M7-09, M8-08
  - Messaging API (not retired LINE Notify), verified group recipient and post-payment message with safe order/sale type/amount/Admin link only
  - Secrets stay in configuration and no address/phone/payment secret leaves the server
  - Acceptance: verified payment emits once after commit and provider retry is safe

- [ ] **M8-11** Implement notification failure page and manual retry
  - Depends on: M8-09, M8-10
  - Admin failure page/actionable badge and manual retry without worker/scheduler
  - Acceptance: email/LINE failure, duplicate retry and eventual success tests pass without duplicate commerce effects

- [ ] **M8-12** Implement delivery and notification Settings
  - Depends on: M7-03, M4-05
  - Admin validates min/max business days; checkout snapshots the displayed range so existing Order/email history never changes
  - Surface email/LINE configuration readiness without exposing secrets
  - Acceptance: authorization/validation/concurrency, launch-warning and immutable-history tests pass

Exit criteria: Order lifecycle, balance, cancellation/refund, shipment and email/LINE delivery are authorized, idempotent, auditable and recoverable through Admin actions.

---

## M9 — Admin dashboard and sales reports

เป้าหมาย: Admin เห็นยอดขายและ operational queues ที่คำนวณจาก authoritative records ตามเวลาไทย

- [ ] **M9-01** Implement sales summary read models
  - Depends on: M7-09, M8-03, M8-05, M8-06
  - Net sales today/current month/current year from verified Payments minus successful refunds in `Asia/Bangkok`
  - Outstanding Pre-order balance is separate and never counted as sales
  - Acceptance: timezone/day-month-year/refund/deposit/balance boundaries pass PostgreSQL aggregation tests

- [ ] **M9-02** Implement revenue drill-down and trend queries
  - Depends on: M9-01
  - Gross received, refunds, net sales, In-stock full, Pre-order deposit/balance by selectable period
  - Database aggregation/indexes; do not load large Domain aggregate sets
  - Acceptance: query plans/data-shape/pagination and no-double-counting tests pass

- [ ] **M9-03** Implement operational queue queries
  - Depends on: M5-02, M8-11
  - ReadyToShip, AwaitingPreOrderArrival, AwaitingBalancePayment, overdue balance, low/out-of-stock and notification failures
  - Acceptance: actionable counts match filtered detail queries and respect effective expiry/overdue rules

- [ ] **M9-04** Implement product/order analytics
  - Depends on: M9-01
  - Recent verified-paid Orders, top Products, top Brands, order count and average order value
  - Acceptance: snapshot attribution, refund handling, tie/order and empty-period tests pass

- [ ] **M9-05** Build responsive Admin dashboard and reports UI
  - Depends on: M4-05, M9-02, M9-03, M9-04
  - Muted Ocean cards/charts/queues, overview/revenue/operations/product-performance top pills and Sales Reports drill-down
  - Thai `th-TH` formatting, loading/empty/error states, accessible chart alternatives and reduced motion
  - Acceptance: mobile/tablet/desktop visual, keyboard and data-consistency tests pass

Exit criteria: Admin can act on accurate operational queues and understand verified net sales without mixing outstanding Pre-order balance into revenue.

---

## M10 — Single-server production readiness

เป้าหมาย: deploy, operate, back up and restore the complete system on one Linux server

- [ ] **M10-01** Finalize production configuration
  - Depends on: M1-05, M2-02, M4-04, M7-06, M8-07, M8-09, M8-10, M8-12
  - Loopback bindings, environment/secrets, Stripe CSP/webhook, local uploads, Data Protection keys and provider launch warnings
  - Acceptance: no production secret is committed and missing required provider config blocks launch readiness

- [ ] **M10-02** Validate production Docker services
  - Depends on: M10-01
  - Use `deploy/compose.production.yaml`; verify non-root Web, startup migration, graceful shutdown, restart policies, persistent volumes and filesystem permissions
  - Acceptance: migration failure stops Web startup and Docker/VPS reboot restores a valid stack without manual intervention

- [ ] **M10-03** Validate Caddy configuration
  - Depends on: M10-02
  - HTTPS, Stripe-compatible CSP/security headers, request/upload limits and SignalR/WebSocket proxying
  - Acceptance: Interactive Server reconnect and embedded Stripe flows work through HTTPS

- [-] **M10-04** Create deployment and rollback runbook
  - Depends on: M10-01
  - Publish, review idempotent migration SQL, backup, copy, restart with startup migration, verify and forward-fix/rollback decision
  - Implemented: manual branch-input `workflow_dispatch`, production environment/concurrency, Release build/migration review gate (deployment workflow intentionallyไม่รัน tests), GHCR SHA-tag build, immutable digest deployment, pinned SSH, root-owned Compose command, quiesced PostgreSQL/media/key backup, readiness wait and previous-image rollback; actual clean Ubuntu VPS run remains pending
  - Verified locally: Release build 0 warnings/errors, unit 1,028/1,028, PostgreSQL integration 268/268, Dockerfile image build/non-root metadata, Compose/YAML/shell syntax and idempotent migration SQL generation pass
  - Acceptance: clean server deploy succeeds from documented commands

- [ ] **M10-05** Create and test backup/restore runbook
  - Depends on: M2-03, M4-04
  - PostgreSQL, media, pinned address data, Data Protection keys and configuration; keep another copy off server disk
  - Quiesce/stop writes while database dump and committed-media/key archive are captured as one restore set; staging is excluded
  - Acceptance: restore drill on clean environment preserves accounts, catalog/media and Order snapshots

- [ ] **M10-06** Complete security and privacy hardening
  - Depends on: M10-03
  - Firewall, least-privilege user, rate/upload/login limits, secure cookies, secrets/log/PII review and media traversal tests
  - Acceptance: only SSH/HTTP/HTTPS are public and logs contain no password/token/card/address secrets

- [ ] **M10-07** Run production smoke, load and provider-failure tests
  - Depends on: M10-04, M10-05, M10-06
  - Browse/login/cart/pre-order/address/Stripe/webhook/Order/balance/shipment/notification/dashboard/media and expected SignalR concurrency
  - Verify synchronous idempotent maintenance; do not add worker/scheduler, Redis or external object storage
  - Acceptance: no critical errors and resource use fits one-server target

Exit criteria: system survives reboot, migration, deployment, provider failure and restore on one server with verified health checks.

---

## M11 — Quality gate before launch

- [ ] **M11-01** Run complete unit, PostgreSQL integration and architecture suites
- [ ] **M11-02** Run end-to-end critical Storefront, customer and Admin flows for both SaleTypes; assert Pre-order never enters cart
- [ ] **M11-03** Re-run oversell, rollback, no-Order-before-payment, duplicate Stripe webhook, refund and notification idempotency tests
- [ ] **M11-04** Review authorization for every Admin action and ownership for address/cart/checkout/Order
- [ ] **M11-05** Review structured logs for secrets, payment data and personal address leakage
- [ ] **M11-06** Validate responsive UI, Thai copy, keyboard/focus, modal/drawer and reduced motion
- [ ] **M11-07** Validate startup migration failure, reviewed SQL and deploy rollback/forward-fix procedure
- [ ] **M11-08** Test PostgreSQL/media/key/config restore and one-server reboot recovery
- [ ] **M11-09** Verify Stripe/email/LINE production configuration, webhook secrets and manual retry operations
- [ ] **M11-10** Review prices, free shipping, Bangkok close/revenue boundaries, balance deadline/forfeiture, ETA/delivery settings and contact information
- [ ] **M11-11** Record known limitations, operational ownership and post-launch priorities

Launch criteria: no open critical defect; checkout/payment/Order/inventory invariants pass; providers and Admin queues are operational; backups restore; production smoke passes.

---

## Deferred ideas — not committed scope

Record ideas here without adding packages or infrastructure until they are promoted into an approved milestone:

- [ ] Public API/mobile client
- [ ] Advanced PostgreSQL full-text search
- [ ] Customer wish list
- [ ] Promotion/discount engine beyond basic rules
- [ ] Product reviews
- [ ] Advanced report exports beyond the approved Admin sales reports

The single-server constraint remains unless the architecture documents are explicitly changed.

## Progress update template

Copy this block under the active task when handing off unfinished work:

```text
Status:
Completed:
Remaining:
Blocked by:
Files changed:
Commands/tests run:
Important decisions:
Next recommended action:
```
