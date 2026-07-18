# Toy Store Commerce Platform Design

**Status:** Approved  
**Date:** 2026-07-17  
**Scope:** Identity, Catalog, Inventory, Cart, Pre-order, Checkout, Payments, Orders, Fulfillment, Notifications, Admin Dashboard และ Storefront UX  
**Architecture baseline:** .NET 10 Blazor Interactive Server, Clean Architecture, vertical slices, EF Core Code First, PostgreSQL, MediatR, FluentValidation

> **Approved UI amendment (2026-07-17):** ข้อความเดิมใน §18 และ §23 สลับ scope ของ theme ระหว่าง Storefront กับ Admin ระหว่างถอด requirement จากการ brainstorm เอกสารฉบับนี้แก้แล้ว: Storefront ที่ส่งมอบและ verify ใน M3 ยังคง bold monochrome/lime ตาม `index.html`; เฉพาะ Admin ใช้ borderless Muted Ocean blue ตามภาพอ้างอิงและ feedback ที่อนุมัติ การแก้นี้ไม่เปลี่ยนหลักฐาน completion ของ M3

## 1. Purpose and authority

เอกสารนี้เป็น master business and product specification สำหรับ Toy Store และเก็บ decision ที่อนุมัติร่วมกันระหว่าง brainstorming session เพื่อให้ agent ใน session ถัดไปไม่ต้องตีความ requirement ใหม่

เมื่อเอกสารนี้ขัดกับ backlog หรือ UI token เดิม ให้แก้เอกสารเฉพาะทางและ `TASKS.md` ให้สอดคล้องกับ design นี้ก่อน implementation โดยยังต้องรักษา architecture และ commerce invariants ใน `AGENTS.md`, `docs/ARCHITECTURE.md` และ `docs/DOMAIN_RULES.md`

ระบบยังคงเป็น modular monolith บน Linux server เครื่องเดียว ไม่เพิ่ม Redis, background worker, scheduler, microservice, event bus หรือ object storage ภายนอก

## 2. Delivery approach

ใช้ master blueprint นี้ร่วมกับ implementation plan แบบ vertical slice แยกแต่ละ milestone:

```text
Identity + initial migration
→ Shared design system + Admin shell
→ Catalog + product media
→ Inventory + In-stock cart
→ Pre-order
→ Checkout + Stripe
→ Orders + shipping + email/LINE
→ Dashboard analytics
```

ห้ามสร้าง implementation plan ขนาดใหญ่ที่รวมทุกส่วนในครั้งเดียว แต่ละ slice ต้องผ่าน TDD, focused verification, full relevant tests และ independent review ก่อนเริ่ม slice ถัดไป

## 3. Roles and Identity

### Language-first rule

- UI ทั้ง Storefront, Customer Account และ Admin ใช้ภาษาไทยเป็นค่าเริ่มต้น
- Validation, business error, empty/loading/error/success state, modal, email และ LINE notification ใช้ภาษาไทย
- ใช้ภาษาอังกฤษเฉพาะชื่อสินค้า, Brand, Character, Universe, carrier/provider name, code/reference และคำเทคนิคที่การแปลทำให้สับสน
- แยกข้อความ UI ออกจาก component เพื่อพร้อมรองรับ localization ภายหลัง แต่รุ่นแรกไม่ต้องมี language switcher
- วันที่ เวลา ตัวเลข ราคา และสถานะต้อง format ตาม `th-TH` และ `Asia/Bangkok` ขณะที่ persistence เก็บเวลาเป็น UTC

รุ่นแรกมีสอง role เท่านั้น:

- `Customer`
- `Admin`

ไม่สร้าง `Staff` จนกว่าจะมี requirement ใหม่

### Customer account

- สมัครด้วย email, password และ confirm password
- Email เป็น normalized unique identifier
- ยังไม่บังคับ email confirmation link
- Login มี `Remember me`
- ปุ่ม `Forgot password` แสดงได้แต่ disabled พร้อมข้อความว่ายังไม่เปิดใช้งาน
- ลูกค้าเก็บ shipping address ได้สูงสุด 5 รายการและตั้ง default address ได้หนึ่งรายการ
- ตรวจ ownership ของ address, cart และ order ฝั่ง server ทุกครั้ง

### Admin bootstrap

- ห้ามสมัคร Admin ผ่าน public registration
- ห้าม hard-code credential
- Admin คนแรกสร้างด้วย explicit bootstrap command บน server โดยอ่าน email/password จาก secret ชั่วคราว
- บังคับเปลี่ยน password หลัง login ครั้งแรก
- ทุก route และ use case ใต้ `/admin` ต้องตรวจ Admin policy ฝั่ง server; การซ่อนเมนูไม่ใช่ authorization boundary

## 4. Catalog model

### Product

Product ไม่มี variant ในรุ่นแรก หนึ่ง Product มีราคาและ stock/capacity ชุดเดียว

Common fields:

