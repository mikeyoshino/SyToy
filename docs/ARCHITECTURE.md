# Architecture Specification

## 1. System context

Toy Store คือเว็บขาย collectible toy ภาษาไทย ครอบคลุม Art Toy และ Gundam สำหรับผู้ใช้งานเริ่มต้นประมาณ 100 คนต่อวัน พัฒนาและดูแลโดยคนเดียว ระบบต้อง deploy ง่ายบน Linux server เครื่องเดียว แต่แยก business rule และ use case ชัดเจนพอที่จะรองรับ API หรือ mobile client ในอนาคตได้โดยยังคงเป็น application เดียว

Business/product contract สำหรับ catalog, inventory, cart, pre-order, checkout, Order, payment, fulfillment, notification, dashboard และ Admin อยู่ที่ [Commerce Platform Design](superpowers/specs/2026-07-17-commerce-platform-design.md)

## 2. Architecture baseline

```text
Blazor Web App (.NET 10)
+ Global Interactive Server
+ Modular Monolith
+ Clean Architecture
+ Vertical Slice
+ MediatR / FluentValidation
+ PostgreSQL / EF Core / Npgsql
```

ใช้ application instance, database, media storage และ reverse proxy บน server เครื่องเดียว ไม่ใช้ microservices, Redis, object storage ภายนอก, background worker หรือ scheduler

## 3. Solution structure

```text
ToyStore.sln
├── src/
│   ├── ToyStore.Web/
│   ├── ToyStore.Application/
│   ├── ToyStore.Domain/
│   └── ToyStore.Infrastructure/
└── tests/
    ├── ToyStore.UnitTests/
    └── ToyStore.IntegrationTests/
```

### ToyStore.Web

รับผิดชอบ Razor components, routing, layouts, authentication UI, authorization display, endpoints, middleware, error boundaries, configuration และ dependency composition

Component ทำหน้าที่รับ input, แสดงผล, จัดการ UI state และส่ง request ผ่าน `ISender` เท่านั้น ห้ามวาง business rule สำคัญใน `.razor`

```text
Components/
├── Layout/
├── Shared/
└── Pages/
    ├── Home/
    ├── Products/
    ├── Catalog/
    ├── Cart/
    ├── Checkout/
    ├── Orders/
    ├── Account/
    └── Admin/
```

### ToyStore.Application

รับผิดชอบ use case, command/query, handler, validation, authorization rule ระดับ use case, interface ของ external concerns และ cross-cutting pipeline behaviors

```text
Products/
├── CreateProduct/
├── UpdateProduct/
├── ArchiveProduct/
├── GetProductDetail/
└── SearchProducts/
Brands/
Universes/
Characters/
Cart/
CheckoutAttempts/
Orders/
Inventory/
Payments/
Shipping/
Notifications/
Dashboard/
Customers/
Common/
├── Behaviors/
├── Interfaces/
├── Models/
├── Exceptions/
├── Result.cs
└── Pagination.cs
```

ใช้หนึ่ง action ต่อหนึ่ง handler และหลีกเลี่ยง service class ขนาดใหญ่ ใช้ CQRS แยก command/query ในระดับโค้ด แต่ยังใช้ database เดียว

Pipeline order:

```text
Logging -> Authorization -> Validation -> PersistenceErrorMapping -> optional Transaction -> Handler
```

Authorization ต้องทำงานก่อน validation เพื่อไม่เปิดเผย validation detail และไม่แตะ database/storage ให้ผู้ไม่มีสิทธิ์ ทุก command/query ที่รับ input ต้องใช้ FluentValidation validator ใน Application slice เป็นกติกาหลักและถูกเรียกผ่าน validation pipeline ก่อน handler ข้อผิดพลาดต้อง map เป็น field-level error ภาษาไทยและ validation summary ที่ Web; DataAnnotations หรือ browser validation ใช้ได้เพียง presentation hint และห้ามเป็นแหล่ง business rule หลัก

