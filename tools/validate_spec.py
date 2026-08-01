#!/usr/bin/env python3
from pathlib import Path
import json, re, sys

root = Path(__file__).resolve().parents[1]
feature_doc = (root / 'docs' / '03_功能整合.md').read_text(encoding='utf-8')
ids = re.findall(r'^- `([A-Z][A-Z0-9-]+-\d{3})`', feature_doc, flags=re.MULTILINE)
duplicates = sorted({x for x in ids if ids.count(x) > 1})
if duplicates:
    print('Duplicate Feature IDs:', duplicates)
    sys.exit(1)

for rel in ['spec/source_paragraphs.json', 'spec/feature_catalog.json', 'config/game-bindings.json', 'config/balance.example.json']:
    with (root / rel).open(encoding='utf-8') as f:
        json.load(f)

required = [
    'docs/01_开发计划.md', 'docs/02_代码参考.md', 'docs/03_功能整合.md',
    'docs/04_实操辅助.md', 'docs/05_高难代码示例.md'
]
missing = [x for x in required if not (root/x).exists()]
if missing:
    print('Missing required files:', missing)
    sys.exit(1)

print(f'OK: {len(ids)} unique Feature IDs; JSON and required files are valid.')
