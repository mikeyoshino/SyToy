# Single-Server Deployment

## Deployment contract

ระบบ production ทั้งหมดที่ Toy Store เป็นเจ้าของต้องรันบน Linux server เครื่องเดียว:

```text
Linux Server
├── Caddy                         :80 / :443
├── ToyStore.Web via systemd      127.0.0.1:5000
├── PostgreSQL in Docker          127.0.0.1:5432
├── /var/lib/toystore/uploads     product media
├── /var/lib/toystore/keys        ASP.NET Core Data Protection keys
├── /var/lib/toystore/logs        application logs
└── /var/backups/toystore         database and media backups
```

ไม่ใช้ Redis, Cloudflare R2, distributed cache, application worker process หรือ job scheduler ระบบต้องเข้าถึงได้โดยตรงผ่าน Caddy แม้ไม่ได้ใช้ Cloudflare; หากใช้ Cloudflare ให้เป็นเพียง optional DNS/proxy layer

Caddy terminate TLS แล้ว proxy เข้า Kestrel ผ่าน loopback เท่านั้น Web เชื่อ `X-Forwarded-For` และ `X-Forwarded-Proto` เฉพาะ connection จาก IPv4/IPv6 loopback และรับ forwarded hop เดียว ห้ามเปลี่ยนเป็น trust-all proxy หรือ network; หากย้าย reverse proxy ไปเครื่องอื่นต้องเพิ่ม IP ที่เจาะจงและ review security boundary ก่อน deploy

## Minimum server

- Linux x64/ARM64 ที่ .NET 10 รองรับ
- 2 vCPU
- RAM 4 GB
- SSD 40–80 GB
- Docker Engine พร้อม Compose plugin
- .NET 10 ASP.NET Core Runtime
- Caddy

เปิด firewall เฉพาะ SSH, HTTP และ HTTPS PostgreSQL และ Kestrel ต้อง bind กับ loopback เท่านั้น

## Filesystem

```bash
sudo useradd --system --home /var/lib/toystore --shell /usr/sbin/nologin toystore
sudo mkdir -p /opt/toystore/current
sudo mkdir -p /etc/toystore
sudo mkdir -p /var/lib/toystore/uploads
sudo mkdir -p /var/lib/toystore/keys
sudo mkdir -p /var/lib/toystore/logs
sudo mkdir -p /var/backups/toystore
sudo chown -R toystore:toystore /opt/toystore /var/lib/toystore /var/backups/toystore
sudo chmod 750 /etc/toystore /var/lib/toystore /var/lib/toystore/uploads /var/backups/toystore
```

อย่าเก็บ uploads, Data Protection keys หรือ application logs ภายใน `/opt/toystore/current` เพราะ directory นี้ถูกแทนที่ตอน deploy ตัวอย่าง systemd unit ใช้ `StateDirectory=toystore/logs toystore/keys` เพื่อสร้างและกำหนด ownership ของ `/var/lib/toystore/logs` และ `/var/lib/toystore/keys` ให้ service user พร้อมจำกัด `ReadWritePaths` เฉพาะ uploads, keys และ logs

`LocalFileStorage` สร้าง `/var/lib/toystore/uploads/.staging` และ `/var/lib/toystore/uploads/files` ด้วย mode `0750` ตั้งแต่ create syscall ภายใต้ `UMask=0027` root และ children ต้องไม่เป็น symlink/reparse point บน Unix root และ fixed children ที่มีอยู่แล้วต้องมี permission ไม่กว้างกว่า `0750`: ห้าม group-write และห้าม other read/write/execute ระบบจะ validate root ที่ operator ดูแลโดยไม่ `chmod` หรือแก้ ancestor directory ค่า `Storage__RootPath` ต้องอยู่นอก release root หลัง resolve stable ancestor aliases และ startup จะใช้ non-overwriting staging→files directory rename probe; process fail ก่อนรับ request เมื่อ configuration, permission, rename/delete probe หรือ stale-staging cleanup ไม่ปลอดภัย

## PostgreSQL

ใช้ `compose.yaml` เดียวกับ local โดยสร้าง `.env` บน server และใช้ password แบบสุ่มที่แข็งแรง:

```bash
docker compose up -d postgres
docker compose ps
docker compose exec postgres pg_isready -U toystore -d toystore
```

Compose bind port ที่ `127.0.0.1` เท่านั้น ห้ามเปิด 5432 ที่ public interface

## Application configuration

