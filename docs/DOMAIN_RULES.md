# Commerce Domain Rules

เอกสารนี้สรุป invariant สำหรับ implementation โดยมี
[`commerce-platform-design.md`](superpowers/specs/2026-07-17-commerce-platform-design.md)
เป็น business/product source of truth

## 1. Catalog and Product lifecycle

- Product ไม่มี variant ใน v1 และมี offer เพียงชุดเดียวตาม `SaleType`: `InStock` หรือ `PreOrder`
- Common fields ต้องมี display/English name, generated slug, description, Category, Brand, Universe, optional Characters, ordered images และ audit timestamps/actor
- `ProductStatus` แยกจากประเภทการขาย: `Draft`, `Published`, `Archived`; out-of-stock และ pre-order-closed เป็น effective state ที่คำนวณ ไม่ใช่ lifecycle status
- `Draft` เห็นเฉพาะ Admin; `Published` ต้องผ่าน relation/image/offer requirements; `Archived` ซื้อไม่ได้และไม่ hard-delete history
- Category required และ seed เฉพาะ `ArtToy`, `Gundam`; v1 ไม่มีหน้า Category Admin และการเพิ่มค่าคือ reviewed schema/data change
- Brand และ Universe มี required display/English name, generated slug, image/logo และ archive state; Admin create ต้องมี image/logo หนึ่งรูป ส่วน Universe seed identity คงที่ `Marvel`, `DC`, `Unknown` เริ่มต้นโดยไม่มี logo และยังใช้กับ Published Product ไม่ได้จนกว่า Admin จะเพิ่ม logo
- Universe seed ที่ยังไม่มี logo ต้องเลือกรูปเมื่อ edit; หลังจากนั้น seed และ custom Universe ใช้กฎ edit/archive/audit เดียวกัน
- Character ผูก Universe หนึ่งรายการ, optional และเลือกหลายรายการได้; ชื่อถูก trim แล้ว normalize แบบ Form KC ด้วยกฎเดียวกับ catalog reference, ทั้งชื่อที่ persist และชื่อ normalized ต้องยาวไม่เกิน 200 ตัวอักษร และ unique ตาม normalized name ภายใน Universe
- ค้นหา/สร้าง Character ได้เฉพาะ Universe สถานะ Active ผ่าน searchable autocomplete และ inline create บน Product form โดยไม่มีหน้า Admin แยก; ผล exact match เป็นข้อมูล authoritative จาก Application/ฐานข้อมูล ไม่ให้ Web ตัดสิน equivalence เอง
- Slug มาจาก English name ตาม `^[a-z0-9]+(?:-[a-z0-9]+)*$`, unique case-insensitive และแก้ collision ด้วย deterministic suffix ภายใน transaction; เมื่อ persisted English name เปลี่ยนต้อง allocate slug ใหม่โดย exclude row ปัจจุบัน
- Display name/English name ซ้ำคืน typed business error; active และ archived row ใช้ namespace ชื่อ/slug เดียวกันและ database unique constraint เป็น final protection
- Brand/Universe archive เป็น terminal ไม่มี hard delete/unarchive และทำได้แม้ Product/Character ยังอ้างอิงอยู่ โดยต้องคง media, foreign keys และ history แต่ห้ามเลือก archived reference กับ Product ใหม่หรือใช้ publish
- Brand/Universe มี persisted `long Version`: create เริ่ม 1, update/archive ที่เปลี่ยน state สำเร็จเพิ่มหนึ่งครั้ง และ rejected/stale/no-op work ไม่เพิ่ม version
- Product มี ordered images สูงสุด 8 รูป รูปแรกเป็น primary; Published ต้องมีอย่างน้อยหนึ่งรูป
- Product มี persisted positive `Version` เริ่มที่ 1; การ Update/Publish/Archive ที่สำเร็จเพิ่มครั้งเดียวต่อ logical command ส่วน stale, rejected และ no-op ไม่เพิ่ม version. Lifecycle ที่อนุญาตคือ `Draft -> Published -> Archived`; M5-03 รองรับเฉพาะ In-stock และ Archived เป็น terminal
- Product model scale เป็นข้อมูล optional สำหรับสินค้าโมเดล (เช่น `1/12`, `1/6`, `1/100`); trim ก่อน persist, ค่าว่างเป็น `null`, ยาวไม่เกิน 30 ตัวอักษร และห้าม control characters
- การสร้าง In-stock Product ต้อง persist Product, ordered media references, characters, InventoryItem และ InitialStock movement ใน transaction เดียว; InitialStock แก้ได้เฉพาะตอนสร้าง
- Publish ต้องตรวจ Brand active พร้อม image, Universe active พร้อม logo และ relation/category/character จากฐานข้อมูลภายใต้ lock เดียวกัน; Archive ต้องคง media, stock และ movement history โดยไม่ลบข้อมูล

