# 유지보수 / 업스트림 동기화

이 포크는 **업스트림 `Kinovea/Kinovea` master + 소스 인코딩 정규화(1개 변경)** 구조입니다.
기능 추가는 없으므로 앞으로의 동기화는 단순합니다.

## 불변식 (invariants)

0. **새 `.cs` 파일은 반드시 해당 프로젝트의 `.csproj`에 등록**한다.
   이 솔루션은 구형(non-SDK) 프로젝트라 파일이 디스크에만 있으면 컴파일되지 않는다
   (`<Compile Include="폴더\파일.cs" />`). 미등록 시 `CS0246` 으로 빌드가 깨진다.
1. **모든 소스/프로젝트 파일은 UTF-8 + BOM**
   - 확장자: `.cs .vb .cpp .h .hpp .c .cc .cxx .resx .csproj .vbproj .props .targets .config .settings .manifest .nsi`
   - 유지: `.editorconfig` 의 `charset = utf-8-bom`
2. **git 텍스트 정규화 금지** — `.gitattributes` 의 `* -text` (대량 재인코딩 diff 방지)
3. CI(`build.yml`, Windows MSBuild x64 Release)가 통과해야 함

## 정적 검사 (CI 게이트)

`build.yml` 이 빌드 전에 아래를 실행하며, 실패 시 워크플로가 중단된다.

| 스크립트 | 검사 | 기준선 |
|:--|:--|:--|
| `Tools/lint/check_encoding.py` | 모든 소스가 UTF-8 + BOM (0건 유지) | — |
| `Tools/lint/check_csproj_includes.py` | .cs/.vb 가 csproj에 등록됨 | `Tools/lint/baseline-csproj.txt` (업스트림 미등록 7건) |
| `Tools/lint/check_culture_parsing.py` | `float/double/decimal.Parse` 무문화 (0건 유지) | — |
| `Tools/i18n/scan_hardcoded_strings.py` | UI 문자열 하드코딩 | `Tools/i18n/baseline-hardcoded.txt` |
| `Tools/i18n/check_translations.py --lang ko` | 한국어 키 누락 0 | — |

하드코딩 기준선에 남은 5건은 **의도적으로 허용**한 항목이다: GenICam 플러그인의
구버전(< 2024.1) 대비 **영문 폴백 리터럴 3건**과 고유명사 `GenICam XML`,
그리고 **컴파일되지 않는 파일**(`FormTrackAnalysis.cs`, csproj 미등록) 1건.

## 업스트림 동기화 절차

```bash
git remote add upstream https://github.com/Kinovea/Kinovea.git   # 최초 1회
git fetch upstream master

git checkout -b sync/upstream-$(date +%Y%m%d) master
git merge upstream/master            # 기능 추가가 없어 충돌은 인코딩 관련뿐

# 인코딩 불변식 재적용 (업스트림이 추가/수정한 파일에도 BOM/복구 적용)
python3 Tools/normalize_encoding.py .

# 검증
python3 - <<'PY'
import os
EXTS={'.cs','.vb','.cpp','.h','.hpp','.c','.cc','.cxx','.resx','.csproj','.vbproj','.props','.targets','.config','.settings','.manifest','.nsi'}
bad=[]
for dp,dn,fn in os.walk('.'):
    dn[:]=[d for d in dn if d not in('.git','bin','obj','Refs')]
    for f in fn:
        if os.path.splitext(f)[1].lower() in EXTS:
            p=os.path.join(dp,f); b=open(p,'rb').read()
            if not b.startswith(b'\xef\xbb\xbf') or b.decode('utf-8',errors='ignore').encode('utf-8')!=b.lstrip(b'\xef\xbb\xbf'):
                bad.append(p)
print("violations:", len(bad), bad[:5])
PY

git add -A && git commit -m "chore: sync upstream + re-apply encoding normalization"
git push -u origin sync/upstream-$(date +%Y%m%d)
# → PR 생성, CI 성공 확인 후 master 로 머지
```

## 참고: 왜 rebase 가 아니라 merge 인가

인코딩 정규화가 **300+ 파일**을 건드리므로, rebase 는 업스트림과 겹치는 모든 파일에서
충돌을 **커밋마다 반복**시킵니다. merge 는 충돌 해결을 1회로 끝냅니다.

## 기록: 프레임 스킵 제거

이 포크가 추가했던 수동 프레임 스킵(`FrameSkip` 설정/커맨드/UI)은 제거되었습니다.
업스트림이 자동 프레임 스킵(`PlayerPreferences.EnableFrameSkipping`, 기본 켜짐)과
지연 기반 스킵 레벨을 제공하므로 중복이기 때문입니다.
제거 이전 이력은 원격 브랜치 `backup-kr-patch` 에 보존되어 있습니다.
