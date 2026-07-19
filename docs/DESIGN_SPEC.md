# Storefront Design Specification

## 1. Source of truth

[index.html](../index.html) คือ visual reference หลักของ storefront ในช่วงก่อนสร้าง Blazor UI ให้เปิดไฟล์นี้เพื่อดู composition, spacing, responsive behavior, hover และ transition

Business/UI flows ของ Catalog, Checkout และ Admin อ้างอิง [Commerce Platform Design](superpowers/specs/2026-07-17-commerce-platform-design.md) เอกสารนี้แยก theme ของ Storefront ที่สร้างเสร็จแล้วออกจาก Admin อย่างชัดเจน

ไฟล์นี้เป็น reference เท่านั้น เมื่อสร้าง production UI ให้:

- แยกเป็น reusable Razor components
- ย้าย CSS ไปยัง stylesheet หรือ component-scoped CSS ตามความเหมาะสม
- ใช้ URL รูปสินค้าจาก query handler และ local media storage ของระบบ
- ไม่คัดลอก base64 image จาก prototype ไปใช้ใน Razor

## 2. Design direction

บุคลิกของ Storefront คือ bold, playful, collectible และ premium โดยใช้ bold monochrome โทนดำ/เทาและสี lime เป็น accent ตาม M3 ที่สร้างและ verify เสร็จแล้ว ห้ามเปลี่ยน Storefront เป็น Muted Ocean จาก requirement ของ Admin

หลักการ:

- ใช้ contrast สูงและพื้นที่ว่างมาก
- หัวข้อหนักและกระชับ
- รูปสินค้าเป็นจุดเด่นกว่ากรอบตกแต่ง
- ใช้มุมโค้งเล็กถึงปานกลาง
- motion ต้องให้ความรู้สึกตอบสนอง ไม่รบกวนการอ่าน
- ไม่มี left navigation rail บน storefront

## 3. Typography

ใช้ Noto Sans Thai ทั้งระบบ:

```css
font-family: "Noto Sans Thai", system-ui, -apple-system, sans-serif;
```

โหลดจาก self-hosted `.woff2` ใน production เพื่อควบคุม performance และ CSP; Google Fonts ใน `index.html` ใช้สำหรับ prototype เท่านั้น

| Token | Desktop | Mobile | Weight | Usage |
|---|---:|---:|---:|---|
| Display | 64–72px | 44–52px | 900 | Hero headline |
| H1 | 48px | 38px | 800 | Page title |
| H2 | 30–36px | 26–30px | 800 | Major section |
| H3 | 18–22px | 17–20px | 700 | Card/group title |
| Body large | 17–18px | 16px | 400–500 | Intro copy |
| Body | 15–16px | 14–15px | 400 | Main content |
| Label | 12–13px | 12px | 700–800 | Eyebrow, nav, CTA |
| Caption | 11–12px | 11px | 400–600 | Metadata |

กฎการอ่าน:

- Customer-facing body text ห้ามต่ำกว่า 14px
- Line height เนื้อหา 1.5–1.7
- Thai heading ใช้ letter spacing ปกติหรือค่าติดลบเพียงเล็กน้อย; ห้ามบีบตัวอักษรจนสระชนกัน
- ใช้เลขอารบิกสำหรับราคา จำนวน และวันที่ เว้นแต่ content requirement ระบุอื่น

## 4. Color tokens

```css
:root {
  --color-bg: #f8f8f6;
  --color-surface: #ffffff;
  --color-ink: #111111;
  --color-muted: #686864;
  --color-line: #dededb;
  --color-accent: #dfff29;
  --color-danger: #b42318;
  --color-success: #16794a;
  --color-focus: #2563eb;
}
```

- ใช้ accent กับ primary CTA, badge หรือจุดเน้นสั้น ๆ ไม่ใช้กับข้อความยาว
- ข้อความปกติต้องผ่าน WCAG AA บนพื้นหลังที่ใช้งาน
- สถานะห้ามสื่อด้วยสีอย่างเดียว ต้องมี label หรือ icon ประกอบ