Automatic Transaction ใช้เฉพาะ request ที่ implement command marker อย่างชัดเจน Brand/Universe mutation ไม่ใช้ automatic transaction แต่เปิด feature-specific mutation session ที่เป็นเจ้าของ fresh context/transaction ของ operation เอง

เพิ่ม performance monitoring และ idempotency เฉพาะ flow ที่ต้องใช้

### ToyStore.Domain

รับผิดชอบ entity, aggregate, value object, invariant, domain event และ business error โดยไม่มี dependency ไปยัง Blazor, ASP.NET Core, EF Core, PostgreSQL หรือ provider SDK

Modules หลัก (Product v1 ไม่มี variant):

```text
Products  Catalog      Cart          Checkout
Orders    Inventory    Customers     Payments
Shipping  Notifications
```

อ่าน invariant ที่ [DOMAIN_RULES.md](DOMAIN_RULES.md)

### ToyStore.Infrastructure

รับผิดชอบ EF Core, PostgreSQL, Identity, migrations, local file storage, payment provider, email, shipping, in-memory caching และ observability โดย implement interface ที่ Application ประกาศ

```text
Persistence/  Identity/  Payments/      Storage/
Email/        Notifications/ Shipping/  Caching/  Observability/
```

## 4. Dependency direction

```text
┌──────────────────────┐
│     ToyStore.Web     │
│ Blazor / Auth / UI   │
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│ ToyStore.Application │
│ Commands / Queries   │
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│   ToyStore.Domain    │
│ Entities / Rules     │
└──────────▲───────────┘
           │
┌──────────┴───────────┐
│ToyStore.Infrastructure│
│ EF / Providers       │
└──────────────────────┘
```

Infrastructure ถูกอ้างจาก Web เฉพาะ composition root เพื่อ register implementations ห้าม Application อ้าง Infrastructure implementation

## 5. Data and persistence

ใช้ PostgreSQL database เดียวและ `ApplicationDbContext` เดียวในระยะแรก Product ไม่มี variant และ Category เป็น seeded lookup (`ArtToy`, `Gundam`) ที่ไม่มี Admin CRUD ใน v1 ตารางหลัก:

M5-03 ใช้ Product mutation session แบบ once-only เพื่อ lock namespace → Product → catalog references และยืนยันผล commit จาก fresh context เมื่อผลลัพธ์ไม่แน่นอน. Create/update/publish/archive เป็น vertical slices; media ใหม่ staged/committed ก่อน database save และมี non-cancellable compensation เมื่อ save หรือ commit acknowledgement ล้มเหลว. Product version เป็น optimistic concurrency watermark เดียวของ lifecycle และ active Product editing (Draft/Published).

```text
Users Roles Products ProductImages ProductCharacters
ProductCategories Brands Universes Characters
InventoryItems StockMovements StockReservations
Carts CartItems SavedAddresses CheckoutAttempts CheckoutAttemptItems
Payments Orders OrderItems BalancePaymentRequests Shipments
NotificationDeliveries MediaCleanupEntries Settings
```

`CheckoutAttempt` เป็น durable pre-payment record ที่ถือ authoritative item/price/address/shipping snapshot, reservation, provider session reference และ idempotency key ก่อนจ่ายยังไม่มี Order ระบบสร้าง `Order` exactly once หลัง verified Stripe payment เท่านั้น พร้อม consume reservation และบันทึก `Payment` ใน transaction/idempotent fulfillment flow

หลัง transaction สร้าง Order commit แล้ว Application เรียก provider-neutral `IOrderPlacedNotificationDispatcher` ทุกครั้งรวมถึง webhook replay Infrastructure ใช้ idempotency key ต่อ Order/provider เพื่อสร้างหรือโหลด `NotificationDelivery`, claim attempt ด้วย PostgreSQL row lock แล้วจึงเรียก Telegram Bot API สำเร็จห้ามส่งซ้ำ ส่วน provider failure เก็บ safe response และ retry delivery row เดิมได้โดยไม่ rollback หรือสร้าง Order/Payment ซ้ำ