เก็บ production environment variables ใน `/etc/toystore/toystore.env` และจำกัด permission:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
ConnectionStrings__Database=Host=127.0.0.1;Port=5432;Database=toystore;Username=toystore;Password=CHANGE_ME
Storage__RootPath=/var/lib/toystore/uploads
DataProtection__KeysPath=/var/lib/toystore/keys
Serilog__WriteTo__1__Args__path=/var/lib/toystore/logs/toystore-.log
```

ไฟล์ systemd ตัวอย่างกำหนดค่า Serilog path นี้โดยตรงด้วย เพื่อให้ production ไม่พยายามเขียน log ภายใต้ release directory ที่เป็น read-only ค่าใน environment file ด้านบนแสดง production contract เดียวกันและสามารถใช้กับ service manager อื่นได้

```bash
sudo chown root:toystore /etc/toystore/toystore.env
sudo chmod 640 /etc/toystore/toystore.env
```

ห้าม commit production secrets ลง repository

## First Admin bootstrap

Admin คนแรกต้องสร้างผ่าน explicit bootstrap command หลัง deploy และก่อนเริ่ม service ปกติเท่านั้น ตั้ง `BootstrapAdmin__Email` และ `BootstrapAdmin__TemporaryPassword` เป็น secret ชั่วคราวใน environment ของคำสั่ง แล้วรัน:

```bash
sudo -u toystore --preserve-env=BootstrapAdmin__Email,BootstrapAdmin__TemporaryPassword \
  /usr/bin/dotnet /opt/toystore/current/ToyStore.Web.dll --bootstrap-admin
```

คำสั่งนี้ apply migration, seed roles, สร้าง Admin ได้ไม่เกินหนึ่งคน และจบโดยไม่ listen จากนั้นลบ temporary secret ออกจาก environment/secret store ก่อน start systemd service ห้ามใส่ password เป็น command-line argument และ Admin ต้องเปลี่ยน temporary password ตอน login ครั้งแรก

## Publish and install

สร้าง artifact บน CI หรือเครื่องพัฒนา:

```bash
dotnet restore ToyStore.sln
dotnet test ToyStore.sln --configuration Release
dotnet publish src/ToyStore.Web --configuration Release --output artifacts/publish
```

คัดลอก artifact ไป `/opt/toystore/current` แล้วติดตั้ง templates:

```bash
sudo cp deploy/toystore.service.example /etc/systemd/system/toystore.service
sudo cp deploy/Caddyfile.example /etc/caddy/Caddyfile
sudo systemctl daemon-reload
sudo systemctl enable toystore
sudo systemctl reload caddy
```

แทน `toys.example.com` ใน Caddyfile ด้วย domain จริงก่อน reload ขั้นตอนนี้ยังไม่ start release ใหม่ ให้ตรวจ migration และ backup ตามหัวข้อถัดไปก่อน

## Database migration

ระบบใช้ EF Core Code First และ Web application เป็นผู้ apply pending migration ตอน startup ก่อน Kestrel เปิดรับ request สร้าง idempotent script เป็น review artifact ก่อน deploy ทุกครั้ง:

```bash
dotnet ef migrations script --idempotent \
  --project src/ToyStore.Infrastructure \
  --startup-project src/ToyStore.Web \
  --output artifacts/migrate.sql