## 5. Layout system

```text
Maximum content width: 1220px
Desktop gutter: 32px each side
Tablet gutter: 24px each side
Mobile gutter: 14–20px each side
Base spacing unit: 4px
Section spacing: 44–72px
Card gap: 12–24px
```

Breakpoints เริ่มต้น:

| Name | Width | Behavior |
|---|---:|---|
| Mobile | `< 560px` | 1 large product column, 1 collection column |
| Tablet | `560–899px` | 2 product columns, 2 collection columns |
| Large | `900–1279px` | 3 product columns and condensed navigation when needed |
| Larger | `>= 1280px` | 4 standard product columns; Catalog with sidebar keeps 3 large-card columns |

ใช้ content-driven responsive behavior; breakpoint เหล่านี้เป็น baseline ไม่ใช่ข้อจำกัดตายตัว

## 6. Component map

```text
StoreShell
├── StoreHeader
│   ├── BrandMark
│   ├── PrimaryNavigation
│   └── HeaderActions
├── HeroShowcase
│   └── PreOrderEditorialCarousel (หนึ่งสินค้าต่อสไลด์ สูงสุด 5, auto 3 วินาที, swipe/controls/pause)
├── ProductSection
│   └── ProductCard[]
├── CollectionSection
│   └── CollectionCard[]
├── JournalFeature
│   └── JournalStory[]
├── TrustBenefits
└── StoreFooter
```