ลำดับที่ห้ามสลับคือ `CheckoutAttempt -> verified Stripe/provider evidence -> Payment + Order exactly once` Browser completion และ Admin UI action ไม่ใช่หลักฐาน payment

`ProductCategories` เป็น lookup table ไม่ใช่ hierarchy/junction table; Product แต่ละรายการมี required `ProductCategoryId` หนึ่งค่า

Order item เก็บ immutable snapshot ของ Product traceability, names/slug, `SaleType`, Category, Brand, Universe, primary image, amounts, quantity, pre-order close/ETA/policy และ line total Order ยัง snapshot recipient/address, `ShippingAmount = 0` และ shipping estimate ที่แสดงตอนจ่าย

Application ประกาศ `IApplicationDbContext`; Infrastructure implement ด้วย EF Core ไม่สร้าง generic repository ซ้อน EF Core เพิ่ม repository เฉพาะ aggregate ที่ต้องป้องกัน persistence rule ซับซ้อน ใช้ EF Core Code First โดยให้ model configuration และ migration ที่ commit ใน repository เป็น source of truth ของ schema

Brand/Universe list และ mutation ใช้ narrow feature ports ที่ Infrastructure implement ด้วย `IDbContextFactory<ApplicationDbContext>` แต่ละ list query เปิด fresh no-tracking context และแต่ละ mutation เปิด once-only session ที่เป็นเจ้าของ context, transaction, aggregate tracking, catalog-reference lock และ slug allocation ชุดเดียวกัน ห้ามใช้ circuit-scoped change tracker ร่วมกันระหว่าง Interactive Server requests ส่วน commit verification และ media-reference recheck ต้องเปิด fresh context แยกจาก operation ที่ผล commit ไม่แน่นอน

Character search ใช้ narrow `ICharacterSearchReader` ที่เปิด fresh no-tracking context ต่อคำขอและคืนทั้ง ordered options กับ exact-match metadata จาก SQL statement เดียว ส่วน inline create ใช้ fresh once-only `ICharacterMutationSession`: lock row ของ Universe เป้าหมายด้วย `FOR UPDATE` ก่อนอ่านชื่อหรือ insert และถือ lock จน transaction จบ เพื่อ linearize create/create กับ create/archive โดยไม่เพิ่ม advisory-lock hierarchy หรือใช้ circuit-scoped context เมื่อ commit acknowledgement ไม่แน่นอนให้ `CatalogCommitOutcomeResolver` ตรวจ exact Character evidence ผ่าน fresh context แบบ non-cancellable และคืน authoritative durable row หรือ safe commit-unknown โดยไม่ retry callback และไม่มี `Superseded` state สำหรับ immutable Character

Inventory Domain แยกจาก Product และใช้ `InventoryItem` เป็น concurrency aggregate ที่ persist OnHand, authoritative HeldQuantity, audit watermark และ Version ส่วน stock change คืน immutable `StockMovement` evidence ให้ operation session persist พร้อม aggregate โดยไม่เก็บ movement history ไม่จำกัดใน aggregate `StockReservation` เก็บ immutable hold/expiry identity กับ controlled terminal evidence; customer availability ต้องมาจาก complete reservation snapshot/query ขณะที่ reserve จริงใช้ HeldQuantity แบบ fail closed การ persist/lock, expected-version, target reservation transition และ movement ต้องอยู่ transaction เดียวใน M5-02/M7; Domain ไม่อ้างว่า batch หลายสินค้าจะ atomic เอง

`AddInventoryFoundation` สร้าง `InventoryItems`, append-only `StockMovements` และ lifecycle-controlled `StockReservations` พร้อม composite ownership FK, quantity/lifecycle checks และ index สำหรับ history/expiry. Mutation เปิด `IInventoryMutationSession` ใหม่ต่อ operation, lock Inventory row ด้วย PostgreSQL `FOR UPDATE`, แล้ว commit aggregate กับ evidence ใน transaction เดียว; `OperationId` ของ movement เป็น idempotency key และกรณี commit acknowledgement ไม่แน่นอนจะเปิด fresh context ตรวจ immutable evidence ก่อนตอบ โดยไม่ replay mutation. Availability และ history ใช้ `IInventoryReadStore` ที่เปิด fresh `AsNoTracking` context: physical held ต้องนับ reservation `Active` ทุกแถวให้ตรง `HeldQuantity`; effective/customer availability นับเฉพาะ `Active` ที่ `ExpiresAtUtc > now` และ mismatch ใด ๆ fail closed เป็น system invariant แทนการเผยตัวเลขไม่ปลอดภัย

