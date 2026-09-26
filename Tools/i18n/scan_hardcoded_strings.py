#!/usr/bin/env python3
"""Find user-facing strings hard-coded in C# code instead of living in .resx.

Targets only the patterns that actually put text in front of the user:
  control.Text = "literal"
  control.Items.Add("literal")
  MessageBox.Show("literal", ...)
  .ToolTipText = "literal"

Usage:
  python3 Tools/i18n/scan_hardcoded_strings.py [--root .] [--baseline FILE] [--write-baseline FILE] [--csv FILE]

Exit code: 0 = clean (or no new findings vs baseline), 1 = violations.
"""
from __future__ import annotations

import argparse
import os
import re
import sys

SKIP_DIRS = {'.git', 'bin', 'obj', 'Refs', '.vs', 'node_modules'}

PATTERNS = [
    re.compile(r'\.Text\s*=\s*"([^"\\]{2,})"'),           # .Text = "Foo"
    re.compile(r'\.Items\.Add\s*\(\s*"([^"\\]{2,})"'),    # .Items.Add("Foo")
    re.compile(r'MessageBox\.Show\s*\(\s*"([^"\\]{2,})"'),
    re.compile(r'\.ToolTipText\s*=\s*"([^"\\]{2,})"'),
]

# Short/technical literals that are legitimately kept as-is.
ALLOWLIST = {
    '[h:][mm:]ss.xx[x]',
    '[h:][mm:]ss.xx[x] + ',
    'www.kinovea.org',
    'RGGB', 'BGGR', 'GRBG', 'GBRG',  # Bayer patterns (proper nouns)
    'X', 'Y', 'Z', '<', '>', '-',
    'MP4',
    'MKV',
    'AVI',
    'MJPEG',
    'H.264',
    'H.265',
    'JPG',
    'PNG',
    'BMP',
    'ORB',
    'SIFT',
    'GOP',
    'Raw',
    'Mono',
    'Color',
    'JSON',
    'CSV',
}


def scan(root: str):
    findings = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if not fn.endswith('.cs') or fn.endswith('.Designer.cs'):
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
                for rx in PATTERNS:
                    m = rx.search(line)
                    if m:
                        val = m.group(1)
                        if val in ALLOWLIST:
                            continue
                        # Ignore whitespace-only / symbol-only literals (layout artifacts).
                        if not any(ch.isalnum() for ch in val):
                            continue
                        rel = os.path.relpath(path, root).replace(os.sep, '/')
                        findings.append((rel, i, val))
    return findings


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--baseline', help='fail only on findings not present in this file')
    ap.add_argument('--write-baseline', help='write current findings to this file and exit 0')
    ap.add_argument('--csv', help='also write findings as CSV')
    args = ap.parse_args()

    findings = scan(args.root)
    # Key findings by file+string (not line number) so that unrelated edits do not
    # invalidate the baseline.
    lines = [f"{p} :: {v}".strip() for p, i, v in findings]

    if args.csv:
        import csv
        with open(args.csv, 'w', encoding='utf-8', newline='') as fh:
            w = csv.writer(fh)
            w.writerow(['file', 'line', 'string'])
            w.writerows(findings)

    if args.write_baseline:
        with open(args.write_baseline, 'w', encoding='utf-8') as fh:
            fh.write('\n'.join(lines) + ('\n' if lines else ''))
        print(f"wrote {len(lines)} findings to {args.write_baseline}")
        return 0

    if args.baseline and os.path.exists(args.baseline):
        with open(args.baseline, encoding='utf-8') as fh:
            known = {ln.strip() for ln in fh if ln.strip()}
        new = [ln for ln in lines if ln not in known]
        print(f"hard-coded UI strings: {len(lines)} total, {len(new)} new")
        for ln in new:
            print(f"  NEW  {ln}")
        return 1 if new else 0

    print(f"hard-coded UI strings: {len(lines)}")
    for ln in lines:
        print(f"  {ln}")
    return 1 if lines else 0


if __name__ == '__main__':
    sys.exit(main())
