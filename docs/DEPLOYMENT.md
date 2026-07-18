# Single-Server Docker Deployment

## Deployment contract

Production รันบน Ubuntu VPS เครื่องเดียวด้วย Docker Compose ส่วน local development ยังคงรันเฉพาะ PostgreSQL ใน Docker และรัน Web ด้วย `dotnet run`:

```text
GitHub Actions
├── restore / Release build
├── generate idempotent migration SQL
├── build linux/amd64 Web image
├── push ghcr.io/<owner>/<repo>@sha256:<digest>
└── SSH ให้ root-owned deploy command activate image digest

Ubuntu VPS
└── Docker Compose
    ├── Caddy       public :80/:443
    ├── Web         private network + host loopback :5000
    └── PostgreSQL  internal network only
```

ไม่ใช้ `latest` ใน production และไม่ build source บน VPS ทุก deployment อ้าง image ด้วย registry digest ที่เปลี่ยนไม่ได้ PostgreSQL ใช้ named volume ส่วน media, Data Protection keys และ logs ใช้ persistent host directories ซึ่งไม่ถูกแทนที่พร้อม container

## Minimum server

- Ubuntu x64, 2 vCPU, RAM 4 GB และ SSD 40–80 GB
- Docker Engine พร้อม Compose plugin
- `curl`, `flock`, `tar` และ OpenSSH server
- DNS ของ domain ชี้เข้า VPS
- firewall เปิดเฉพาะ SSH, TCP 80, TCP/UDP 443

VPS ไม่ต้องติดตั้ง .NET SDK/runtime หรือ Caddy บน host และห้ามเปิด PostgreSQL port 5432 สู่ host/public interface

## One-time filesystem setup

ติดตั้ง Docker จาก official repository แล้วสร้าง operational users/directories:

```bash
sudo groupadd --system toystore
sudo useradd --system --gid toystore --home /var/lib/toystore --shell /usr/sbin/nologin toystore
sudo useradd --create-home --shell /bin/bash toystore-deploy

sudo install -d -o root -g toystore -m 0750 /opt/toystore /etc/toystore
sudo install -d -o 1654 -g 1654 -m 0750 \
  /var/lib/toystore/uploads \
  /var/lib/toystore/keys \
  /var/lib/toystore/logs
sudo install -d -o toystore -g toystore -m 0750 /var/backups/toystore
```

UID/GID `1654` คือ non-root `app` user ของ official .NET runtime image และต้องเป็นเจ้าของ bind-mounted runtime directories ห้ามวาง uploads, keys หรือ logs ไว้ใน container writable layer

ติดตั้ง production files จาก repository:

```bash
sudo install -o root -g root -m 0644 \
  deploy/compose.production.yaml /opt/toystore/compose.production.yaml
sudo install -o root -g root -m 0644 \
  deploy/Caddyfile /opt/toystore/Caddyfile
sudo install -o root -g root -m 0755 \
  deploy/toystore-deploy /usr/local/sbin/toystore-deploy
```

## Production configuration

สร้าง `/etc/toystore/postgres.env`:

```text
POSTGRES_DB=toystore
POSTGRES_USER=toystore
POSTGRES_PASSWORD=CHANGE_TO_A_LONG_RANDOM_VALUE
```

สร้าง `/etc/toystore/toystore.env` โดยใช้ database password เดียวกัน:

```text
ConnectionStrings__Database=Host=postgres;Port=5432;Database=toystore;Username=toystore;Password=CHANGE_TO_A_LONG_RANDOM_VALUE
Stripe__SecretKey=sk_live_REPLACE_ME
Stripe__PublishableKey=pk_live_REPLACE_ME
Stripe__WebhookSecret=whsec_REPLACE_ME
Stripe__ReturnUrlBase=https://toys.example.com
```

สร้าง `/etc/toystore/compose.env`; ก่อน deployment แรกยังไม่ต้องมี `TOYSTORE_IMAGE`:

```text
TOYSTORE_DOMAIN=toys.example.com
```

ตั้ง permission:

