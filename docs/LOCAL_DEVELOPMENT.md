# Local Development

## Model

Local development ใช้ PostgreSQL ใน Docker แต่รัน Blazor Web App บน host ด้วย `dotnet run` เพื่อให้ inner loop, hot reload และ debugging ง่าย

```text
Host: dotnet run / dotnet watch
Docker: PostgreSQL only
```

## Prerequisites

- .NET SDK 10
- Docker Desktop หรือ Docker Engine พร้อม Compose
- EF Core CLI เมื่อทำ migration: `dotnet tool install --global dotnet-ef`
- [Stripe CLI](https://docs.stripe.com/stripe-cli/install) สำหรับรับและตรวจ webhook ระหว่างทดสอบ payment flow ในเครื่อง

## First setup

```bash
cp .env.example .env
docker compose up -d postgres
docker compose ps
```

ก่อนรัน Web ครั้งแรก ให้ตั้งค่า connection string และ persistent media directory ใน .NET user secrets (คำสั่งนี้ใช้ credential default จาก `.env.example`):

```bash
mkdir -p .data/uploads
chmod 750 .data .data/uploads
dotnet user-secrets set \
  "ConnectionStrings:Database" \
  "Host=localhost;Port=5432;Database=toystore;Username=toystore;Password=toystore_dev" \
  --project src/ToyStore.Web
dotnet user-secrets set \
  "Storage:RootPath" \
  "$(pwd)/.data/uploads" \
  --project src/ToyStore.Web
```

ถ้าแก้ credential ใน `.env` ให้ใช้ค่าเดียวกันใน connection string จากนั้น restore, build และรัน solution:

```bash
dotnet restore ToyStore.sln
dotnet build ToyStore.sln --no-restore
dotnet run --project src/ToyStore.Web
```

โปรเจกต์ใช้ EF Core Code First เมื่อ Web เริ่มทำงาน ระบบจะ apply pending migration ด้วย `Database.MigrateAsync()` ก่อนเปิดรับ request หาก PostgreSQL ใช้งานไม่ได้หรือ migration ล้มเหลว application ต้องหยุด startup

สำหรับ hot reload:

```bash
dotnet watch --project src/ToyStore.Web
```

URL เริ่มต้นให้ดูจาก output ของ ASP.NET Core launch profile

## Configuration

Connection string สำหรับ host application:

```text
Host=localhost;Port=5432;Database=toystore;Username=toystore;Password=<local password>
```

อย่า commit password จริง ให้ใช้ .NET user secrets ตามขั้นตอน First setup สำหรับ Web project ตรวจค่าที่ตั้งแล้วได้ด้วย:

```bash
dotnet user-secrets list --project src/ToyStore.Web
```

`.env` ใช้กำหนด credential ของ PostgreSQL container เท่านั้น และถูก ignore จาก Git

รูปสินค้าสำหรับ local development เก็บใน `.data/uploads` ที่กำหนดใน First setup

## Telegram แจ้งเตือนคำสั่งซื้อใหม่

Telegram notification เป็น optional และปิดไว้ตามค่าเริ่มต้น ระบบส่งหลัง Stripe ยืนยัน payment และสร้าง Order สำเร็จแล้วเท่านั้น ข้อความมีเลข Order, ประเภทการขาย, ยอดที่รับชำระ และลิงก์ Admin โดยไม่ส่งชื่อ ที่อยู่ เบอร์โทร หรือข้อมูล payment ของลูกค้า

1. สร้าง bot กับ `@BotFather` แล้วเก็บ bot token เป็น secret
2. เพิ่ม bot เข้า chat/group ที่ต้องการ จากนั้นส่งข้อความหนึ่งครั้งและอ่าน `chat.id` จาก Bot API `getUpdates` ห้ามนำ token หรือ response ที่มีข้อมูล chat ไปใส่ source control/log
3. ตั้งค่า local ด้วย .NET user secrets แล้ว restart Web:

```bash
dotnet user-secrets set "Telegram:Enabled" "true" --project src/ToyStore.Web
dotnet user-secrets set "Telegram:BotToken" "<bot-token>" --project src/ToyStore.Web
dotnet user-secrets set "Telegram:ChatId" "<chat-id>" --project src/ToyStore.Web
dotnet user-secrets set "Telegram:AdminBaseUrl" "https://sytoys.shop" --project src/ToyStore.Web
```

Production ใช้ configuration secret/environment variables ชื่อ `Telegram__Enabled`, `Telegram__BotToken`, `Telegram__ChatId` และ `Telegram__AdminBaseUrl` ห้าม commit token/chat ID ลง `appsettings*.json` หรือ `.env` หากเปิดใช้งานแต่ configuration ไม่ครบ application จะหยุด startup พร้อมข้อความที่ไม่เปิดเผย secret

เมื่อ provider ล้มเหลว Order/Payment ยังคงสำเร็จ และผลล้มเหลวแบบ safe ถูกบันทึกใน `NotificationDeliveries` การ replay Stripe webhook จะ retry delivery เดิมด้วย idempotency key เดิม โดยไม่สร้าง Order, Payment หรือ notification row ซ้ำ

## Stripe sandbox และ local checkout flow

Checkout ทั้งสินค้าพร้อมส่งและพรีออเดอร์ใช้ Stripe Checkout Sessions แบบ Embedded page (`ui_mode=embedded_page`) แอปส่งราคาและชื่อสินค้าด้วย `price_data` จึงไม่ต้องสร้าง Product หรือ Price ใน Stripe Product catalog ก่อนทดสอบ

### 1. เตรียม Stripe sandbox

1. สร้างหรือเลือก Stripe sandbox/test account และตรวจว่า dashboard อยู่ในโหมดทดสอบ
2. เปิด Card และ PromptPay ใน Payment methods ของ sandbox
3. คัดลอก test API keys จาก Developers > API keys โดยต้องเป็นคู่ `pk_test_...` และ `sk_test_...` จาก sandbox เดียวกัน
4. ติดตั้ง Stripe CLI แล้วเชื่อม CLI กับ sandbox:

```bash
brew install stripe/stripe-cli/stripe
stripe version
stripe login
```

ดูวิธีติดตั้งสำหรับระบบอื่นได้จาก [Stripe CLI installation](https://docs.stripe.com/stripe-cli/install) และรายละเอียด test keys จาก [Stripe API keys](https://docs.stripe.com/keys)

### 2. ตั้งค่า local secrets

ตั้งค่าเฉพาะใน .NET user secrets และห้าม commit key ลง `appsettings*.json`, `.env`, source code หรือเอกสาร:

```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/ToyStore.Web
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..." --project src/ToyStore.Web
dotnet user-secrets set "Stripe:ReturnUrlBase" "http://localhost:5141" --project src/ToyStore.Web
```

ไม่ต้องตั้ง `Stripe:ReturnUrlBase` ถ้าใช้ URL default นี้ หาก launch profile เปลี่ยน port ต้องแก้ค่านี้ให้ตรง origin ที่เปิดใน browser

### 3. เปิด local webhook listener

Login ด้วย Stripe CLI แล้วเปิด listener ค้างไว้ใน terminal แยก:

```bash
stripe listen \
  --events checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.expired \
  --forward-to http://localhost:5141/webhooks/stripe
```

คัดลอก signing secret `whsec_...` ที่ listener แสดง แล้วตั้งค่า:

```bash
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project src/ToyStore.Web
```

`whsec_...` จาก `stripe listen` เป็น secret ของ listener รอบ local นี้ และอาจไม่เหมือน secret ของ webhook endpoint ที่สร้างใน Dashboard หากเริ่ม listener ใหม่แล้ว CLI แสดง secret ใหม่ ให้แก้ user secret ให้ตรง จากนั้น restart Web process เพราะ application อ่าน configuration ตอน startup

รูปแบบ terminal ที่แนะนำ:

```text
Terminal 1: docker compose up -d postgres
Terminal 2: stripe listen ... --forward-to http://localhost:5141/webhooks/stripe
Terminal 3: dotnet watch --project src/ToyStore.Web
```

### 4. ทดสอบ flow จริงผ่านหน้าเว็บ

ก่อนทดสอบให้มีสินค้าสถานะ Published และ login ด้วยบัญชีลูกค้า:

- **In-stock:** สินค้าต้องมี stock พร้อมขาย เพิ่มลงตะกร้า ไป `/checkout` เลือก/กรอกที่อยู่ไทย แล้วชำระราคาเต็ม
- **Pre-order:** พรีออเดอร์ต้องอยู่ในช่วงเปิดรับและมี capacity ไปจากหน้าสินค้า ตรวจสิทธิ์ เลือกจำนวนและที่อยู่ แล้วชำระมัดจำ
- **Card:** ใช้เลขบัตรทดสอบ `4242 4242 4242 4242`, วันหมดอายุในอนาคต, CVC 3 หลักใดก็ได้ และข้อมูล billing ที่ถูกต้อง ดูกรณีสำเร็จ/ปฏิเสธ/3DS เพิ่มเติมที่ [Stripe testing](https://docs.stripe.com/testing)
- **PromptPay:** เลือก PromptPay ใน Embedded Checkout แล้วทำ test flow ที่ Stripe แสดง ดูข้อจำกัดและขั้นตอนที่ [Stripe PromptPay](https://docs.stripe.com/payments/promptpay)

ระหว่างชำระให้ดู terminal ของ Stripe CLI ต้องเห็น request เข้า `POST /webhooks/stripe` และ response `200` สำหรับ event ที่เกี่ยวข้อง หน้า return ของ browser เพียงอย่างเดียวไม่ใช่หลักฐานการชำระเงิน

ลำดับข้อมูลที่ต้องเกิด:

```text
ก่อนชำระ: CheckoutAttempt + reservation ถูกบันทึก และยังไม่มี Order
Stripe ยืนยัน: signed webhook ถูก verify ด้วย Stripe:WebhookSecret
หลัง webhook: Payment + Order ถูกสร้าง exactly once และ reservation ถูก consume
```

หลังจ่ายสำเร็จให้ตรวจหน้าบัญชี/รายการคำสั่งซื้อ และลอง refresh หน้า return ได้โดยต้องไม่เกิด Payment หรือ Order ซ้ำ สำหรับ payment ที่หมดอายุ `checkout.session.expired` ต้องคืน reservation โดยไม่สร้าง Order

อย่าใช้ `stripe trigger checkout.session.completed` เป็น E2E ของ commerce flow เพราะ event จำลองทั่วไปไม่มี `checkout_attempt_id` และ metadata ของ session ที่แอปสร้าง ต้องชำระผ่าน Embedded Checkout ของแอปเพื่อทดสอบ Order creation จริง

### Stripe troubleshooting

- ขึ้น “ระบบชำระเงินยังไม่พร้อม”: เปิด [Stripe Workbench logs](https://dashboard.stripe.com/test/logs) แล้วดู request `POST /v1/checkout/sessions` ล่าสุด ตรวจ error message, code, parameter และ request ID โดยห้ามส่ง secret key หรือข้อมูลบัตรจริง
- Stripe แจ้งว่า `ui_mode embedded is no longer supported`: process ยังรัน build เก่า ปัจจุบันแอปต้องส่ง `embedded_page`; stop/rebuild/restart Web แล้วเริ่ม checkout session ใหม่
- Stripe ยังแสดงช่องอีเมลว่าง: restart Web หลังอัปเดต configuration/code แล้วเริ่ม checkout ใหม่ ระบบส่ง `customer_email` จาก ASP.NET Identity ฝั่ง server โดยไม่รับค่าอีเมลจาก browser
- Webhook ได้ HTTP 400 หรือ signature verification fail: ค่า `Stripe:WebhookSecret` ไม่ตรงกับ `whsec_...` ของ listener ที่กำลังทำงาน
- หน้าค้างที่กำลังตรวจสอบหลังจ่าย: ตรวจว่า listener ยังทำงาน, forward URL/port ถูกต้อง และ webhook ตอบ 200 อย่าสร้าง Order จาก browser completion เพื่อแก้อาการนี้
- Session หมดอายุหรือ `expires_at` เก่า: กลับไปเริ่ม checkout ใหม่ แอปไม่ควร reuse session ที่หมดอายุ
- เปลี่ยน user secrets แล้วอาการไม่เปลี่ยน: restart `dotnet run`/`dotnet watch` process

ใช้ test keys และ test payment data เท่านั้นใน local development ห้ามใช้ live key หรือข้อมูลบัตรจริง ดูภาพรวมการรับ webhook และ signature verification ที่ [Stripe webhooks](https://docs.stripe.com/webhooks)

directory `.data` ถูก ignore จาก Git และใช้รูปแบบเดียวกับ local media directory บน production server

`Storage:RootPath` ต้องเป็น absolute path ระบบสร้างเฉพาะ `.staging` และ `files` ใต้ root นี้และตรวจว่า root กับสอง directory ไม่ใช่ symlink/reparse point ก่อนรับ request บน Unix permission ของ root และ fixed children ที่มีอยู่แล้วต้องไม่กว้างกว่า `0750` (ห้าม group-write และห้ามสิทธิ์ทั้งหมดของ other) ระบบจะ validate เท่านั้นและไม่ `chmod` directory ที่ operator สร้างไว้; หาก root หรือ fixed child ยังไม่มี ระบบสร้างด้วย mode `0750` ตั้งแต่ create syscall ตั้ง retention ของ staging ได้ (default 24 ชั่วโมง):

```bash
dotnet user-secrets set \
  "Storage:StagingRetention" \
  "1.00:00:00" \
  --project src/ToyStore.Web
```

startup ปกติและ `--bootstrap-admin` initialize storage ด้วย crash-recoverable directory rename probe จาก `.staging` ไป `files`, cleanup probe artifact ที่ค้างอย่างปลอดภัย และลบเฉพาะ staging batch ที่เก่ากว่า retention หนึ่งครั้ง หาก root ไม่ปลอดภัย, child เป็น symlink, เขียน/rename/delete ไม่ได้ หรือ cleanup ไม่สำเร็จ process จะหยุด startup ไม่มี background cleanup loop และไม่มีการลบ committed file จากการเดาอายุไฟล์

Committed media อ่านผ่าน `/media/{batch-id}/{file-id}.{jpg|png|webp}` เท่านั้น `.staging` ไม่ถูก serve และไม่ต้องรวมใน backup

Migration `AddCatalogCleanupLedger` เพิ่ม persisted `Version` ให้ Brand/Universe และตาราง `MediaCleanupEntries` ใน PostgreSQL Ledger นี้ไม่ใช่ filesystem queue แต่บันทึก trusted storage key เมื่อ commit outcome ยังยืนยันไม่ได้, reference verification ใช้งานไม่ได้ หรือ post-commit delete ล้มเหลว Startup apply migration นี้ตาม workflow ปกติและไม่มี background cleanup loop

ห้ามลบ committed file ตาม ledger ด้วยมือหรือเดาจากอายุไฟล์ การ reconcile ในอนาคตต้องเปิด fresh database context และตรวจ Brand, Universe และ Product image references ทั้งหมดอีกครั้งก่อนลบ Ledger อยู่ใน PostgreSQL จึงรวมอยู่ใน database dump โดยไม่ต้อง backup เป็นไฟล์แยก

## Code First database workflow

แก้ model และ EF configuration ใน code ก่อน แล้วสร้าง migration ใน Infrastructure ห้ามใช้ `EnsureCreated` เพราะจะข้าม migration history

สร้าง migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/ToyStore.Infrastructure \
  --startup-project src/ToyStore.Web \
  --output-dir Persistence/Migrations
```

ตรวจ generated migration และ model snapshot แล้วดู SQL ก่อน commit:

```bash
dotnet ef migrations script --idempotent \
  --project src/ToyStore.Infrastructure \
  --startup-project src/ToyStore.Web
```

การรัน Web เป็นวิธีปกติสำหรับ apply migration ใน local environment:

```bash
dotnet run --project src/ToyStore.Web
```

อย่ารัน `dotnet ef database update` เป็นขั้นตอนปกติ เพราะ startup เป็นเจ้าของการ apply migration ใช้คำสั่ง EF CLI เพื่อ target migration เฉพาะเมื่อกู้คืนหรือ rollback ตามแผนที่ตรวจ review แล้วเท่านั้น

## Tests

```bash
dotnet test ToyStore.sln
```

เมื่อ integration tests ใช้ PostgreSQL ให้แยก database หรือ container จาก development database ห้ามรัน destructive test กับ `toystore` database

Integration test harness ใช้ PostgreSQL Testcontainer ชื่อ database `toystore_integration_test` และตรวจ suffix ก่อน reset ทุกครั้ง จึงต้องเปิด Docker ขณะรัน integration tests แต่ไม่ต้องใช้ container/database เดียวกับ local development

## First Admin bootstrap

สร้าง Admin คนแรกด้วยคำสั่ง explicit เท่านั้น โดยเก็บ temporary credential ใน user secrets และลบ temporary password หลังใช้งาน:

```bash
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project src/ToyStore.Web
dotnet user-secrets set "BootstrapAdmin:TemporaryPassword" "CHANGE_THIS_NOW" --project src/ToyStore.Web
dotnet run --project src/ToyStore.Web -- --bootstrap-admin
dotnet user-secrets remove "BootstrapAdmin:TemporaryPassword" --project src/ToyStore.Web
```

คำสั่งจะ apply migration, seed roles, สร้าง Admin ได้เพียงคนแรก แล้วจบโดยไม่เปิด Web server ห้ามใส่ password ต่อท้าย command เพราะ process argument อาจถูกอ่านได้ Admin ต้องเปลี่ยน temporary password ตอน login ครั้งแรกก่อนใช้หน้าจัดการ

## Container operations

```bash
docker compose up -d postgres
docker compose logs -f postgres
docker compose stop postgres
docker compose down
```

`docker compose down` ไม่ลบ volume; ใช้ `docker compose down -v` เฉพาะเมื่อตั้งใจล้าง local database ทั้งหมด

## Health and troubleshooting

ตรวจ health endpoints หลัง Web เริ่มรับ request แล้ว (แทน `<web-url>` ด้วย URL จาก output ของ `dotnet run`):

```bash
curl -i <web-url>/health/live
curl -i <web-url>/health/ready
curl -i <web-url>/health
```

- `/health/live` ตรวจว่า process ยังตอบสนอง โดยไม่ขึ้นกับ PostgreSQL
- `/health/ready` ตรวจว่า PostgreSQL พร้อมใช้งาน; คืน HTTP 503 เมื่อเชื่อมต่อไม่ได้
- `/health` รวมทุก check และคืน HTTP 503 เมื่อ dependency ใดไม่พร้อม

- ตรวจ container: `docker compose ps`
- ตรวจ readiness: `docker compose exec postgres pg_isready -U toystore -d toystore`
- ตรวจ port 5432 ว่าไม่ชน PostgreSQL ในเครื่อง
- ถ้า Web ต่อฐานข้อมูลไม่ได้ ให้เทียบ password ใน `.env` กับ user secret
- ถ้า startup หยุดเพราะ migration ให้ตรวจ log, สถานะ PostgreSQL และ migration SQL ก่อนแก้ไข ห้ามข้าม migration ด้วย `EnsureCreated`
- ถ้าเปลี่ยนค่าหลัง volume ถูกสร้างแล้ว ค่า init บางอย่างจะไม่เปลี่ยนจนกว่าจะสร้าง volume ใหม่