- `Id`
- `DisplayName` — required; ใช้ไทยหรืออังกฤษได้
- `EnglishName` — required; ใช้สร้าง URL
- `Slug` — generated; ไม่มีช่องแก้ slug โดยตรง
- `Description` — required
- `SaleType` — required: `InStock` หรือ `PreOrder`
- `ProductCategoryId` — required
- `BrandId` — required
- `UniverseId` — required
- Characters หลายรายการได้ แต่ optional
- Ordered images สูงสุด 8 รูป
- `ProductStatus` — `Draft`, `Published`, `Archived`
- Created/updated/published/archive timestamps และ actor audit

ไม่มี hard delete สำหรับ Product ที่เคยถูกอ้างอิง ให้ใช้ `Archived`

### Product categories

Seed ค่าเริ่มต้น:

- `ArtToy`
- `Gundam`

Category เป็น required แต่รุ่นแรกไม่มีหน้า Admin จัดการ Category การเพิ่มค่าใหม่ต้องเป็น explicit schema/data change ที่ผ่าน review

### Brand

- Display name — required
- English name — required
- Generated unique slug
- Brand image — required ตอน Admin create และก่อนใช้งานกับ Published product; edit เก็บรูปเดิมหรือ replace ได้แต่ไม่มี remove-only action
- Active/archived state
- สร้างและแก้ไขจาก modal บน Brand list page
- List แสดง Product reference count จริง
- มี persisted concurrency version เริ่ม 1 และ update/archive รับ expected version

### Universe

- Display name — required
- English name — required
- Generated unique slug
- Logo image — required ก่อนใช้งานกับ Published product
- Seed identity คงที่ `Marvel`, `DC`, `Unknown` เริ่มโดยไม่มี logo และแสดง `ต้องเพิ่มโลโก้`; edit seed ที่ยังไม่มี logo ต้องเลือกรูปก่อนบันทึก
- Admin เพิ่ม Universe ใหม่ได้จาก Universe list page
- Seed และ custom Universe ใช้ edit/archive/audit rules ชุดเดียวกัน
- List แสดง Product และ Character reference counts แยกกัน
- มี persisted concurrency version เริ่ม 1 และ update/archive รับ expected version

Brand/Universe archive เป็น terminal ไม่มี hard delete/unarchive และทำได้ทั้งเมื่อไม่มีหรือมี Product/Character อ้างอิง Existing media, foreign keys และ history ต้องคงอยู่ ส่วน archived reference ถูกตัดออกจาก future selection/publication และชื่อ/slug ยัง reserved

### Character

- Character ผูกกับ Universe หนึ่งรายการ
- Product เลือก Character ได้หลายรายการ
- Character เป็น optional
- ไม่มี Character management page ในรุ่นแรก
- Product form ใช้ searchable multi-select autocomplete
- เมื่อค้นไม่พบ Admin เพิ่ม Character inline ได้ และบันทึกลงฐานข้อมูลเพื่อค้นพบครั้งถัดไป
- Character name unique ภายใน Universe หลัง normalize

### Slug rules

- สร้างจาก English name ด้วยรูปแบบ `^[a-z0-9]+(?:-[a-z0-9]+)*$`
- Normalize case, punctuation และ whitespace ก่อนสร้าง
- Slug unique แบบ case-insensitive และมี database unique constraint
- Display name และ English name ซ้ำภายใน entity type คืน typed duplicate-name error
- หาก English name ต่างกันแต่ normalize เป็น slug เดียวกัน ให้เติม deterministic suffix `-2`, `-3`, ... ภายใน transaction-safe allocation
- เมื่อ persisted English name เปลี่ยน ต้อง allocate slug ใหม่ใน operation transaction โดย exclude current ID; whitespace-only/no-op edit ไม่เปลี่ยน slug/version
- Active และ archived row ใช้ namespace ชื่อ/slug เดียวกัน Archive ไม่คืนชื่อหรือ slug ให้ใช้ซ้ำ

### Product images

- สูงสุด 8 รูป
- รองรับ JPEG, PNG และ WebP เท่านั้น
- ไม่เกิน 5 MB ต่อรูป
- ตรวจ file signature และ MIME type; ไม่เชื่อ extension อย่างเดียว
- ห้าม SVG ใน product upload รุ่นแรก
- Admin เลือกหลายรูปและ preview ได้ก่อน upload
- Drag-and-drop เพื่อจัดลำดับ; รูปแรกเป็น primary image ของ list/card
- ยังไม่ upload ตอนเลือกไฟล์
- เมื่อกด Create จึง stream ไป temporary staging, validate, สร้าง Product/metadata และย้ายไป persistent media directory
- เมื่อ operation rollback แน่นอนต้อง cleanup staging/committed media ใหม่หลัง fresh reference guard; หาก commit acknowledgement ไม่แน่นอนต้องคงไฟล์ไว้และบันทึก cleanup/verification ledger จนกว่าจะพิสูจน์ได้อย่างปลอดภัย ห้ามลบเพียงเพราะ client เห็น failure
- Published product ต้องมีอย่างน้อยหนึ่งรูป; Draft อนุญาตให้ยังไม่มีรูป
- เก็บ storage key, relative URL, sort order, alt text และ primary flag ใน PostgreSQL; binary อยู่ใน local media directory นอก deployment

