# 샤샤룽 다운로더 (Shyshyroong Downloader)

[![CI](https://github.com/ghlee0786/Multiplatform.Downloader/actions/workflows/ci.yml/badge.svg)](https://github.com/ghlee0786/Multiplatform.Downloader/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ghlee0786/Multiplatform.Downloader?label=release)](https://github.com/ghlee0786/Multiplatform.Downloader/releases/latest)

YouTube · Instagram · TikTok · 샤오홍슈(RedNote) · Threads · Facebook · X(Twitter) · 도우인 · Reddit · Pinterest —
**10개 플랫폼**의 영상을 내려받는 Windows 데스크톱 앱.
카드형 UI로 썸네일·제목·플랫폼·진행률을 보여주고, 여러 URL을 한 번에 등록해 해상도를 골라 받는다.
받은 영상은 **인앱 플레이어로 바로 재생**하고, **캡컷 등 다른 앱으로 드래그해 내보낼** 수 있다.
새 버전이 나오면 **앱이 스스로 감지해 업데이트**한다.

> **개인 사용 전제.** 저작권·각 플랫폼 이용약관을 준수해서 사용해야 한다. [사용 범위·라이선스 고지](#사용-범위--라이선스) 참고.

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| **10개 플랫폼** | YouTube · Instagram · TikTok · 샤오홍슈(xiaohongshu/rednote/xhslink) · Threads · Facebook(fb.watch) · X(twitter) · 도우인(douyin) · Reddit · Pinterest(pin.it) |
| **자동 업데이트** | 새 버전이 GitHub Releases에 올라오면 앱이 감지해 안내 → 클릭 몇 번으로 업데이트. 릴리스마다 **최신 다운로드 엔진(yt-dlp)이 동봉**되어 플랫폼 측 변경에 계속 대응한다 |
| **드래그&드롭 내보내기** | 완료 카드를 **캡컷 미디어 패널·타임라인, 탐색기, 카카오톡** 등 파일을 받는 앱으로 바로 끌어다 놓기. 체크된 여러 항목을 한 번에 멀티 드래그 가능(복사 방식 — 원본 유지) |
| **분석 우선 · 수동 다운로드** | 등록하면 먼저 **분석**(제목·썸네일·해상도)만 하고, 카드의 **[받기]**(또는 **모두 받기 / 선택 받기**)를 눌러야 내려받는다. 설정의 **자동 다운로드**를 켜면 분석 직후 바로 받는다 |
| **해상도 선택** | URL별 사용 가능한 화질 중 선택. 영상만 있는 포맷은 최적 오디오를 자동 병합 |
| **인앱 재생** | 완료 카드의 **[재생]**(또는 더블클릭)으로 앱 안에서 바로 재생 — 전체화면·**HEVC 코덱** 지원 |
| **간헐 차단 자동 재시도** | 틱톡처럼 요청 단위로 확률적 차단이 걸리는 플랫폼은 실패를 감지해 **자동 재시도**(백오프 적용) — 사용자가 다시 누를 필요 없음 |
| **로그인 / 봇 확인 해결** | 로그인·봇 확인으로 막힌 항목은 카드의 **[로그인]** → 앱 내 브라우저 창에서 실제 로그인/확인 → 쿠키 자동 저장 → 자동 재시도 (연령 제한 YouTube·도우인·Reddit 등) |
| **받음/안받음 상태 유지** | 재시작해도 받은 항목은 완료로 복원(다운로드 폴더의 파일 존재 대조). 파일을 지웠으면 "파일 없음" 안내 + [재시도]로 다시 받기 |
| **자체 폴백 추출** | yt-dlp가 못 받는 Threads는 자체 추출기로, 샤오홍슈는 다층 폴백으로 스트림을 직접 파싱해 다운로드 |
| **일괄 등록** | 여러 줄 URL 붙여넣기 → 한 번에 큐 등록(기본 최대 15개) |
| **카드형 진행률 UI** | 썸네일 · 플랫폼 배지 · 실시간 진행률/속도/ETA · 준비 중 스피너 · 상태별 액션(받기/일시정지/재개/취소/재시도/재생/폴더/삭제) |
| **다크/라이트 테마** | 시스템 추종 또는 수동 선택 — 타이틀바까지 즉시 반영. 확인 대화상자도 테마 일치 |
| **실패 조각 자동 정리** | 실패·취소 시 남는 `.part`/`.ytdl` 조각 파일을 자동 삭제(기동 시 고아 조각 일괄 정리 포함) |
| **트레이 상주** | 창을 닫아도 트레이에서 계속 동작 — 열기/일괄 정지·재개/폴더/완전 종료는 트레이 메뉴에서 |
| **Windows 시작 등록** | 부팅 시 자동 실행(`--minimized`) 옵션 — 인스톨러 체크박스와 연동 |
| **Chrome 확장 연동** | 우클릭 "샤샤룽 다운로더로 다운로드" 또는 **툴바 아이콘 클릭**(단일 영상 페이지에서 초록 다운로드 아이콘으로 변신). TikTok/Facebook 피드에서는 화면 중앙 영상을 자동 인식. 전송하면 앱 창이 앞으로 나타난다 |

---

## 설치

**[📦 최신 버전 다운로드](https://github.com/ghlee0786/Multiplatform.Downloader/releases/latest)** —
`ShyshyroongDownloader_Setup_v*.exe` 를 받아 실행하면 끝.

- 이전 버전이 있으면 실행 중인 앱을 종료하고 **구버전 제거 → 신버전 설치**가 진행바로 표시된다
- 설정·다운로드 기록·로그인 쿠키는 `%APPDATA%`에 있어 업그레이드 후에도 유지된다
- 설치 후에는 **앱이 새 릴리스를 스스로 감지**하므로 다시 받으러 올 필요 없다

**요구 사항**: Windows 10/11 (x64) · WebView2 런타임(Win11 기본 포함 — 로그인 창·인앱 재생용).
다운로드 엔진(yt-dlp/ffmpeg/deno)은 설치 파일에 동봉되어 별도 설치가 없다.

**Chrome 확장 설치**: `chrome://extensions` → 개발자 모드 켜기 → "압축해제된 확장 프로그램 로드" →
설치 폴더의 `chrome-extension` 선택 (`C:\Program Files\Shyshyroong Downloader\chrome-extension`).

---

## 사용법

1. 상단 입력창에 영상 URL을 붙여넣고 **추가**(여러 줄이면 **일괄 추가**) — 또는 Chrome에서 우클릭/확장 아이콘으로 전송.
2. 카드가 **분석 중 → 대기(Ready)** 로 바뀌며 썸네일·제목·해상도가 표시된다.
3. 카드의 **[받기]**(또는 상단 **모두 받기 / 선택 받기**)를 눌러 내려받는다.
   설정에서 **자동 다운로드**를 켜면 이 단계가 생략된다.
4. 로그인·봇 확인으로 막히면 카드에 **[로그인]** 버튼이 나타난다 — 창에서 로그인하고 [완료]를 누르면 자동 재시도.
5. 완료 후에는 **[재생]**(인앱 플레이어) · **폴더** · **삭제** — 또는 카드를 **캡컷/탐색기/카톡으로 드래그**해 바로 내보낸다.
6. 앱을 껐다 켜도 받은 항목은 완료로 남는다(폴더에서 파일을 지웠으면 "파일 없음" 표시 + [재시도]).
7. 다운로드 폴더·동시 수·기본 화질·테마·자동 다운로드는 **설정**에서 변경한다.

---

## 아키텍처

```
Multiplatform-Downloader.Core   순수 로직(net10.0, WPF 비의존, 테스트 대상)
  Engine     yt-dlp 인자/출력 파서 · 오류 분류기 · 진행률 매퍼 · Threads/샤오홍슈 폴백 추출 · 조각 정리
  Queue      다운로드 큐 오케스트레이터 · 상태머신 · 자동 재시도 · 영속화(재시작 폴더 대조 복원)
  Update     자동 업데이트 — GitHub Releases 감지 · 체크섬 검증 · 설치
  Platforms  10개 플랫폼 감지 · URL 정규화(FB watch/도우인 modal 등)
  Net        SSRF 방어 가드(DNS 리바인딩 포함) · cookies.txt 직렬화
  Ipc        단일 인스턴스 · mpdl:// 프로토콜 파서 · Named Pipe
  Settings   JSON 설정(원자적 저장·손상 복구)
Multiplatform-Downloader        WPF(.NET 10) + Caliburn.Micro 4 + Autofac 8
  ViewModels/Views  Shell(카드 큐·드래그 아웃) · Player(인앱 재생) · LoginBrowser(로그인/봇확인) ·
                    Settings · AddLinks · ConfirmDialog · About · Splash
  Services          트레이 · 시작 등록 · mpdl:// 프로토콜 · 테마 · 토스트
chrome-extension/               Chrome MV3 확장(우클릭·아이콘 클릭 → mpdl://, 다운로드 가능 모드 아이콘)
Installer/                      Inno Setup 스크립트 + 엔진 다운로드 스크립트(fetch-engines.ps1)
.github/workflows/              CI(푸시마다 전체 테스트) · Release(태그 → 인스톨러 빌드·릴리스 자동 배포)
```

**릴리스 파이프라인**: `v*` 태그를 푸시하면 GitHub Actions가 최신 엔진을 내려받아(체크섬 검증)
self-contained 빌드 → Inno Setup 컴파일 → Releases에 업로드까지 자동으로 수행한다.
배포된 앱들은 이 릴리스를 자동 업데이트로 감지한다.

---

## 개발 / 테스트

```powershell
# 단위·통합 테스트 (xUnit, 409케이스 + 시뮬레이션 856시나리오)
dotnet test tests/Multiplatform-Downloader.Tests/Multiplatform-Downloader.Tests.csproj -v minimal

# 포맷 검사
dotnet format

# 소스 빌드 (엔진은 Installer/fetch-engines.ps1로 tools/에 준비)
dotnet build Multiplatform-Downloader/Multiplatform-Downloader.csproj -c Release
```

CI가 main 푸시/PR마다 전체 테스트를 실행한다.

---

## 사용 범위 / 라이선스

- 이 앱은 **개인적·합법적 용도**로만 사용한다. 저작권이 있는 콘텐츠의 무단 배포·상업적 이용을 하지 않는다.
- 각 플랫폼의 **이용약관과 저작권법**을 준수할 책임은 사용자에게 있다.
- 번들 서드파티 도구는 각자의 라이선스를 따른다:
  - **yt-dlp** — [Unlicense](https://github.com/yt-dlp/yt-dlp/blob/master/LICENSE)
  - **FFmpeg / ffprobe** — LGPL/GPL ([ffmpeg.org/legal.html](https://ffmpeg.org/legal.html))
  - **Deno** — MIT
