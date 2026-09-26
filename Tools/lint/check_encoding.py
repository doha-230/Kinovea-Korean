#!/usr/bin/env python3
"""Check the fork invariant: every source/project file is valid UTF-8 with a BOM.

Needed because the C# compiler decodes source files using the system code page
unless a BOM is present, which is what made the build fail on Korean/CP949
Windows (CS1010/CS1003) before this fork normalized the encoding.

Usage:
  python3 Tools/lint/check_encoding.py [--root .] [--baseline FILE] [--write-baseline FILE]
"""
import argparse
import os
import sys

EXTS = {'.cs', '.vb', '.cpp', '.h', '.hpp', '.c', '.cc', '.cxx',
        '.resx', '.csproj', '.vbproj', '.props', '.targets',
        '.config', '.settings', '.manifest', '.nsi'}
SKIP_DIRS = {'.git', 'bin', 'obj', 'Refs', '.vs', 'node_modules'}
BOM = b'\xef\xbb\xbf'


def scan(root):
    findings = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            if os.path.splitext(fn)[1].lower() not in EXTS:
                continue
            p = os.path.join(dirpath, fn)
            rel = os.path.relpath(p, root).replace(os.sep, '/')
            try:
                raw = open(p, 'rb').read()
            except OSError:
                continue
            if not raw.startswith(BOM):
                findings.append(f"{rel} :: missing UTF-8 BOM")
            try:
                raw.decode('utf-8')
            except UnicodeDecodeError as e:
                findings.append(f"{rel} :: invalid UTF-8 ({e})")
    return sorted(findings)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--baseline')
    ap.add_argument('--write-baseline')
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
        print(f"encoding violations: {len(findings)} total, {len(new)} new")
        for f in new:
            print(f"  NEW  {f}")
        return 1 if new else 0

    print(f"encoding violations: {len(findings)}")
    for f in findings:
        print(f"  {f}")
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main())
