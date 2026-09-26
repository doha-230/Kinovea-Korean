# Kinovea — 한글(비아스키) 환경 빌드 수정 포크

> 원본 [Kinovea](https://github.com/Kinovea/Kinovea) 를 **한국어 Windows(CP949)** 환경에서
> 정상적으로 **빌드**할 수 있도록 소스 인코딩을 복구한 포크입니다.
> 한글 경로/파일명 영상도 정상적으로 열고·재생·저장됩니다.

원본 프로젝트와 **동기화된 최신 상태**를 유지하며, 변경 사항은 **인코딩 정규화 하나**입니다.

---

## 변경 내용 (원본 대비 단 하나)

### 소스 인코딩 정규화 — CP949 빌드 실패 해결

**증상**
- 시스템 언어가 한국어(코드페이지 CP949)인 환경에서 빌드가 실패했습니다.
  - `error CS1010: 상수에 줄 바꿈 문자가 있습니다.`
  - `error CS1003 / CS1002 / CS1519: 토큰 오류`

**원인**
- 소스 일부가 손상된 채 저장되어 있었습니다. 원래 UTF-8이어야 할 문자의
  **리드 바이트가 유실**되어 홀로 남은 바이트(예: `©` → `0xA9`, `°` → `0xB0`)로 저장됨.
- 서양 코드페이지에서는 컴파일러가 그냥 넘어가지만, **CP949에서는 그 바이트를
  "미완성 한글"로 해석**해 문자열 상수를 깨뜨립니다.

**해결** (재현 스크립트: `Tools/normalize_encoding.py`)
- **319개** 소스/프로젝트 파일에 **UTF-8 BOM** 부여 → 코드페이지와 무관하게 UTF-8로 해석
- **72개** 파일의 손상 바이트를 **CP1252 폴백으로 복구** (`0xA9`→`©`, `0xB0`→`°`)
  하되, 이미 정상인 UTF-8 시퀀스는 그대로 보존
- 결과: **전체 1263개 소스 파일이 유효한 UTF-8 + BOM**

재발 방지를 위해 `.editorconfig`에 `charset = utf-8-bom`을 지정했습니다.

### 참고: 프레임 스킵
이 포크가 한때 추가했던 **수동 프레임 스킵**은 **제거**했습니다.
업스트림이 이미 자동 프레임 스킵(`PlayerPreferences.EnableFrameSkipping`, 기본 켜짐)과
지연(lag) 기반 스킵 레벨을 제공하므로 중복이기 때문입니다.

---

## 빌드 방법

```bash
# 1. Visual Studio Build Tools 2022 (MSVC v143 + C++/CLI + .NET Framework 4.8)
# 2. FFmpeg 라이브러리 복원 (Refs/FFmpeg)
# 3. NuGet 복원 후 빌드
```

```bat
MSBuild.exe Kinovea.VS2019.sln /t:Build /p:Configuration=Release /p:Platform=x64 /m
```

산출물: `Kinovea\bin\x64\Release\Kinovea.exe`

CI(`.github/workflows/build.yml`)가 Windows MSBuild x64 Release 전체 빌드와
NSIS 인스톨러 생성을 검증합니다.

---

## 검증

- 한국어 폴더에서 실행 → 정상
- 한국어 폴더 안의 한국어 파일명 영상 열기·재생 → 정상
  (FFmpeg 리더가 .NET UTF-16 → UTF-8로 변환해 전달)

---

## 변경 요약

| 항목 | 내용 |
|---|---|
| 인코딩 정규화 | 319개 BOM 부여 + 72개 손상 바이트 복구 (전체 1263개 유효 UTF-8) |
| 재발 방지 | `.editorconfig` `charset = utf-8-bom`, `.gitattributes` `* -text` |
| 업스트림 | 최신 `Kinovea/Kinovea` master와 동기화 |
| 유지보수 | `MAINTENANCE.md` 참고 |