### Catalog media mutation safety

- File selection เป็น browser-local preview เท่านั้นและยังไม่ stage ก่อน submit
- Stage media ครั้งเดียวก่อน database transaction แล้ว commit media ก่อน database save/commit
- Definite rollback ชดเชย media ใหม่ด้วย non-cancelled cleanup; media เก่าลบหลัง verified durable commit เท่านั้น
- Commit acknowledgement failure ใช้ fresh action-specific entity/version/status verification และ fresh full-reference search ด้วย trusted storage key; confirmed/superseded commit เก็บไฟล์และ refresh result
- Verification unavailable/inconsistent ต้องคงไฟล์, บันทึก unresolved cleanup entry และแสดง `Persistence.CommitOutcomeUnknown` ให้ Admin refresh ก่อน retry
- Ledger รับเฉพาะ trusted staged/persisted key และ future reconciliation ต้อง recheck ทุก database media reference ก่อน delete ไม่มี background worker/scheduler

## 5. Product offers and validation

Product ต้องมี offer เพียงชนิดเดียวตาม `SaleType`

### In-stock offer

- `Price` มากกว่า 0 และใช้ THB
- `InitialStock` ไม่ติดลบ
- ห้ามส่ง Pre-order fields

### Pre-order offer

- `FullPrice` มากกว่า 0
- `DepositAmount` มากกว่า 0 และน้อยกว่า FullPrice
- `BalanceAmount = FullPrice - DepositAmount`; เป็นค่าคำนวณ ไม่ให้ Admin กรอกซ้ำ
- `CloseDate` ต้องอยู่ในอนาคต
- ปิดรับอัตโนมัติ `23:59:59` ตาม `Asia/Bangkok` ของวันที่เลือก และเก็บ instant เป็น UTC
- `EstimatedArrivalMonth` และ `EstimatedArrivalYear`; UI แสดงเป็นประมาณการ เช่น “ประมาณเดือนธันวาคม 2026”
- `TotalCapacity` มากกว่า 0
- `MaxPerCustomer` required ใน Product form และมากกว่า 0
- `MaxPerCustomer` ห้ามเกิน TotalCapacity
- `BalancePaymentDays` ปรับได้ต่อสินค้า ค่าเริ่มต้น 7 วัน
- Estimated arrival month/year ห้ามอยู่ก่อนเดือนปิดรอบ
- หน้า Product ต้องแสดงมัดจำ, ยอดคงเหลือ, ราคาเต็ม, วันปิดรอบ, ETA, capacity ที่เหลือ, MaxPerCustomer และเงื่อนไขมัดจำ

Pre-order ไม่มี cart และเข้า direct checkout จาก Product detail เท่านั้น

## 6. Product lifecycle

- `Draft`: เห็นเฉพาะ Admin และซื้อไม่ได้
- `Published`: ลูกค้าเห็นเมื่อ relation/image/price requirements ครบ
- `Archived`: ไม่แสดงและซื้อไม่ได้ แต่ข้อมูลเดิมยังใช้กับ order history

`OutOfStock` และ `PreOrderClosed` เป็น effective state ที่คำนวณจาก stock/capacity และเวลา ไม่ใช่ ProductStatus ที่ Admin เปลี่ยนเอง

## 7. Inventory and stock audit

### Core invariant

```text
AvailableQuantity = OnHandQuantity - ActiveReservedQuantity
```

- จำนวนทุกชนิดห้ามติดลบ
- ห้าม reserve เกิน available
- ใช้ database concurrency control ป้องกัน overselling
- การจองหลาย item ใน In-stock checkout ต้องสำเร็จทั้งหมดหรือ rollback ทั้งหมด

### Admin stock workflow

- Product create รับ `InitialStock`
- หลังสร้างแล้วห้ามแก้เลข stock โดยตรง
- ใช้ `ReceiveStock` หรือ `AdjustStock` command พร้อมจำนวน, reason, reference และ actor
- ทุกการเปลี่ยนแปลงสร้าง immutable `StockMovement`
- Dashboard แสดง low-stock/out-of-stock queue

### Pre-order capacity

- Pre-order ใช้ capacity reservation ก่อนจ่ายมัดจำ
- หลัง verified deposit payment reservation ถูก consume เป็นยอดจองจริง
- Customer/Admin cancellation บันทึก capacity movement
- Cancellation หลัง close date ไม่เปิดการขายรอบเดิมกลับมาเอง

## 8. Cart behavior

Cart ใช้เฉพาะ In-stock

