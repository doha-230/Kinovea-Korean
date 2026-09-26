#!/usr/bin/env python3
"""Scan C# sources for culture-sensitive number parsing/formatting.

A `float.Parse(s)` / `double.Parse(s)` without an explicit culture uses
CultureInfo.CurrentCulture, so it breaks on locales whose decimal separator is
not '.' (de, fr, ...). Kinovea reads/writes fixed, '.'-separated file formats
(TRC, KVA, ...), so these must use CultureInfo.InvariantCulture.

Usage:
  python3 Tools/lint/check_culture_parsing.py [--root .] [--baseline FILE] [--write-baseline FILE]

Exit code: 0 = clean (or no new findings vs baseline), 1 = violations.
"""
from __future__ import annotations

import argparse
import os
import re
import sys

# float/double/decimal.Parse(...) not followed by a culture argument on the same line.
PARSE_RE = re.compile(r'\b(?:float|double|decimal)\.Parse\s*\(')
SKIP_DIRS = {'.git', 'bin', 'obj', 'Refs', '.vs', 'node_modules', 'packages'}
EXT = '.cs'


def has_culture(line: str) -> bool:
    return ('CultureInfo' in line) or ('NumberFormatInfo' in line) or ('Invariant' in line)


def scan(root: str) -> list[str]:
    findings: list[str] = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if not fn.endswith(EXT):
                continue
            path = os.path.join(dirpath, fn)
            try:
                with open(path, encoding='utf-8-sig', errors='replace') as fh:
                    lines = fh.readlines()
            except OSError:
                continue
            for i, line in enumerate(lines, 1):
                stripped = line.lstrip()
                if stripped.startswith('//') or stripped.startswith('*'):
                    continue
                if PARSE_RE.search(line) and not has_culture(line):
                    rel = os.path.relpath(path, root).replace(os.sep, '/')
                    findings.append(f"{rel}:{i}: {stripped.strip()}")
    return sorted(findings)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--baseline', help='fail only on findings not present in this file')
    ap.add_argument('--write-baseline', help='write current findings to this file and exit 0')
    args = ap.parse_args()

    findings = scan(args.root)

    if args.write_baseline:
        with open(args.write_baseline, 'w', encoding='utf-8') as fh:
            fh.write('\n'.join(findings) + ('\n' if findings else ''))
        print(f"wrote {len(findings)} findings to {args.write_baseline}")
        return 0

    if args.baseline and os.path.exists(args.baseline):
        with open(args.baseline, encoding='utf-8') as fh:
            known = {ln.strip() for ln in fh if ln.strip()}
        new = [f for f in findings if f not in known]
        print(f"culture-sensitive parses: {len(findings)} total, {len(new)} new")
        for f in new:
            print(f"  NEW  {f}")
        return 1 if new else 0

    print(f"culture-sensitive parses: {len(findings)}")
    for f in findings:
        print(f"  {f}")
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main())