## 2. Conditional offer invariants

### In-stock

- `Price` มากกว่า 0 เป็น THB และ `InitialStock` ไม่ติดลบ
- ห้ามส่งหรือ persist field ของ Pre-order

### Pre-order

- `FullPrice > 0`, `DepositAmount > 0` และ `DepositAmount < FullPrice`
- `BalanceAmount = FullPrice - DepositAmount` เป็นค่าคำนวณ
- `CloseDate` ต้องอยู่ในอนาคต; วันที่ที่ Admin เลือกปิดที่ `23:59:59 Asia/Bangkok` แล้วแปลงเป็น UTC instant ก่อน persist
- `EstimatedArrivalMonth/Year` ต้องไม่ก่อนเดือนปิดรอบ
- `TotalCapacity > 0`, `MaxPerCustomer > 0` และ `MaxPerCustomer <= TotalCapacity`
- `BalancePaymentDays` ค่าเริ่มต้น 7 วันและปรับได้ต่อ Product
- Pre-order ไม่มี cart; เริ่ม direct checkout จาก Product detail และต้องแสดง deposit, balance, full price, close time, ETA, remaining capacity, MaxPerCustomer และนโยบายมัดจำไม่คืน

## 3. Inventory and capacity

```text
AvailableQuantity = OnHandQuantity - ActiveReservedQuantity
```

- On-hand, reserved, available และ pre-order capacity ห้ามติดลบหรือ reserve เกิน available
- `InventoryItem.HeldQuantity` เป็น authoritative physical hold ของ reservation สถานะ Active ทุกแถว รวมรายการที่เลยเวลาแต่ยังไม่ผ่าน provider reconciliation; `ReservableQuantity = OnHandQuantity - HeldQuantity` และใช้ค่านี้ตัดสิน reserve จริงแบบ fail closed
- customer-facing `AvailableQuantity(now) = OnHandQuantity - EffectiveActiveReservedQuantity(now)` โดย effective active ต้องเป็นสถานะ Active และ `now < ExpiresAtUtc`; ที่เวลาเท่ากับ expiry ให้ไม่แสดงเป็น reserved แล้ว แต่ checkout ใหม่ยัง reserve ไม่ได้จากส่วนต่างนั้นจน synchronous maintenance ตรวจ provider และ transition hold เดิมอย่างปลอดภัย
- หน้าดู availability ของ Admin ต้องอ่าน reservation snapshot ใหม่แบบ no-tracking และเปรียบผลรวม Active ทุกแถวกับ `HeldQuantity` ก่อนแสดงผล หากยอด, ownership, audit หรือ lifecycle ที่ persist ไม่สอดคล้องกัน ให้ fail closed เป็น system error; ห้ามแปลงเป็น business validation หรือรายงาน availability ที่คาดเดาเอง
- ใช้ PostgreSQL concurrency control ป้องกัน overselling; การ reserve หลาย In-stock item ต้องสำเร็จทั้งหมดหรือ rollback ทั้งหมด
- หลังสร้าง Product ห้ามแก้ stock โดยตรง; ใช้ `ReceiveStock`/`AdjustStock` พร้อม quantity, reason, reference, actor และ immutable `StockMovement`
- `ReceiveStock` และ `AdjustStock` ใช้ caller-stable `OperationId` เป็น idempotency key: retry ที่ intent เดิมและ aggregate evidence ตรงกันคืนผลเดิมโดยไม่เพิ่ม movement; intent ต่างกันหรือ evidence/ownership ผิดปกติห้าม apply ซ้ำและต้อง fail closed
- Inventory เริ่ม Version 1 และทุก counter/lifecycle mutation ที่เปลี่ยนจริงเพิ่ม Version หนึ่งครั้งพร้อม UTC audit watermark; same-terminal retry เป็น no-op ได้เฉพาะ terminal status และ immutable reference/movement evidence ตรงกัน
- Pre-order capacity reservation ถูก consume หลัง verified deposit payment
- Pre-order capacity ใช้สมการ `TotalCapacity = RemainingQuantity + HeldQuantity + CommittedQuantity + RetiredQuantity`; ทุกจำนวนห้ามติดลบ, `HeldQuantity` คือ checkout hold, `CommittedQuantity` คือยอดที่ verified deposit แล้ว และ `RetiredQuantity` คือยอดยกเลิกหลังปิดรอบซึ่งห้ามนำกลับมาขายรอบเดิม
- ทุก reserve/release/expire/consume/cancel สร้าง immutable capacity movement พร้อม resulting counters/version และ caller-stable movement/reference evidence; retry terminal intent เดิมเป็น no-op ส่วน evidence ต่างกัน fail closed
- Reservation เริ่มได้เฉพาะก่อน `CloseAtUtc` (เวลาเท่ากับ close ถือว่าปิด), expiry ต้องหลังเวลา reserve และ expire ได้เมื่อ `now >= ExpiresAtUtc`
- Verified deposit เปลี่ยน hold เป็น committed โดย remaining ไม่เปลี่ยน; cancellation ก่อน close คืน capacity แต่ cancellation ที่หรือหลัง close ย้าย committed ไป retired จึงไม่เปิดรอบเดิมกลับมาขายอัตโนมัติ
- Customer cancellation และ balance overdue ริบมัดจำ (`Forfeited`); Admin/supplier cancellation ต้องคืนมัดจำ (`RefundRequired`) โดย Order/Payment slice เป็นผู้ทำ provider refund จริงใน phase ถัดไป
- การ reserve Pre-order lock capacity row ก่อนอ่าน counter และรวม quantity ของ customer จาก reservation สถานะ `Active` (รวม hold ที่เลยเวลาแต่ยังไม่ reconcile เพื่อ fail closed) กับ `Consumed`; เมื่อยอดเดิม + requested quantity เกิน `MaxPerCustomer` ต้อง reject โดยไม่มี partial write