- Shared `ProductCard` ในหน้า list ใช้ thumbnail แนวตั้งอัตราส่วน 4:5, แสดงเฉพาะ sale-type badge โดยไม่แสดง model scale; status ใช้ class แยก ขนาด 1.2rem น้ำหนัก 300 และชื่อสินค้าใช้ 1rem น้ำหนัก 300 แสดงบรรทัดเดียวพร้อม ellipsis เฉพาะ ProductCard ส่วน Product Detail ยังคงแสดงชื่อเต็มและ model scale ใน facts เมื่อมีค่า
- Shared `ProductCard` บนหน้า Home/Catalog/Search แสดงรูปสินค้าทั้งหมดจาก list query และมีลูกศรซ้าย–ขวาแบบ icon-only ไม่มีพื้นหลัง/เส้นขอบเมื่อมีมากกว่า 1 รูป โดยยังคงพื้นที่กดอย่างน้อย 44px ใช้ได้ทั้ง touch, mouse และ keyboard; บน mobile ปัดซ้าย–ขวาบนรูปเพื่อเปลี่ยนภาพได้โดยยังเลื่อนหน้าแนวตั้งได้ และ gesture จะไม่เปิด Product Detail โดยไม่ตั้งใจ
- ส่วนสินค้าแนะนำบน Home แสดงสูงสุด 8 รายการ และ Catalog แบ่งหน้า 8 รายการ; shared product grid เรียงตามขนาดจอเป็น mobile 2 คอลัมน์, tablet 3 คอลัมน์ และ desktop ขนาดใหญ่ 4 คอลัมน์ รวมถึง Catalog ที่ใช้ large-card mode เพื่อให้แนวการ์ดพอดีกับพื้นที่หน้าจอ
- Storefront container รองรับความกว้างสูงสุด 112rem เพื่อใช้พื้นที่บนจอ desktop/2K โดยยังคง responsive gutters และเต็มความกว้างอย่างปลอดภัยบนจอขนาดเล็ก
- Home hero แสดงเฉพาะ Product ประเภท Pre-order จาก query แยก สูงสุด 5 รายการ ครั้งละ 1 ภาพ/สินค้า ใช้ editorial split layout ที่แตกต่างจาก ProductCard, auto-slide ทุก 3 วินาทีเมื่ออยู่ใน viewport และผู้ใช้ไม่ได้ hover/focus พร้อมปุ่มก่อนหน้า/ถัดไป จุดเลือกสไลด์ ปุ่มหยุด และ native touch swipe; ปิด autoplay เมื่อ `prefers-reduced-motion: reduce`
- ภาพ Spotlight บนมือถือยังใช้ `object-fit: cover` แต่ยก focal point ขึ้นที่แนวตั้งประมาณ 20% เพื่อให้ภาพ figure แนวตั้งเห็นศีรษะและช่วงลำตัวแทนการ crop จากกึ่งกลาง; tablet/desktop คง composition เดิม
- Product Detail ใช้ shared expandable text กับคำอธิบายยาว: collapsed 3 บรรทัดแล้ว fade จากโปร่งใสไป 50% ก่อนปุ่ม `อ่านเพิ่มเติม`; expanded แสดงข้อความทั้งหมดโดยไม่มี fade และย่อกลับได้ด้วย keyboard-accessible button
- Product Detail purchase column ใช้ sticky offset จาก shared `--store-header-height` และเว้นช่องว่างเพิ่ม เพื่อให้ชื่อ ราคา สถานะ และปุ่มซื้อหยุดอยู่ใต้ sticky Store Header เสมอ; จอ mobile ยังคง flow ปกติ
- Product Detail mobile เรียงข้อมูลเป็น gallery → ชื่อ/ราคา/สถานะ/CTA → รายละเอียด → facts พร้อม section spacing และเส้นแบ่งที่อ่านง่าย; หน้า Pre-order ไม่แสดง warning card `นโยบายมัดจำ` ซ้ำใน purchase section แต่ยังคงการยอมรับเงื่อนไขในขั้นตอนตรวจสอบพรีออเดอร์
- Product Detail ใช้ app-like hierarchy ทั้ง mobile/desktop: mobile gallery เต็มความกว้างอัตราส่วนแนวตั้งพร้อม dot selector ทับภาพ, purchase summary กึ่งกลาง, CTA สีดำทรง pill, trust/delivery facts จากระบบจริง และ disclosure แยก `รายละเอียดสินค้า`/`ข้อมูลสินค้า`; desktop คง gallery ซ้ายกับ sticky purchase panel ขวาโดยไม่สร้าง rating, variant, wishlist หรือ Buy Now ที่ v1 ไม่มี
- Storefront mobile ใช้ app-like bottom navigation 5 จุด (หน้าหลัก, สินค้า, ตะกร้า, พรีออเดอร์, บัญชี) พร้อม active state, cart badge, iPhone safe-area และซ่อนระหว่าง checkout; mobile header แสดงโลโก้กึ่งกลางโดยไม่มี hamburger ซ้ำ ส่วน desktop คง header navigation เดิม
- แท็บ `สินค้า` mobile ไปที่ route `/brands` และบังคับ flow แบบเลือกแบรนด์ก่อน จากนั้นจึงไป `/brands/{slug}` เพื่อดูสินค้าพร้อมส่งและพรีออเดอร์ของแบรนด์นั้น; `/search` คงเป็นหน้า search/navigation hub แยกและไม่รวม brand directory กับ product results ไว้ใน section เดียวกัน
- Catalog/Search mobile ใช้หัวเรื่องกะทัดรัด, search discovery ภาษาไทย, quick links พรีออเดอร์/พร้อมส่ง, filter pill และ toggle แสดงสินค้าแบบกริด 2 คอลัมน์หรือภาพใหญ่ 1 คอลัมน์จริง โดยไม่สร้าง sort/filter ที่ backend ไม่รองรับ
- Cart mobile เป็น full-screen drawer พร้อม item controls, order summary และ CTA `ชำระเงินอย่างปลอดภัย` แบบ pill ที่ฐานจอ; In-stock/Pre-order checkout ใช้ flat mobile sheet, field ขนาดอ่านง่าย, summary และ sticky submit แต่คง Stripe Embedded และ commerce invariants เดิม
- Cart แสดง action `ช้อปปิ้งต่อ →` แบบ ghost แนวนอนชิดขวาทั้ง mobile/desktop หลังเพิ่มสินค้า โดยปิด drawer และกลับไป `/brands/{slug}` ของสินค้าที่เพิ่งเพิ่มจาก BrandSlug ที่ server อ่านจริง; fallback ไป `/brands` เมื่อไม่มีปลายทางที่ใช้ได้ และ CTA ชำระเงินอยู่เต็มแถวแยกด้านล่าง