- ผู้ใช้ยังไม่ login เพิ่มสินค้าใน cart ได้
- Anonymous cart เก็บ product IDs/quantities ใน browser และถือว่า untrusted
- Cart ไม่รับประกันราคา, publication หรือ stock
- เมื่อ login ให้ merge anonymous cart กับ customer cart และตรวจ quantity/product state ใหม่
- ก่อน checkout ต้อง re-read price, stock และ publication จาก server
- Add-to-cart เปิด right-side cart drawer โดยไม่ navigate ออกจากหน้าปัจจุบัน
- Drawer มี items, quantity controls, remove, current display total, Continue shopping และ Checkout
- Mobile drawer ต้อง focus trap, คืน focus เมื่อปิด และรองรับ keyboard/reduced motion
- บังคับ login ตอนเริ่ม In-stock checkout

## 9. Thai shipping addresses

ใช้ข้อมูลจาก `kongvut/thai-province-data` แบบ version-pinned local JSON ไม่เรียก GitHub ตอน runtime

### Runtime design

- Infrastructure implement `IThaiAddressCatalog`
- โหลดและ validate JSON ตอน startup
- เก็บเป็น immutable/frozen lookup และ register Singleton
- Startup ต้อง fail หาก dataset เสียหรือ relation ไม่ครบ
- Lookup Province → District → Sub-district และเติม postal code
- Razor ใช้ Application query ผ่าน `ISender`; ไม่เรียก Infrastructure singleton โดยตรง

### Snapshots

Order เก็บ recipient, address lines, province, district, sub-district และ postal code snapshot เสมอ เพื่อให้ order เก่าไม่เปลี่ยนตาม saved address หรือ dataset รุ่นใหม่

รุ่นแรกจัดส่งฟรี แต่ยังเก็บ `ShippingAmount = 0` ใน checkout/order snapshot เพื่อรองรับ pricing rule ภายหลัง

ระยะเวลาจัดส่งมาตรฐานเริ่มต้นคือ 2–5 วันทำการ Admin แก้ min/max ได้ใน Settings โดย Order ต้อง snapshot ข้อความ/ช่วงเวลาที่แสดงให้ลูกค้า ณ ตอนจ่าย เพื่อให้อีเมลและประวัติย้อนหลังไม่เปลี่ยนตาม setting ใหม่

## 10. Checkout, reservation and Order creation

### Important terminology

ก่อนจ่ายยังไม่มี Order แต่ต้องมี durable pre-payment records:

- `CheckoutAttempt`
- `StockReservation` หรือ pre-order capacity reservation
- authoritative item/price/address snapshot
- provider session reference และ idempotency key

Order จริงสร้างหลัง Stripe ยืนยัน payment เท่านั้น

### Reservation lifetime

- Customer payment window: 30 นาที
- Safety grace: 2 นาที
- Stripe Checkout Session และ local reservation share the same checkout identity
- `checkout.session.expired` releases reservation idempotently
- ก่อน checkout ใหม่ ให้ synchronous maintenance ตรวจ stale reservations ที่เกี่ยวข้อง
- เมื่อ webhook ขาดหาย ให้ retrieve Stripe session ก่อน release
- ถ้า Stripe ติดต่อไม่ได้ ให้ fail closed ชั่วคราวแทนการเสี่ยง oversell
- Admin มี authorized maintenance action เพื่อ inspect/retry/release stale records
- ไม่มี background worker หรือ scheduler

### In-stock flow

```text
Browse
→ Anonymous/customer cart
→ Login and merge
→ Validate current price/product/stock/address
→ Transactionally create CheckoutAttempt + reservations
→ Stripe Embedded Checkout
→ Verified webhook
→ Consume stock reservation + record payment + create Order once
→ Paid + ReadyToShip
```

### Pre-order flow

```text
Product detail
→ Login
→ Direct pre-order checkout
→ Validate close date/capacity/MaxPerCustomer
→ Create CheckoutAttempt + capacity reservation
→ Stripe Embedded deposit payment
→ Verified webhook
→ Create Pre-order Order once
→ DepositPaid + AwaitingPreOrderArrival
```

## 11. Stripe integration

ใช้ Stripe Embedded Checkout + Checkout Sessions API ภายในหน้าเว็บ

- รองรับ card และ PromptPay ที่เปิดใน Stripe Dashboard
- Browser ได้ publishable key/client secret ที่จำเป็นเท่านั้น
- Stripe secret key และ webhook secret อยู่ใน server secret
- ใช้ Stripe.js/embedded component ผ่าน JS module ที่ mount/unmount ตาม Blazor component lifecycle
- ตั้ง CSP สำหรับ Stripe domains เท่าที่จำเป็น
- Browser completion และ return URL ไม่ใช่หลักฐาน payment
- Webhook ต้อง verify signature, event timestamp, merchant/session metadata, amount, currency และ payment status
- Stripe event ID, Checkout Session ID และ provider payment reference มี unique constraints
- Handler ต้อง idempotent และปลอดภัยเมื่อ webhook ซ้ำหรือเข้าพร้อมกัน
- Payment เป็น record แยกตาม purpose: `Full`, `Deposit`, `Balance`, `Refund`
- ห้ามเก็บ card number หรือ CVV