Anonymous cart เป็น untrusted browser payload ของ `ProductId`/quantity และไม่มี table หรือ server identity ถาวร ส่วน authenticated `Cart` เป็น customer-owned concurrency aggregate ที่มี owner ได้หนึ่ง cart, quantity ต่อ Product 1–99, audit watermark และ Version. `CartItems` เก็บเพียง `CartId`, `ProductId`, quantity โดยไม่เก็บราคา publication หรือ stock; Domain รับ authoritative Product และยอมเพิ่มเฉพาะ `Published + InStock` ขณะที่ login merge/checkout ต้อง re-read Product/price/availability จาก server อีกครั้ง

Cart query/commands ใช้ policy `CanUseCustomerCart` และรับ owner จาก authorized actor เท่านั้น ไม่รับ `CustomerId` จาก browser Mutation เปิด fresh `ICartMutationSession`, เริ่ม transaction แล้ว lock แถว `AspNetUsers` ของ customer ด้วย `FOR UPDATE` ก่อนอ่าน/สร้าง Cart เพื่อ serialize หลายแท็บ ทุก add/change/remove/clear/anonymous-merge รับ client-stable `OperationId`; `CartOperations` เก็บเฉพาะ operation type, SHA-256 intent fingerprint, resulting Cart version/total, ผล rejected/clamped ที่ปลอดภัยสำหรับ merge และ UTC instant เพื่อให้ retry intent เดิมมีผลครั้งเดียวและ replay ผลเดิมได้แม้ Cart เปลี่ยนภายหลัง โดยไม่ persist browser payload ราคา หรือ stock การใช้ operation ID เดิมกับ intent/Cart อื่น fail closed Login merge group Product ซ้ำแบบ deterministic, re-read Product ทั้งชุด, clamp ผลรวมต่อ Product ที่ 99 และคืนรายการ unavailable/clamped แยกจาก valid items ที่ commit ใน transaction เดียว

`ApplicationDbContext`, Identity user และ migrations อยู่ใน Infrastructure โดยใช้ Npgsql เท่านั้น ระยะแรกคงชื่อตารางและคอลัมน์ตาม convention ของ EF Core/Identity เพื่อไม่เพิ่ม naming-convention package; schema ใหม่ที่ต้องการชื่อเฉพาะให้กำหนดอย่างชัดเจนใน EF configuration

Migration rules:

