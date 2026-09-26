# Kinovea — 한글(비아스키) 경로/파일명 지원 및 프레임 스킵 개선 버전

> Kinovea (Kinovea-master) 를 한국어 환경(Windows, 한글 코드페이지)에서 원활하게
> **빌드**하고, **한글 경로/파일명**을 가진 영상을 안정적으로 **열고/재생/추적/저장**할
> 수 있도록 수정한 포크입니다. 또한 재생 및 트래킹 중 **프레임 스킵**을 직접 지정할 수
> 있는 기능을 추가했습니다.

원본 프로젝트: [Kinovea](https://github.com/Kinovea/Kinovea) — 모션 분석용 영상 주석 도구.

---

## 1. 해결한 문제 #1 — 한글 코드페이지에서 빌드 실패

### 증상
- Windows 시스템 언어가 한국어(또는 한글 코드페이지 CP949)인 환경에서 빌드하면
  컴파일 자체가 실패했습니다.
- 대표 에러:
  - `error CS1010: 상수에 줄 바꿈 문자가 있습니다.`
  - `error CS1003, CS1002, CS1519: 토큰 오류`
  - 예: `FormConfigureTrajectoryDisplay.Designer.cs`, `VideoFilterLensCalibration.cs`

### 원인
소스 파일 100여 개가 손상된 인코딩으로 저장되어 있었습니다.

- 원래는 **UTF-8로 저장**돼야 하는 파일들인데, **멀티바이트 문자의 앞부분
  (리드 바이트)이 빠진 채 Latin-1(Windows-1252)처럼 변환**되어 있었습니다.
  - 예: `©` → byte `0xA9`, `°` → byte `0xB0`, `×` → byte `0xD7`
- 이런 파일은 **서양 코드페이지(라틴 계열) 환경에서는 컴파일러가 그냥 넘어가지만**,
  **한글(CP949) 코드페이지에서는 해당 바이트를 "미완성 한글 글자"로 해석**해
  문자열 상수를 깨뜨리며 오류를 냅니다.
- 즉, **같은 소스가 서양 PC에서는 빌드되고 한글 PC에서는 빌드되지 않는** 상황이었습니다.

### 해결
손상된 소스 파일을 올바른 UTF-8로 복구하고, **모든 소스 파일에 UTF-8 BOM**을
일괄 추가해 어떤 코드페이지에서도 무조건 UTF-8로 해석되도록 했습니다.

- **CP1252 → UTF-8 재인코딩**: `©`, `°`, `×` 등 원래 의도했던 유니코드 문자를 복원
  (자동화 스크립트로 72개 파일 수정)
- **BOM(Byte Order Mark) 추가**: `.cs`, `.vb`, `.cpp`, `.h`, `.resx`, `.csproj` 등
  340여 개 소스 파일에 UTF-8 BOM 부여 → 컴파일러가 코드페이지와 무관하게 UTF-8로만 해석
- 그 결과 한국어 Windows에서도 **Release|x64 전체 솔루션 빌드 성공** (exit 0)

추가로, 이 소스의 빌드에 필요한 도구를 정리했습니다.

- **Visual Studio Build Tools 2022** 설치: MSVC v143 + C++/CLI 지원 + .NET Framework 4.8 타기팅
- **FFmpeg 8.1.2 라이브러리** 복원 (`Refs/FFmpeg`)
- **NuGet 의존성** 복원

---

## 2. 해결한 문제 #2 — 한글 경로/파일명에서의 영상 열기·재생·추적·저장

### 증상
- 동영상 파일 이름이나 폴더 경로에 **한글이 포함되면 에러가 발생 / 동영상을 열 수 없거나
  추적·내보내기가 실패**하는 문제가 보고되었습니다.

### 원인 (점검 결과)
- 동영상을 열고 재생하는 핵심 경로(FFmpeg C++/CLI 연동부)는 이미 유니코드를
  올바르게 처리하고 있었습니다.
  - `VideoReaderFFMpeg.cpp`: `.NET UTF-16 문자열`을 `UTF-8`로 변환해 `avformat_open_input` 전달
  - `MJPEGWriter.cpp`: 저장 시에도 동일하게 UTF-8 버퍼(`GetByteCount`로 정확한 크기) 사용
- 그런데 **위 문제 #1(인코딩 손상) 때문에 소스 자체가 깨져 있어** 한글 환경에서
  제대로 빌드조차 되지 않았고, 이로 인해 정상 동작 여부를 확인할 수 없었습니다.
- 문제 #1을 해결해 "빌드가 되는" 상태로 만든 뒤, 실제로 검증한 결과:

### 검증 결과
빌드된 실행 파일로 다음을 모두 확인했습니다.
- **한글 폴더**에서 Kinovea 실행 → 정상
  (예: `C:\...\키노베아폴더\Kinovea.exe`)
- **한글 폴더 안의 한글 영상** 열기 → 정상
  (예: `...\kopriv\한글영상.mp4`, FFmpeg 리더로 정상 로드, 로그에 오류 없음)
- 저수준(Open) 테스트에서 한글 파일명/한글 경로 조합 모두 `Success`

즉, **"한글 경로/파일명 사용"은 원래 지원 가능했지만, 인코딩 손상이 이를 가로막고
있었으며, 인코딩 복구로 해결**되었습니다.

---

## 3. 추가한 기능 — 프레임 스킵 (Frame Skip)

기존에는 재생/트래킹 속도를 **2배까지**만 조절할 수 있었고, **트래킹 중에는 프레임을
건너뛸 수 없었습니다** (트래커가 연속 프레임을 요구).

### 추가 내용
사용자가 **배속과 프레임 스킵을 각각 지정**할 수 있게 했습니다.

- **프레임 스킵 설정**: 재생 중 1회 렌더링할 때 건너뛸 프레임 수를 지정 (0 = off)
- **UI**: 배속 슬라이더 우측에 **`FrameSkip:` 입력란** 추가 → 숫자 직접 입력
- **핫키**: `IncreaseFrameSkip` / `DecreaseFrameSkip` 명령 추가 (단축키는 사용자가 설정)
- **트래킹 중에도 스킵 적용**: 기존 로직은 트래킹 중 `skip=0`을 강제했으나,
  프레임 스킵이 지정되면 **N번째 프레임마다 트랙 확장(서브샘플링)** 하도록 수정
- **값 지속성**: `PlayerPreferences.FrameSkip`으로 저장되어 세션 간 유지

### 구현 포인트
- `Kinovea.Services/Preferences/PlayerPreferences.cs` — `FrameSkip` 설정 (XML 저장/로드)
- `Kinovea.Services/Commands/Commands.cs` — `IncreaseFrameSkip` / `DecreaseFrameSkip` 명령
- `Kinovea.Services/Commands/HotkeySettingsManager.cs` — 핫키 기본값 등록
- `Kinovea.ScreenManager/.../PlayerScreenUserInterface2.cs` — 스킵 로직 및 UI 입력란
- `Kinovea.ScreenManager/.../PlayerScreenUserInterface2.Designer.cs` — 입력란 레이아웃

### 알려진 주의사항
- 프레임을 과도하게 건너뛰면 물체가 템플릿 검색창 밖으로 나가 **추적이 실패**할 수
  있습니다. (알고리즘은 교체하지 않았고, 기존 템플릿 매칭 트래커를 그대로 사용)

---

## 빌드 방법

```bash
# 1. Visual Studio Build Tools 2022 설치 (MSVC v143 + C++/CLI + .NET 4.8)
# 2. FFmpeg 8.1.2 라이브러리를 Refs/FFmpeg 에 복원
# 3. NuGet 의존성 복원 후 빌드
```

또는 Visual Studio에서 `Kinovea.VS2019.sln`을 열고 `Kinovea` 프로젝트를
시작 프로젝트로 설정한 뒤 Rebuild 하면 됩니다.

```bash
# 커맨드라인 예시
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" ^
  Kinovea.VS2019.sln /t:Build /p:Configuration=Release /p:Platform=x64 /m
```

산출물: `Kinovea\bin\x64\Release\Kinovea.exe`

---

## 변경 요약

| 항목 | 내용 |
|---|---|
| 인코딩 복구 | 손상된 소스 72개 재인코딩 + 340여 개 소스에 UTF-8 BOM |
| 한글 경로/파일명 | 한글 폴더에서 실행·한글 영상 열기·재생·추적 검증 완료 |
| 프레임 스킵 | 재생·트래킹 중 프레임 스킵 지정 가능 (숫자 입력 + 핫키) |
| 값 유지 | 입력란을 덮어쓰지 않도록 수정 (마우스 이탈 시 값 유지) |