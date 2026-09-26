#!/usr/bin/env python3
"""Check the *quality* of the Korean translations (not just their presence).

Complements check_translations.py, which only reports missing keys. This one
looks for machine-translation artifacts that make the Korean UI hard to read:

  spellout      an acronym written out in Hangul syllables ("오 케이" for "OK")
  verb-button   a button/menu label translated as a verb form ("취소하다")
  untranslated  the value is still the English source although a translation exists
  mistranslation a known wrong term is used ("퍼지는도표" for "spreadsheet")
  english-left  a translatable value still contains an untranslated English word

Usage:
    python Tools/i18n/check_translation_quality.py                     # report
    python Tools/i18n/check_translation_quality.py --write-baseline F   # snapshot
    python Tools/i18n/check_translation_quality.py --baseline F         # gate
    python Tools/i18n/check_translation_quality.py --list               # every finding

The baseline is keyed by "file :: key" so unrelated edits do not invalidate it.
"""
import argparse
import glob
import os
import re
import sys

# Legitimate as-is: product names, formats, acronyms, units.
ALLOWED_WORDS = {
    'CSV', 'JSON', 'PDF', 'ODS', 'XLSX', 'XLS', 'MP4', 'MKV', 'AVI', 'MJPEG', 'JPG', 'JPEG', 'PNG',
    'BMP', 'WebM', 'RGB', 'RGGB', 'BGGR', 'GRBG', 'GBRG', 'SIFT', 'ORB', 'FAST', 'UDP', 'TCP',
    'Kinovea', 'Windows', 'Excel', 'LibreOffice', 'Microsoft', 'Markdown', 'OpenCV', 'FFmpeg',
    'HTML', 'URL', 'USB', 'GPU', 'CPU', 'GenICam', 'DirectShow', 'Direct3D', 'OpenGL', 'Bayer',
    'IP', 'HTTP', 'XML', 'UTF-8', 'BOM', 'DXGI', 'dBFS', 'RMS', 'peak', 'ffmpeg', 'ffmpeg.exe',
    'MB', 'GB', 'ms', 'fps', 'GOP', 'AAC', 'Mono', 'Ctrl', 'Shift', 'Alt',
}

# Terms that are plainly wrong in this application's context.
WRONG_TERMS = {
    '퍼지는도표': 'spreadsheet -> 표',
    '씨 에스 브이': 'CSV spelled out',
    '제이슨': 'JSON spelled out',
    '종이고정철사판': 'clipboard -> 클립보드',
    '열쇠 사진': 'key image -> 키 이미지',
    '열쇠 이미지': 'key image -> 키 이미지',
    '붙임딱지': 'label -> 레이블',
    '선수 주석': 'player -> 플레이어',
    '잡기 서류철': 'capture folder -> 캡처 폴더',
    '서류철': 'folder -> 폴더',
    '서류': 'file -> 파일',
    '틀 비율': 'framerate -> 프레임레이트',
    '판올림': 'update -> 업데이트',
    '작은창고': 'buffer -> 버퍼',
    '저주자': 'cursor -> 커서',
    '밀어보여주개': 'slideshow -> 슬라이드쇼',
    '뒷가르기': 'backslash -> 백슬래시',
    '하이프헨': 'hyphen -> 하이픈',
    '저아래점수': 'underscore -> 밑줄',
    '날뛰는': 'active -> 활성',
    '방아쇠': 'trigger -> 트리거',
    '숙주': 'host -> 호스트',
    '것구 성': 'broken spacing',
    '그림그리는것': 'drawing -> 그리기',
    '보여주는것': 'broken spacing',
    '구하십시오': 'save -> 저장',
    '선호사항': 'preferences -> 환경설정',
    '단위들': 'units -> 단위',
    '잡기': 'capture -> 캡처',
    '틀들': 'frames -> 프레임',
    '틀 번호': 'frame number -> 프레임 번호',
    '틀 보여주십시오': 'show frame -> 프레임 표시',
    '틀 변환': 'frame transforms -> 프레임 변환',
    '틀 숫자': 'frame number -> 프레임 번호',
    '실시간 틀들': 'frames -> 프레임',
    '지연된 틀들': 'frames -> 프레임',
    '시간 틀': 'time section -> 시간 구간',
    '수입': 'import -> 가져오기',
    '열쇠단어': 'keyword -> 키워드',
    '열쇠판': 'keyboard -> 키보드',
    '뜨거운열쇠': 'hotkey -> 단축키',
    '팔을 뽑았다': 'disarmed -> 해제됨',
    '팔을 붙였다': 'armed -> 준비됨',
    '짐을 쌓으십시오': 'reload -> 다시 불러오기',
    '흐려지는것': 'fading -> 페이드',
    '소급적용': 'retroactive -> 소급 기록',
    '지연했습니다': 'delayed -> 지연',
    '보여주십시오': 'show -> 표시',
    '복사하십시오': 'copy -> 복사',
    '바릅니다': 'paste -> 붙여넣기',
    '틀 비율을': 'framerate -> 프레임레이트',
    '촬영기': 'camera -> 카메라',
    '뒷놀이': 'playback -> 재생',
}

