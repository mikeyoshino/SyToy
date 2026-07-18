#!/usr/bin/env python3
r"""Real-Chrome Admin shell smoke using only the Python standard library.

The application and a system Chrome instance must already be running. Example:

    /Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome \
      --headless=new --remote-debugging-port=9222 \
      --user-data-dir=/tmp/toystore-admin-smoke-chrome about:blank
    TOYSTORE_SMOKE_EMAIL=... TOYSTORE_SMOKE_PASSWORD=... \
      TOYSTORE_SMOKE_NEW_PASSWORD=... \
      python3 scripts/admin-shell-smoke.py

The optional new password is required only when the account has the forced-password
claim. Credentials are read from the environment and never written to the report.
The script talks directly to the Chrome DevTools Protocol (CDP); it does not require
Playwright, Selenium, Node, or a browser package in the solution.
"""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import struct
import sys
import time
from typing import Any
from urllib.parse import quote, urlparse
from urllib.request import urlopen


class SmokeFailure(RuntimeError):
    pass


class CdpClient:
    """Small synchronous CDP client for one Chrome page target."""

    def __init__(self, endpoint: str, timeout: float) -> None:
        pages = json.load(urlopen(f"{endpoint.rstrip('/')}/json", timeout=timeout))
        page = next((item for item in pages if item.get("type") == "page"), None)
        if page is None:
            raise SmokeFailure("Chrome remote debugging has no page target")

        websocket = urlparse(page["webSocketDebuggerUrl"])
        self._socket = socket.create_connection(
            (websocket.hostname, websocket.port), timeout=timeout
        )
        self._socket.settimeout(timeout)
        key = base64.b64encode(secrets.token_bytes(16)).decode("ascii")
        request = (
            f"GET {websocket.path} HTTP/1.1\r\n"
            f"Host: {websocket.hostname}:{websocket.port}\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            "Sec-WebSocket-Version: 13\r\n\r\n"
        )
        self._socket.sendall(request.encode("ascii"))
        response = self._receive_http_headers()
        expected_accept = base64.b64encode(
            hashlib.sha1(
                (key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode("ascii")
            ).digest()
        ).decode("ascii")
        if " 101 " not in response or expected_accept.lower() not in response.lower():
            raise SmokeFailure("Chrome rejected the CDP WebSocket upgrade")

        self._sequence = 0

    def _receive_http_headers(self) -> str:
        data = b""
        while b"\r\n\r\n" not in data:
            data += self._socket.recv(4096)
        return data.decode("latin-1")

    def _receive_message(self) -> dict[str, Any]:
        header = self._read_exact(2)
        opcode = header[0] & 0x0F
        length = header[1] & 0x7F
        if length == 126:
            length = struct.unpack("!H", self._read_exact(2))[0]
        elif length == 127:
            length = struct.unpack("!Q", self._read_exact(8))[0]
        payload = self._read_exact(length)
        if opcode == 9:
            self._socket.sendall(bytes([0x8A, len(payload)]) + payload)
            return self._receive_message()
        return json.loads(payload)

    def _read_exact(self, length: int) -> bytes:
        data = b""
        while len(data) < length:
            chunk = self._socket.recv(length - len(data))
            if not chunk:
                raise SmokeFailure("Chrome closed the CDP WebSocket")
            data += chunk
        return data

    def call(self, method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
        self._sequence += 1
        request = json.dumps(
            {"id": self._sequence, "method": method, "params": params or {}},
            separators=(",", ":"),
        ).encode("utf-8")
        mask = secrets.token_bytes(4)
        length = len(request)
        if length < 126:
            header = bytes([0x81, 0x80 | length])
        elif length < 65_536:
            header = bytes([0x81, 0xFE]) + struct.pack("!H", length)
        else:
            header = bytes([0x81, 0xFF]) + struct.pack("!Q", length)
        masked = bytes(value ^ mask[index % 4] for index, value in enumerate(request))
        self._socket.sendall(header + mask + masked)

        while True:
            response = self._receive_message()
            if response.get("id") != self._sequence:
                continue
            if "error" in response:
                raise SmokeFailure(f"CDP {method} failed: {response['error']}")
            return response.get("result", {})

    def evaluate(self, expression: str) -> Any:
        response = self.call(
            "Runtime.evaluate",
            {"expression": expression, "returnByValue": True, "awaitPromise": True},
        )
        if "exceptionDetails" in response:
            raise SmokeFailure(f"Browser expression failed: {response['exceptionDetails']}")
        return response["result"].get("value")

    def wait(self, expression: str, timeout: float) -> None:
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                if self.evaluate(expression):
                    return
            except (SmokeFailure, KeyError):
                pass
            time.sleep(0.1)
        raise SmokeFailure(f"Timed out waiting for: {expression}")


class AdminShellSmoke:
    def __init__(self, cdp: CdpClient, base_url: str, timeout: float) -> None:
        self.cdp = cdp
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout
        self.viewport: int | None = None
        self.assertions: list[dict[str, Any]] = []

    def check(self, name: str, condition: bool, details: Any = None) -> None:
        assertion = {
            "name": name,
            "viewport": self.viewport,
            "status": "passed" if condition else "failed",
        }
        if details is not None:
            assertion["details"] = details
        self.assertions.append(assertion)
        print(f"{'PASS' if condition else 'FAIL'} {name}", flush=True)
        if not condition:
            raise SmokeFailure(f"Assertion failed: {name}; details={details}")

    def navigate(self, path: str) -> None:
        target = path if path.startswith("http") else f"{self.base_url}{path}"
        self.cdp.call("Page.navigate", {"url": target})
        expected_path = urlparse(target).path
        self.cdp.wait(
            f"location.pathname === {json.dumps(expected_path)} && document.readyState === 'complete'",
            self.timeout,
        )

    def set_viewport(self, width: int, height: int = 900) -> None:
        self.viewport = width
        self.cdp.call(
            "Emulation.setDeviceMetricsOverride",
            {"width": width, "height": height, "deviceScaleFactor": 1, "mobile": False},
        )
        time.sleep(0.35)

    def click(self, selector: str, focus_first: bool = False) -> None:
        focus = "element.focus();" if focus_first else ""
        clicked = self.cdp.evaluate(
            "(() => {"
            f"const element = document.querySelector({json.dumps(selector)});"
            "if (!element) return false;"
            f"{focus}element.click(); return true;"
            "})()"
        )
        if not clicked:
            raise SmokeFailure(f"Element not found: {selector}")

    def fill(self, selector: str, value: str) -> None:
        filled = self.cdp.evaluate(
            "(() => {"
            f"const element = document.querySelector({json.dumps(selector)});"
            "if (!element) return false;"
            f"element.value = {json.dumps(value)};"
            "element.dispatchEvent(new Event('input', { bubbles: true }));"
            "element.dispatchEvent(new Event('change', { bubbles: true }));"
            "return true;"
            "})()"
        )
        if not filled:
            raise SmokeFailure(f"Field not found: {selector}")

    def authenticate(self, email: str, password: str, new_password: str | None) -> None:
        return_url = quote("/admin", safe="")
        self.navigate(f"/Account/Login?ReturnUrl={return_url}")
        self.cdp.wait("!!document.querySelector('#login-email')", self.timeout)
        self.fill("#login-email", email)
        self.fill("#login-password", password)
        self.click(".account-form button[type='submit']")
        self.cdp.wait(
            "location.pathname === '/admin' || "
            "location.pathname.toLowerCase() === '/account/manage/changepassword'",
            self.timeout,
        )

        if self.cdp.evaluate(
            "location.pathname.toLowerCase() === '/account/manage/changepassword'"
        ):
            if not new_password:
                raise SmokeFailure(
                    "TOYSTORE_SMOKE_NEW_PASSWORD is required for a forced-password account"
                )
            self.cdp.wait("!!document.querySelector('#current-password')", self.timeout)
            self.fill("#current-password", password)
            self.fill("#new-password", new_password)
            self.fill("#confirm-new-password", new_password)
            self.click(".account-form button[type='submit']")
            self.cdp.wait(
                "document.body.innerText.includes('เปลี่ยนรหัสผ่านเรียบร้อยแล้ว')",
                self.timeout,
            )
            self.navigate("/admin")

        self.cdp.wait(
            "location.pathname === '/admin' && "
            "document.querySelector('h1')?.textContent.includes('ภาพรวมร้าน')",
            self.timeout,
        )
        self.check("authenticated Thai Admin dashboard", True)

    def inject_modal_fixture(self) -> None:
        injected = self.cdp.evaluate(
            r"""(() => {
              document.querySelector('[data-admin-smoke-modal]')?.remove();
              let scope = null;
              const visit = rules => {
                for (const rule of rules) {
                  if (rule.cssRules) visit(rule.cssRules);
                  const selector = rule.selectorText || '';
                  if (selector.includes('.admin-modal-host') && selector.includes('.admin-modal')) {
                    const match = selector.match(/\[(b-[^\]]+)\]/);
                    if (match) scope = match[1];
                  }
                }
              };
              for (const sheet of document.styleSheets) {
                try { visit(sheet.cssRules); } catch { /* only same-origin styles are needed */ }
              }
              if (!scope) return false;
              const host = document.createElement('div');
              host.className = 'admin-modal-host';
              host.dataset.adminSmokeModal = 'true';
              host.setAttribute(scope, '');
              host.innerHTML = '<dialog class="store-dialog admin-modal" aria-label="smoke modal">'
                + '<div class="store-dialog__surface"><p>smoke</p></div></dialog>';
              document.querySelector('.admin-shell').append(host);
              host.querySelector('dialog').showModal();
              return true;
            })()"""
        )
        if not injected:
            raise SmokeFailure("Could not derive the AdminModal scoped CSS attribute")

    def assert_fullscreen_modal(self) -> None:
        self.inject_modal_fixture()
        metrics = self.cdp.evaluate(
            """(() => {
              const dialog = document.querySelector('[data-admin-smoke-modal] dialog');
              const rect = dialog.getBoundingClientRect();
              const style = getComputedStyle(dialog);
              return {
                width: rect.width, height: rect.height,
                viewportWidth: innerWidth, viewportHeight: innerHeight,
                radius: style.borderRadius,
                maxWidth: style.maxWidth, maxHeight: style.maxHeight,
                bodyOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
              };
            })()"""
        )
        self.check(
            "AdminModal fills mobile shell",
            abs(metrics["width"] - metrics["viewportWidth"]) < 1
            and abs(metrics["height"] - metrics["viewportHeight"]) < 1,
            metrics,
        )
        self.check(
            "AdminModal removes mobile radius and max constraints",
            metrics["radius"] == "0px"
            and metrics["maxWidth"] == "none"
            and metrics["maxHeight"] == "none",
            metrics,
        )
        self.check("AdminModal causes no body overflow", not metrics["bodyOverflow"], metrics)
        self.cdp.evaluate(
            "(() => { const host = document.querySelector('[data-admin-smoke-modal]'); "
            "host.querySelector('dialog').close(); host.remove(); return true; })()"
        )

    def assert_mobile_navigation(self) -> None:
        mobile = self.cdp.evaluate(
            """(() => {
              const trigger = document.querySelector('.admin-mobile-nav__trigger');
              const box = trigger.getBoundingClientRect();
              return {
                trigger: getComputedStyle(trigger).display,
                rail: getComputedStyle(document.querySelector('.admin-shell__rail')).display,
                width: box.width, height: box.height,
                overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
              };
            })()"""
        )
        self.check("mobile shell has no body overflow", not mobile["overflow"], mobile)
        self.check(
            "mobile trigger replaces desktop rail",
            mobile["trigger"] != "none" and mobile["rail"] == "none",
            mobile,
        )
        self.check(
            "mobile trigger is at least 44px",
            mobile["width"] >= 44 and mobile["height"] >= 44,
            mobile,
        )

    def assert_focus_modes(self) -> None:
        trigger = ".admin-mobile-nav__trigger"
        self.click(trigger, focus_first=True)
        self.cdp.wait("document.querySelector('dialog.store-drawer')?.open === true", self.timeout)
        self.check(
            "drawer moves focus inside",
            self.cdp.evaluate(
                "document.querySelector('dialog.store-drawer').contains(document.activeElement)"
            ),
        )
        key = {
            "key": "Escape",
            "code": "Escape",
            "windowsVirtualKeyCode": 27,
            "nativeVirtualKeyCode": 27,
        }
        self.cdp.call("Input.dispatchKeyEvent", {"type": "rawKeyDown", **key})
        self.cdp.call("Input.dispatchKeyEvent", {"type": "keyUp", **key})
        self.cdp.wait("document.querySelector('dialog.store-drawer')?.open === false", self.timeout)
        self.check(
            "manual drawer close returns focus to trigger",
            self.cdp.evaluate(
                "document.activeElement === document.querySelector('.admin-mobile-nav__trigger')"
            ),
        )

        self.click(trigger, focus_first=True)
        self.cdp.wait("document.querySelector('dialog.store-drawer')?.open === true", self.timeout)
        self.click("dialog.store-drawer a[href='/admin/products']")
        self.cdp.wait(
            "location.pathname === '/admin/products' && "
            "document.querySelector('h1')?.textContent.includes('สินค้า')",
            self.timeout,
        )
        self.cdp.wait("document.querySelector('dialog.store-drawer')?.open === false", self.timeout)
        self.check(
            "route navigation focuses the new heading",
            self.cdp.evaluate("document.activeElement === document.querySelector('h1')"),
        )
        self.check(
            "route navigation suppresses stale trigger focus",
            not self.cdp.evaluate(
                "document.activeElement === document.querySelector('.admin-mobile-nav__trigger')"
            ),
        )

    def assert_compact_rail(self, width: int) -> None:
        self.cdp.call("Input.dispatchMouseEvent", {"type": "mouseMoved", "x": 600, "y": 500})
        self.set_viewport(width)
        time.sleep(0.35)
        collapsed = self.cdp.evaluate(
            """(() => {
              const rail = document.querySelector('.admin-rail');
              const content = document.querySelector('.admin-shell__content');
              const link = document.querySelector('.admin-rail__link');
              return {
                railWidth: rail.getBoundingClientRect().width,
                contentLeft: content.getBoundingClientRect().left,
                title: link.title, label: link.getAttribute('aria-label'),
                tooltip: link.dataset.tooltip,
                overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
              };
            })()"""
        )
        self.check("compact rail is 84px", abs(collapsed["railWidth"] - 84) < 1, collapsed)
        self.check(
            "collapsed rail exposes matching native Thai tooltip",
            bool(collapsed["title"])
            and collapsed["title"] == collapsed["label"] == collapsed["tooltip"],
            collapsed,
        )
        self.check("compact rail has no body overflow", not collapsed["overflow"], collapsed)

        self.cdp.call("Input.dispatchMouseEvent", {"type": "mouseMoved", "x": 42, "y": 180})
        time.sleep(0.4)
        expanded = self.cdp.evaluate(
            """(() => {
              const rail = document.querySelector('.admin-rail');
              const content = document.querySelector('.admin-shell__content');
              return {
                railWidth: rail.getBoundingClientRect().width,
                railRight: rail.getBoundingClientRect().right,
                contentLeft: content.getBoundingClientRect().left,
                overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
              };
            })()"""
        )
        self.check(
            "hover rail expands as an overlay without shifting content",
            expanded["railWidth"] > 250
            and abs(expanded["contentLeft"] - collapsed["contentLeft"]) < 0.5
            and expanded["railRight"] > expanded["contentLeft"],
            expanded,
        )
        self.check("expanded overlay has no body overflow", not expanded["overflow"], expanded)

    def assert_wide_and_reduced_motion(self) -> None:
        self.set_viewport(1200)
        wide = self.cdp.evaluate(
            """(() => ({
              railWidth: document.querySelector('.admin-rail').getBoundingClientRect().width,
              railColumn: document.querySelector('.admin-shell__rail').getBoundingClientRect().width,
              contentLeft: document.querySelector('.admin-shell__content').getBoundingClientRect().left,
              mobile: getComputedStyle(document.querySelector('.admin-shell__mobile-header')).display,
              overflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
            }))()"""
        )
        self.check(
            "1200px uses the stable 264px desktop grid",
            abs(wide["railWidth"] - 264) < 1
            and abs(wide["railColumn"] - 264) < 1
            and abs(wide["contentLeft"] - 264) < 1
            and wide["mobile"] == "none",
            wide,
        )
        self.check("wide shell has no body overflow", not wide["overflow"], wide)

        self.cdp.call(
            "Emulation.setEmulatedMedia",
            {"features": [{"name": "prefers-reduced-motion", "value": "reduce"}]},
        )
        time.sleep(0.1)
        duration = self.cdp.evaluate(
            "getComputedStyle(document.querySelector('.admin-rail')).transitionDuration"
        )
        seconds = max(
            float(value[:-2]) / 1000 if value.endswith("ms") else float(value[:-1])
            for value in duration.split(", ")
        )
        self.check("reduced motion minimizes transitions", seconds <= 0.001, duration)
        self.cdp.call("Emulation.setEmulatedMedia", {"features": []})

    def run(self, email: str, password: str, new_password: str | None) -> None:
        self.cdp.call("Runtime.enable")
        self.cdp.call("Page.enable")
        self.cdp.call("Network.enable")
        self.cdp.call("Network.clearBrowserCookies")
        self.authenticate(email, password, new_password)

        self.set_viewport(390, 844)
        self.assert_mobile_navigation()
        self.assert_fullscreen_modal()
        self.assert_focus_modes()

        self.set_viewport(768)
        self.assert_mobile_navigation()
        self.assert_fullscreen_modal()

        self.assert_compact_rail(900)
        self.assert_compact_rail(1199)
        self.assert_wide_and_reduced_motion()


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://127.0.0.1:5099")
    parser.add_argument("--cdp-url", default="http://127.0.0.1:9222")
    parser.add_argument("--timeout", type=float, default=15)
    parser.add_argument(
        "--report",
        default="artifacts/browser/admin-shell-smoke-report.json",
    )
    return parser.parse_args()


def main() -> int:
    args = arguments()
    email = os.environ.get("TOYSTORE_SMOKE_EMAIL")
    password = os.environ.get("TOYSTORE_SMOKE_PASSWORD")
    new_password = os.environ.get("TOYSTORE_SMOKE_NEW_PASSWORD")
    if not email or not password:
        print(
            "TOYSTORE_SMOKE_EMAIL and TOYSTORE_SMOKE_PASSWORD are required",
            file=sys.stderr,
        )
        return 2

    report_path = Path(args.report)
    generated = dt.datetime.now(dt.timezone.utc).isoformat()
    smoke: AdminShellSmoke | None = None
    error: str | None = None
    browser: dict[str, Any] = {}
    try:
        cdp = CdpClient(args.cdp_url, args.timeout)
        browser = cdp.call("Browser.getVersion")
        smoke = AdminShellSmoke(cdp, args.base_url, args.timeout)
        smoke.run(email, password, new_password)
        result = "passed"
    except Exception as exception:  # report every smoke failure before returning non-zero
        result = "failed"
        error = f"{type(exception).__name__}: {exception}"
        print(f"FAIL {error}", file=sys.stderr, flush=True)

    report = {
        "schemaVersion": 1,
        "name": "M4-05 Thai Admin shell real-Chrome smoke",
        "generatedAtUtc": generated,
        "baseUrl": args.base_url,
        "browser": {
            "product": browser.get("product"),
            "revision": browser.get("revision"),
            "userAgent": browser.get("userAgent"),
        },
        "result": result,
        "assertions": smoke.assertions if smoke else [],
    }
    if error:
        report["error"] = error
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"REPORT {report_path} assertions={len(report['assertions'])} result={result}")
    return 0 if result == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