Production routes อย่างน้อย:

```text
Storefront: /, /products, /products/{slug}, /categories/{slug}, /search
Commerce: /cart, /checkout, /account/orders, /account/orders/{number}
Admin: /admin, /admin/products, /admin/inventory, /admin/orders
```

Admin routes เพิ่มเติมคือ `/admin/brands`, `/admin/universes`, `/admin/notifications`, `/admin/reports` และ `/admin/settings`; Category ไม่มี management route ใน v1

## 7. Component states

ทุก data component ต้องออกแบบ state ต่อไปนี้:

- Loading: ใช้ skeleton ที่รักษาขนาด layout เพื่อลด layout shift
- Empty: อธิบายสถานะและเสนอ next action
- Error: ข้อความไทยที่เข้าใจง่าย พร้อม retry เมื่อทำได้
- Disabled: แสดงเหตุผลสำหรับ action สำคัญ เช่นสินค้าหมด
- Success: feedback ชัดเจนแต่ไม่บัง flow

Product card ต้องรองรับ normal, hover/focus, out of stock, pre-order, sale และ loading; In-stock ใช้ปุ่มเพิ่มตะกร้าที่มี accessible name/feedback ส่วน Pre-order ใช้ direct-checkout action และห้ามเพิ่มเข้า cart

## 8. Motion

ใช้ motion token:

```css
--duration-fast: 160ms;
--duration-normal: 260ms;
--duration-slow: 600ms;
--ease-standard: cubic-bezier(.2,.8,.2,1);
```

- Hover lift: 2–7px และ scale ไม่เกิน 1.03
- Button press: scale ประมาณ 0.97
- Scroll reveal ใช้ครั้งเดียวและไม่ซ่อนเนื้อหาหาก JavaScript ล้มเหลว
- Parallax ใช้เฉพาะ pointer แบบละเอียดและไม่เกิน 10px
- หลีกเลี่ยง animation ที่กระตุ้น layout; ใช้ `transform` และ `opacity`
- รองรับ `prefers-reduced-motion: reduce` เสมอ

## 9. Accessibility

- ใช้ semantic landmarks: `header`, `nav`, `main`, `section`, `footer`
- รองรับ keyboard navigation ทุก interactive element
- Focus ring ต้องเห็นชัดและไม่ถูกลบ
- Target size อย่างน้อย 44×44px สำหรับ mobile actions หลัก
- รูปสินค้ามี alt text ภาษาไทยที่บอกสินค้า ไม่ใส่คำว่า “รูปภาพของ”
- Icon-only button ต้องมี `aria-label`
- Form มี label, validation summary และ error ที่ผูกด้วย `aria-describedby`
- Modal/drawer ต้องจัดการ focus trap และคืน focus เมื่อปิด

### Shared form controls