- สร้าง Code First migration จาก Infrastructure project โดยระบุ Web เป็น startup project และ commit migration พร้อม model snapshot
- ตอน Web startup ให้สร้าง service scope และเรียก `Database.MigrateAsync()` หลัง build application แต่ก่อนเริ่มรับ request เพื่อ apply pending migration ทั้ง local และ production
- หากเชื่อมต่อ database หรือ apply migration ไม่สำเร็จ ให้ log ข้อผิดพลาดและหยุด startup ห้ามเปิด application ด้วย schema ที่เก่าหรือ migrate ไม่ครบ
- ห้ามใช้ `EnsureCreated`, ห้าม apply migration จาก HTTP request, health check หรือ business handler และห้ามรัน application หลาย instance พร้อมกันใน topology ระยะแรก
- สร้าง idempotent SQL script เพื่อตรวจ review ก่อน merge/deploy ทุกครั้ง โดยเฉพาะการเปลี่ยน column, constraint, index หรือข้อมูลสำคัญ แม้ application จะเป็นผู้ apply migration ตอน startup
- สำรอง production database ก่อน restart release ที่มี destructive migration และกำหนด rollback/forward-fix plan ที่ผ่านการ review; การ rollback binary ไม่ได้ rollback schema อัตโนมัติ
- ใช้ persisted `long Version` เป็น optimistic concurrency token สำหรับ Brand/Universe โดย create เริ่ม 1 และรับ expected version สำหรับ update/archive; ใช้ optimistic concurrency token หรือ atomic PostgreSQL behavior กับ InventoryItem, pre-order capacity, CheckoutAttempt และ Order transition ที่มีการแก้พร้อมกันได้ และทดสอบ concurrency จริงกับ PostgreSQL
- `PreOrderCapacities` แยกจาก Product offer โดย snapshot `TotalCapacity`/`CloseAtUtc` สำหรับรอบขายและเก็บ counter `HeldQuantity`, `CommittedQuantity`, `RetiredQuantity`; PostgreSQL check constraint บังคับผลรวมไม่เกิน total, unique Product ownership และ `Version` concurrency token ส่วน `PreOrderCapacityReservations` เก็บ checkout/customer/expiry และ lifecycle evidence และ `PreOrderCapacityMovements` เป็น append-only history หนึ่ง version ต่อ mutation
- การยกเลิก Pre-order ก่อน close ลด committed และคืน remaining; ที่หรือหลัง close ลด committed แล้วย้าย quantity ไป retired เพื่อรักษายอดรอบเดิมไม่ให้กลับมาขาย ทั้งสองกรณี persist movement และ deposit disposition (`Forfeited` สำหรับ customer/overdue, `RefundRequired` สำหรับ Admin/supplier) โดยไม่เรียก payment provider จาก Domain
- Pre-order capacity mutation เปิด fresh DbContext/transaction ต่อคำสั่งและ `SELECT ... FOR UPDATE` capacity row ก่อนอ่าน reservation/movement/customer aggregate; lock นี้ serialize การแย่ง capacity สุดท้าย ส่วน persisted Version และ unique movement-version เป็น final concurrency protection ทุก failure rollback capacity, reservation และ movement พร้อมกัน
- Admin ปรับ TotalCapacity ของ Published Pre-order ก่อน close ได้ผ่าน Product mutation transaction ที่ lock Product ก่อน capacity; ต้องอัปเดต Product offer และ `PreOrderCapacity` พร้อม append `CapacityIncreased`/`CapacityDecreased` movement atomically และห้าม total ใหม่ต่ำกว่า `Held + Committed + Retired`
- Customer reserve command รับ actor จาก authorization context เท่านั้นและ query ยอด `Active + Consumed` ภายใต้ capacity lock เพื่อบังคับ `MaxPerCustomer`; lifecycle transition เลือก policy ตาม action (customer cancellation, payment verification หรือ Order Admin) และ retry ต้องใช้ movement/reference/ownership evidence เดิม
- Pre-order reserve command ไม่รับ expiry จาก browser; Application derive hold lifetime ฝั่ง serverเป็น payment window 30 นาที + safety grace 2 นาที และ exact retry ตรวจ reserved/expiry instant กับ movement evidence เดิม

## 6. Identity and authorization

- ASP.NET Core Identity
- Cookie authentication สำหรับ Blazor Interactive Server
- Roles: `Customer`, `Admin`
- Policies: `CanManageProducts`, `CanManageOrders`, `CanVerifyPayments`, `CanManageUsers`

ทุก management policy ต้องใช้ role `Admin` และต้องปฏิเสธ principal ที่มี claim `MustChangePassword=true` จนกว่า Admin จะเปลี่ยน temporary password สำเร็จ Public registration สร้างได้เฉพาะ Customer ด้วย email/password และยังไม่บังคับ email confirmation ใน v1

ระบบ seed roles แบบ idempotent ตอน startup หลัง migration ส่วน Admin คนแรกสร้างได้เฉพาะ explicit `--bootstrap-admin` command ซึ่งอ่าน email และ temporary password จาก configuration/secret คำสั่งนี้ migrate, seed roles, สร้าง Admin ได้ไม่เกินหนึ่งคน แล้วจบ process โดยไม่เปิดรับ request ห้ามส่ง password ผ่าน command-line argument หรือเขียนลง log

