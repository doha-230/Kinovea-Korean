#!/usr/bin/env python3
"""Lock the fork's preference defaults so they keep matching upstream.

The fork's rule is: an untouched installation behaves exactly like upstream
Kinovea, and every improvement is opt-in. That only holds while the *default
values* of the preferences stay what they are, so this check fails when an
existing default changes or disappears. New preferences are reported and must be
recorded in the baseline with --write-baseline (and they must default to the
upstream behaviour).

Usage:
  python3 Tools/lint/check_preference_defaults.py [--baseline FILE] [--write-baseline FILE]

Exit code: 0 = clean (or no new findings vs baseline), 1 = violations.
"""
from __future__ import annotations

import argparse
import re
import sys

PREFS = 'Kinovea.Services/Preferences/PlayerPreferences.cs'
# private <type> <name> = <value>;
FIELD_RE = re.compile(r'^ {8}private\s+(?:readonly\s+)?[\w<>\[\]\.]+\s+(\w+)\s*=\s*([^;]+);', re.M)


def collect(path):
    text = open(path, encoding='utf-8-sig').read()
    return {m.group(1): ' '.join(m.group(2).split()) for m in FIELD_RE.finditer(text)}


def read_baseline(path):
    out = {}
    try:
        for line in open(path, encoding='utf-8'):
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            name, _, value = line.partition('=')
            out[name.strip()] = value.strip()
    except FileNotFoundError:
        pass
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--prefs', default=PREFS)
    ap.add_argument('--baseline', default='Tools/lint/baseline-preference-defaults.txt')
    ap.add_argument('--write-baseline', default=None)
    args = ap.parse_args()

    current = collect(args.prefs)

    if args.write_baseline:
        with open(args.write_baseline, 'w', encoding='utf-8') as f:
            f.write('# Preference defaults. A changed or removed entry fails the check:\n')
            f.write('# an untouched installation must behave like upstream.\n')
            for name in sorted(current):
                f.write('{0}={1}\n'.format(name, current[name]))
        print('baseline written: {0} ({1} fields)'.format(args.write_baseline, len(current)))
        return 0

    baseline = read_baseline(args.baseline)
    changed = []
    removed = []
    for name, value in sorted(baseline.items()):
        if name not in current:
            removed.append(name)
        elif current[name] != value:
            changed.append((name, value, current[name]))
    new = sorted(set(current) - set(baseline)) if baseline else []

    for name, old, now in changed:
        print('ERROR changed default: {0}: {1} -> {2}'.format(name, old, now))
    for name in removed:
        print('ERROR missing preference: {0}'.format(name))

    if not baseline:
        print('no baseline yet: run with --write-baseline first')
    else:
        print('preference defaults: {0} checked, {1} changed, {2} removed, {3} new (not in baseline)'
              .format(len(baseline), len(changed), len(removed), len(new)))

    if changed or removed:
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