## 4. Cart, address and checkout

- Cart ใช้เฉพาะ In-stock; anonymous cart เก็บใน browser ได้แต่เป็น untrusted และบังคับ login เมื่อเริ่ม checkout
- Anonymous cart เก็บเฉพาะ `ProductId`/quantity และไม่ persist; customer Cart ผูก owner หนึ่งคนและ persist เฉพาะ `ProductId`/quantity โดยไม่ snapshot หรือรับประกันราคา publication หรือ stock
- Quantity ต่อ Product ใน cart ต้องอยู่ระหว่าง 1–99; duplicate Product รวม quantity ได้ไม่เกิน 99 และ server ต้องตรวจ Product/quantity ใหม่ตอน login merge และ checkout
- ตอน login ให้ merge cart แล้วตรวจ product publication, price และ quantity ฝั่ง server ใหม่; รายการ Published In-stock รวมกับจำนวนเดิมและ clamp ที่ 99 ส่วน Product ที่ไม่พร้อมใช้ถูกตัดออกพร้อมผลลัพธ์ภาษาไทยโดยไม่ทำให้รายการ valid rollback
- Cart mutation ทุกคำสั่งใช้ client-stable `OperationId` และ persisted intent fingerprint; retry intent เดิมห้ามเพิ่มผลซ้ำและต้อง replay resulting version/total รวมถึง rejected/clamped merge outcome เดิม แม้ Cart มี mutation ใหม่แล้ว ส่วนการใช้ ID เดิมกับ intent/owner อื่นต้องคืน conflict แบบ typed และ fail closed
- Thai address ใช้ version-pinned local dataset ผ่าน `IThaiAddressCatalog`; startup validate และโหลด immutable/frozen singleton โดย Razor เรียก Application query ผ่าน `ISender`
- Saved address สูงสุด 5 รายการ มี default ได้หนึ่งรายการ และตรวจ ownership ทุกครั้ง
- รุ่นแรกจัดส่งฟรีแต่ snapshot `ShippingAmount = 0`; ระยะจัดส่งเป็น setting ที่ Admin แก้ไขได้ โดยมีค่าเริ่มต้น 2–5 วันทำการ และต้อง snapshot recipient/address กับข้อความระยะจัดส่ง ณ ตอนจ่าย

ก่อนจ่ายยังไม่มี Order ให้สร้าง durable record ดังนี้ใน transaction:

ลำดับที่ห้ามสลับคือ `CheckoutAttempt -> verified Stripe/provider evidence -> Payment + Order exactly once`

```text
Validate authoritative Product/offer/price/address
-> Create CheckoutAttempt + stock/capacity reservation
-> Store item/price/address/shipping snapshot + provider session + idempotency key
-> Start Stripe Embedded Checkout
-> Receive verified Stripe/provider evidence
-> Consume reservation + record Payment + create Order exactly once
```

- `CheckoutAttempt` และ reservation มี payment window 30 นาที + safety grace 2 นาที
- release จาก `checkout.session.expired` หรือ synchronous maintenance ต้อง idempotent; ก่อน release ที่ไม่แน่ใจให้ retrieve Stripe session และ fail closed เมื่อ provider ติดต่อไม่ได้
- ไม่มี background worker/scheduler; checkout เรียก synchronous maintenance และ Admin มี authorized inspect/retry/release action
- Browser completion/return URL และการกดจาก Admin ไม่ใช่หลักฐาน payment