### Checkout success UX

- เมื่อ embedded checkout complete ให้ปิด payment area
- Server retrieve/fulfill session แบบ idempotent ขณะ webhook ยังคงเป็นหลักฐานหลัก
- แสดง modal เลข Order, รายการ, ราคา, shipping/ETA และลิงก์ Order detail
- เปลี่ยน page state/URL เป็น success state เพื่อไม่ให้ refresh แล้วเปิด payment เดิม
- หากยังรอ webhook ให้แสดง processing state และ poll server อย่างจำกัด ไม่ mark paid ใน browser

## 12. Order, payment and fulfillment states

แยก `PaymentStatus` และ `FulfillmentStatus`

State tuple ทุกแห่งใช้ลำดับ `PaymentStatus + FulfillmentStatus` เสมอ

Order item ต้อง snapshot อย่างน้อย ProductId สำหรับ traceability, display name, English name, slug ณ ตอนซื้อ, SaleType, Category, Brand, Universe, primary image URL, unit/full price, deposit/balance allocation, quantity, Pre-order close/ETA/policy และ line total ห้ามเปิด Order เก่าแล้วคำนวณข้อมูลเหล่านี้ใหม่จาก Product ปัจจุบัน

### PaymentStatus

- `DepositPaid`
- `Paid`
- `PartiallyRefunded`
- `Refunded`
- `DepositForfeited`

### FulfillmentStatus

- `AwaitingPreOrderArrival`
- `AwaitingBalancePayment`
- `ReadyToShip`
- `Shipped`
- `Cancelled`

### In-stock transitions

```text
Paid + ReadyToShip
→ Paid + Shipped

Paid + ReadyToShip
→ Refunded + Cancelled     (Admin only)
```

- Customer ไม่มี self-cancel หลังจ่าย
- Admin cancel/refund ได้ก่อน Shipped พร้อม reason/audit/provider reference
- Shipped order ห้าม cancel

### Pre-order transitions

```text
DepositPaid + AwaitingPreOrderArrival
→ DepositPaid + AwaitingBalancePayment
→ Paid + ReadyToShip
→ Paid + Shipped
```

Cancellation rules:

- Customer cancel: `DepositForfeited + Cancelled`
- Balance overdue: `DepositForfeited + Cancelled`
- Admin/supplier cancel: `Refunded + Cancelled`
- ทุก transition บันทึก actor, reason และ timestamp
- เงื่อนไขมัดจำไม่คืนต้องแสดงก่อน payment และเก็บ policy snapshot ใน Order

## 13. Balance payment

เมื่อของมาถึง Admin เลือก action `MarkPreOrderArrived`:

- สร้าง `BalancePaymentRequest`
- Due date = action time + Product snapshot `BalancePaymentDays` (default 7)
- Order เปลี่ยนเป็น `AwaitingBalancePayment`
- ส่ง email พร้อม authenticated route `/account/orders/{number}/pay-balance`

ไม่สร้าง Stripe Session ค้าง 7 วัน เมื่อ customer login และเปิด route:

- ตรวจ ownership, due date และ current state
- สร้าง embedded Checkout Session อายุ 30 นาที
- Retry ได้จนถึง due date
- หลัง due date ห้ามสร้าง session ใหม่
- UI/query แสดง Overdue ทันที
- Idempotent synchronous cancellation command persist `DepositForfeited + Cancelled`; เรียกจาก Admin overdue queue หรือเมื่อมี action ต่อ order

Verified balance webhook records Balance payment and moves Order to `Paid + ReadyToShip`

## 14. Shipment

Admin order detail มี shipping action:

- เลือก carrier: Thailand Post, Flash, Kerry, J&T หรือ Other
- กรอก tracking number
- ระบบ validate required/format ตาม carrier เท่าที่กำหนดได้
- สร้าง tracking URL จาก carrier template; `Other` รับ explicit URL แบบ validated HTTPS
- แสดง confirmation summary ก่อน commit
- เมื่อยืนยัน สร้าง Shipment, set shipped timestamp, transition เป็น `Shipped` และบันทึก audit
- หลัง durable commit จึงส่ง shipping email

Shipping email มี order number, recipient name, carrier, tracking number และ tracking link โดยไม่ใส่ข้อมูล payment ที่ไม่จำเป็น

## 15. Transactional email and LINE notifications

### Email

Application ประกาศ `ITransactionalEmailSender`; template ไม่ผูก provider

Templates ขั้นต่ำ:

- Order confirmed
- Pre-order deposit confirmed
- Balance payment requested
- Balance payment confirmed
- Order cancelled/refunded/forfeited
- Shipment confirmed

Development ใช้ local capture sender Production อ่าน provider/from address/credential จาก configuration/secret หากยังไม่ configure ให้ Admin แสดง warning และถือเป็น launch blocker

