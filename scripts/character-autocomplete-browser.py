#!/usr/bin/env python3
"""M4-07 Character autocomplete real-Chrome verification using the standard library.

Run from the repository root:

    python3 scripts/character-autocomplete-browser.py

The harness reuses the retained disposable application/Chrome environment and talks
directly to the Chrome DevTools Protocol. It exercises only the fake-data specimen
on ``/design-system`` and installs no Playwright, Selenium, Node or browser package.
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
ENVIRONMENT_SCRIPT = ROOT / "scripts" / "admin-catalog-browser.py"
DEFAULT_REPORT = ROOT / "artifacts" / "browser" / "character-autocomplete-browser-report.json"


def load_environment_types() -> tuple[type, type, type, type]:
    spec = importlib.util.spec_from_file_location(
        "admin_catalog_browser", ENVIRONMENT_SCRIPT
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the retained Chrome environment")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return (
        module.CdpClient,
        module.AdminShellSmoke,
        module.DisposableEnvironment,
        module.VerificationFailure,
    )


CdpClient, AdminShellSmoke, DisposableEnvironment, VerificationFailure = (
    load_environment_types()
)


class CharacterAutocompleteSmoke(AdminShellSmoke):
    INPUT = "#design-character-autocomplete"
    LISTBOX = "#design-character-autocomplete-listbox"
    INLINE = "#design-character-autocomplete-inline-create"
    SPECIMEN = "[data-character-autocomplete-specimen]"

    def __init__(self, cdp: Any, base_url: str, timeout: float) -> None:
        super().__init__(cdp, base_url, timeout)
        self.stage = "startup"

    def key(self, value: str, code: str | None = None, key_code: int = 0) -> None:
        event = {
            "key": value,
            "code": code or value,
            "windowsVirtualKeyCode": key_code,
            "nativeVirtualKeyCode": key_code,
        }
        self.cdp.call("Input.dispatchKeyEvent", {"type": "rawKeyDown", **event})
        self.cdp.call("Input.dispatchKeyEvent", {"type": "keyUp", **event})

    def mouse_click(self, selector: str) -> None:
        self.cdp.evaluate(
            "(() => { const e=document.querySelector(" + json.dumps(selector) + ");"
            "if(!e) return false; e.scrollIntoView({behavior:'instant',block:'center',inline:'nearest'}); return true; })()"
        )
        time.sleep(0.1)
        point = self.cdp.evaluate(
            "(() => { const e=document.querySelector(" + json.dumps(selector) + ");"
            "if(!e) return null; const r=e.getBoundingClientRect();"
            "return {x:r.left+r.width/2,y:r.top+r.height/2}; })()"
        )
        if point is None:
            raise VerificationFailure(f"Pointer target was not found: {selector}")
        for kind in ("mousePressed", "mouseReleased"):
            self.cdp.call(
                "Input.dispatchMouseEvent",
                {
                    "type": kind,
                    "x": point["x"],
                    "y": point["y"],
                    "button": "left",
                    "clickCount": 1,
                },
            )

    def search(self, term: str) -> None:
        self.fill(self.INPUT, term)
        time.sleep(0.5)
        self.cdp.wait(
            f"document.querySelector({json.dumps(self.INPUT)})?.getAttribute('aria-expanded') === 'true'",
            self.timeout,
        )

    def selection_count(self) -> int:
        return int(
            self.cdp.evaluate(
                f"document.querySelectorAll({json.dumps(self.SPECIMEN + ' .store-autocomplete__chip')}).length"
            )
        )

    def create_count(self) -> int:
        return int(
            self.cdp.evaluate(
                f"Number(document.querySelector({json.dumps(self.SPECIMEN)})"
                ".getAttribute('data-create-count'))"
            )
        )

    def assert_semantics_and_existing_selection(self) -> None:
        self.stage = "semantics-keyboard"
        focus = self.cdp.evaluate(
            "(() => {const e=document.querySelector(" + json.dumps(self.INPUT) + ");"
            "e.focus();return {focused:document.activeElement===e,disabled:e.disabled,"
            "rect:e.getBoundingClientRect().toJSON()};})()"
        )
        if not focus["focused"] or focus["disabled"]:
            raise VerificationFailure(f"Combobox cannot receive focus: {focus}")
        self.fill(self.INPUT, "")
        self.cdp.wait(
            f"document.querySelector({json.dumps(self.INPUT)})?.getAttribute('aria-expanded') === 'true'",
            self.timeout,
        )
        try:
            self.cdp.wait(
                f"document.querySelectorAll({json.dumps(self.LISTBOX + ' [role=option]')}).length === 3",
                self.timeout,
            )
        except Exception as exception:
            snapshot = self.cdp.evaluate(
                "({expanded:document.querySelector(" + json.dumps(self.INPUT) + ")?.getAttribute('aria-expanded'),"
                "options:document.querySelectorAll(" + json.dumps(self.LISTBOX + " [role=option]") + ").length,"
                "status:document.querySelector('#design-character-autocomplete-status')?.innerText,"
                "alert:document.querySelector('#design-character-autocomplete-alert')?.innerText})"
            )
            raise VerificationFailure(f"Initial fake search failed: {snapshot}") from exception
        semantics = self.cdp.evaluate(
            "(() => { const box=document.querySelector(" + json.dumps(self.LISTBOX) + ");"
            "const options=[...box.querySelectorAll('[role=option]')];"
            "return {combo:document.querySelector(" + json.dumps(self.INPUT) + ").getAttribute('role'),"
            "multi:box.getAttribute('aria-multiselectable'),"
            "direct:options.every(x=>x.parentElement===box),"
            "nested:options.some(x=>x.querySelector('button,a,input,select,textarea'))}; })()"
        )
        self.check("combobox and multiselect listbox semantics are present",
                   semantics["combo"] == "combobox" and semantics["multi"] == "true", semantics)
        self.check("every role=option is a direct non-interactive listbox child",
                   semantics["direct"] and not semantics["nested"], semantics)

        self.search("Iron")
        self.cdp.wait(
            f"document.querySelectorAll({json.dumps(self.LISTBOX + ' .store-autocomplete__option:not(.store-autocomplete__option--create)')}).length === 1",
            self.timeout,
        )
        self.key("ArrowDown", "ArrowDown", 40)
        self.cdp.wait(
            f"!!document.querySelector({json.dumps(self.INPUT)}).getAttribute('aria-activedescendant')",
            self.timeout,
        )
        active = self.cdp.evaluate(
            "(() => {const i=document.querySelector(" + json.dumps(self.INPUT) + ");"
            "const id=i.getAttribute('aria-activedescendant'),e=document.getElementById(id);"
            "return {id,role:e?.getAttribute('role'),direct:e?.parentElement===document.querySelector("
            + json.dumps(self.LISTBOX) + ")};})()"
        )
        self.check("ArrowDown exposes an existing option through aria-activedescendant",
                   bool(active["id"]) and active["role"] == "option" and active["direct"], active)
        self.key("Enter", "Enter", 13)
        self.cdp.wait(
            f"document.querySelectorAll({json.dumps(self.SPECIMEN + ' .store-autocomplete__chip')}).length === 1",
            self.timeout,
        )
        self.check("Enter selects an existing Character exactly once", self.selection_count() == 1)
        self.check("keyboard selection retains combobox focus",
                   self.cdp.evaluate("document.activeElement === document.querySelector(" + json.dumps(self.INPUT) + ")"))

        self.search("Bat")
        self.mouse_click(self.LISTBOX + " [role=option]")
        self.cdp.wait(
            f"document.querySelectorAll({json.dumps(self.SPECIMEN + ' .store-autocomplete__chip')}).length === 2",
            self.timeout,
        )
        self.check("real pointer selection publishes one additional value", self.selection_count() == 2)
        self.check("pointer selection retains combobox focus through mousedown prevention",
                   self.cdp.evaluate("document.activeElement === document.querySelector(" + json.dumps(self.INPUT) + ")"))

    def assert_inline_create_and_duplicate_suppression(self) -> None:
        self.stage = "inline-create"
        self.search("Moon Knight")
        self.key("ArrowDown", "ArrowDown", 40)
        self.cdp.wait(
            f"document.querySelector({json.dumps(self.INPUT)}).getAttribute('aria-activedescendant') === "
            + json.dumps(self.INLINE[1:]),
            self.timeout,
        )
        pseudo = self.cdp.evaluate(
            "(() => {const i=document.querySelector(" + json.dumps(self.INPUT) + ");"
            "const id=i.getAttribute('aria-activedescendant'),e=document.getElementById(id);"
            "return {id,expected:" + json.dumps(self.INLINE[1:]) + ",role:e?.getAttribute('role'),"
            "direct:e?.parentElement===document.querySelector(" + json.dumps(self.LISTBOX) + ")};})()"
        )
        self.check("aria-activedescendant reaches the direct inline pseudo-option",
                   pseudo["id"] == pseudo["expected"] and pseudo["role"] == "option" and pseudo["direct"], pseudo)
        self.key("Enter", "Enter", 13)
        self.cdp.wait(
            f"document.querySelector({json.dumps(self.INLINE)})?.getAttribute('aria-disabled') === 'true'",
            self.timeout,
        )
        self.key("Enter", "Enter", 13)
        self.cdp.wait(
            f"document.querySelectorAll({json.dumps(self.SPECIMEN + ' .store-autocomplete__chip')}).length === 3",
            self.timeout,
        )
        self.check("busy Enter cannot submit inline create twice",
                   self.create_count() == 1 and self.selection_count() == 3,
                   {"creates": self.create_count(), "selected": self.selection_count()})

        self.search("Robin")
        self.mouse_click(self.INLINE)
        self.mouse_click(self.INLINE)
        self.cdp.wait(
            f"document.querySelectorAll({json.dumps(self.SPECIMEN + ' .store-autocomplete__chip')}).length === 4",
            self.timeout,
        )
        self.check("busy pointer clicks cannot submit inline create twice",
                   self.create_count() == 2 and self.selection_count() == 4,
                   {"creates": self.create_count(), "selected": self.selection_count()})

    def assert_close_focus_and_ime(self) -> None:
        self.stage = "focus-ime"
        self.search("Spi")
        self.key("Escape", "Escape", 27)
        self.cdp.wait(
            f"document.querySelector({json.dumps(self.INPUT)}).getAttribute('aria-expanded') === 'false'",
            self.timeout,
        )
        time.sleep(0.3)
        self.check("Escape closes without reopening the listbox",
                   self.cdp.evaluate("document.querySelector(" + json.dumps(self.LISTBOX) + ").hidden"))

        self.search("Spi")
        self.key("Tab", "Tab", 9)
        time.sleep(0.3)
        tab = self.cdp.evaluate(
            "({closed:document.querySelector(" + json.dumps(self.LISTBOX) + ").hidden,"
            "moved:document.activeElement!==document.querySelector(" + json.dumps(self.INPUT) + ")})"
        )
        self.check("Tab moves naturally and does not reopen", tab["closed"] and tab["moved"], tab)

        self.cdp.evaluate(f"document.querySelector({json.dumps(self.INPUT)}).focus()")
        self.cdp.wait(
            f"document.querySelector({json.dumps(self.INPUT)}).getAttribute('aria-expanded') === 'true'",
            self.timeout,
        )
        self.mouse_click("[data-character-after]")
        time.sleep(0.3)
        blur = self.cdp.evaluate(
            "({closed:document.querySelector(" + json.dumps(self.LISTBOX) + ").hidden,"
            "after:document.activeElement.matches('[data-character-after]')})"
        )
        self.check("blur closes without reopening and preserves next focus", blur["closed"] and blur["after"], blur)

        before = {"selected": self.selection_count(), "creates": self.create_count()}
        self.cdp.evaluate(f"document.querySelector({json.dumps(self.INPUT)}).focus()")
        self.fill(self.INPUT, "IME Hero")
        self.cdp.evaluate(
            "document.querySelector(" + json.dumps(self.INPUT) + ")"
            ".dispatchEvent(new CompositionEvent('compositionstart',{bubbles:true,data:'ไ'}))"
        )
        self.key("ArrowDown", "ArrowDown", 40)
        self.key("Enter", "Enter", 13)
        composing = {
            "selected": self.selection_count(),
            "creates": self.create_count(),
        }
        self.check("IME composition suppresses Arrow and Enter actions", composing == before, composing)
        self.cdp.evaluate(
            "document.querySelector(" + json.dumps(self.INPUT) + ")"
            ".dispatchEvent(new CompositionEvent('compositionend',{bubbles:true,data:'ไ'}))"
        )
        self.cdp.wait(
            f"!document.querySelector({json.dumps(self.INLINE)})?.hidden && "
            f"document.querySelector({json.dumps(self.INPUT)}).getAttribute('aria-expanded') === 'true'",
            self.timeout,
        )
        self.check("compositionend resumes the cancellable search", True)

    def assert_responsive_targets_and_motion(self) -> None:
        self.stage = "responsive-motion"
        for width in (390, 768, 1200):
            self.set_viewport(width, 900)
            metrics = self.cdp.evaluate(
                "(() => {const s=document.querySelector(" + json.dumps(self.SPECIMEN) + "),"
                "chips=s.querySelector('.store-autocomplete__chips'),"
                "targets=[...s.querySelectorAll('button,input')].filter(x=>x.offsetParent!==null)"
                ".map(x=>x.getBoundingClientRect());"
                "const rows=chips?[...new Set([...chips.children].map(x=>Math.round(x.getBoundingClientRect().top)))].length:0;"
                "return {body:document.documentElement.scrollWidth>document.documentElement.clientWidth,"
                "specimen:s.scrollWidth>s.clientWidth,targets:targets.every(x=>x.width>=44&&x.height>=44),"
                "wrap:chips?getComputedStyle(chips).flexWrap:null,rows};})()"
            )
            self.check(f"{width}px specimen has no horizontal overflow",
                       not metrics["body"] and not metrics["specimen"], metrics)
            self.check(f"{width}px visible input and buttons keep 44px targets",
                       metrics["targets"], metrics)
            self.check(f"{width}px selected chips use wrapping layout",
                       metrics["wrap"] == "wrap" and (width != 390 or metrics["rows"] >= 2), metrics)

        self.cdp.call(
            "Emulation.setEmulatedMedia",
            {"features": [{"name": "prefers-reduced-motion", "value": "reduce"}]},
        )
        duration = self.cdp.evaluate(
            "getComputedStyle(document.querySelector(" + json.dumps(self.INLINE) + ")).transitionDuration"
        )
        self.check("prefers-reduced-motion minimizes option transitions",
                   duration in ("0s", "1e-05s", "0.00001s"), duration)
        self.cdp.call("Emulation.setEmulatedMedia", {"features": []})

    def run_character_autocomplete(self) -> None:
        self.cdp.call("Runtime.enable")
        self.cdp.call("Page.enable")
        self.cdp.call("DOM.enable")
        self.navigate("/design-system")
        self.cdp.wait(
            f"!!document.querySelector({json.dumps(self.SPECIMEN)}) && "
            f"!!document.querySelector({json.dumps(self.INPUT)})",
            self.timeout,
        )
        time.sleep(0.7)
        self.check("fake-data Character specimen renders on /design-system", True)
        self.assert_semantics_and_existing_selection()
        self.assert_inline_create_and_duplicate_suppression()
        self.assert_close_focus_and_ime()
        self.assert_responsive_targets_and_motion()
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
    smoke: CharacterAutocompleteSmoke | None = None
    browser: dict[str, Any] = {}
    result = "failed"
    error_category: str | None = None
    try:
        environment.start()
        cdp = CdpClient(environment.cdp_url, args.timeout)
        browser = cdp.call("Browser.getVersion")
        smoke = CharacterAutocompleteSmoke(cdp, environment.base_url, args.timeout)
        smoke.run_character_autocomplete()
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
        "name": "M4-07 Character autocomplete real-Chrome verification",
        "generatedAtUtc": generated,
        "command": "python3 scripts/character-autocomplete-browser.py",
        "browser": {"product": browser.get("product"), "revision": browser.get("revision")},
        "result": result,
        "assertions": smoke.assertions if smoke else [],
        "coverageNotes": [
            "The real reusable Admin/shared component runs against page-local fake data only.",
            "Chrome is controlled directly through CDP with no browser automation package.",
        ],
        "cleanup": cleanup,
    }
    if error_category:
        report["errorCategory"] = error_category
        report["failureStage"] = smoke.stage if smoke else "environment-startup"
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"REPORT assertions={len(report['assertions'])} result={result}", flush=True)
    return 0 if result == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