ตรวจ authorization ทั้งที่ route/UI และใน server-side use case ห้ามใช้การซ่อนปุ่มเป็น security boundary และยังไม่ใช้ JWT จนกว่าจะมี external API หรือ mobile client

## 7. External services and immutable reference data

Application ประกาศ interface อย่างน้อย `IFileStorage`, `IThaiAddressCatalog`, `IPaymentGateway`, `ITransactionalEmailSender` และ notification provider boundary แล้ว Infrastructure จึง implement รายละเอียดจริง Application และ Razor ห้ามอ้าง provider SDK/Infrastructure implementation โดยตรง

รูปสินค้าเก็บใน directory ถาวรบน server เช่น `/var/lib/toystore/uploads` ผ่าน provider-neutral `IFileStorage` และ Infrastructure `LocalFileStorage`; database เก็บ storage key, relative URL, sort order, alt text และ primary flag ห้ามเก็บ binary image ใน PostgreSQL หรือเก็บ upload ไว้ภายใน deployment directory

`Storage:RootPath` ต้องเป็น absolute service-owned path และใช้ layout เดียวกันทุก environment:

```text
uploads/
├── .staging/{batch-id}/
│   ├── .owner
│   └── {file-id}.{jpg|png|webp}
└── files/{batch-id}/
    ├── .owner
    └── {file-id}.{jpg|png|webp}
```

batch/file id สร้างจาก CSPRNG 16 bytes แล้ว encode เป็น lowercase hex 32 ตัว (128 random bits ต่อ id) และ storage key มีสอง segmentแบบตายตัว `.owner` เป็น service-owned marker ที่คงอยู่ตั้งแต่ staging ผ่าน directory rename จน media สุดท้ายใน batch ถูกลบ ระบบรับเฉพาะ canonical `image/jpeg`, `image/png`, `image/webp`, ตรวจ signature ระหว่าง stream, จำกัดไฟล์ละ 5 MiB แบบ inclusive และ batch ละไม่เกิน `Product.MaximumImageCount` (8) โดยไม่เชื่อ filename, extension, `Stream.Length` หรือ MIME จาก client เพียงอย่างเดียว

Product image upload สร้าง derivative WebP quality 84 ที่จำกัดด้านยาวไม่เกิน 960px ใน staging batch เดียวกัน โดยต้นฉบับยังใช้กับ Product detail/gallery ส่วน storefront list, search, Home และ cart ใช้ thumbnail URL; schema เก็บ original/thumbnail key แยกกันและ mutation commit, rollback, reference verification และ cleanup ทั้งคู่เป็นหน่วยเดียวกัน สินค้าเดิมที่ยังไม่มี derivative ต้อง fallback ไป original URL โดยไม่สร้างงาน resize ระหว่าง request หรือ startup

staging ไม่เป็น public; commit ใช้ non-overwriting directory rename จาก `.staging/{batch-id}` ไป `files/{batch-id}` บน filesystem เดียวกัน `/media/{batch-id}/{file-name}` เปิดอ่านเฉพาะ committed regular file ใน batch ที่มี ownership marker และใช้ immutable cache/conditional/range response Draft query ต้องไม่เปิดเผย media URL แต่ v1 ยอมให้ผู้ที่ถือ opaque URL ที่เดาไม่ได้อ่าน committed Draft media ได้ การ initialize ก่อน startup ตรวจ root/fixed children, permission และ symlink/reparse point บน Unix directory ทั้งสามที่มีอยู่แล้วต้องไม่กว้างกว่า `0750` (ห้าม group-write และ other ทุกสิทธิ์) และ validate operator-managed root โดยไม่ `chmod` หรือเปลี่ยน ancestor ส่วน directory ที่ขาดถูกสร้างด้วย `0750` ตั้งแต่ create syscall จากนั้นสร้าง probe directory+file ใน staging, rename แบบ non-overwriting ไป committed tree และลบ probe เพื่อพิสูจน์ write/rename/delete boundary; probe artifact ที่ crash ค้างทั้งสองฝั่งถูกตรวจและ cleanup อย่างปลอดภัยใน startup ถัดไป แล้วจึง cleanup staging ที่เก่ากว่า retention (default 24 ชั่วโมง) หนึ่งครั้ง ไม่มี cleanup worker และไม่เดาเพื่อลบ committed orphan ตอน startup

