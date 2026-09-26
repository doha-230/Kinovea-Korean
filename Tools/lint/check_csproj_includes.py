#!/usr/bin/env python3
"""Check that every .cs/.vb file on disk is referenced by a .csproj.

This solution uses non-SDK projects with explicit <Compile Include> items and no
wildcards, so a new file that is not registered in its project never compiles and
produces CS0246 at the first use site.

Usage:
  python3 Tools/lint/check_csproj_includes.py [--root .] [--baseline FILE] [--write-baseline FILE]
"""
import argparse
import os
import sys

SKIP_DIRS = {'.git', 'bin', 'obj', 'Refs', '.vs', 'node_modules'}


def scan(root):
    proj_texts, sources = [], []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for fn in filenames:
            p = os.path.join(dirpath, fn)
            if fn.endswith('.csproj'):
                try:
                    proj_texts.append(open(p, encoding='utf-8-sig', errors='replace').read())
                except OSError:
                    pass
            elif fn.endswith('.cs') or fn.endswith('.vb'):
                sources.append(p)
    blob = '\n'.join(proj_texts)
    return sorted(os.path.relpath(p, root).replace(os.sep, '/')
                  for p in sources if os.path.basename(p) not in blob)


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
        print(f"unregistered source files: {len(findings)} total, {len(new)} new")
        for f in new:
            print(f"  NEW  {f}")
        return 1 if new else 0

    print(f"unregistered source files: {len(findings)}")
    for f in findings:
        print(f"  {f}")
    return 1 if findings else 0


if __name__ == '__main__':
    sys.exit(main())