### LINE

LINE Notify ถูกยกเลิกแล้ว จึงใช้ LINE Official Account + Messaging API

- ร้านต้องสร้าง Official Account และ Messaging API channel
- เก็บ channel access token เป็น production secret
- เพิ่ม Official Account เข้ากลุ่มร้านและ capture group ID จาก verified webhook
- แจ้งเมื่อ verified payment สำเร็จ พร้อม order number, sale type, amount received และ Admin order link
- ห้ามส่ง address, phone หรือ personal/payment secrets เข้า LINE

### Delivery semantics

- ส่ง notification หลัง durable database commit เท่านั้น
- Notification failure ห้าม rollback Order/Payment
- เก็บ `NotificationDelivery` type, recipient key, status, attempts, safe provider response และ timestamps
- Admin notification page แสดง failures และมี manual retry
- Retry ต้อง idempotent

## 16. Admin information architecture

Admin ใช้ global left rail และ contextual top pills โดยไม่ทำหน้าที่ซ้ำกัน

### Global rail

- Dashboard
- Catalog
- Inventory
- Orders
- Notifications
- Sales Reports
- Settings
- Logout

Rail ยุบเป็น icon mode และขยายเมื่อ pin/hover/focus โดยทุก icon มี Thai label, accessible name และ tooltip เมื่อ collapsed Badge ใช้เฉพาะ actionable counts เช่น ReadyToShip, low stock และ notification failure

บน mobile ใช้ menu button + drawer พร้อม focus trap และคืน focus

### Contextual top pills

ตัวอย่าง:

- Dashboard: Overview, Revenue, Operations, Product performance
- Catalog: Products, Brands, Universes
- Orders: All, In-stock, Pre-order, ReadyToShip, Shipped, Cancelled

### Product management

- Product list มี search และ filter sale type, category, brand, universe และ status
- Create/Edit อยู่บนหน้า list ด้วย large modal; mobile เป็น full-screen dialog
- Conditional fields เปลี่ยนตาม SaleType
- Image drop zone, previews, drag ordering และ primary indicator
- Draft, publish, archive actions
- Stock receive/adjust ใช้ modal แยกพร้อม reason

Brand และ Universe ใช้ Thai-first list + create/edit modal Pattern เดียวกันบน route เดิม Character สร้าง inline ใน Product form List state อยู่ใน URL ด้วย `q`, `status=active|archived|all`, `page`; default Active/page 1 ถูก omit, page size 20 และ response ที่เก่ากว่าต้องไม่ทับ navigation state ใหม่

Brand list แสดงรูป, names, read-only non-link slug, lifecycle/readiness, Product count จริงและ Bangkok update time Universe แสดงข้อมูลเดียวกันพร้อม Product/Character counts แยกและ seed readiness `ต้องเพิ่มโลโก้` Archive confirmation ต้องอธิบายว่า action เป็น terminal แต่ media/references เดิมยังคงอยู่ ทุก query/command บังคับ `CanManageProducts` ฝั่ง server

### Order management

- Orders page แสดงรวมและ filter In-stock/Pre-order
- Filter payment status, fulfillment status, date range และ searchable order/customer/tracking
- Order detail ใช้ full route ไม่ใช้ modal เพราะต้องแสดง snapshots, payment history, balance request, shipment, audit และ notification history

## 17. Admin Dashboard and analytics

Dashboard ใช้ timezone `Asia/Bangkok`

Primary cards:

- Net sales today
- Net sales current month
- Net sales current year
- Outstanding Pre-order balance — แยกและไม่รวมเป็น sales

Drill-down แยก:

- Gross received
- Refunds
- Net sales
- In-stock full payments
- Pre-order deposits
- Pre-order balance payments

Operational queues:

- ReadyToShip
- AwaitingPreOrderArrival
- AwaitingBalancePayment
- Overdue balance
- Low/out-of-stock products
- Notification delivery failures

Additional widgets:

- Revenue trend by selectable period
- Recent verified-paid Orders
- Top products
- Top brands
- Order counts และ average order value

Analytics ใช้ optimized read models/queries และ database aggregation ไม่โหลด Domain aggregate จำนวนมาก ยอดขายนับจาก verified Payment records และหัก successful refunds

## 18. Customer storefront UX

### Visual direction

Storefront คง visual language ที่สร้างและ verify เสร็จใน M3: bold monochrome พื้นสว่าง โทนดำ/เทา และ lime accent ตาม `index.html`:

```text
Accent:       #DFFF29
Background:   #F8F8F6
Surface:      #FFFFFF
Ink:          #111111
```

- ใช้ contrast สูง, whitespace, restrained border/shadow และ lime เฉพาะ primary CTA/badge/จุดเน้นสั้น
- Product card ใช้ responsive grid และ motion language ที่ M3 verify แล้ว
- รองรับ reduced motion
- Noto Sans Thai ยังเป็น primary font