filesystem กับ PostgreSQL ไม่เป็น atomic resource เดียวกัน media mutation จึง stage ครั้งเดียวก่อน transaction แล้ว commit media ภายใน operation ก่อน database save/commit หาก save หรือ transaction rollback แน่นอนให้ชดเชย media ใหม่ด้วย non-cancelled cleanup ส่วน media เก่าลบได้หลัง database commit ที่ยืนยันแล้วเท่านั้นและ deletion failure ต้องบันทึก ledger โดยยังคืนผลสำเร็จของ mutation ที่ commit แล้ว

Mutation session แยกผล commit เป็น `Committed`, `DefinitelyRolledBack` และ `Indeterminate` การที่ commit acknowledgement ล้มเหลวห้ามเดาว่า rollback: ให้ใช้ fresh context ตรวจ action-specific ID/version/details/status และค้นทุก persisted media reference ด้วย trusted storage key หากพบ state ที่ commit หรือ supersede แล้วให้เก็บ media และ refresh authoritative result; ชดเชยได้เฉพาะเมื่อ fresh full-reference check พิสูจน์ว่า key ไม่ถูกอ้างอิง หาก verification unavailable/inconsistent ให้เก็บ media, บันทึก `MediaCleanupEntries` และคืน safe `Persistence.CommitOutcomeUnknown` เพื่อให้ Admin refresh ก่อน retry

cleanup ledger เก็บเฉพาะ opaque trusted key จาก staged media หรือ persisted media snapshot พร้อม reason, entity context, first/last attempt, attempt count และ resolution instant โดยมี unresolved row ได้หนึ่งรายการต่อ storage key ไม่มี worker/scheduler หรือ startup orphan deletion การทำ age-graced reconciliation ในอนาคตต้องใช้ grace period และ fresh full-reference recheck ก่อนลบทุกครั้ง; service-owned filesystem เป็น security boundary และ implementation ป้องกัน traversal/symlink ตามข้อจำกัด TOCTOU ของ managed filesystem API

Thai address catalog ใช้ version-pinned local JSON จาก `kongvut/thai-province-data` ไม่เรียก network ตอน runtime Infrastructure validate Province → District → Sub-district → postal code relation ตอน startup แล้วโหลดเป็น immutable/frozen lookup และ register Singleton หาก dataset เสีย startup ต้อง fail Application query เข้าถึงผ่าน `IThaiAddressCatalog`; Razor ส่ง query ผ่าน `ISender`

เวลา persist เป็น UTC; UI/close-date/dashboard ใช้ `Asia/Bangkok` และ format ภาษาไทย `th-TH` วันที่ปิด Pre-order ที่ Admin เลือกแปลงจาก `23:59:59 Asia/Bangkok` เป็น UTC instant

`IPaymentGateway` รองรับ Stripe Embedded Checkout/Checkout Sessions, signature/session retrieval และ provider references โดย browser ไม่ได้รับ secret `ITransactionalEmailSender`, Telegram Bot API และ LINE Official Account/Messaging API ส่งหลัง durable commit เท่านั้นและบันทึกผลผ่าน `NotificationDelivery`; provider failure ไม่ rollback commerce transaction Telegram token/chat configuration อยู่ใน server secret และข้อความแจ้งร้านห้ามมี address, phone หรือ payment secret

## 8. Cache and maintenance

ใช้เฉพาะ `IMemoryCache` และ Output Cache สำหรับข้อมูลสาธารณะที่อ่านบ่อย ไม่ใช้ Redis หรือ distributed cache และห้าม cache cart, checkout, account, admin, order หรือ authentication response