```bash
sudo chown root:root /etc/toystore/postgres.env /etc/toystore/toystore.env
sudo chmod 600 /etc/toystore/postgres.env /etc/toystore/toystore.env
sudo chown root:toystore /etc/toystore/compose.env
sudo chmod 640 /etc/toystore/compose.env
```

Application secrets อยู่บน VPS เท่านั้นและห้าม commit หรือส่งผ่าน GitHub Actions Production Environment Compose กำหนดค่า runtime ที่ไม่เป็น secret ให้แล้ว ได้แก่:

```text
ASPNETCORE_ENVIRONMENT=Production
DataProtection__KeysPath=/var/lib/toystore/keys
Storage__RootPath=/var/lib/toystore/uploads
Serilog__WriteTo__1__Args__path=/var/lib/toystore/logs/toystore-.log
ReverseProxy__KnownProxy=172.30.0.2
```

Caddy ใช้ fixed address `172.30.0.2` ใน private Compose network และ Web เชื่อ forwarded headers จาก address นี้เท่านั้น หากแก้ subnet/address ต้องแก้ `ReverseProxy__KnownProxy` พร้อมกัน ไม่ตั้ง trust-all proxy

## GHCR access on VPS

ถ้า GHCR package เป็น public ไม่ต้อง login ถ้าเป็น private ให้สร้าง token ของ machine account ที่มีเฉพาะ `read:packages` แล้ว login สำหรับ root ซึ่งเป็นผู้รัน deploy command:

```bash
printf '%s' "$GHCR_READ_TOKEN" | sudo docker login ghcr.io \
  --username GITHUB_MACHINE_USER \
  --password-stdin
unset GHCR_READ_TOKEN
```

อย่าใส่ token ใน command history, Compose file หรือ repository

## Manual GitHub Actions deployment

workflow [`.github/workflows/deploy-production.yml`](../.github/workflows/deploy-production.yml) ใช้ `workflow_dispatch` และรับช่อง `branch` ซึ่ง default เป็น `main` ลำดับงานคือ:

1. Checkout exact branch, restore และรัน Release build ของ Web application โดย deployment workflow ไม่รัน test suite
2. สร้าง idempotent EF migration SQL เป็น artifact อายุ 14 วัน
3. Build Docker image สำหรับ `linux/amd64` และ push ไป GHCR ด้วย commit SHA tag
4. ใช้ digest ที่ registry คืนมาเป็น immutable deployment reference
5. SSH เข้า VPS ผ่าน pinned host key และรัน root-owned deploy command
6. ตรวจ local และ public `/health/ready`

สร้าง deploy key แยกจาก personal/root key:

```bash
ssh-keygen -t ed25519 -f ./toystore-deploy-key -C toystore-github-actions
sudo install -d -o toystore-deploy -g toystore-deploy -m 0700 /home/toystore-deploy/.ssh
sudo install -o toystore-deploy -g toystore-deploy -m 0600 \
  ./toystore-deploy-key.pub /home/toystore-deploy/.ssh/authorized_keys
```

เพิ่ม sudo rule ด้วย `sudo visudo -f /etc/sudoers.d/toystore-deploy`:

```text
toystore-deploy ALL=(root) NOPASSWD: /usr/local/sbin/toystore-deploy *
```

script รับ argument เดียวและ validate ว่าต้องเป็น `ghcr.io/...@sha256:<64 lowercase hex>` เท่านั้น deploy user จึงไม่ได้สิทธิ์รัน shell, Docker หรือคำสั่ง root อื่นโดยตรง

สร้าง GitHub Environment ชื่อ `production`:

| Type | Name | Value |
|---|---|---|
| Variable | `VPS_HOST` | domain หรือ IP ของ VPS |
| Variable | `VPS_PORT` | SSH port; ถ้าว่าง workflow ใช้ `22` |
| Variable | `VPS_USER` | `toystore-deploy` |
| Variable | `PRODUCTION_URL` | เช่น `https://toys.example.com` |
| Secret | `VPS_SSH_PRIVATE_KEY` | private deploy key |
| Secret | `VPS_SSH_KNOWN_HOSTS` | verified host-key line ของ VPS |

