#!/usr/bin/env python3
"""Normalize source-file encoding for the Kinovea_kr fork.

Goal: make every source/project file (a) valid UTF-8 with a BOM so the C#
compiler decodes it as UTF-8 regardless of the system code page (CP949 etc.),
and (b) repair bytes that were originally mangled (UTF-8 lead bytes lost,
leaving stray single bytes in the 0x80-0xFF range).

Repair strategy: decode as UTF-8; wherever a byte is not part of a valid UTF-8
sequence, map that single byte back through CP1252 (so 0xA9 -> ©, 0xB0 -> °).
Valid UTF-8 sequences are preserved untouched.
"""
import codecs, os, sys

EXTS = {'.cs', '.vb', '.cpp', '.h', '.hpp', '.c', '.cc', '.cxx',
        '.resx', '.csproj', '.vbproj', '.props', '.targets',
        '.config', '.settings', '.manifest', '.nsi'}

def _cp1252_fallback(err):
    b = err.object[err.start:err.end]
    try:
        s = b.decode('cp1252')
    except Exception:
        s = ''.join(chr(x) for x in b)
    return (s, err.end)

codecs.register_error('cp1252fb', _cp1252_fallback)

def normalize(path):
    raw = open(path, 'rb').read()
    bom = raw.startswith(codecs.BOM_UTF8)
    body = raw[len(codecs.BOM_UTF8):] if bom else raw

    try:
        text = body.decode('utf-8')
        repaired = False
    except UnicodeDecodeError:
        text = body.decode('utf-8', 'cp1252fb')
        repaired = True

    out = codecs.BOM_UTF8 + text.encode('utf-8')
    added_bom = not bom
    changed = (out != raw)
    if changed:
        open(path, 'wb').write(out)
    return (added_bom, repaired, changed)

def main(root):
    n_files = n_bom = n_rep = 0
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in ('.git', 'bin', 'obj', 'Refs', 'packages', 'node_modules', '.vs')]
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            if ext not in EXTS:
                continue
            p = os.path.join(dirpath, fn)
            n_files += 1
            a, r, c = normalize(p)
            if a: n_bom += 1
            if r: n_rep += 1
    print(f"scanned={n_files} bom_added={n_bom} repaired={n_rep}")

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '.')