ระบบไม่มีกระบวนการประมวลผลงานตามเวลาในตัวแอป งานหมดอายุ reservation/CheckoutAttempt และ balance overdue ใช้ Application command แบบ idempotent ที่เรียก synchronously ก่อน checkout/action ที่เกี่ยวข้อง หรือเรียกจากหน้า Admin maintenance โดยผู้มีสิทธิ์ Query availability ต้องไม่นับ reservation ที่หมดอายุแม้ยังไม่ได้ cleanup เมื่อ Stripe webhook อาจขาดหาย ให้ retrieve provider session ก่อน release และ fail closed ชั่วคราวเมื่อ provider ติดต่อไม่ได้

## 9. Production topology

ทุกส่วนที่ระบบเป็นเจ้าของ deploy บน Linux server เครื่องเดียว:

```text
Internet
   -> Caddy container :80/:443
      -> ToyStore.Web container :8080 (host loopback diagnostics :5000)
         -> PostgreSQL container :5432 (internal Docker network เท่านั้น)
         -> /var/lib/toystore/uploads bind mount

/var/backups/toystore
   <- PostgreSQL dump
   <- uploads archive
   <- data-protection keys
```

Caddy, application และ PostgreSQL รันด้วย production Docker Compose บนเครื่องเดียว Web image ถูก build/test บน GitHub Actions, push ไป GHCR และ deploy ด้วย immutable registry digest โดย local development ยังรัน Web ด้วย `dotnet run` Caddy ต้องรองรับ WebSocket/SignalR สำหรับ Interactive Server Cloudflare ใช้เป็น optional DNS/proxy ได้ แต่ระบบต้องทำงานได้โดยไม่พึ่ง Cloudflare service เครื่องเริ่มต้น 2 vCPU, RAM 4 GB และ SSD 40–80 GB เพียงพอเมื่อ query และ media ถูกจัดการเหมาะสม

อ่านขั้นตอน production ที่ [DEPLOYMENT.md](DEPLOYMENT.md) ต้อง backup PostgreSQL, uploads และ ASP.NET Core Data Protection keys ทุกวัน และทดสอบ restore เป็นระยะ

## 10. Observability and health

ใช้ Serilog และ health checks:

```text
/health
/health/live
/health/ready
```

Log แบบ structured พร้อม request/correlation ID, user ID เมื่อเหมาะสม, order ID, payment reference, handler name และ elapsed time ห้าม log password, token, card data หรือข้อมูลส่วนบุคคลเกินจำเป็น

### Dependency decisions

- Pin MediatR ที่เวอร์ชัน 12.5.0 ซึ่งใช้สัญญาอนุญาต Apache-2.0 การอัปเกรดเป็นเวอร์ชัน 13 ขึ้นไปต้องผ่าน license review ใหม่ และห้าม suppress license warning

## 11. Testing strategy

- Unit: domain invariants, value objects, validators, price/discount calculations และ state transitions
- Integration: EF Core/PostgreSQL, MediatR handlers, transactions, Identity และ payment webhook
- End-to-end: browse, cart, checkout, payment verification และ admin order flow ที่สำคัญ

เน้น integration tests สำหรับ Brand/Universe normalized duplicate และ replacement update/update/update/archive races, commit acknowledgement verification, reference preservation, cleanup-ledger idempotency รวมถึง Create CheckoutAttempt, Reserve Stock/Capacity, verified-payment Order creation, Confirm Balance Payment, Cancel Order และ Refund พร้อม retry/concurrency/rollback/idempotency ใช้ PostgreSQL จริงผ่าน Testcontainers และ Respawn

Destructive integration tests ต้องใช้ PostgreSQL Testcontainer ที่ database ลงท้ายด้วย `_test` หรือ `_integration_test` เท่านั้น และมี guard ปฏิเสธ database `toystore`, `postgres` และ template databases โดยไม่มี bypass configuration