อ่าน host public key บน VPS ผ่านช่องทางที่เชื่อถือได้และตรวจ fingerprint ก่อนบันทึก ห้ามใช้ `StrictHostKeyChecking=no`

จากนั้นเปิด GitHub → Actions → **Deploy production** → **Run workflow**, ใส่ `main` และกด deploy

## Deploy, backup and rollback behavior

`/usr/local/sbin/toystore-deploy` ทำงานภายใต้ `flock` เพื่อห้าม deploy ซ้อนกัน:

1. ตรวจ immutable GHCR digest และ production files
2. Start/ตรวจ PostgreSQL และ pull image ก่อนเกิด downtime
3. หยุด Web ชั่วคราวเพื่อหยุด writes
4. สร้าง PostgreSQL custom dump และ archive ของ uploads/keys เป็น restore set เดียวกัน
5. เปลี่ยน `TOYSTORE_IMAGE` ใน `compose.env` แบบ atomic
6. `docker compose up -d --remove-orphans`
7. Web apply `Database.MigrateAsync()` ก่อนรับ request และ deploy รอ `/health/ready` สูงสุด 120 วินาที
8. หากไม่ ready ให้คืน image digest เดิมและ start container เดิม

Application rollback ไม่ย้อน database migration อัตโนมัติ Migration production จึงต้อง backward-compatible เมื่อทำได้ หาก schema ไม่เข้ากับ image เดิม ให้หยุดระบบและใช้ forward fix หรือ restore database/media/key set ที่ script รายงาน ห้าม restore database อย่างเดียวเพราะ media reference และ `MediaCleanupEntries` ต้องตรงกับ committed files

backup ทุกชุดอยู่ที่ `/var/backups/toystore/<UTC timestamp>-<digest prefix>/` และประกอบด้วย:

- `database.dump`
- `files-and-keys.tar.gz` ซึ่งมีเฉพาะ committed `uploads/files` และ Data Protection `keys`; ไม่รวม `.staging`

ต้องเข้ารหัส/สำรอง `/etc/toystore/*.env` แยกใน secret storage และคัดลอก backup ออกนอก VPS เป็นระยะ เพราะ backup บน disk เดียวไม่ป้องกัน server/disk loss ก่อน launch ต้องทำ clean restore drill ตาม M10-05

## First Admin bootstrap

หลัง deployment แรก export temporary secrets โดยไม่ใส่ password ใน command argument แล้วรัน one-off Web container:

```bash
export BootstrapAdmin__Email='admin@example.com'
export BootstrapAdmin__TemporaryPassword='CHANGE_TO_A_TEMPORARY_PASSWORD'
sudo --preserve-env=BootstrapAdmin__Email,BootstrapAdmin__TemporaryPassword \
  docker compose \
    --env-file /etc/toystore/compose.env \
    -f /opt/toystore/compose.production.yaml \
    run --rm \
    -e BootstrapAdmin__Email \
    -e BootstrapAdmin__TemporaryPassword \
    web --bootstrap-admin
unset BootstrapAdmin__Email BootstrapAdmin__TemporaryPassword
```

คำสั่งนี้สร้าง Admin คนแรกได้ไม่เกินหนึ่งคนและ Admin ต้องเปลี่ยน temporary password ตอน login ครั้งแรก

## Verification and operations

```bash
sudo docker compose \
  --env-file /etc/toystore/compose.env \
  -f /opt/toystore/compose.production.yaml ps

sudo docker compose \
  --env-file /etc/toystore/compose.env \
  -f /opt/toystore/compose.production.yaml logs --tail 200 web caddy postgres

curl --fail https://toys.example.com/health/live
curl --fail https://toys.example.com/health/ready
```

Docker restart policies ทำให้ Caddy, Web และ PostgreSQL กลับมาหลัง Docker daemon/VPS reboot ต้องทดสอบ reboot recovery, login, media, cart, checkout, Stripe webhook และ Order smoke test บน production/test domain ก่อนเปิดใช้งานจริง
