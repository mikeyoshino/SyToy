#!/usr/bin/env python3
"""M6-05 disposable real-Chrome verification for the Storefront Pre-order flow.

Run from the repository root:

    python3 scripts/storefront-preorder-browser.py

The harness creates and removes its own migrated PostgreSQL database, application,
Chrome profile, media directory and customer. Its JSON report contains no secrets.
"""

from __future__ import annotations

import argparse
import datetime as dt
import importlib.util
import json
from pathlib import Path
import sys
import time
from typing import Any


ROOT = Path(__file__).resolve().parent.parent
ADMIN_HARNESS = ROOT / "scripts" / "admin-catalog-browser.py"
DEFAULT_REPORT = ROOT / "artifacts" / "browser" / "storefront-preorder-browser-report.json"


def load_harness() -> Any:
    spec = importlib.util.spec_from_file_location("admin_catalog_browser", ADMIN_HARNESS)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the retained disposable Chrome harness")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


HARNESS = load_harness()


class PreOrderSmoke(HARNESS.AdminShellSmoke):
    def __init__(self, cdp: Any, environment: Any, timeout: float) -> None:
        super().__init__(cdp, environment.base_url, timeout)
        self.environment = environment
        self.stage = "initialization"

    def key(self, name: str) -> None:
        codes = {"Escape": (27, "Escape")}
        virtual, code = codes[name]
        params = {
            "key": name, "code": code,
            "windowsVirtualKeyCode": virtual, "nativeVirtualKeyCode": virtual,
        }
        self.cdp.call("Input.dispatchKeyEvent", {"type": "keyDown", **params})
        self.cdp.call("Input.dispatchKeyEvent", {"type": "keyUp", **params})

    def click_text(self, selector: str, text_value: str) -> None:
        clicked = self.cdp.evaluate(
            "(() => { const e=[...document.querySelectorAll(" + json.dumps(selector) + ")]"
            ".find(x=>x.textContent.trim().includes(" + json.dumps(text_value) + "));"
            "if(!e)return false;e.focus();e.click();return true})()"
        )
        if not clicked:
            raise HARNESS.VerificationFailure("Expected Storefront action was not found")

    def seed_products(self) -> None:
        sql = """
INSERT INTO "Brands" ("Id","DisplayName","NormalizedDisplayName","EnglishName","NormalizedEnglishName","Slug","Status","CreatedAtUtc","CreatedBy","UpdatedAtUtc","UpdatedBy","Version") VALUES
('b0000000-0000-0000-0000-000000000001','แบรนด์พรีออเดอร์','แบรนด์พรีออเดอร์','Preorder Brand','PREORDER BRAND','preorder-brand','Active','2026-07-01T00:00:00Z','browser','2026-07-01T00:00:00Z','browser',1);

INSERT INTO "Products" ("Id","DisplayName","NormalizedDisplayName","EnglishName","NormalizedEnglishName","Description","Slug","ProductCategoryId","BrandId","UniverseId","SaleType","Status","InStockPrice","PreOrderFullPrice","PreOrderDepositAmount","PreOrderCloseAtUtc","PreOrderEstimatedArrivalMonth","PreOrderEstimatedArrivalYear","PreOrderTotalCapacity","PreOrderMaxPerCustomer","PreOrderBalancePaymentDays","CreatedAtUtc","CreatedBy","UpdatedAtUtc","UpdatedBy","PublishedAtUtc","PublishedBy","Version") VALUES
('a0000000-0000-0000-0000-000000000001','อาร์ตทอยพรีออเดอร์','อาร์ตทอยพรีออเดอร์','Open Preorder','OPEN PREORDER','รายละเอียดพรีออเดอร์สำหรับทดสอบ','browser-open-preorder','10000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','PreOrder','Published',NULL,2500,500,'2026-12-31T16:59:59Z',12,2026,4,3,7,'2026-07-01T00:00:00Z','browser','2026-07-02T00:00:00Z','browser','2026-07-02T00:00:00Z','browser',2),
('a0000000-0000-0000-0000-000000000002','พรีออเดอร์ปิดแล้ว','พรีออเดอร์ปิดแล้ว','Closed Preorder','CLOSED PREORDER','รายละเอียดพรีออเดอร์ปิดแล้ว','browser-closed-preorder','10000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','PreOrder','Published',NULL,3000,600,'2026-07-16T16:59:59Z',12,2026,2,2,7,'2026-07-01T00:00:00Z','browser','2026-07-02T00:00:00Z','browser','2026-07-02T00:00:00Z','browser',2),
('a0000000-0000-0000-0000-000000000003','พรีออเดอร์เต็มแล้ว','พรีออเดอร์เต็มแล้ว','Full Preorder','FULL PREORDER','รายละเอียดพรีออเดอร์เต็มแล้ว','browser-full-preorder','10000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','PreOrder','Published',NULL,3500,700,'2026-12-31T16:59:59Z',12,2026,1,1,7,'2026-07-01T00:00:00Z','browser','2026-07-02T00:00:00Z','browser','2026-07-02T00:00:00Z','browser',2);

INSERT INTO "ProductImages" ("Id","StorageKey","PublicRelativeUrl","AltText","SortOrder","IsPrimary","ProductId") VALUES
('d0000000-0000-0000-0000-000000000001','browser/open.webp','/media/browser/open.webp','ภาพพรีออเดอร์เปิด',0,true,'a0000000-0000-0000-0000-000000000001'),
('d0000000-0000-0000-0000-000000000002','browser/closed.webp','/media/browser/closed.webp','ภาพพรีออเดอร์ปิด',0,true,'a0000000-0000-0000-0000-000000000002'),
('d0000000-0000-0000-0000-000000000003','browser/full.webp','/media/browser/full.webp','ภาพพรีออเดอร์เต็ม',0,true,'a0000000-0000-0000-0000-000000000003');

INSERT INTO "PreOrderCapacities" ("Id","ProductId","TotalCapacity","HeldQuantity","CommittedQuantity","RetiredQuantity","CloseAtUtc","CreatedAtUtc","CreatedBy","UpdatedAtUtc","UpdatedBy","Version") VALUES
('c0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001',4,0,0,0,'2026-12-31T16:59:59Z','2026-07-02T00:00:00Z','browser','2026-07-02T00:00:00Z','browser',1),
('c0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000002',2,0,0,0,'2026-07-16T16:59:59Z','2026-07-02T00:00:00Z','browser','2026-07-02T00:00:00Z','browser',1),
('c0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003',1,1,0,0,'2026-12-31T16:59:59Z','2026-07-02T00:00:00Z','browser','2026-07-10T00:00:00Z','browser',2);

INSERT INTO "PreOrderCapacityReservations" ("Id","CapacityId","ProductId","CheckoutAttemptId","CustomerId","Quantity","ReservedAtUtc","ExpiresAtUtc","ReserveMovementId","ReserveReason","ReserveReference","ReservedBy","Status") VALUES
('e0000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003','f0000000-0000-0000-0000-000000000003','browser-full',1,'2026-07-10T00:00:00Z','2026-12-30T00:00:00Z','90000000-0000-0000-0000-000000000004','จองทดสอบ','browser-full','browser','Active');

INSERT INTO "PreOrderCapacityMovements" ("Id","CapacityId","ProductId","Type","Quantity","AvailableQuantityDelta","ResultingRemainingQuantity","ResultingHeldQuantity","ResultingCommittedQuantity","ResultingRetiredQuantity","ResultingCapacityVersion","Reason","Reference","Actor","OccurredAtUtc","ReservationId") VALUES
('90000000-0000-0000-0000-000000000001','c0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','InitialCapacity',4,4,4,0,0,0,1,'เปิดรอบ','browser-open','browser','2026-07-02T00:00:00Z',NULL),
('90000000-0000-0000-0000-000000000002','c0000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000002','InitialCapacity',2,2,2,0,0,0,1,'เปิดรอบ','browser-closed','browser','2026-07-02T00:00:00Z',NULL),
('90000000-0000-0000-0000-000000000003','c0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003','InitialCapacity',1,1,1,0,0,0,1,'เปิดรอบ','browser-full','browser','2026-07-02T00:00:00Z',NULL),
('90000000-0000-0000-0000-000000000004','c0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000003','Reserved',1,-1,0,1,0,0,2,'จองทดสอบ','browser-full','browser','2026-07-10T00:00:00Z','e0000000-0000-0000-0000-000000000003');
"""
        self.environment._psql(sql, database=self.environment.database)

    def reservation_count(self) -> int:
        command = self.environment._psql_command(
            'SELECT COUNT(*) FROM "PreOrderCapacityReservations";',
            self.environment.database,
        )
        import subprocess
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, check=True)
        lines = [line.strip() for line in result.stdout.splitlines() if line.strip().isdigit()]
        if not lines:
            raise HARNESS.VerificationFailure("Could not read Pre-order reservation count")
        return int(lines[-1])

    def assert_responsive_detail(self) -> None:
        for width, height in ((390, 844), (768, 900), (1200, 900)):
            self.set_viewport(width, height)
            metrics = self.cdp.evaluate(
                "({overflow:document.documentElement.scrollWidth>document.documentElement.clientWidth,"
                "thai:document.body.innerText.includes('ราคาเต็ม')&&document.body.innerText.includes('มัดจำ')"
                "&&document.body.innerText.includes('ยอดคงเหลือ')&&document.body.innerText.includes('ประมาณเดือนธันวาคม 2026'),"
                "cart:document.body.innerText.includes('เพิ่มลงตะกร้า')})"
            )
            self.check(f"{width}px Thai Pre-order detail fits without cart or body overflow",
                       not metrics["overflow"] and metrics["thai"] and not metrics["cart"], metrics)

    def register_customer(self) -> None:
        email = f"preorder-{int(time.time() * 1000)}@example.invalid"
        password = "BrowserPreorder9A!"
        self.click_text("a", "สมัครสมาชิก")
        self.cdp.wait("location.pathname.toLowerCase()==='/account/register'", self.timeout)
        self.fill("#register-email", email)
        self.fill("#register-password", password)
        self.fill("#register-confirm-password", password)
        self.click_text("button", "สมัครสมาชิก")
        try:
            self.cdp.wait("location.pathname==='/products/browser-open-preorder'", self.timeout * 2)
        except Exception:
            state = self.cdp.evaluate(
                "({path:location.pathname,search:location.search,heading:document.querySelector('h1')?.innerText,"
                "feedback:[...document.querySelectorAll('[role=alert],.status-message')].map(x=>x.innerText).filter(Boolean)})"
            )
            self.check("customer registration returns to the local Product intent", False, state)

    def run_preorder(self) -> None:
        self.cdp.call("Runtime.enable")
        self.cdp.call("Page.enable")
        self.cdp.call("DOM.enable")
        self.cdp.call("Network.enable")
        self.cdp.call("Network.clearBrowserCookies")
        self.seed_products()

        self.stage = "anonymous-detail"
        self.navigate("/products/browser-open-preorder")
        self.cdp.wait("document.querySelector('h1')?.textContent.includes('อาร์ตทอยพรีออเดอร์')", self.timeout)
        self.assert_responsive_detail()
        time.sleep(0.7)
        self.click_text("button", "ตรวจสอบสิทธิ์พรีออเดอร์")
        self.cdp.wait("location.pathname.toLowerCase()==='/account/login'", self.timeout)
        return_url = self.cdp.evaluate("new URLSearchParams(location.search).get('ReturnUrl')")
        self.check("anonymous Pre-order uses a local quantity-preserving login ReturnUrl",
                   return_url == "/products/browser-open-preorder?preorder=1&quantity=1", return_url)

        self.stage = "customer-registration"
        self.register_customer()
        self.cdp.wait("!!document.querySelector('dialog.store-dialog[open]')", self.timeout)
        before = self.reservation_count()
        dialog = self.cdp.evaluate(
            "({focus:document.activeElement?.id,cart:document.querySelector('dialog[open]').innerText.includes('ตะกร้า'),"
            "policy:document.querySelector('dialog[open]').innerText.includes('มัดจำไม่คืน')})"
        )
        self.check("authenticated ReturnUrl opens an accessible no-cart policy dialog",
                   dialog["focus"] == "preorder-quantity" and not dialog["cart"] and dialog["policy"], dialog)
        self.cdp.evaluate(
            "(() => {const e=document.querySelector('dialog[open] input[type=checkbox]');"
            "e.click();return e.checked})()"
        )
        self.click_text("dialog[open] button", "ยืนยันและตรวจสอบสิทธิ์")
        self.cdp.wait("document.querySelector('dialog[open]')?.innerText.includes('ยังไม่มีการกันสินค้าและยังไม่เกิดคำสั่งซื้อ')", self.timeout)
        snapshot = self.cdp.evaluate(
            "(() => {const t=document.querySelector('dialog[open]').innerText;return "
            "['ราคาเต็มต่อชิ้น','ปิดรับพรีออเดอร์','ประมาณเดือนธันวาคม 2026','สูงสุดต่อคน','ภายใน 7 วัน'].every(x=>t.includes(x))})()"
        )
        after = self.reservation_count()
        self.check("eligibility success shows the authoritative snapshot without reservation writes",
                   snapshot and before == after, {"before": before, "after": after, "snapshot": snapshot})

        self.click_text("dialog[open] button", "ปิด")
        self.cdp.wait("!document.querySelector('dialog.store-dialog[open]') && location.search===''", self.timeout)
        self.click_text("button", "ตรวจสอบสิทธิ์พรีออเดอร์")
        self.cdp.wait("!!document.querySelector('dialog.store-dialog[open]')", self.timeout)
        self.key("Escape")
        self.cdp.wait("!document.querySelector('dialog.store-dialog[open]')", self.timeout)
        self.check("Escape dismisses the dialog and restores the canonical Product URL", self.cdp.evaluate("location.search===''"))

        self.stage = "closed-and-full"
        for slug, label in (("browser-closed-preorder", "ปิดรับพรีออเดอร์แล้ว"),
                            ("browser-full-preorder", "พรีออเดอร์เต็มแล้ว")):
            self.navigate(f"/products/{slug}")
            self.cdp.wait("document.querySelector('h1')", self.timeout)
            disabled = self.cdp.evaluate(
                "(() => {const b=document.querySelector('.product-detail__preorder');"
                "return !!b&&b.disabled&&b.textContent.includes(" + json.dumps(label) + ")"
                "&&!document.body.innerText.includes('เพิ่มลงตะกร้า')})()"
            )
            self.check(f"{label} is disabled and never exposes cart", disabled)

        self.cdp.call("Emulation.setEmulatedMedia", {"features": [{"name": "prefers-reduced-motion", "value": "reduce"}]})
        duration = self.cdp.evaluate("getComputedStyle(document.querySelector('.product-detail__preorder')).transitionDuration")
        self.check("reduced motion removes the Pre-order action transition", duration in ("0s", "1e-05s", "0.00001s"), duration)
        self.cdp.call("Emulation.setEmulatedMedia", {"features": []})
        self.stage = "complete"


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    return parser.parse_args()


