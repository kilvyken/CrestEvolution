#!/usr/bin/env python3
from pathlib import Path
import sys

if len(sys.argv) < 3:
    print('Usage: make_probe_request.py FEATURE_ID topic')
    raise SystemExit(2)
feature_id, topic = sys.argv[1], sys.argv[2]
root = Path(__file__).resolve().parents[1]
dir_ = root / 'artifacts' / 'probes' / f'{feature_id}_{topic}'
dir_.mkdir(parents=True, exist_ok=True)
template = (
    f"# Probe request: {feature_id} / {topic}\n\n"
    "## Goal\n\n"
    "## In-game reproduction steps\n\n"
    "## Types/methods to locate\n\n"
    "## CUEP/FSM pages to capture\n\n"
    "## Expected output files\n\n"
)
(dir_/'request.md').write_text(template, encoding='utf-8')
print(dir_)