BUTTON_EN = re.compile(
    r'^(Cancel|Close|Apply|OK|Save|Copy|Edit|Remove|Reset|Add|Open|Delete|Export|Import|Load|New)\b')
VERB_END = re.compile(r'(하다|하십시오|하세요|하십시요)$')
SINGLE_SYLLABLE_RUN = re.compile(r'(?:(?<=[\s(])|^)([가-힣](?:\s+[가-힣])+)(?=[\s)]|$)')


def load_values(path):
    text = open(path, encoding='utf-8-sig').read()
    return dict(re.findall(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', text, re.S))


def english_left(text):
    for token in re.findall(r'[A-Za-z][A-Za-z0-9.\-]*', text):
        if len(token) >= 4 and token not in ALLOWED_WORDS:
            return token
    return None


def spellout_acronym(value, source):
    """True when a run of single-syllable Hangul words spells an English acronym."""
    for run in SINGLE_SYLLABLE_RUN.findall(value):
        syllables = [t for t in run.split() if len(t) == 1]
        if len(syllables) < 2:
            continue
        for acronym in re.findall(r'[A-Z]{2,}', source):
            if len(acronym) == len(syllables):
                return True
    return False


def scan(root):
    rows = []
    for ko_path in sorted(glob.glob(os.path.join(root, '**', '*.ko.resx'), recursive=True)):
        en_path = ko_path.replace('.ko.resx', '.resx')
        ko = load_values(ko_path)
        en = load_values(en_path) if os.path.exists(en_path) else {}
        rel = os.path.relpath(ko_path, root).replace(os.sep, '/')

        for key, value in ko.items():
            source = en.get(key, '')
            v = value.strip()
            reasons = []

            if spellout_acronym(v, source):
                reasons.append('spellout')

            # A label/tooltip translated as a Korean imperative ("취소하다" for "Cancel").
            # Real instruction sentences end with a period in the English source, labels do not.
            if VERB_END.search(v) and source.strip() and not source.strip().endswith(('.', '…', '!', '?')):
                reasons.append('verb-button')

            if v and v == source.strip() and re.fullmatch(r"[A-Za-z][A-Za-z0-9 \-.']{2,}", source.strip()) \
                    and not re.findall(r'[A-Z]{2,}', source) and source.strip() not in ALLOWED_WORDS:
                reasons.append('untranslated')

            for term in WRONG_TERMS:
                if term in v:
                    reasons.append('mistranslation')
                    break

            if re.search(r'[가-힣]', v) and english_left(v):
                reasons.append('english-left')

            if reasons:
                rows.append((f'{rel} :: {key}', rel, key, v, source.strip(), ','.join(sorted(set(reasons)))))
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('root', nargs='?', default='.')
    parser.add_argument('--baseline')
    parser.add_argument('--write-baseline')
    parser.add_argument('--list', action='store_true')
    args = parser.parse_args()

    rows = scan(args.root)
    keys = sorted({r[0] for r in rows})

    if args.write_baseline:
        with open(args.write_baseline, 'w', encoding='utf-8') as fh:
            fh.write('\n'.join(keys) + ('\n' if keys else ''))
        print(f'wrote {len(keys)} findings to {args.write_baseline}')
        return 0

    if args.baseline:
        known = set()
        if os.path.exists(args.baseline):
            known = {line.strip() for line in open(args.baseline, encoding='utf-8') if line.strip()}
        new = [k for k in keys if k not in known]
        fixed = len([k for k in known if k not in keys])
        print(f'translation quality: {len(keys)} total, {len(new)} new, {fixed} fixed since baseline')
        for k in new:
            print(f'  NEW  {k}')
        return 1 if new else 0

    print(f'translation quality findings: {len(keys)}')
    if args.list:
        for row in rows:
            print('  [{0}] {1} :: {2}'.format(row[5], row[1], row[2]))
            print('      KO: {0}'.format(row[3]))
            print('      EN: {0}'.format(row[4]))
    else:
        by_reason = {}
        for row in rows:
            for reason in row[5].split(','):
                by_reason[reason] = by_reason.get(reason, 0) + 1
        for reason, count in sorted(by_reason.items(), key=lambda x: -x[1]):
            print(f'  {reason}: {count}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