Admin ใช้ borderless `Muted Ocean` blue theme แยก:

```text
Admin accent:      #3F91B8
Admin accent soft: #EAF6FB
Admin background:  #F5F8FA
Admin surface:     #FFFFFF
```

- ไม่ใช้ harsh/heavy borders รอบ card/button/layout; ใช้ whitespace, surface contrast และ soft shadow
- transition 160–260ms สำหรับ rail, pills, hover, modal และ drawer
- Global rail/contextual pills ใช้ Muted Ocean; semantic status ยังคงมีข้อความ/icon ไม่สื่อด้วยสีอย่างเดียว

### Catalog list

- Routes รองรับ all products, brand list และ search
- Product card แสดง primary image, name, brand/category, SaleType และ pricing ตามประเภท
- In-stock แสดง full price
- Pre-order แสดง deposit และ full price
- Filters อยู่ใน URL และประกอบด้วย SaleType, ProductCategory, Brand, Character, Universe และ price range
- Server-side pagination
- Query แสดง Published products เท่านั้น

### Product detail

- Display name, Brand, Category, Universe, Characters, description และ SaleType
- Multiple-image gallery; desktop thumbnails และ mobile swipe
- Alt text ที่มีความหมาย
- In-stock แสดง price, availability และ Add to cart
- Pre-order แสดง close date, ETA month/year, deposit, balance, full price, remaining capacity, MaxPerCustomer และ non-refundable policy
- เมื่อ close time ผ่านไป button disabled และ server ปฏิเสธ reservation

### Checkout

- In-stock: cart → login → address → order summary → Stripe embedded
- Pre-order: product detail → login → direct address/quantity/policy → Stripe embedded deposit
- Thai address cascading selector ใช้ local singleton catalog
- Saved address selection และ checkbox save new address
- Shipping ฟรีและ snapshot `ShippingAmount = 0`
- Server คำนวณทุก total
- Loading, processing, success, failure และ retry states เป็นภาษาไทย

### Customer order account

- Order history/detail ตรวจ ownership
- แสดง PaymentStatus และ FulfillmentStatus เป็น Thai labels
- แสดง item/address/pre-order policy snapshots
- Pre-order balance route แสดง deadline และ embedded payment เมื่อยัง valid
- Shipment แสดง carrier, tracking number และ link

## 19. Error handling and validation

FluentValidation validator ที่อยู่ข้าง command/query ใน Application vertical slice เป็นแหล่ง input-validation หลักเพียงชุดเดียว Web map failure กลับไปยัง reusable field components และ validation summary เป็นภาษาไทย ส่วน UI/browser validation เป็นเพียง presentation hint และต้องไม่ขัดกับ validator

Expected failures ใช้ typed `Error` ผ่าน `Result<T>` ไม่ throw exception เป็น control flow

Examples:

- `DuplicateProductNameError`
- `Brand.DuplicateDisplayName` / `Brand.DuplicateEnglishName`
- `Universe.DuplicateDisplayName` / `Universe.DuplicateEnglishName`
- `Brand.StaleVersion` / `Universe.StaleVersion`
- typed archived, not-found และ missing-media errors แยก Brand/Universe
- `PreOrderClosedError`
- `PreOrderCapacityExceededError`
- `CustomerPreOrderLimitExceededError`
- `InsufficientStockError`
- `BalancePaymentExpiredError`
- `InvalidOrderTransitionError`
- `TrackingNumberRequiredError`

Business errors:

- แสดงเฉพาะข้อความไทยที่เหมาะกับ UI
- ไม่เขียน error log
- Field-level validation ผูกกับ input และมี validation summary
- Text, number และ select/dropdown ใช้ shared form components; dropdown กำหนด cross-browser appearance, icon, focus, disabled และ error styles เอง ไม่ใช้หน้าตา default ของ browser

System/provider failures:

- Log structured error พร้อม correlation ID และ safe identifiers
- ไม่ log request bodies, password, token, card data หรือ personal address
- Global exception handler คืน safe Thai RFC 7807 response และไม่เผย stack trace
- Razor ไม่กระจาย `try/catch`; ใช้ Result mapping และ ErrorBoundary เฉพาะ rendering failure
- `Persistence.CommitOutcomeUnknown` เป็น safe system failure เมื่อ fresh verification ยังยืนยัน commit/rollback ไม่ได้ UI ต้องรักษา input/preview และให้ Admin refresh ก่อน retry

Authorization ของ request ต้องทำก่อน validation/database/storage เพื่อไม่เปิดเผย validation detail หรือสร้าง side effect ให้ผู้ไม่มีสิทธิ์

## 20. Uniqueness and database protection

Application validation ช่วย UX แต่ database เป็น final protection

Unique constraints อย่างน้อย:

- Normalized customer email
- Product normalized display name
- Product normalized English name
- Product slug
- Brand normalized name/English name/slug
- Universe normalized name/English name/slug
- Character normalized name + UniverseId
- Order number
- Stripe event ID
- Checkout Session ID
- Provider payment reference
- Notification idempotency key

