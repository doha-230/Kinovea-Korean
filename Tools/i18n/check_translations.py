#!/usr/bin/env python3
"""Check translation completeness for a language against the base .resx files.

Compares every `<X>.<lang>.resx` with its base `<X>.resx` and reports keys that
are missing from (or extra in) the translation.

The resx template header contains sample rows (Bitmap1/Color1/Icon1/Name1) that
are NOT real translatable strings, so they are ignored by default.

Usage:
  python3 Tools/i18n/check_translations.py [--root .] [--lang ko] [--strict]

Exit code: 0 = complete, 1 = missing keys found.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import sys

NAME_RE = re.compile(r'<data\s+name="([^"]+)"')

# Boilerplate rows present in the resx template comment block, not real strings.
IGNORE_KEYS = {'Bitmap1', 'Color1', 'Icon1', 'Name1'}


def keys(path: str) -> set[str]:
    try:
        with open(path, encoding='utf-8-sig', errors='replace') as fh:
            text = fh.read()
    except OSError:
        return set()
    return {k for k in NAME_RE.findall(text)}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--lang', default='ko')
    ap.add_argument('--strict', action='store_true',
                    help='also fail on extra keys (present in translation but not in base)')
    args = ap.parse_args()

    pattern = os.path.join(args.root, '**', f'*.{args.lang}.resx')
    total_missing = 0
    total_keys = 0
    rows = []

    for tr in sorted(glob.glob(pattern, recursive=True)):
        base = re.sub(rf'\.{re.escape(args.lang)}\.resx$', '.resx', tr)
        if not os.path.exists(base):
            rows.append((os.path.relpath(tr, args.root), 0, 0, 0, 'NO BASE'))
            continue
        base_keys = keys(base) - IGNORE_KEYS
        tr_keys = keys(tr) - IGNORE_KEYS
        missing = base_keys - tr_keys
        extra = tr_keys - base_keys
        total_missing += len(missing)
        total_keys += len(base_keys)
        rows.append((os.path.relpath(tr, args.root), len(base_keys), len(missing), len(extra),
                     ','.join(sorted(missing)[:5])))

    print(f"language: {args.lang}")
    print(f"{'resx':66s} {'KEYS':>6s} {'MISS':>6s} {'EXTRA':>6s}")
    for rel, nk, nm, ne, sample in rows:
        flag = '  <-- ' + sample if nm else ''
        print(f"{rel:66s} {nk:6d} {nm:6d} {ne:6d}{flag}")

    pct = 100.0 * (total_keys - total_missing) / total_keys if total_keys else 100.0
    print(f"\nTOTAL keys={total_keys} missing={total_missing} coverage={pct:.1f}%")

    failed = total_missing > 0
    if args.strict and any(r[3] for r in rows):
        failed = True
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