```

ตรวจ `artifacts/migrate.sql` และ generated migration files ก่อน publish หาก migration ลบหรือแปลงข้อมูล, เปลี่ยน constraint หรือ lock ตารางสำคัญ ต้องสำรอง database และกำหนด rollback/forward-fix plan ก่อน restart

หลังคัดลอก release ให้ restart service เพื่อเริ่ม migration:

```bash
sudo systemctl restart toystore
sudo systemctl status toystore
sudo journalctl -u toystore -n 200 --no-pager
```

Startup ต้องสร้าง service scope และเรียก `Database.MigrateAsync()` ก่อนเริ่มรับ request หาก database ใช้งานไม่ได้หรือ migration ล้มเหลว process ต้อง exit non-zero เพื่อให้ deployment ล้มเหลวอย่างชัดเจน ห้ามใช้ `EnsureCreated` และห้าม trigger migration จาก HTTP request

Topology นี้มี Web process เดียว จึงไม่ต้องประสาน migration ระหว่างหลาย instance บัญชี PostgreSQL ของ application ต้องมีสิทธิ์ apply migration กับ schema ที่ระบบเป็นเจ้าของ การ rollback application binary ไม่ rollback database schema อัตโนมัติ จึงต้องออกแบบ migration ให้ backward-compatible เมื่อทำได้ และใช้ migration ที่ตรวจ review แล้วหรือ forward fix เมื่อต้องย้อนการเปลี่ยนแปลง

Catalog cleanup migration เพิ่ม Brand/Universe concurrency `Version` และ `MediaCleanupEntries` ซึ่งเก็บ trusted storage keys สำหรับ unresolved commit/reference/delete cleanup state ตารางนี้ต้อง migrate และ restore พร้อม catalog rows; ไม่มี application worker/scheduler ที่ลบไฟล์จาก ledger อัตโนมัติ

## Backup and restore

backup หนึ่ง restore set อย่างน้อยต้องมี:

- PostgreSQL dump
- `/var/lib/toystore/uploads/files`
- `/var/lib/toystore/keys`
- production configuration ที่เข้ารหัสหรือเก็บในที่ปลอดภัย

staging (`uploads/.staging`) ไม่ต้อง backup แต่ committed media (`uploads/files`) ต้องอยู่ใน restore set เดียวกับ database และ Data Protection keys เพื่อไม่ให้ snapshot เหลื่อมกัน ให้หยุด service ชั่วคราวก่อนเริ่ม และ restart เฉพาะเมื่อทั้ง dump และ archive สำเร็จ:

```bash
backup_id="$(date -u +%Y%m%dT%H%M%S%NZ)"
dump_path="/var/backups/toystore/toystore-${backup_id}.dump"
archive_path="/var/backups/toystore/toystore-${backup_id}-files.tar.gz"

sudo systemctl stop toystore
set -o pipefail
docker compose exec -T postgres pg_dump -U toystore -d toystore -Fc \
  | sudo -u toystore sh -c 'set -C; cat > "$1"' sh "$dump_path" && \
sudo -u toystore tar -C /var/lib/toystore -czf - uploads/files keys \
  | sudo -u toystore sh -c 'set -C; cat > "$1"' sh "$archive_path" && \
sudo systemctl start toystore
```

`backup_id` ระบุเวลา UTC ระดับ nanosecond เพื่อให้แต่ละ restore set มีชื่อใหม่ โดย shell ที่เขียน dump และ archive ใช้ `set -C` เพื่อหยุดแทนการเขียนทับไฟล์เดิม ทั้งสองไฟล์เขียนด้วยบัญชี `toystore` ซึ่งเป็นเจ้าของ `/var/backups/toystore`; ห้ามเปลี่ยนกลับเป็น shell redirection ของ operator หรือ `root` เพราะจะทำให้สิทธิ์ไฟล์ใน backup directory ไม่สม่ำเสมอ

`tar --keep-old-files` เป็นตัวป้องกันการเขียนทับเฉพาะตอน extract ไม่ใช่ตอนสร้าง archive จึงต้องใช้ no-clobber writer ด้านบนสำหรับ backup และต้องใช้ `--keep-old-files` เมื่อทำ restore drill ลง directory ที่เตรียมไว้ เพื่อให้ restore หยุดทันทีหากมีไฟล์เดิมชนกัน

หากคำสั่งใดล้มเหลว ห้าม restart จนกว่าจะบันทึก incident และได้ database dump กับ committed-media/key archive ที่สำเร็จเป็น restore set เดียวกัน PostgreSQL dump รวม `MediaCleanupEntries` โดยอัตโนมัติและต้องอยู่คู่กับ committed files เพราะ reference/cleanup state ต้องตรงกับ filesystem หลัง restore ห้ามลบไฟล์จาก unresolved ledger entry โดยตรง ต้องเปิด fresh context และตรวจทุก persisted media reference ใหม่ก่อนเสมอ

เก็บ production configuration แบบเข้ารหัสร่วมกับ set และเก็บสำเนาอีกชุดนอก server หาก server และ backup อยู่บน disk เดียวกัน ความเสียหายของเครื่องจะทำให้สูญเสียทั้งสองชุด ระบบไม่เพิ่ม scheduler/backup provider ใน application; การกำหนดรอบ backup เป็นงาน operational ของผู้ดูแล server M10-05/M11-08 ยังต้องทำ clean-environment restore drill และ reboot-recovery จริงก่อน launch

## Verification

```bash
systemctl status toystore
journalctl -u toystore -n 200 --no-pager
curl --fail https://toys.example.com/health/live
curl --fail https://toys.example.com/health/ready
docker compose ps
```

ทดสอบ login, product media, cart และ checkout smoke test หลัง deploy ทุกครั้ง รวมถึงทดสอบ restore backup เป็นระยะ