Brand/Universe ใช้ persisted `long Version`, expected-version check, row/mutation locks และ exact PostgreSQL constraint mapping Aggregate อื่นที่แก้พร้อมกันได้ เช่น InventoryItem, Pre-order capacity, CheckoutAttempt และ Order transition ต้องมี optimistic concurrency token หรือ atomic SQL behavior ที่ทดสอบจริงกับ PostgreSQL

## 21. Testing strategy and acceptance gates

### Unit tests

- Product conditional invariants
- Money/deposit/balance calculations
- Slug generation and collision behavior
- State transitions and cancellation/refund/forfeiture
- Reservation expiry
- Available stock/capacity
- Address validation mapping
- FluentValidation rules and typed error mapping

### PostgreSQL integration tests

- EF mappings and unique constraints
- Identity persistence and policies
- Transaction rollback
- Two customers compete for final item/capacity
- Multiple-item all-or-nothing reservation
- Duplicate/concurrent Stripe webhooks
- Payment amount/currency mismatch
- Cancellation compensation and refund idempotency
- Balance payment due boundary
- Startup migration behavior and migration SQL
- Brand/Universe normalized create race, replacement update/update และ update/archive races
- Brand/Universe archive with zero/many Product/Character references
- commit acknowledgement verification, superseded refresh และ cleanup-ledger idempotency/recheck

### Storage/provider tests

- File signature, oversize, traversal and staging cleanup
- definite rollback compensation, old-media post-commit cleanup failure และ unresolved trusted-key ledger recording
- Stripe signature and session retrieval
- Email/LINE failure does not rollback order
- Notification retry idempotency
- Thai address dataset validation at startup

### Authorization and ownership

- Customer cannot enter Admin route/use case
- Customer cannot read another customer’s order/address/cart
- Admin-only inventory/order/refund/shipment actions enforce policy server-side

### End-to-end

- Register/login/remember-me
- Product/Brand/Universe modal flows
- Image preview/reorder/create
- Anonymous cart drawer → login merge
- In-stock embedded checkout and success modal
- Pre-order direct deposit checkout
- Balance payment within deadline and rejection after deadline
- Admin arrival, shipping/tracking, refund and notification retry
- Responsive, keyboard, focus, modal/drawer and reduced-motion behavior

## 22. External references and pinned decisions

- Thai address dataset: [kongvut/thai-province-data](https://github.com/kongvut/thai-province-data), MIT; pin a reviewed local version
- Stripe Checkout Sessions: [Checkout Sessions API](https://docs.stripe.com/payments/checkout-sessions)
- Stripe embedded integration comparison: [Checkout Sessions vs Payment Intents](https://docs.stripe.com/payments/checkout-sessions-and-payment-intents-comparison)
- Stripe limited inventory: [Manage limited inventory](https://docs.stripe.com/payments/checkout/managing-limited-inventory)
- Stripe fulfillment/webhooks: [Fulfill orders](https://docs.stripe.com/checkout/fulfillment)
- Stripe PromptPay: [PromptPay payments](https://docs.stripe.com/payments/promptpay)
- LINE Notify ended 2025-03-31: [LINE Developers announcement](https://developers.line.biz/en/news/2025/04/01/line-notify/)
- LINE replacement: [Messaging API push messages](https://developers.line.biz/en/reference/messaging-api/#send-push-message)

## 23. Documentation alignment record

Source-of-truth alignment completed 2026-07-17 before M4 implementation planning:

- `AGENTS.md` and the project Skill route catalog/inventory/checkout/Order/notification/dashboard/Admin work to this specification
- `docs/DESIGN_SPEC.md` preserves the completed monochrome/lime Storefront and defines borderless Muted Ocean Admin navigation/patterns
- `docs/DOMAIN_RULES.md` uses the approved no-variant `SaleType`, Pre-order deposit/balance and Order-after-verified-payment model
- `docs/ARCHITECTURE.md` defines CheckoutAttempt, BalancePaymentRequest, NotificationDelivery and immutable Thai address catalog boundaries
- `TASKS.md` preserves M0–M3 history and rebaselines M4–M11 into executable catalog/Admin, inventory/cart, Pre-order, Stripe/checkout, Order/notification, dashboard, production and launch slices

This alignment does not mark any M4+ implementation task complete.

M4-06 alignment amendment:

- Brand/Universe use fresh operation contexts, persisted versions and PostgreSQL-tested mutation locks
- English-name edits reallocate slug while archived names/slugs remain reserved
- Universe seeds begin without logos and otherwise follow custom edit/archive rules
- Archive preserves media and Product/Character foreign keys
- commit ambiguity uses fresh evidence/reference verification plus an idempotent trusted-key cleanup ledger without a worker
- Brand/Universe Admin routes are Thai-first Muted Ocean same-page list/create/edit/archive experiences
