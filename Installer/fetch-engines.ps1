# 번들 엔진(yt-dlp/ffmpeg/ffprobe/deno)을 최신으로 다운로드한다 — CI 릴리스 빌드용.
# 근거: 엔진 노화가 플랫폼 다운로드 실패의 근본 원인(2026-08 인스타·틱톡 실측 분석) — 릴리스마다 최신 동봉.
# yt-dlp·ffmpeg는 배포처가 제공하는 SHA-256으로 무결성을 검증한다.
param(
    [Parameter(Mandatory = $true)][string]$ToolsDir
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Force $ToolsDir | Out-Null
$work = Join-Path ([System.IO.Path]::GetTempPath()) "engines-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $work | Out-Null

# 일시 네트워크 오류 대비 재시도(로컬·CI 공통 — 실측: 단발 GET이 간헐적으로 끊김)
function Get-WithRetry([string]$Url, [string]$OutFile) {
    for ($try = 1; $try -le 3; $try++) {
        try {
            if ($OutFile) { Invoke-WebRequest -UseBasicParsing $Url -OutFile $OutFile; return }
            # octet-stream 응답은 Content가 byte[] — 반환 시 배열이 풀리므로 여기서 문자열화
            $c = (Invoke-WebRequest -UseBasicParsing $Url).Content
            if ($c -is [byte[]]) { return [System.Text.Encoding]::UTF8.GetString($c) }
            return [string]$c
        }
        catch {
            if ($try -eq 3) { throw }
            Write-Output "  재시도 $try/3: $($_.Exception.Message.Trim())"
            Start-Sleep -Seconds (3 * $try)
        }
    }
}

function Assert-Sha256([string]$Path, [string]$Expected, [string]$Name) {
    $actual = (Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) { throw "$Name 체크섬 불일치: $actual != $Expected" }
    Write-Output "  $Name 체크섬 OK"
}

# ── yt-dlp (공식 릴리스 + SHA2-256SUMS 검증) ──────────────────────────────
Write-Output "[1/3] yt-dlp"
$rel = Invoke-RestMethod "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest"
Write-Output "  버전: $($rel.tag_name)"
Get-WithRetry "https://github.com/yt-dlp/yt-dlp/releases/download/$($rel.tag_name)/yt-dlp.exe" "$work\yt-dlp.exe"
$sums = Get-WithRetry "https://github.com/yt-dlp/yt-dlp/releases/download/$($rel.tag_name)/SHA2-256SUMS" $null
if ($sums -is [byte[]]) { $sums = [System.Text.Encoding]::UTF8.GetString($sums) }
$expected = ($sums -split "`n" | Where-Object { $_ -match '^\S+\s+yt-dlp\.exe\s*$' }) -replace '\s+yt-dlp\.exe\s*$', ''
if (-not $expected) { throw "SHA2-256SUMS에서 yt-dlp.exe 항목을 찾지 못함" }
Assert-Sha256 "$work\yt-dlp.exe" $expected.Trim() "yt-dlp.exe"
Move-Item "$work\yt-dlp.exe" (Join-Path $ToolsDir "yt-dlp.exe") -Force

# ── ffmpeg / ffprobe (BtbN 자동빌드 + .sha256 검증) ───────────────────────
Write-Output "[2/3] ffmpeg/ffprobe"
$zipName = "ffmpeg-master-latest-win64-gpl.zip"
$base = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest"
Get-WithRetry "$base/$zipName" "$work\$zipName"
# BtbN은 통합 checksums.sha256 하나만 제공(개별 .sha256 없음 — 실측)
$ffSums = Get-WithRetry "$base/checksums.sha256" $null
$ffLine = $ffSums -split "`n" | Where-Object { $_ -match [regex]::Escape($zipName) + '\s*$' } | Select-Object -First 1
if (-not $ffLine) { throw "checksums.sha256에서 $zipName 항목을 찾지 못함" }
Assert-Sha256 "$work\$zipName" ($ffLine -split '\s+')[0].Trim() $zipName
Expand-Archive "$work\$zipName" "$work\ff" -Force
$bin = Get-ChildItem "$work\ff" -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
Move-Item $bin.FullName (Join-Path $ToolsDir "ffmpeg.exe") -Force
$probe = Get-ChildItem "$work\ff" -Recurse -Filter "ffprobe.exe" | Select-Object -First 1
Move-Item $probe.FullName (Join-Path $ToolsDir "ffprobe.exe") -Force

# ── deno (공식 릴리스 zip — yt-dlp JS 챌린지 해결용) ──────────────────────
Write-Output "[3/3] deno"
Get-WithRetry "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip" "$work\deno.zip"
Expand-Archive "$work\deno.zip" "$work\deno" -Force
Move-Item "$work\deno\deno.exe" (Join-Path $ToolsDir "deno.exe") -Force

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

# ── 결과 확인 ─────────────────────────────────────────────────────────────
Write-Output "── 엔진 준비 완료 ──"
foreach ($name in "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe") {
    $f = Get-Item (Join-Path $ToolsDir $name)
    Write-Output ("  {0,-12} {1,7:N0} KB" -f $f.Name, ($f.Length / 1KB))
}
& (Join-Path $ToolsDir "yt-dlp.exe") --version | ForEach-Object { Write-Output "  yt-dlp 버전: $_" }