- สร้าง reusable Blazor components สำหรับ field wrapper, text input, number field, select/dropdown, validation message, alert, dialog และ confirmation เมื่อ behavior/style ใช้ซ้ำหรือเกิดตั้งแต่ 2 จุดขึ้นไป
- ทุก field รองรับ label, required marker, help text, disabled/read-only, loading, Thai field error, `aria-describedby` และ visible focus state ด้วย API รูปแบบเดียวกัน
- Select/dropdown ต้องใช้ `appearance: none`, ลูกศรของระบบ design, token สำหรับ background/focus/error และทดสอบ desktop/mobile เพื่อไม่แสดง default browser styling
- Number field ต้องกำหนด `min`, `max`, `step`, input mode และป้องกันค่าที่ server ไม่ยอมรับ โดย server ยังคำนวณและตรวจซ้ำเสมอ
- FluentValidation validator ใน Application slice เป็น validation หลัก UI component มีหน้าที่แสดงผล field errors/summary และอาจมี presentation hint ที่ไม่ขัดกับ validator
- Component ที่ซับซ้อนและใช้ซ้ำ เช่น modal, drawer, pagination, table state, badge, upload/reorder และ autocomplete ให้แยกเป็น shared component; ไม่แยก markup เล็ก ๆ ที่ไม่มี behavior จน API กระจัดกระจาย

## 10. Thai content style

- ใช้ภาษากระชับ เป็นมิตร และมั่นใจ
- ใช้ “บาท” ในข้อความทั่วไป และ `฿` เมื่อพื้นที่จำกัด
- แยกข้อความ UI ออกจาก component เพื่อพร้อมใช้ localization
- ข้อความ error บอกสิ่งที่เกิดขึ้นและสิ่งที่ผู้ใช้ทำต่อได้
- ไม่ผสมคำอังกฤษโดยไม่จำเป็น ยกเว้นชื่อศิลปิน แบรนด์ และชื่อคอลเลกชัน

## 11. Blazor implementation notes

- ใช้ SSR output ที่มีเนื้อหาหลักก่อน hydration
- ใช้ `@key` กับรายการที่เปลี่ยนแปลง
- หลีกเลี่ยง interop สำหรับ interaction ที่ทำด้วย CSS ได้
- ยกเลิก async work เมื่อ component ถูก dispose และใช้ request generation/version ป้องกัน response เก่าทับ URL/navigation state ใหม่
- แยก presentation model ออกจาก Domain entity
- ไม่ query database จาก Razor component โดยตรง; ส่ง Query ผ่าน `ISender`
- รับ FluentValidation failures จาก Application แล้วผูกกลับไปยัง field และ validation summary เป็นภาษาไทย; ห้ามปล่อย validation exception กลายเป็น HTTP 500

## 12. Admin design system — Muted Ocean

Admin ใช้ borderless `Muted Ocean` แยกจาก Storefront:

```css
:root {
  --admin-accent: #3f91b8;
  --admin-accent-strong: #2f789b;
  --admin-accent-soft: #eaf6fb;
  --admin-bg: #f5f8fa;
  --admin-surface: #ffffff;
  --admin-ink: #15212a;
  --admin-muted: #667782;
}
```

- ใช้ soft surface contrast, whitespace และ restrained shadow สร้าง hierarchy; ไม่ใช้ harsh/heavy border รอบ card, button หรือ layout
- สีเขียวใช้เฉพาะ semantic success ที่อ่านง่าย ไม่ใช้เป็น navigation accent; warning/danger ต้องมีข้อความหรือ icon ไม่สื่อด้วยสีอย่างเดียว
- Noto Sans Thai, visible focus, reduced motion, target 44×44px และ shared form/feedback components ใช้ร่วมกับ Storefront
- transition 160–260ms สำหรับ hover/focus/rail/dialog/drawer และห้ามซ่อนข้อมูลเมื่อ motion/JavaScript ใช้งานไม่ได้

### Admin navigation

- Global left rail: Dashboard, Catalog, Inventory, Orders, Notifications, Sales Reports, Settings และ Logout
- Rail ยุบเป็น icon mode ได้และขยายเมื่อ pin/hover/focus; collapsed icon มี Thai accessible name และ tooltip; badge แสดงเฉพาะ actionable count
- Mobile ใช้ menu button + shared drawer พร้อม focus trap และคืน focus
- Contextual top pills ไม่ทำหน้าที่ซ้ำ rail เช่น Catalog มี Products/Brands/Universes และ Orders มี All/In-stock/Pre-order/ReadyToShip/Shipped/Cancelled