def main() -> int:
    args = arguments()
    environment = HARNESS.DisposableEnvironment(args.timeout)
    smoke: PreOrderSmoke | None = None
    browser: dict[str, Any] = {}
    error_category: str | None = None
    result = "failed"
    try:
        environment.start()
        cdp = HARNESS.CdpClient(environment.cdp_url, args.timeout)
        browser = cdp.call("Browser.getVersion")
        smoke = PreOrderSmoke(cdp, environment, args.timeout)
        smoke.run_preorder()
        result = "passed"
    except Exception as exception:
        error_category = type(exception).__name__
        print(f"FAIL {error_category}", file=sys.stderr, flush=True)
    finally:
        cleanup = environment.close()
        if not all(cleanup.values()):
            result = "failed"
            error_category = error_category or "CleanupFailure"

    report: dict[str, Any] = {
        "schemaVersion": 1,
        "name": "M6-05 Storefront Pre-order real-Chrome verification",
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "command": "python3 scripts/storefront-preorder-browser.py",
        "browser": {"product": browser.get("product"), "revision": browser.get("revision")},
        "result": result,
        "assertions": smoke.assertions if smoke else [],
        "coverageNotes": [
            "Real production routes use a disposable migrated PostgreSQL database and a disposable customer.",
            "Reservation row counts are compared before and after eligibility; no Checkout or reservation command is invoked.",
        ],
        "cleanup": cleanup,
    }
    if error_category:
        report["errorCategory"] = error_category
        report["failureStage"] = smoke.stage if smoke else "environment-startup"
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"REPORT assertions={len(report['assertions'])} result={result}", flush=True)
    return 0 if result == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