## 5. Order snapshots and states

Order สร้างหลัง verified payment เท่านั้นและเก็บ immutable snapshots อย่างน้อย ProductId, names, slug, `SaleType`, Category, Brand, Universe, primary image, unit/full price, deposit/balance, quantity, pre-order close/ETA/policy, address, shipping amount/estimate และ totals ห้ามเปิด Order เก่าแล้วคำนวณจาก Product/address/settings ปัจจุบัน

แยกสถานะสองแกน:

- `PaymentStatus`: `DepositPaid`, `Paid`, `PartiallyRefunded`, `Refunded`, `DepositForfeited`
- `FulfillmentStatus`: `AwaitingPreOrderArrival`, `AwaitingBalancePayment`, `ReadyToShip`, `Shipped`, `Cancelled`
- ทุก state tuple เขียนตามลำดับ `PaymentStatus + FulfillmentStatus` เสมอ

In-stock:

```text
Paid + ReadyToShip -> Paid + Shipped
Paid + ReadyToShip -> Refunded + Cancelled (Admin only)
```

Pre-order:

```text
DepositPaid + AwaitingPreOrderArrival
-> DepositPaid + AwaitingBalancePayment
-> Paid + ReadyToShip
-> Paid + Shipped
```

- Customer ไม่มี self-cancel สำหรับ In-stock หลังจ่าย; Admin cancel/refund ได้ก่อน Shipped พร้อม reason/audit/provider reference
- Pre-order customer cancellation หรือ balance overdue เป็น `DepositForfeited + Cancelled`
- Admin/supplier cancellation เป็น `Refunded + Cancelled`; Shipped ห้าม cancel
- ทุก transition ตรวจ current state/actor, idempotency และบันทึก actor, reason, timestamp

## 6. Balance payment, shipment and notifications

- `MarkPreOrderArrived` สร้าง `BalancePaymentRequest`, due date จาก action time + snapshot `BalancePaymentDays` และส่ง authenticated payment route
- เปิด route แล้วจึงสร้าง Stripe Session อายุ 30 นาที; retry ได้ถึง due date; overdue ห้ามสร้าง session และ synchronous cancellation persist forfeiture idempotently
- Verified balance webhook บันทึก Balance payment และเปลี่ยนเป็น `Paid + ReadyToShip`
- Shipping action ต้องมี carrier/tracking, validated HTTPS URL สำหรับ Other, confirmation และ durable Shipment/audit ก่อนส่ง email
- Notification ส่งหลัง durable commit; failure ห้าม rollback Order/Payment
- เก็บ `NotificationDelivery` พร้อม idempotency key, safe response, attempts และ timestamps; Admin retry ได้โดยไม่ส่งผลซ้ำ

## 7. Payment security and privacy

- Stripe webhook ตรวจ signature, timestamp, merchant/session metadata, amount, currency และ status
- Stripe event ID, Checkout Session ID และ provider payment reference ต้อง unique; webhook ซ้ำต้องให้ durable effect เดียว
- Payment แยก purpose `Full`, `Deposit`, `Balance`, `Refund`; refund เก็บ amount, reason, provider reference, actor และผลลัพธ์
- ห้ามเก็บ card number/CVV หรือ log password, token, full payment data, address และ personal data เกินจำเป็น
- ตรวจ ownership ของ cart, address, checkout และ Order บน server; Admin use case ใช้ policy authorization

## 8. Validation and failure handling

- FluentValidation validator ใน Application vertical slice เป็นกติกา input authoritative และคืน Thai field messages/summary
- Domain aggregate บังคับ invariant แม้ validation ผ่านแล้ว; database ป้องกัน uniqueness, referential integrity และ concurrency ขั้นสุดท้าย
- Expected failure คืน typed `BusinessError` ผ่าน `Result<T>` และแสดงเฉพาะข้อความไทย; ไม่ log business error เป็น system error
- System/provider failure ใช้ structured safe logging พร้อม correlation ID; UI/browser validation เป็น presentation hint เท่านั้น

## 9. Cross-cutting delivery constraints

- UI เป็น Thai-first; Application vertical-slice FluentValidation เป็น authoritative input validation และ map เป็นข้อความ/summary ภาษาไทย
- Persist instant เป็น UTC; format และแสดงผลด้วย `th-TH`/`Asia/Bangkok`; Product v1 ไม่มี variant
- ระบบ deploy บน Linux server เครื่องเดียวและไม่เพิ่ม Redis, background worker หรือ scheduler
