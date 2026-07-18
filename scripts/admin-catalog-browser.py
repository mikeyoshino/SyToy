#!/usr/bin/env python3
"""M4-06 real-Chrome Admin catalog verification using only the standard library.

Exact command from the repository root:

    python3 scripts/admin-catalog-browser.py

The harness owns its PostgreSQL database, media root, data-protection keys, Admin
account, application process, Chrome profile and Chrome process. It talks directly
to the Chrome DevTools Protocol (CDP), exercises real production routes, writes a
secret-free JSON report, and tears every temporary resource down in ``finally``.
"""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import select
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time
from typing import Any
from urllib.request import urlopen


ROOT = Path(__file__).resolve().parent.parent
SHELL_SCRIPT = ROOT / "scripts" / "admin-shell-smoke.py"
DEFAULT_REPORT = ROOT / "artifacts" / "browser" / "admin-catalog-browser-report.json"
CHROME_CANDIDATES = (
    Path("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"),
    Path("/Applications/Chromium.app/Contents/MacOS/Chromium"),
    Path("/usr/bin/google-chrome"),
    Path("/usr/bin/chromium"),
)


def load_cdp_types() -> tuple[type, type]:
    spec = importlib.util.spec_from_file_location("admin_shell_smoke", SHELL_SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the retained CDP client")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.CdpClient, module.AdminShellSmoke


CdpClient, AdminShellSmoke = load_cdp_types()


class VerificationFailure(RuntimeError):
    pass


def free_port() -> int:
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return int(listener.getsockname()[1])


def dotenv() -> dict[str, str]:
    values: dict[str, str] = {}
    path = ROOT / ".env"
    if not path.exists():
        return values
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip("'\"")
    return values


class PostgresCommitFaultProxy:
    """Transparent TCP proxy that can suppress one COMMIT acknowledgement."""

    def __init__(self, backend_port: int) -> None:
        self.port = free_port()
        self.backend_port = backend_port
        self.listener: socket.socket | None = None
        self.thread: threading.Thread | None = None
        self.stop_event = threading.Event()
        self.armed = threading.Event()
        self.tripped = threading.Event()
        self.lock = threading.Lock()
        self.connections: set[socket.socket] = set()

    def start(self) -> None:
        listener = socket.socket()
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind(("127.0.0.1", self.port))
        listener.listen()
        listener.settimeout(0.2)
        self.listener = listener
        self.thread = threading.Thread(target=self._accept, daemon=True)
        self.thread.start()

    def arm(self) -> None:
        self.tripped.clear()
        self.armed.set()

    def disarm(self) -> None:
        self.armed.clear()
        self.tripped.clear()

    def wait_tripped(self, timeout: float) -> bool:
        return self.tripped.wait(timeout)

    def _accept(self) -> None:
        assert self.listener is not None
        while not self.stop_event.is_set():
            try:
                client, _ = self.listener.accept()
            except socket.timeout:
                continue
            except OSError:
                return
            threading.Thread(target=self._forward, args=(client,), daemon=True).start()

    def _forward(self, client: socket.socket) -> None:
        backend: socket.socket | None = None
        try:
            if self.tripped.is_set():
                return
            backend = socket.create_connection(("127.0.0.1", self.backend_port), timeout=3)
            client.setblocking(False)
            backend.setblocking(False)
            with self.lock:
                self.connections.update((client, backend))
            commit_seen = False
            server_tail = b""
            while not self.stop_event.is_set():
                readable, _, _ = select.select([client, backend], [], [], 0.2)
                if not readable:
                    continue
                for source in readable:
                    data = source.recv(64 * 1024)
                    if not data:
                        return
                    if source is client:
                        if self.tripped.is_set():
                            return
                        if self.armed.is_set() and b"COMMIT" in data.upper():
                            commit_seen = True
                        backend.sendall(data)
                    else:
                        combined = server_tail + data
                        if commit_seen and b"COMMIT" in combined.upper():
                            self.tripped.set()
                            self.armed.clear()
                            self._close_all()
                            return
                        server_tail = combined[-16:]
                        client.sendall(data)
        except (ConnectionError, OSError):
            return
        finally:
            with self.lock:
                self.connections.discard(client)
                if backend is not None:
                    self.connections.discard(backend)
            for connection in (client, backend):
                if connection is not None:
                    try:
                        connection.close()
                    except OSError:
                        pass

    def _close_all(self) -> None:
        with self.lock:
            connections = tuple(self.connections)
        for connection in connections:
            try:
                connection.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass

    def stop(self) -> bool:
        self.stop_event.set()
        self._close_all()
        if self.listener is not None:
            self.listener.close()
        if self.thread is not None:
            self.thread.join(timeout=3)
        return self.thread is None or not self.thread.is_alive()


class DisposableEnvironment:
    def __init__(self, timeout: float) -> None:
        config = dotenv()
        suffix = secrets.token_hex(6)
        self.database = f"toystore_browser_{suffix}"
        self.pg_user = config.get("POSTGRES_USER", "toystore")
        self.pg_password = config.get("POSTGRES_PASSWORD", "toystore_dev")
        self.pg_port = config.get("POSTGRES_PORT", "5432")
        self.proxy = PostgresCommitFaultProxy(int(self.pg_port))
        self.timeout = timeout
        self.app_port = free_port()
        self.cdp_port = free_port()
        self.base_url = f"http://127.0.0.1:{self.app_port}"
        self.cdp_url = f"http://127.0.0.1:{self.cdp_port}"
        self.email = f"browser-{suffix}@example.invalid"
        self.temporary_password = f"Temp{suffix}!9a"
        self.password = f"Ready{suffix}!8z"
        self.temp_owner: tempfile.TemporaryDirectory[str] | None = None
        self.temp_root: Path | None = None
        self.storage_root: Path | None = None
        self.image_path: Path | None = None
        self.app: subprocess.Popen[bytes] | None = None
        self.chrome: subprocess.Popen[bytes] | None = None
        self.locks: list[subprocess.Popen[bytes]] = []
        self.handles: list[Any] = []
        self.database_created = False
        self.cleanup = {
            "applicationStopped": False,
            "chromeStopped": False,
            "databaseDropped": False,
            "temporaryFilesRemoved": False,
            "proxyStopped": False,
        }

    def start(self) -> None:
        self.temp_owner = tempfile.TemporaryDirectory(prefix="toystore-browser-")
        self.temp_root = Path(self.temp_owner.name)
        self.storage_root = self.temp_root / "media"
        keys_root = self.temp_root / "keys"
        chrome_root = self.temp_root / "chrome"
        for directory in (self.storage_root, keys_root, chrome_root):
            directory.mkdir(mode=0o750)
            os.chmod(directory, 0o750)

        self.image_path = self.temp_root / "catalog.png"
        self.image_path.write_bytes(base64.b64decode(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII="
        ))

        self._run(["docker", "compose", "up", "-d", "postgres"], timeout=120)
        self._psql(f'CREATE DATABASE "{self.database}";')
        self.database_created = True
        self.proxy.start()

        environment = os.environ.copy()
        environment.update({
            "ASPNETCORE_ENVIRONMENT": "Development",
            "DOTNET_ENVIRONMENT": "Development",
            "ASPNETCORE_URLS": self.base_url,
            "ConnectionStrings__Database": (
                f"Host=127.0.0.1;Port={self.proxy.port};Database={self.database};"
                f"Username={self.pg_user};Password={self.pg_password};SSL Mode=Disable"
            ),
            "Storage__RootPath": str(self.storage_root),
            "DataProtection__KeysPath": str(keys_root),
            "BootstrapAdmin__Email": self.email,
            "BootstrapAdmin__TemporaryPassword": self.temporary_password,
            "Serilog__WriteTo__1__Args__path": str(self.temp_root / "application-.log"),
        })
        command = [
            "dotnet", "run", "--no-build", "--no-launch-profile",
            "--project", "src/ToyStore.Web", "--", "--bootstrap-admin",
        ]
        self._run(command, environment=environment, timeout=120)

        app_log = (self.temp_root / "app-process.log").open("wb")
        self.handles.append(app_log)
        self.app = subprocess.Popen(
            command[:-2], cwd=ROOT, env=environment,
            stdout=app_log, stderr=subprocess.STDOUT,
        )
        self._wait_http(f"{self.base_url}/health/ready", self.timeout * 4)

        chrome = next((path for path in CHROME_CANDIDATES if path.exists()), None)
        if chrome is None:
            raise VerificationFailure("A system Google Chrome or Chromium executable is required")
        chrome_log = (self.temp_root / "chrome-process.log").open("wb")
        self.handles.append(chrome_log)
        self.chrome = subprocess.Popen(
            [
                str(chrome), "--headless=new", f"--remote-debugging-port={self.cdp_port}",
                f"--user-data-dir={chrome_root}", "--remote-allow-origins=*",
                "--no-first-run", "--no-default-browser-check", "--disable-extensions",
                "--disable-sync", "--disable-background-networking", "about:blank",
            ], cwd=ROOT, stdout=chrome_log, stderr=subprocess.STDOUT,
        )
        self._wait_http(f"{self.cdp_url}/json/version", self.timeout * 2)

    def storage_file_count(self) -> int:
        assert self.storage_root is not None
        return sum(1 for path in self.storage_root.rglob("*") if path.is_file())

    def storage_image_count(self) -> int:
        assert self.storage_root is not None
        return sum(
            1 for path in self.storage_root.rglob("*")
            if path.is_file() and path.suffix.lower() in (".jpg", ".png", ".webp")
        )

    def bump_brand_version(self) -> None:
        self._psql(
            'UPDATE "Brands" SET "Version" = "Version" + 1 '
            "WHERE \"DisplayName\" = 'แบรนด์ทดสอบ Chrome';",
            database=self.database,
        )

    def hold_brand_table(self, seconds: int = 3) -> subprocess.Popen[bytes]:
        sql = (
            'BEGIN; LOCK TABLE "Brands" IN ACCESS EXCLUSIVE MODE; '
            f"SELECT pg_sleep({seconds}); COMMIT;"
        )
        process = subprocess.Popen(
            self._psql_command(sql, self.database), cwd=ROOT,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        self.locks.append(process)
        time.sleep(0.5)
        return process

    def _psql_command(self, sql: str, database: str = "postgres") -> list[str]:
        if not re.fullmatch(r"[a-z0-9_]+", database):
            raise VerificationFailure("Unsafe temporary database identifier")
        return [
            "docker", "compose", "exec", "-T", "postgres", "psql",
            "-v", "ON_ERROR_STOP=1", "-U", self.pg_user, "-d", database,
            "-c", sql,
        ]

    def _psql(self, sql: str, database: str = "postgres") -> None:
        self._run(self._psql_command(sql, database), timeout=60)

    @staticmethod
    def _run(
        command: list[str], environment: dict[str, str] | None = None,
        timeout: float = 60,
    ) -> None:
        result = subprocess.run(
            command, cwd=ROOT, env=environment, stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, timeout=timeout, check=False,
        )
        if result.returncode != 0:
            raise VerificationFailure(f"Environment command failed with exit {result.returncode}")

    @staticmethod
    def _wait_http(url: str, timeout: float) -> None:
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                with urlopen(url, timeout=1) as response:
                    if response.status < 500:
                        return
            except Exception:
                time.sleep(0.2)
        raise VerificationFailure("Timed out waiting for a local verification endpoint")

    @staticmethod
    def _stop(process: subprocess.Popen[bytes] | None) -> bool:
        if process is None:
            return True
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
        return process.poll() is not None

    def close(self) -> dict[str, bool]:
        for lock in self.locks:
            self._stop(lock)
        self.cleanup["chromeStopped"] = self._stop(self.chrome)
        self.cleanup["applicationStopped"] = self._stop(self.app)
        self.cleanup["proxyStopped"] = self.proxy.stop()
        for handle in self.handles:
            handle.close()

        if self.database_created:
            try:
                self._psql(
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                    f"WHERE datname = '{self.database}' AND pid <> pg_backend_pid();"
                )
                self._psql(f'DROP DATABASE "{self.database}";')
                self.cleanup["databaseDropped"] = True
            except Exception:
                self.cleanup["databaseDropped"] = False
        else:
            self.cleanup["databaseDropped"] = True

        if self.temp_owner is not None:
            root = self.temp_root
            self.temp_owner.cleanup()
            self.cleanup["temporaryFilesRemoved"] = bool(root and not root.exists())
        else:
            self.cleanup["temporaryFilesRemoved"] = True
        return self.cleanup


class CatalogSmoke(AdminShellSmoke):
    def __init__(self, cdp: Any, environment: DisposableEnvironment, timeout: float) -> None:
        super().__init__(cdp, environment.base_url, timeout)
        self.environment = environment
        self.stage = "initialization"

    def visible_dialog(self) -> str:
        return "dialog.admin-modal[open]"

    def key(self, name: str) -> None:
        codes = {"Tab": (9, "Tab"), "Escape": (27, "Escape")}
        virtual, code = codes[name]
        params = {
            "key": name, "code": code,
            "windowsVirtualKeyCode": virtual, "nativeVirtualKeyCode": virtual,
        }
        self.cdp.call("Input.dispatchKeyEvent", {"type": "keyDown", **params})
        self.cdp.call("Input.dispatchKeyEvent", {"type": "keyUp", **params})

    def click_text(self, selector: str, text_value: str) -> None:
        clicked = self.cdp.evaluate(
            "(() => { const e = [...document.querySelectorAll(" + json.dumps(selector) + ")]"
            ".find(x => x.textContent.trim().includes(" + json.dumps(text_value) + "));"
            "if (!e) return false; e.focus(); e.click(); return true; })()"
        )
        if not clicked:
            raise VerificationFailure("Expected action was not found")

    def set_file(self) -> None:
        assert self.environment.image_path is not None
        document = self.cdp.call("DOM.getDocument", {"depth": -1, "pierce": True})
        node = self.cdp.call(
            "DOM.querySelector",
            {"nodeId": document["root"]["nodeId"], "selector": ".admin-catalog-editor input[type=file]"},
        ).get("nodeId", 0)
        if not node:
            raise VerificationFailure("Catalog image input was not found")
        self.cdp.call(
            "DOM.setFileInputFiles",
            {"nodeId": node, "files": [str(self.environment.image_path)]},
        )

    def wait_heading(self, text_value: str) -> None:
        self.cdp.wait(
            "document.querySelector('h1')?.textContent.includes(" + json.dumps(text_value) + ")",
            self.timeout,
        )

    def assert_ssr_thai(self, path: str, expected: str, expected_content: str) -> None:
        self.cdp.call("Emulation.setScriptExecutionDisabled", {"value": True})
        try:
            self.cdp.call("Page.navigate", {"url": f"{self.base_url}{path}"})
            self.cdp.wait("document.readyState === 'complete'", self.timeout)
            server_rendered = self.cdp.evaluate(
                "({heading:!!document.querySelector('h1')?.textContent.includes(" + json.dumps(expected) + "),"
                "content:document.body.innerText.includes(" + json.dumps(expected_content) + "),"
                "login:location.pathname.toLowerCase().includes('/account/login')})"
            )
            self.check(
                f"{path} SSR contains Thai heading and content",
                server_rendered["heading"] and server_rendered["content"] and not server_rendered["login"],
                server_rendered,
            )
        finally:
            self.cdp.call("Emulation.setScriptExecutionDisabled", {"value": False})
        self.navigate(path)
        self.wait_heading(expected)

    def assert_route_and_seed(self) -> None:
        self.navigate("/admin/universes?status=active&page=1")
        self.wait_heading("จักรวาล")
        self.cdp.wait("document.querySelectorAll('.admin-data-table tbody tr').length >= 3", self.timeout)
        self.check(
            "default Active and page 1 are omitted from the canonical URL",
            self.cdp.evaluate("location.pathname === '/admin/universes' && location.search === ''"),
        )
        self.check(
            "seed Universes render the approved missing-logo state",
            self.cdp.evaluate(
                "document.body.innerText.includes('Marvel') && "
                "document.body.innerText.includes('DC') && "
                "document.querySelectorAll('.admin-catalog-reference-list__missing-image').length === 3 && "
                "document.body.innerText.includes('ต้องเพิ่มโลโก้')"
            ),
        )
        self.assert_ssr_thai("/admin/universes", "จักรวาล", "ดูแลจักรวาล")
        self.assert_ssr_thai("/admin/brands", "แบรนด์", "ดูแลข้อมูลแบรนด์")

    def assert_layouts(self) -> None:
        self.navigate("/admin/universes")
        self.wait_heading("จักรวาล")
        for width, height in ((390, 844), (768, 900)):
            self.set_viewport(width, height)
            metrics = self.cdp.evaluate(
                "(() => { const s=document.querySelector('.admin-data-table__scroller');"
                "const y=scrollY;scrollTo(10000,y);const horizontalScroll=scrollX;scrollTo(0,y);"
                "return {body:document.documentElement.scrollWidth>document.documentElement.clientWidth,"
                "local:s.scrollWidth>s.clientWidth,"
                "horizontalScroll,innerWidth,"
                "documentWidth:document.documentElement.scrollWidth,viewportWidth:document.documentElement.clientWidth,"
                "bodyWidth:document.body.scrollWidth,bodyClientWidth:document.body.clientWidth,"
                "visualViewportWidth:visualViewport?.width??null,rootOffsetWidth:document.documentElement.offsetWidth,"
                "rootOverflowX:getComputedStyle(document.documentElement).overflowX,"
                "experiments:(()=>{const root=document.documentElement,t=s.querySelector('table');"
                "const snap=()=>({root:root.scrollWidth,body:document.body.scrollWidth,local:s.scrollWidth>s.clientWidth});"
                "const original={root:root.style.overflowX,scroller:s.style.overflowX,min:t.style.minWidth,width:t.style.width,display:t.style.display};"
                "const result={baseline:snap()};root.style.overflowX='clip';result.rootClip=snap();root.style.overflowX=original.root;"
                "s.style.overflowX='hidden';result.scrollerHidden=snap();s.style.overflowX=original.scroller;"
                "t.style.minWidth='0';t.style.width='100%';result.tableFluid=snap();t.style.minWidth=original.min;t.style.width=original.width;"
                "t.style.display='none';result.tableHidden=snap();t.style.display=original.display;return result})(),"
                "ancestors:(()=>{let a=[],e=s;while(e&&a.length<8){const r=e.getBoundingClientRect(),c=getComputedStyle(e);"
                "a.push({tag:e.tagName.toLowerCase(),className:typeof e.className==='string'?e.className:'',"
                "width:Math.round(r.width),scrollWidth:e.scrollWidth,clientWidth:e.clientWidth,"
                "minWidth:c.minWidth,maxWidth:c.maxWidth,overflowX:c.overflowX});e=e.parentElement}return a})(),"
                "overflowers:[...document.body.querySelectorAll('*')].map(e=>{const r=e.getBoundingClientRect();"
                "return {tag:e.tagName.toLowerCase(),className:typeof e.className==='string'?e.className:'',"
                "left:Math.round(r.left),right:Math.round(r.right),width:Math.round(r.width),"
                "scrollWidth:e.scrollWidth,clientWidth:e.clientWidth}})"
                ".filter(x=>x.right>document.documentElement.clientWidth+1||x.left < -1).slice(0,8)}; })()"
            )
            self.check(
                f"{width}px list has no body overflow",
                metrics["horizontalScroll"] == 0
                and metrics["bodyWidth"] <= metrics["bodyClientWidth"],
                metrics,
            )
            if width == 390:
                self.check("narrow data table scrolls locally", metrics["local"], metrics)
            self.click_text("button", "เพิ่มจักรวาล")
            self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
            modal = self.cdp.evaluate(
                "(() => { const d=document.querySelector('dialog.admin-modal[open]');"
                "const r=d.getBoundingClientRect(); const fields=[...d.querySelectorAll('.store-field')].map(x=>x.getBoundingClientRect().left);"
                "const targets=[...d.querySelectorAll('button,input,select')].filter(x=>x.offsetParent!==null).map(x=>x.getBoundingClientRect());"
                "const y=scrollY;scrollTo(10000,y);const horizontalScroll=scrollX;scrollTo(0,y);"
                "return {w:r.width,h:r.height,vw:innerWidth,vh:innerHeight,radius:getComputedStyle(d).borderRadius,"
                "oneColumn:fields.every(x=>Math.abs(x-fields[0])<1), targets:targets.every(x=>x.width>=44&&x.height>=44),"
                "body:document.documentElement.scrollWidth>document.documentElement.clientWidth,"
                "horizontalScroll,bodyWidth:document.body.scrollWidth,bodyClientWidth:document.body.clientWidth}; })()"
            )
            self.check(f"{width}px editor is full-screen and borderless", abs(modal["w"]-modal["vw"]) < 1 and abs(modal["h"]-modal["vh"]) < 1 and modal["radius"] == "0px", modal)
            self.check(f"{width}px editor is one column with 44px targets", modal["oneColumn"] and modal["targets"], modal)
            self.check(
                f"{width}px editor has no body overflow",
                modal["horizontalScroll"] == 0
                and modal["bodyWidth"] <= modal["bodyClientWidth"],
                modal,
            )
            self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
            self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)

        for width in (900, 1199):
            self.set_viewport(width)
            layout = self.cdp.evaluate(
                "(() => ({rail:document.querySelector('.admin-rail').getBoundingClientRect().width,"
                "modalOpen:!!document.querySelector('dialog.admin-modal[open]'),"
                "body:document.documentElement.scrollWidth>document.documentElement.clientWidth}))()"
            )
            self.check(f"{width}px uses compact rail and stable list layout", abs(layout["rail"]-84)<1 and not layout["modalOpen"] and not layout["body"], layout)
            self.click_text("button", "เพิ่มจักรวาล")
            self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
            desktop_modal = self.cdp.evaluate(
                "(() => {const d=document.querySelector('dialog.admin-modal[open]'),r=d.getBoundingClientRect();"
                "const targets=[...d.querySelectorAll('button,input,select')].filter(x=>x.offsetParent!==null).map(x=>x.getBoundingClientRect());"
                "return {width:Math.round(r.width),height:Math.round(r.height),viewportWidth:innerWidth,viewportHeight:innerHeight,"
                "radius:getComputedStyle(d).borderRadius,targets:targets.every(x=>x.width>=44&&x.height>=44)}})()"
            )
            self.check(
                f"{width}px desktop editor is constrained and keeps 44px targets",
                desktop_modal["width"] < desktop_modal["viewportWidth"]
                and desktop_modal["height"] <= desktop_modal["viewportHeight"]
                and desktop_modal["radius"] != "0px"
                and desktop_modal["targets"],
                desktop_modal,
            )
            self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
            self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)

        self.set_viewport(1200)
        wide = self.cdp.evaluate(
            "(() => ({rail:document.querySelector('.admin-rail').getBoundingClientRect().width,"
            "body:document.documentElement.scrollWidth>document.documentElement.clientWidth}))()"
        )
        self.check("1200px uses wide rail without body overflow", abs(wide["rail"]-264)<1 and not wide["body"], wide)
        self.click_text("button", "เพิ่มจักรวาล")
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        wide_modal = self.cdp.evaluate(
            "(() => {const d=document.querySelector('dialog.admin-modal[open]'),r=d.getBoundingClientRect();"
            "return {width:Math.round(r.width),viewportWidth:innerWidth,radius:getComputedStyle(d).borderRadius}})()"
        )
        self.check(
            "1200px desktop editor stays constrained and rounded",
            wide_modal["width"] < wide_modal["viewportWidth"] and wide_modal["radius"] != "0px",
            wide_modal,
        )
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)
        self.cdp.call("Emulation.setEmulatedMedia", {"features": [{"name": "prefers-reduced-motion", "value": "reduce"}]})
        duration = self.cdp.evaluate("getComputedStyle(document.querySelector('.admin-rail')).transitionDuration")
        self.check("prefers-reduced-motion minimizes Admin transitions", duration in ("0s", "1e-05s", "0.00001s"), duration)
        self.cdp.call("Emulation.setEmulatedMedia", {"features": []})

    def assert_url_keyboard_and_select(self) -> None:
        self.navigate("/admin/universes")
        self.wait_heading("จักรวาล")
        self.set_viewport(1200)
        appearance = self.cdp.evaluate("getComputedStyle(document.querySelector('.admin-filter-bar select')).appearance")
        self.check("status field uses the custom cross-browser select", appearance == "none", appearance)
        self.cdp.evaluate("document.querySelector('.admin-filter-bar input[type=text]').focus()")
        self.key("Tab")
        tab_focus = self.cdp.evaluate(
            "({select:document.activeElement === document.querySelector('.admin-filter-bar select'),"
            "tag:document.activeElement.tagName.toLowerCase(),className:document.activeElement.className})"
        )
        self.check("Tab moves from search to the custom status select", tab_focus["select"], tab_focus)
        self.fill(".admin-filter-bar input[type=text]", "  Marvel  ")
        self.click_text(".admin-filter-bar button", "ใช้ตัวกรอง")
        self.cdp.wait("location.search === '?q=Marvel'", self.timeout)
        self.check("filter submit normalizes URL state and omits defaults", True)
        self.click_text(".admin-filter-bar a", "ล้างตัวกรอง")
        self.cdp.wait("location.search === ''", self.timeout)
        self.check("clear removes all default query state", True)
        self.cdp.evaluate("history.back()")
        self.cdp.wait("location.search === '?q=Marvel' && document.querySelector('.admin-filter-bar input[type=text]').value === 'Marvel'", self.timeout)
        self.check("browser Back restores URL and filter controls", True)
        self.cdp.evaluate("history.forward()")
        self.cdp.wait("location.search === '' && document.querySelector('.admin-filter-bar input[type=text]').value === ''", self.timeout)
        self.check("browser Forward restores the cleared filter state", True)
        self.cdp.evaluate("document.querySelector('.admin-data-table__scroller').focus()")
        self.check("data table scroller is keyboard focusable", self.cdp.evaluate("document.activeElement === document.querySelector('.admin-data-table__scroller')"))
        self.cdp.evaluate("document.querySelector('.admin-data-table tbody button').focus()")
        self.check("row actions are keyboard focusable", self.cdp.evaluate("document.activeElement.matches('.admin-data-table tbody button')"))

    def assert_seed_universe_wiring(self) -> None:
        self.stage = "seed-universe-edit"
        self.navigate("/admin/universes")
        self.wait_heading("จักรวาล")
        edit_selector = "button[aria-label='แก้ไขจักรวาล Marvel']"
        self.click(edit_selector, focus_first=True)
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        required_logo = self.cdp.evaluate(
            "(() => {const label=[...document.querySelectorAll('dialog.admin-modal[open] label')]"
            ".find(x=>x.textContent.includes('โลโก้จักรวาล'));"
            "return !!label && label.textContent.includes('จำเป็น') && !document.querySelector('.store-image-field__preview')})()"
        )
        self.check("seed Universe edit visibly requires its missing logo", required_logo)
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "บันทึกจักรวาล")
        self.cdp.wait("document.querySelector('.store-validation-summary')?.innerText.includes('กรุณาเลือกโลโก้จักรวาล')", self.timeout)
        self.check("seed Universe missing-logo feedback is Thai and summary-focused", self.cdp.evaluate("document.activeElement.matches('.store-validation-summary')"))
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)

        self.stage = "seed-universe-archive"
        archive_selector = "button[aria-label='เก็บจักรวาล Marvel']"
        self.click(archive_selector, focus_first=True)
        self.cdp.wait("document.activeElement.matches('[data-admin-archive-cancel]')", self.timeout)
        self.check("seed Universe archive opens cancel-first with reference summary", self.cdp.evaluate("document.body.innerText.includes('สินค้า 0 รายการ') && document.body.innerText.includes('ตัวละคร 0 รายการ')"))
        self.click("[data-admin-archive-cancel]")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)
        self.check("seed Universe archive cancel returns focus to its row action", self.cdp.evaluate("document.activeElement.matches(" + json.dumps(archive_selector) + ")"))

    def open_brand_create(self) -> None:
        self.navigate("/admin/brands")
        self.wait_heading("แบรนด์")
        time.sleep(0.6)  # DOM complete precedes the Interactive Server circuit becoming event-ready.
        self.click_text("button", "เพิ่มแบรนด์")
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)

    def fill_editor(self, thai: str, english: str, with_file: bool = True) -> None:
        self.fill(".admin-catalog-editor input[type=text]:nth-of-type(1)", thai)
        inputs = self.cdp.evaluate("document.querySelectorAll('.admin-catalog-editor input[type=text]').length")
        if inputs != 2:
            raise VerificationFailure("Catalog editor text-field contract changed")
        self.cdp.evaluate(
            "(() => { const e=document.querySelectorAll('.admin-catalog-editor input[type=text]')[1];"
            f"e.value={json.dumps(english)};e.dispatchEvent(new Event('input',{{bubbles:true}}));"
            "e.dispatchEvent(new Event('change',{bubbles:true}));return true;})()"
        )
        if with_file:
            self.set_file()

    def assert_blob_and_validation(self) -> None:
        self.open_brand_create()
        initial_files = self.environment.storage_file_count()
        self.cdp.evaluate(
            "window.__catalogBlob={created:[],revoked:[]};"
            "window.__nativeCreateObjectURL=URL.createObjectURL.bind(URL);"
            "window.__nativeRevokeObjectURL=URL.revokeObjectURL.bind(URL);"
            "URL.createObjectURL=x=>{const u=window.__nativeCreateObjectURL(x);window.__catalogBlob.created.push(u);return u};"
            "URL.revokeObjectURL=u=>{window.__catalogBlob.revoked.push(u);window.__nativeRevokeObjectURL(u)}"
        )
        self.set_file()
        self.cdp.wait("document.querySelector('.store-image-field__preview img')?.src.startsWith('blob:')", self.timeout)
        self.check("file selection creates an in-browser blob preview", self.cdp.evaluate("window.__catalogBlob.created.length === 1"))
        self.check("blob preview performs zero media-storage writes", self.environment.storage_file_count() == initial_files)
        self.cdp.evaluate("document.querySelector('.store-image-field__picker').focus()")
        self.check("custom file picker is keyboard focusable", self.cdp.evaluate("document.activeElement.matches('.store-image-field__picker')"))
        self.click_text(".store-image-field__clear", "ยกเลิกไฟล์ที่เลือก")
        self.cdp.wait("!document.querySelector('.store-image-field__preview')", self.timeout)
        self.check("cancel clears and revokes the blob preview", self.cdp.evaluate("window.__catalogBlob.revoked.length === 1") and self.environment.storage_file_count() == initial_files)

        self.click_text("dialog.admin-modal[open] button", "บันทึกแบรนด์")
        self.cdp.wait("document.querySelector('.store-validation-summary')?.innerText.trim().length > 0", self.timeout)
        summary = self.cdp.evaluate(
            "({focused:document.activeElement.matches('.store-validation-summary'),"
            "thai:document.querySelector('.store-validation-summary').innerText.includes('กรุณา')})"
        )
        self.check("Thai validation summary receives focus after invalid submit", summary["focused"] and summary["thai"], summary)
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)

    def assert_brand_mutations(self) -> None:
        self.stage = "brand-create"
        before = self.environment.storage_image_count()
        self.open_brand_create()
        self.fill_editor("แบรนด์ทดสอบ Chrome", "Chrome Test Brand")
        self.cdp.wait("document.querySelector('.store-image-field__preview img')?.src.startsWith('blob:')", self.timeout)
        self.click_text("dialog.admin-modal[open] button", "บันทึกแบรนด์")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]') && document.body.innerText.includes('เพิ่มแบรนด์ แบรนด์ทดสอบ Chrome แล้ว')", self.timeout)
        after_create = self.environment.storage_image_count()
        self.check(
            "Brand create succeeds and commits one media file",
            after_create == before + 1,
            {"before": before, "after": after_create},
        )

        self.stage = "brand-duplicate"
        self.click_text("button", "เพิ่มแบรนด์")
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        self.fill_editor("แบรนด์ทดสอบ Chrome", "Chrome Test Brand")
        self.cdp.wait("document.querySelector('.store-image-field__preview img')?.src.startsWith('blob:')", self.timeout)
        self.click_text("dialog.admin-modal[open] button", "บันทึกแบรนด์")
        self.cdp.wait("document.querySelector('.store-validation-summary')?.innerText.includes('ชื่อแบรนด์นี้มีอยู่แล้ว')", self.timeout)
        self.check("duplicate feedback is Thai and preserves the blob preview", self.cdp.evaluate("document.querySelector('.store-image-field__preview img')?.src.startsWith('blob:')"))
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)

        self.stage = "brand-commit-unknown"
        self.click_text("button", "เพิ่มแบรนด์")
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        self.fill_editor("แบรนด์ผลการบันทึกไม่ชัดเจน", "Unknown Commit Brand")
        self.cdp.wait("document.querySelector('.store-image-field__preview img')?.src.startsWith('blob:')", self.timeout)
        self.environment.proxy.arm()
        try:
            self.click_text("dialog.admin-modal[open] button", "บันทึกแบรนด์")
            if not self.environment.proxy.wait_tripped(self.timeout):
                raise VerificationFailure("The disposable commit fault did not trigger")
            self.cdp.wait(
                "document.querySelector('.store-validation-summary')?.innerText.includes('ยังยืนยันผลการบันทึกไม่ได้')",
                self.timeout,
            )
        finally:
            self.environment.proxy.disarm()
        self.check(
            "commit-acknowledgement loss shows safe Thai feedback and preserves preview",
            self.cdp.evaluate(
                "document.activeElement.matches('.store-validation-summary') && "
                "document.querySelector('.store-image-field__preview img')?.src.startsWith('blob:')"
            ),
        )
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)
        self.navigate("/admin/brands")
        self.wait_heading("แบรนด์")
        self.cdp.wait("document.body.innerText.includes('แบรนด์ผลการบันทึกไม่ชัดเจน')", self.timeout)
        self.check("commit-unknown refresh reveals the authoritative committed Brand", True)

        self.stage = "brand-stale-open"
        self.click("button[aria-label='แก้ไขแบรนด์ แบรนด์ทดสอบ Chrome']", focus_first=True)
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        self.stage = "brand-stale-submit"
        self.environment.bump_brand_version()
        self.cdp.evaluate(
            "(() => {const e=document.querySelector('.admin-catalog-editor input[type=text]');"
            "e.value='แบรนด์ทดสอบ Chrome ใหม่';e.dispatchEvent(new Event('input',{bubbles:true}));return true})()"
        )
        self.click_text("dialog.admin-modal[open] button", "บันทึกแบรนด์")
        self.cdp.wait("document.querySelector('.store-validation-summary')?.innerText.includes('ข้อมูลแบรนด์มีการเปลี่ยนแปลง')", self.timeout)
        self.check("stale-version feedback is Thai and summary-focused", self.cdp.evaluate("document.activeElement.matches('.store-validation-summary')"))
        self.click_text("dialog.admin-modal[open] .store-dialog__actions button", "ยกเลิก")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)
        self.stage = "brand-stale-reload"
        self.navigate("/admin/brands")
        self.wait_heading("แบรนด์")

        self.stage = "brand-busy-edit"
        self.click("button[aria-label='แก้ไขแบรนด์ แบรนด์ทดสอบ Chrome']", focus_first=True)
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        self.fill(
            ".admin-catalog-editor input[type=text]",
            "แบรนด์ Chrome สำเร็จ",
        )
        lock = self.environment.hold_brand_table()
        self.click_text("dialog.admin-modal[open] button", "บันทึกแบรนด์")
        self.cdp.wait("[...document.querySelectorAll('dialog.admin-modal[open] button')].some(x=>x.textContent.includes('กำลังบันทึก'))", self.timeout)
        busy_before = self.cdp.evaluate(
            "(() => {const d=document.querySelector('dialog.admin-modal[open]'),c=d?.querySelector('.store-dialog__close');"
            "const synthetic=new Event('cancel',{cancelable:true});d?.dispatchEvent(synthetic);"
            "window.__busyEscapeProbe={cancelSeen:false,prevented:false,closeSeen:false};"
            "document.addEventListener('keydown',e=>{if(e.key==='Escape'){"
            "window.__busyEscapeProbe.keydownSeen=true;window.__busyEscapeProbe.keydownCancelable=e.cancelable;"
            "window.__busyEscapeProbe.keydownPrevented=e.defaultPrevented}},{capture:true,once:true});"
            "d?.addEventListener('cancel',e=>{window.__busyEscapeProbe.cancelSeen=true;"
            "window.__busyEscapeProbe.prevented=e.defaultPrevented;window.__busyEscapeProbe.cancelable=e.cancelable;"
            "window.__busyEscapeProbe.dataDismissible=d.dataset.dismissible},{once:true});"
            "d?.addEventListener('close',()=>window.__busyEscapeProbe.closeSeen=true,{once:true});"
            "return {open:!!d,busyText:!!d&&d.innerText.includes('กำลังบันทึก'),"
            "closeDisabled:!!c?.disabled,closeAriaDisabled:c?.getAttribute('aria-disabled'),"
            "dataDismissible:d?.dataset.dismissible,syntheticPrevented:synthetic.defaultPrevented}})()"
        )
        escape_started = time.monotonic()
        self.key("Escape")
        busy_after = self.cdp.evaluate(
            "({open:!!document.querySelector('dialog.admin-modal[open]'),probe:window.__busyEscapeProbe})"
        )
        busy_details = {
            "before": busy_before,
            "after": busy_after,
            "lockAlive": lock.poll() is None,
            "elapsedMilliseconds": round((time.monotonic() - escape_started) * 1000),
        }
        self.check("Escape cannot dismiss a busy editor", busy_after["open"], busy_details)
        lock.wait(timeout=8)
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]') && document.body.innerText.includes('แบรนด์ Chrome สำเร็จ')", self.timeout)
        self.check("Brand edit succeeds after the busy operation completes", True)

        self.stage = "brand-archive"
        archive_selector = "button[aria-label='เก็บแบรนด์ แบรนด์ Chrome สำเร็จ']"
        self.click(archive_selector, focus_first=True)
        self.cdp.wait(f"!!document.querySelector('{self.visible_dialog()}')", self.timeout)
        self.cdp.wait("document.activeElement.matches('[data-admin-archive-cancel]')", self.timeout)
        self.check("archive dialog puts cancel first and focuses it", True)
        self.click("[data-admin-archive-cancel]")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]')", self.timeout)
        self.check("archive cancel returns focus to its row action", self.cdp.evaluate("document.activeElement.matches(" + json.dumps(archive_selector) + ")"))
        self.click(archive_selector, focus_first=True)
        self.cdp.wait("document.activeElement.matches('[data-admin-archive-cancel]')", self.timeout)
        self.click_text("dialog.admin-modal[open] button", "เก็บแบรนด์")
        self.cdp.wait("!document.querySelector('dialog.admin-modal[open]') && !document.querySelector(" + json.dumps(archive_selector) + ")", self.timeout)
        deadline = time.monotonic() + self.timeout
        while time.monotonic() < deadline and not self.cdp.evaluate(
            "document.activeElement === document.querySelector('h1')"
        ):
            time.sleep(0.1)
        focus_after_archive = self.cdp.evaluate(
            "({heading:document.activeElement === document.querySelector('h1'),"
            "activeTag:document.activeElement.tagName.toLowerCase(),"
            "activeClass:document.activeElement.className,"
            "activeConnected:document.activeElement.isConnected,"
            "headingTabIndex:document.querySelector('h1')?.tabIndex,"
            "archiveActionPresent:!!document.querySelector(" + json.dumps(archive_selector) + ")})"
        )
        self.check(
            "Brand archive succeeds and falls focus back to the page heading",
            focus_after_archive["heading"],
            focus_after_archive,
        )

    def run_catalog(self, email: str, temporary_password: str, password: str) -> None:
        self.cdp.call("Runtime.enable")
        self.cdp.call("Page.enable")
        self.cdp.call("DOM.enable")
        self.cdp.call("Network.enable")
        self.cdp.call("Network.clearBrowserCookies")
        self.stage = "authentication"
        self.authenticate(email, temporary_password, password)
        self.stage = "routes-and-seeds"
        self.assert_route_and_seed()
        self.stage = "responsive-layouts"
        self.assert_layouts()
        self.stage = "url-and-keyboard"
        self.assert_url_keyboard_and_select()
        self.assert_seed_universe_wiring()
        self.stage = "blob-and-validation"
        self.assert_blob_and_validation()
        self.stage = "brand-mutations"
        self.assert_brand_mutations()
        self.stage = "complete"


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    return parser.parse_args()


def main() -> int:
    args = arguments()
    generated = dt.datetime.now(dt.timezone.utc).isoformat()
    environment = DisposableEnvironment(args.timeout)
    smoke: CatalogSmoke | None = None
    browser: dict[str, Any] = {}
    error_category: str | None = None
    result = "failed"
    try:
        environment.start()
        cdp = CdpClient(environment.cdp_url, args.timeout)
        browser = cdp.call("Browser.getVersion")
        smoke = CatalogSmoke(cdp, environment, args.timeout)
        smoke.run_catalog(environment.email, environment.temporary_password, environment.password)
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
        "name": "M4-06 Thai Brand and Universe Admin real-Chrome verification",
        "generatedAtUtc": generated,
        "command": "python3 scripts/admin-catalog-browser.py",
        "browser": {"product": browser.get("product"), "revision": browser.get("revision")},
        "result": result,
        "assertions": smoke.assertions if smoke else [],
        "coverageNotes": [
            "All assertions use real production routes and a disposable migrated PostgreSQL database.",
            "A harness-owned plaintext PostgreSQL TCP proxy suppresses one armed COMMIT acknowledgement and blocks verification until the safe Thai commit-unknown feedback renders.",
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
