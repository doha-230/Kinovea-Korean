# 업스트림 동기화(리베이스/머지) 전략

이 문서는 이 포크(`doha-230/Kinovea_kr`)를 원본 [`Kinovea/Kinovea`](https://github.com/Kinovea/Kinovea)와
동기화하는 절차를 정의합니다. (이슈 #6)

## 현재 상태

| 항목 | 값 |
|:--|:--|
| 업스트림 | `Kinovea/Kinovea` `master` |
| 앞선 커밋 | 이 포크 고유 커밋 (인코딩 정규화 + FrameSkip + 개선) |
| 뒤처진 커밋 | 203 커밋 (2026-09-26 기준, `merge-base = b9bf9012`) |
| 상태 | **diverged** (양방향 분기) |

## 전략 선택: rebase 가 아니라 merge

- 포크 고유 커밋 중 **인코딩 정규화 커밋이 320개 파일**을 건드립니다.
- rebase 는 이 커밋을 업스트림 위에서 **다시 재생**하므로, 업스트림이 같은 파일을
  수정한 곳마다 충돌이 **커밋마다 반복**됩니다.
- **merge 는 충돌 해결을 1회로 끝냅니다.** 따라서 주기적 동기화는 merge 를 사용합니다.

## 충돌 규모 실측 (merge-tree 기준, 2026-09-26)

```
충돌 파일:        55
 ├─ BOM/인코딩 전용: 51   ← 업스트림 채택 후 BOM만 재부여 (자동화 가능)
 └─ 실제 내용 충돌:   4   ← 수동 검토 필요
```

실제 충돌 4개 (모두 FrameSkip 기능이 건드린 파일):

- `Kinovea.ScreenManager/PlayerScreen/Controls/PlayerScreenUserInterface2.cs` (업스트림 2600행 변경 — 재생 루프 전면 리팩터링)
- `Kinovea.ScreenManager/PlayerScreen/Controls/PlayerScreenUserInterface2.Designer.cs` (6행)
- `Kinovea.Services/Commands/HotkeySettingsManager.cs` (460행)
- `Kinovea.Services/Preferences/PlayerPreferences.cs` (144행)

## ⚠️ 결정 포인트 — 업스트림이 자체 프레임 스킵을 도입함

업스트림에는 이미 **자동 프레임 스킵**이 있습니다.

- `PlayerPreferences.EnableFrameSkipping` (bool, 기본 true)
- 재생 루프가 **지연(lag) 기반으로 스킵 레벨을 자동 결정** (`PlayerScreenUserInterface2.cs` 2900–3062행)
- `VideoReader.UpdateAllowFrameSkipping()` 신규 API

이 포크의 `FrameSkip`(사용자가 N프레임을 **수동 지정**)과는 목적이 다릅니다.
머지 시 다음 중 하나를 결정해야 합니다.

1. **업스트림 자동 스킵만 사용**하고 이 포크의 수동 FrameSkip 커밋을 드롭 (가장 단순, 유지보수 최소)
2. 수동 FrameSkip을 업스트림 구조에 **이식** (작업량 큼, 업스트림 API 변경 대응 필요)
3. 포크를 **현재 베이스에 고정**하고 업스트림 동기화를 하지 않음 (분기 확대)

> 이 포크의 남은 고유 가치는 **CP949/인코딩 빌드 수정 + 한글 경로**이며, 프레임 스킵은
> 업스트림이 대체 제공합니다. → **옵션 1 권장.**

## 동기화 절차 (옵션 1 기준)

```bash
# 0) 최초 1회
git remote add upstream https://github.com/Kinovea/Kinovea.git

# 1) 최신 업스트림 가져오기
git fetch upstream master

# 2) 동기화 브랜치 생성 후 머지 (master 직접 머지 금지)
git checkout -b sync/upstream-$(date +%Y%m%d) master
git merge upstream/master        # 충돌 발생 (exit != 0)

# 3) BOM/인코딩 전용 51개 → 업스트림 채택 + BOM 재부여
#    (3-way: ours=포크, theirs=업스트림)
for f in $(cat /tmp/conf_bomonly.txt); do
  git checkout --theirs -- "$f"
  python3 - "$f" <<'PY'
import sys
p=sys.argv[1]
b=open(p,'rb').read()
if not b.startswith(b'\xef\xbb\xbf'):
    open(p,'wb').write(b'\xef\xbb\xbf'+b)
PY
  git add "$f"
done

# 4) 실제 충돌 4개는 수동 해결 (옵션 1: 업스트림 채택, FrameSkip 커밋 드롭)

# 5) 커밋 → PR 로 CI 검증 (build.yml 은 PR to master 에서 실행됨)
git commit -m "chore: sync with upstream/master (drop manual FrameSkip, keep encoding fix)"
git push -u origin sync/upstream-$(date +%Y%m%d)
# GitHub 에서 PR 생성 → CI 성공 확인 후 master 로 머지
```

## 검증 게이트

- `build.yml`(Windows MSBuild, x64 Release)이 **PR 단계에서** 통과해야 머지합니다.
- 특히 CP949 환경 컴파일이 목적이므로, CI 성공 = "한글 Windows 빌드 가능" 증명입니다.

## 재발 방지

- `.editorconfig` 에 `charset = utf-8-bom` 지정 → BOM 누락 재발 차단
- `.gitattributes` 에 `* -text` → 인코딩/줄바꿈 재정규화로 인한 대량 diff 차단
