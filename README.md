# Toy Store

เว็บขายของเล่นและ Art Toy ภาษาไทย สร้างด้วย Blazor Web App บน .NET 10 โดยออกแบบเป็น Modular Monolith สำหรับทีมขนาดเล็กและปริมาณใช้งานเริ่มต้นประมาณ 100 คนต่อวัน

## Technology baseline

- Blazor Web App — Interactive Server แบบ Global
- ASP.NET Core Identity และ Cookie Authentication
- Clean Architecture + Vertical Slice + CQRS ระดับโค้ด
- MediatR และ FluentValidation ใน Application vertical slice เป็น validation หลัก พร้อม map error ราย field เป็นภาษาไทยบน Blazor UI
- EF Core Code First + PostgreSQL + Npgsql; apply pending migrations during application startup
- PostgreSQL รันผ่าน Docker สำหรับ local development
- Web application รันด้วย `dotnet run`
- Local file storage บน application server สำหรับรูปสินค้า
- Production ทั้งระบบ deploy บน Linux server เครื่องเดียว

## Repository status

ขณะนี้ repository มี .NET 10 solution ครบทั้ง 4 application layers กับ 2 test projects และ enforce project dependency direction แล้ว ขั้นถัดไปคือติดตั้ง baseline packages และ foundation services ตาม [Task tracker](TASKS.md) ส่วน [index.html](index.html) ยังคงเป็น visual prototype สำหรับอ้างอิงตอนสร้าง Blazor UI

โครงสร้างเป้าหมาย:

```text
ToyStore.sln
src/
├── ToyStore.Web
├── ToyStore.Application
├── ToyStore.Domain
└── ToyStore.Infrastructure
tests/
├── ToyStore.UnitTests
└── ToyStore.IntegrationTests
```

## Documentation

- [Task tracker](TASKS.md)
- [Agent guide](AGENTS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Domain rules](docs/DOMAIN_RULES.md)
- [UI and design specification](docs/DESIGN_SPEC.md)
- [Local development](docs/LOCAL_DEVELOPMENT.md)
- [Single-server deployment](docs/DEPLOYMENT.md)
- [Delivery roadmap](docs/ROADMAP.md)

## Local development target

เมื่อมีการ scaffold solution แล้ว ให้เริ่ม PostgreSQL ใน Docker และรันเว็บบน host:

```bash
cp .env.example .env
docker compose up -d postgres
dotnet restore ToyStore.sln
dotnet run --project src/ToyStore.Web
```

เมื่อ Web เริ่มทำงาน ระบบจะ apply Code First migration ที่ยังค้างอยู่ก่อนเปิดรับ request

ดูค่าตั้งต้นและคำสั่งทั้งหมดใน [Local development](docs/LOCAL_DEVELOPMENT.md)
