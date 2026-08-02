#!/usr/bin/env python3
"""Static contract checks for Silk Crest Overhaul.

This checker is intentionally conservative. It does not prove game compatibility;
it catches common Agent quality failures before local compile/run validation.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

FIXED_INPUT = re.compile(r"\bInput\.(GetKey|GetKeyDown|GetKeyUp|GetAxis|GetAxisRaw)\s*\(")
DIRECT_SILK_WRITE = re.compile(r"\.playerData\.silk\s*=")
EMPTY_CATCH = re.compile(r"catch\s*(?:\([^)]*\))?\s*\{\s*\}", re.S)
PLACEHOLDER = re.compile(r"REPLACE_WITH_|TODO_BINDING|NOT_BOUND_YET")
FEATURE_DEFINITION = re.compile(r"^\s*-\s*`((?:HUN|REA|WAN|BEA|WIT|ARC|SHA|MOT|CUR|USE|EXP|ULT)-\d{3})`", re.M)

ALLOWED_FIXED_INPUT_PATHS = {
    # Debug-only tooling may be added here after review. Gameplay files are never allowed.
}
ALLOWED_RAW_GAME_PATH_PARTS = {
    "GameInterop",
    "Patches",
    "Features/CrestSwitching",
}


def rel_posix(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def allowed_raw_game_access(rel: str) -> bool:
    return any(part in rel for part in ALLOWED_RAW_GAME_PATH_PARTS)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("root", nargs="?", default=".")
    args = ap.parse_args()
    root = Path(args.root).resolve()
    src = root / "src"
    failures: list[str] = []
    warnings: list[str] = []

    if not src.exists():
        failures.append(f"missing source directory: {src}")
    else:
        for path in src.rglob("*.cs"):
            rel = rel_posix(path, root)
            text = path.read_text(encoding="utf-8", errors="replace")

            if FIXED_INPUT.search(text) and rel not in ALLOWED_FIXED_INPUT_PATHS:
                failures.append(f"fixed physical input API in gameplay code: {rel}")

            if DIRECT_SILK_WRITE.search(text) and not allowed_raw_game_access(rel):
                failures.append(f"direct playerData.silk write outside interop/switch layer: {rel}")

            if "ToolItemManager.SetEquippedCrest" in text and "Features/CrestSwitching" not in rel and "GameInterop" not in rel:
                failures.append(f"crest set outside single switch owner: {rel}")

            if "ResetAllCrestState" in text and "Features/CrestSwitching" not in rel and "GameInterop" not in rel:
                failures.append(f"crest reset outside single switch owner: {rel}")

            if EMPTY_CATCH.search(text):
                warnings.append(f"empty catch requires compatibility justification/rate-limited diagnostics: {rel}")

            if ("Update(" in text or "FixedUpdate(" in text) and "FindObjectsOfType" in text:
                failures.append(f"scene-wide object scan in update path: {rel}")

    docs = root / "docs" / "03_功能整合.md"
    if docs.exists():
        text = docs.read_text(encoding="utf-8", errors="replace")
        ids = FEATURE_DEFINITION.findall(text)
        duplicates = sorted({x for x in ids if ids.count(x) > 1})
        if duplicates:
            warnings.append("duplicate Feature IDs in human-readable requirements: " + ", ".join(duplicates))
    else:
        failures.append("missing docs/03_功能整合.md")

    binding = root / "config" / "game-bindings.json"
    if binding.exists():
        try:
            data = json.loads(binding.read_text(encoding="utf-8"))
            raw = json.dumps(data)
            if PLACEHOLDER.search(raw):
                warnings.append("game-bindings.json still contains placeholders; affected features must remain disabled")
        except Exception as exc:
            failures.append(f"invalid game-bindings.json: {exc}")
    else:
        failures.append("missing config/game-bindings.json")

    skill = root / "skills" / "silksong-crest-overhaul" / "SKILL.md"
    if not skill.exists():
        failures.append("missing mandatory Agent skill")

    print("Silk Crest Overhaul contract validation")
    for item in warnings:
        print("WARN:", item)
    for item in failures:
        print("FAIL:", item)
    if failures:
        print(f"Result: FAILED ({len(failures)} failures, {len(warnings)} warnings)")
        return 1
    print(f"Result: PASSED ({len(warnings)} warnings)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
