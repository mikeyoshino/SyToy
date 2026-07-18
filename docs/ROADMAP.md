# Delivery Roadmap

รายละเอียด business/product ใช้ [Commerce Platform Design](superpowers/specs/2026-07-17-commerce-platform-design.md) และ executable task ใช้ [`TASKS.md`](../TASKS.md) แผนนี้ส่งมอบเป็น vertical slices บน process/database/server เดียว ไม่มี background worker หรือ scheduler

## Phase 0 — Foundation and shared UI (M0–M3, completed)

- .NET 10 Blazor Interactive Server, Clean Architecture, MediatR, FluentValidation, PostgreSQL Code First/Identity/startup migration
- Thai-first Storefront shell, bold monochrome/lime tokens, Noto Sans Thai และ reusable display/form/feedback components
- Admin authentication/policies และ first-admin bootstrap ที่ปลอดภัย

Exit criteria: completed verification ใน `TASKS.md` คงเป็นประวัติอ้างอิง

## Phase 1 — Catalog and Admin foundation (M4)

- Borderless Muted Ocean Admin shell, global rail, contextual top pills และ shared Admin patterns
- Product ไม่มี variant; conditional `InStock`/`PreOrder` offer, `Draft`/`Published`/`Archived`
- Seed Category `ArtToy`/`Gundam` โดยไม่มี Category Admin CRUD
- Brand, Universe, inline Character autocomplete, generated collision-safe slug
- Local media storage/staging/commit/cleanup ผ่าน `IFileStorage` และ ordered media metadata primitives
- PostgreSQL catalog schema/migration พร้อม reviewed idempotent SQL

Exit criteria: catalog schema/reference/media/Admin foundation พร้อมสำหรับ Product management โดยยังไม่ claim ว่า Product create/publish flow ส่งมอบแล้ว

## Phase 2 — Inventory, storefront catalog and In-stock cart (M5)

- Auditable InventoryItem/StockMovement/StockReservation พร้อม concurrency
- In-stock-only Product management/publish flow หลัง inventory persistence พร้อม InitialStock movement และ staged media commit
- Product image preview/reorder/primary UI อยู่ใน In-stock Product management modal ไม่ใช่ M4 storage primitive
- Published In-stock-only search/detail/filter/pagination และ Product gallery บน Storefront M3 language เดิม
- Anonymous In-stock cart, login merge, ownership และ right-side cart drawer
- Re-read price/publication/availability จาก server; cart ไม่รับประกัน stock/price

Exit criteria: ลูกค้าค้นหา In-stock Product และเตรียม checkout ได้โดย stock ยังคง audit ได้

## Phase 3 — Pre-order (M6)

- Direct flow ไม่มี cart, Bangkok close at 23:59:59, ETA, capacity และ `MaxPerCustomer`
- Deposit/balance/full-price invariant และ `BalancePaymentDays` default 7
- Capacity reservation/concurrency/cancellation movement และ policy presentation/snapshot inputs
- เปิด Pre-order Product Admin create/update/publish และ Storefront extension หลัง capacity persistence ผ่านแล้วเท่านั้น

Exit criteria: closed/over-capacity/over-limit Pre-order ถูกปฏิเสธบน server และ concurrent attempts ไม่ oversell

## Phase 4 — Thai address, Checkout, Stripe and Order creation (M7)

- Version-pinned Thai address JSON, startup validation และ immutable `IThaiAddressCatalog` singleton
- Saved addresses สูงสุด 5, free shipping และ shipping-estimate snapshot
- Durable `CheckoutAttempt` + stock/capacity reservation; ก่อน verified payment ยังไม่มี Order
- Stripe Embedded Checkout/Checkout Sessions, verified idempotent webhook, expiry/retrieval/maintenance
- Create Payment + post-payment Order exactly once พร้อม immutable snapshots และ Thai success/processing UX

Exit criteria: browser ไม่ forge payment, retry/webhook ซ้ำไม่สร้างผลซ้ำ และ checkout แข่งขันไม่ oversell

## Phase 5 — Orders, balance, fulfillment and notifications (M8)

- Customer/Admin Order list/detail, payment/fulfillment state machines, ownership/policies/audit
- `BalancePaymentRequest`, due/overdue/deposit-forfeiture และ Stripe balance payment
- Admin cancellation/refund, Shipment/carrier/tracking และ shipping transition
- Persist durable/idempotent `NotificationDelivery` ก่อน provider dispatch จากนั้นจึงเชื่อม `ITransactionalEmailSender` และ LINE Official Account/Messaging API; manual retry อยู่หลัง provider flows
- Editable delivery estimate setting with immutable Order snapshot

Exit criteria: ทุก transition/provider retry idempotent; notification failure ไม่ rollback commerce commit

## Phase 6 — Admin dashboard and sales reports (M9)

- Net sales today/current month/current year จาก verified Payment ลบ successful refund
- Outstanding pre-order balance แยกจาก sales
- Revenue drill-down/trend, recent Orders, top Products/Brands, order count/AOV
- Operational queues: ReadyToShip, pre-order/balance/overdue, low stock และ notification failures

Exit criteria: Asia/Bangkok dashboard/report queries aggregate ใน PostgreSQL และแสดง Thai-first responsive Admin UI

## Phase 7 — Production readiness (M10)

- Linux single-server Caddy/Kestrel/PostgreSQL/local media, secrets, CSP/Stripe, persistent keys และ startup migrations
- Deployment/backup/restore/rollback runbooks, security hardening, provider launch blockers
- Production smoke/load/failure verification โดยไม่เพิ่ม Redis, worker, scheduler หรือ external object storage

## Phase 8 — Launch quality gate (M11)

- Full unit/integration/E2E, concurrency/idempotency/authorization/privacy review
- Responsive/keyboard/focus/reduced-motion และ Thai copy/policy review
- Migration failure, restore drill, operational checklist และ known limitations

## Later improvements within the same server

- Public API/mobile client
- Advanced PostgreSQL full-text search
- Wish list, promotion engine และ product reviews