### Admin page patterns

- Dashboard ใช้ cards/graphs/operational queues ตามข้อมูลจริงและ timezone `Asia/Bangkok`
- Product, Brand และ Universe list ใช้ shared table state, filter, pagination, badge และ empty/error/loading state
- Product Create/Edit ใช้ large modal บนหน้า list; mobile เป็น full-screen dialog; แถว Draft และ Published ต้องมีปุ่มแก้ไขที่มองเห็นได้ โดย Published บันทึกแล้วอัปเดตหน้าร้านทันทีและ Published Pre-order ล็อกวันปิดรอบ/จำนวนรับทั้งหมด; Brand/Universe ใช้ modal pattern เดียวกัน
- Product form ใช้ conditional In-stock/Pre-order fields, shared upload/reorder และ searchable Character autocomplete; inline create Character ได้
- Character autocomplete เป็น shared controlled EditContext field: เลือกหลายค่าเป็น removable chips, debounce 250 ms, รองรับ Thai/Japanese IME, keyboard/active-descendant และ mouse โดยไม่ใช้ native `<select>`; Universe เป็น owner ที่เปลี่ยนแล้วต้อง cancel request เก่าและล้าง selection ที่ไม่เข้ากัน ผล exact match และสิทธิ์ inline create มาจาก Application เท่านั้น
- listbox/pseudo-option ต้องใช้ ARIA combobox/listbox/option ที่สัมพันธ์กัน, ประกาศ loading/result/error ผ่าน live region, ปิดและ invalidate pending search เมื่อ blur/Escape/Tab โดย Tab ห้ามถูก trap และ external FluentValidation ต้องอัปเดตทั้งข้อความกับ `aria-invalid`
- Stock receive/adjust ใช้ modal แยกพร้อม reason; Order detail ใช้ full routeเพื่อรองรับ snapshot, payment, shipment, audit และ notification history

### Brand and Universe management

- ทุก visible copy, loading/empty/error/success state, validation, toast และ confirmation เป็นภาษาไทยบน borderless Muted Ocean
- List state อยู่ใน URL ด้วย `q`, `status=active|archived|all`, `page`; omit default Active/page 1, ใช้ page size 20 และ browser back/forward ต้อง restore controls
- Brand table แสดง image, display/English names, read-only non-link `ส่วน URL (slug)`, lifecycle/readiness, Product count จริงและเวลา `Asia/Bangkok`
- Universe table ใช้ pattern เดียวกันแต่แสดง Product/Character counts แยกกันและใช้ readiness `ต้องเพิ่มโลโก้` สำหรับ seed ที่ยังไม่มี logo
- Create/edit อยู่ใน same-page modal; mobile เป็น full-screen dialog Field มี display name, English name, current image/logo, local blob preview และ read-only generated slug โดยไม่ upload ก่อน submit
- Edit Universe ที่ยังไม่มี logo ต้องเลือกรูป ส่วน row ที่มี media แล้วเก็บรูปเดิมหรือ replace ได้ ไม่มี remove-only action
- Archive dialog เริ่ม focus ที่ “ยกเลิก” อธิบายว่า action เป็น terminal และแสดง Product/Character reference impact พร้อมยืนยันว่า media/references เดิมยังคงอยู่
- Busy editor/archive ปิด Escape/close และ double-submit; duplicate/stale/commit-unknown failure ต้องรักษา input และ blob preview
- Success ใช้ Thai toast, reload authoritative list และคืน focus ไป connected opener หรือ `h1` เมื่อ row ถูก filter/reorder ออกจากหน้า
- Query navigation/disposal ต้อง cancel request เดิมและตรวจ generation ก่อน apply response เพื่อไม่ให้ state เก่าทับ state ใหม่
