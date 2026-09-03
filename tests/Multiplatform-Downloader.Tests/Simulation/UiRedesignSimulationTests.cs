using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Multiplatform_Downloader.Core.Diagnostics;
using Multiplatform_Downloader.Core.Engine;
using Multiplatform_Downloader.Core.Models;
using Multiplatform_Downloader.Core.Platforms;
using Multiplatform_Downloader.Core.Queue;
using Multiplatform_Downloader.Core.Settings;
using Multiplatform_Downloader.ViewModels;

namespace Multiplatform_Downloader.Tests.Simulation;

/// <summary>
/// UI 리디자인(ui-redesign-prd) 사이드이펙트 시뮬레이션 러너.
/// docs/analyses/simulation/ui-redesign-scenarios.json(608건)을 실제
/// DownloadItemViewModel / ShellViewModel / 테마 사전 / View XAML에 대해 실행하고
/// 시나리오별 판정을 ui-redesign-sim-round{N}.log에 기록한다.
/// FAIL=0이어야 통과. ISSUE/PENDING은 리디자인 설계 입력(PRD 이슈 로그 근거).
/// </summary>
public class UiRedesignSimulationTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string AppDir => Path.Combine(RepoRoot, "Multiplatform-Downloader");

    // 시나리오 픽스처는 저장소에 추적되는 tests/ 하위가 정본 — docs/는 git 제외라 CI에 없다(CI 첫 실행 실측)
    private static string FixtureDir =>
        Path.Combine(RepoRoot, "tests", "Multiplatform-Downloader.Tests", "Simulation", "fixtures");

    // 라운드 로그는 로컬에선 기존 docs 위치를 유지, docs가 없는 CI에선 임시 폴더에 쓴다
    private static string LogDir
    {
        get
        {
            var docsSim = Path.Combine(RepoRoot, "docs", "analyses", "simulation");
            if (Directory.Exists(docsSim))
                return docsSim;
            var tmp = Path.Combine(Path.GetTempPath(), "mpdl-sim-logs");
            Directory.CreateDirectory(tmp);
            return tmp;
        }
    }

    private static readonly MediaFormatSelector _selector = new();

    [Fact]
    public void should_pass_all_ui_redesign_scenarios_with_zero_fail()
    {
        var simDir = LogDir;
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDir, "ui-redesign-scenarios.json")));
        var all = doc.RootElement.GetProperty("scenarios").EnumerateArray().ToList();
        Assert.True(all.Count >= 480, $"요구: 시나리오 500건 내외 (현재 {all.Count}건)");

        var lightKeys = LoadThemeKeys("WireframeLight.xaml");
        var darkKeys = LoadThemeKeys("WireframeDark.xaml");
        var iconKeys = LoadThemeKeys("Icons.xaml");

        var lines = new List<string>();
        int pass = 0, fail = 0, pending = 0, issues = 0;
        var kindTotals = new Dictionary<string, int[]>(); // kind → [pass, fail, pending]

        foreach (var s in all)
        {
            var kind = s.GetProperty("kind").GetString()!;
            var id = s.GetProperty("id").GetString()!;
            string verdict;
            string detail;
            try
            {
                (verdict, detail) = kind switch
                {
                    "card-action" => RunCardAction(s),
                    "selection" => RunSelection(s),
                    "status-summary" => RunStatusSummary(s),
                    "theme-key" => RunThemeKey(s, lightKeys, darkKeys),
                    "icon-key" => RunIconKey(s, iconKeys),
                    "caliburn" => RunCaliburn(s),
                    "emoji" => RunEmoji(s),
                    "theme-parity" => RunThemeParity(s, lightKeys, darkKeys),
                    _ => ("FAIL", $"알 수 없는 kind={kind}"),
                };
            }
            catch (Exception ex)
            {
                (verdict, detail) = ("FAIL", $"러너 예외 {ex.GetType().Name}: {ex.Message}");
            }

            var t = kindTotals.TryGetValue(kind, out var arr) ? arr : kindTotals[kind] = new int[3];
            switch (verdict)
            {
                case "PASS": pass++; t[0]++; break;
                case "FAIL": fail++; t[1]++; break;
                case "PENDING": pending++; t[2]++; break;
            }
            lines.Add($"{verdict} {id} {detail}");

            // 설계 입력(issueNote)은 판정과 별개로 ISSUE 라인 추가
            if (s.TryGetProperty("issueNote", out var note))
            {
                issues++;
                lines.Add($"ISSUE {id} {note.GetString()}");
            }
        }

        // 교차 검증: Queued/Ready 라벨 중복(현행 명세) — 리디자인 분리 대상
        var probe = BuildItem(["analyze", "ready"], "none", false);
        var probeVm = new DownloadItemViewModel(probe, new SimQueue(), 0, null);
        var queuedVm = new DownloadItemViewModel(
            BuildItem([], "none", false), new SimQueue(), 0, null);
        if (probeVm.StatusText == queuedVm.StatusText)
        {
            issues++;
            lines.Add($"ISSUE CA-XLBL Queued와 Ready가 동일 라벨 '{probeVm.StatusText}' — 리디자인에서 '받기 준비됨'으로 분리(FR)");
        }

        var round = Directory.GetFiles(simDir, "ui-redesign-sim-round*.log").Length + 1;
        var header = new List<string>
        {
            $"# UI-Redesign 시뮬레이션 {round}차 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"# 시나리오 {all.Count}건: PASS={pass} FAIL={fail} PENDING={pending} · ISSUE(설계 입력)={issues}",
        };
        foreach (var (k, t) in kindTotals.OrderBy(kv => kv.Key))
            header.Add($"#   {k}: PASS={t[0]} FAIL={t[1]} PENDING={t[2]}");
        header.Add("");
        File.WriteAllLines(Path.Combine(simDir, $"ui-redesign-sim-round{round}.log"),
            header.Concat(lines));

        Assert.True(fail == 0,
            $"FAIL {fail}건 — ui-redesign-sim-round{round}.log 참조. 첫 실패: " +
            lines.FirstOrDefault(l => l.StartsWith("FAIL", StringComparison.Ordinal)));
    }

    // ── D1. card-action ────────────────────────────────────────────

    private static (string, string) RunCardAction(JsonElement s)
    {
        var ops = s.GetProperty("path").EnumerateArray().Select(e => e.GetString()!).ToList();
        var formats = s.GetProperty("formats").GetString()!;
        var select = s.GetProperty("select").GetBoolean();

        if (s.TryGetProperty("guardOp", out var guardOp))
        {
            var item0 = BuildItem(ops, formats, select);
            try
            {
                ApplyOp(item0, guardOp.GetString()!);
                return ("FAIL", $"guard {guardOp}@{item0.Status} — 예외가 발생하지 않음");
            }
            catch (InvalidOperationException)
            {
                return ("PASS", $"guard {guardOp}@{item0.Status} → InvalidOperationException");
            }
        }

        var item = BuildItem(ops, formats, select);
        var vm = new DownloadItemViewModel(item, new SimQueue(), 0, null);
        var e = s.GetProperty("expect");
        var diffs = new List<string>();

        Check(diffs, "status", e.GetProperty("status").GetString()!, item.Status.ToString());
        Check(diffs, "statusText", e.GetProperty("statusText").GetString()!, vm.StatusText);
        CheckFlag(diffs, e, "canStart", vm.CanStartItem);
        CheckFlag(diffs, e, "canPause", vm.CanPauseItem);
        CheckFlag(diffs, e, "canResume", vm.CanResumeItem);
        CheckFlag(diffs, e, "canCancel", vm.CanCancelItem);
        CheckFlag(diffs, e, "canRetry", vm.CanRetryItem);
        CheckFlag(diffs, e, "canRemove", vm.CanRemoveItem);
        CheckFlag(diffs, e, "canLoginFix", vm.CanLoginFixItem);
        CheckFlag(diffs, e, "canOpenFolder", vm.CanOpenFolderItem);
        CheckFlag(diffs, e, "canPlay", vm.CanPlayItem);
        CheckFlag(diffs, e, "canChangeResolution", vm.CanChangeResolution);
        CheckFlag(diffs, e, "showBadge", vm.ShowResolutionBadge);
        CheckFlag(diffs, e, "isIndeterminate", vm.IsIndeterminate);
        CheckFlag(diffs, e, "isActive", vm.IsActive);

        var primary = vm.CanLoginFixItem ? "login"
            : vm.CanStartItem ? "start"
            : vm.CanResumeItem ? "resume"
            : vm.CanPauseItem ? "pause"
            : vm.CanRetryItem ? "retry"
            : vm.CanPlayItem ? "play"
            : vm.CanOpenFolderItem ? "folder"
            : vm.CanCancelItem ? "cancel"
            : vm.CanRemoveItem ? "remove" : "none";
        Check(diffs, "primary", e.GetProperty("primary").GetString()!, primary);

        // Phase 3(FR-U3.1): VM의 실제 주요 액션 파생값이 우선순위 체인과 일치하는지 검증
        var (expKind, expLabel) = primary switch
        {
            "login" => ("accent", "로그인"),
            "start" => ("accent", "받기"),
            "resume" => ("accent", "재개"),
            "pause" => ("neutral", "일시정지"),
            "retry" => ("accent", "재시도"),
            "play" => ("accent", "재생"),
            "folder" => ("neutral", "폴더 열기"),
            "cancel" => ("danger", "취소"),
            "remove" => ("danger", "삭제"),
            _ => ("none", ""),
        };
        Check(diffs, "primaryKind", expKind, vm.PrimaryActionKind);
        Check(diffs, "primaryLabel", expLabel, vm.PrimaryActionLabel);
        if (vm.CanPrimaryAction != (primary != "none"))
            diffs.Add($"CanPrimaryAction 불일치: {vm.CanPrimaryAction} (primary={primary})");

        // 배타 불변식: 콤보와 배지는 동시에 보이지 않는다 / Start·Pause·Resume은 하나만
        if (vm.CanChangeResolution && vm.ShowResolutionBadge)
            diffs.Add("불변식 위반: 콤보+배지 동시 노출");
        if ((vm.CanStartItem ? 1 : 0) + (vm.CanPauseItem ? 1 : 0) + (vm.CanResumeItem ? 1 : 0) > 1)
            diffs.Add("불변식 위반: Start/Pause/Resume 복수 활성");
        if (vm.CanRemoveItem == vm.IsActive)
            diffs.Add("불변식 위반: CanRemove ⇔ !IsActive 깨짐");

        if (s.TryGetProperty("subTextContains", out var frag) &&
            !vm.SubText.Contains(frag.GetString()!, StringComparison.Ordinal))
            diffs.Add($"subText '{vm.SubText}'에 '{frag}' 없음");

        return diffs.Count == 0
            ? ("PASS", $"status={item.Status} primary={primary}")
            : ("FAIL", string.Join(" | ", diffs));
    }

    private static DownloadItem BuildItem(IReadOnlyList<string> ops, string formats, bool select)
    {
        var item = new DownloadItem("https://example.com/v/" + Guid.NewGuid().ToString("N"),
            PlatformType.YouTube)
        { Title = "시뮬 항목" };
        item.Formats = formats switch
        {
            "va" =>
            [
                new MediaFormat { FormatId = "f1080", Height = 1080, VideoCodec = "avc1", ApproxSize = 100 },
                new MediaFormat { FormatId = "f720", Height = 720, VideoCodec = "avc1", ApproxSize = 50 },
                new MediaFormat { FormatId = "fa", IsAudioOnly = true, AudioCodec = "opus", ApproxSize = 10 },
            ],
            "audio" => [new MediaFormat { FormatId = "fa", IsAudioOnly = true, AudioCodec = "opus" }],
            _ => [],
        };
        if (select && item.Formats.Count > 0)
            item.SelectedFormatId = _selector.BuildOptions(item.Formats)[0].FormatId;
        foreach (var op in ops)
            ApplyOp(item, op);
        return item;
    }

    private static void ApplyOp(DownloadItem item, string op)
    {
        var parts = op.Split(':');
        switch (parts[0])
        {
            case "analyze": item.MarkAnalyzing(); break;
            case "ready": item.MarkReady(); break;
            case "start": item.Start(); break;
            case "pause": item.Pause(); break;
            case "resume": item.Resume(); break;
            case "merge": item.MarkMerging(); break;
            case "complete": item.Complete(@"C:\sim\out\video.mp4"); break;
            case "completeNoPath": item.Complete(null); break;
            case "cancel": item.Cancel(); break;
            case "retry": item.PrepareRetry(); break;
            case "fail":
                item.Fail("시뮬 실패", Enum.Parse<ErrorCategory>(parts[1]));
                break;
            case "unavailable":
                item.MarkUnavailable("시뮬 불가",
                    parts[1] == "none" ? null : Enum.Parse<ErrorCategory>(parts[1]));
                break;
            case "progress":
                var pct = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                long? speed = parts.Length > 2 ? long.Parse(parts[2]) : null;
                TimeSpan? eta = parts.Length > 3 ? TimeSpan.FromSeconds(int.Parse(parts[3])) : null;
                item.UpdateProgress(new DownloadProgress(pct, speed, eta));
                break;
            default: throw new InvalidDataException($"알 수 없는 op: {op}");
        }
    }

    // ── D2. selection ──────────────────────────────────────────────

    private static readonly Dictionary<string, string[]> _codePaths = new()
    {
        ["Q"] = [], ["A"] = ["analyze"], ["R"] = ["analyze", "ready"],
        ["D"] = ["analyze", "ready", "start", "progress:40"],
        ["P"] = ["analyze", "ready", "start", "pause"],
        ["M"] = ["analyze", "ready", "start", "merge"],
        ["C"] = ["analyze", "ready", "start", "merge", "complete"],
        ["F"] = ["analyze", "fail:Network"],
        ["X"] = ["analyze", "cancel"],
        ["U"] = ["analyze", "unavailable:GeoBlocked"],
        ["UL"] = ["analyze", "unavailable:LoginRequired"],
    };

    private static (string, string) RunSelection(JsonElement s)
    {
        var codes = s.GetProperty("items").EnumerateArray().Select(e => e.GetString()!).ToList();
        var pattern = s.GetProperty("checks").GetString()!;
        var action = s.GetProperty("action").GetString()!;
        var e = s.GetProperty("expect");

        var queue = new SimQueue();
        var shell = new ShellViewModel(queue, new SimSettings(), NullAppLogger.Instance,
            null!, new BatchUrlParser(new PlatformDetector()));
        string? confirmMessage = null;
        var confirmAnswer = action != "deleteDecline";
        shell.ConfirmInteraction = (_, message) =>
        {
            confirmMessage = message;
            return Task.FromResult(confirmAnswer);
        };

        foreach (var code in codes)
            queue.AddAndRaise(BuildItem(_codePaths[code], "none", false));

        for (var i = 0; i < shell.Items.Count; i++)
            shell.Items[i].IsChecked = pattern switch
            {
                "all" => true,
                "none" => false,
                "first" => i == 0,
                "evens" => i % 2 == 0,
                _ => throw new InvalidDataException(pattern),
            };

        var raised = new List<string>();
        shell.PropertyChanged += (_, args) => raised.Add(args.PropertyName ?? "");

        switch (action)
        {
            case "none": break;
            case "toggleFirst": shell.Items[0].IsChecked = !shell.Items[0].IsChecked; break;
            case "selectAllOn": shell.SelectAllState = true; break;
            case "selectAllOff": shell.SelectAllState = false; break;
            case "startChecked": shell.StartChecked(); break;
            case "deleteConfirm":
            case "deleteDecline":
                shell.DeleteChecked().GetAwaiter().GetResult();
                break;
            default: throw new InvalidDataException(action);
        }

        var diffs = new List<string>();
        Check(diffs, "itemsAfter", e.GetProperty("itemsAfter").GetInt32().ToString(),
            shell.Items.Count.ToString());
        Check(diffs, "startLabel", e.GetProperty("startLabel").GetString()!, shell.StartCheckedLabel);
        CheckFlag(diffs, e, "canStart", shell.CanStartChecked);
        Check(diffs, "deleteLabel", e.GetProperty("deleteLabel").GetString()!, shell.DeleteCheckedLabel);
        CheckFlag(diffs, e, "canDelete", shell.CanDeleteChecked);
        var selectAll = shell.SelectAllState switch { true => "true", false => "false", null => "null" };
        Check(diffs, "selectAll", e.GetProperty("selectAll").GetString()!, selectAll);
        Check(diffs, "summary", e.GetProperty("summary").GetString()!, shell.SelectionSummary);
        Check(diffs, "startedCount", e.GetProperty("startedCount").GetInt32().ToString(),
            queue.StartedIds.Count.ToString());
        Check(diffs, "removedCount", e.GetProperty("removedCount").GetInt32().ToString(),
            queue.RemovedIds.Count.ToString());
        if (e.TryGetProperty("confirmContains", out var frag) &&
            (confirmMessage is null || !confirmMessage.Contains(frag.GetString()!, StringComparison.Ordinal)))
            diffs.Add($"확인 메시지에 '{frag}' 없음: '{confirmMessage}'");

        // 리디자인 컨텍스트 바 의존: 선택 변화가 6개 파생 속성 알림으로 반드시 전파돼야 한다
        if (action is "toggleFirst" or "selectAllOn" or "selectAllOff")
        {
            string[] required =
            [
                nameof(shell.StartCheckedLabel), nameof(shell.CanStartChecked),
                nameof(shell.SelectAllState), nameof(shell.SelectionSummary),
                nameof(shell.DeleteCheckedLabel), nameof(shell.CanDeleteChecked),
            ];
            foreach (var name in required)
                if (!raised.Contains(name) && !raised.Contains(""))
                    diffs.Add($"알림 누락: {name}");
        }

        return diffs.Count == 0
            ? ("PASS", $"items={codes.Count} checks={pattern} action={action} after={shell.Items.Count}")
            : ("FAIL", $"[{pattern}/{action}] " + string.Join(" | ", diffs));
    }

    // ── D3. status-summary ─────────────────────────────────────────

    private static (string, string) RunStatusSummary(JsonElement s)
    {
        var codes = s.GetProperty("items").EnumerateArray().Select(e => e.GetString()!).ToList();
        var e = s.GetProperty("expect");
        var queue = new SimQueue();
        var settings = new SimSettings();
        var shell = new ShellViewModel(queue, settings, NullAppLogger.Instance,
            null!, new BatchUrlParser(new PlatformDetector()));
        foreach (var code in codes)
            queue.AddAndRaise(BuildItem(_codePaths[code], "none", false));

        // Phase 2(FR-U2.3): 상태바 문자열 평문화(이모지 제거)에 파서 동기(PRD §5-B)
        var m = Regex.Match(shell.StatusSummary,
            @"진행 (\d+) · 대기 (\d+) · 완료 (\d+) · 실패 (\d+)");
        if (!m.Success)
            return ("FAIL", $"StatusSummary 파싱 실패: '{shell.StatusSummary}'");
        var qm = Regex.Match(shell.ConcurrencyInfo, @"큐 (\d+)/(\d+)");
        if (!qm.Success)
            return ("FAIL", $"ConcurrencyInfo 파싱 실패: '{shell.ConcurrencyInfo}'");

        var diffs = new List<string>();
        Check(diffs, "downloading", e.GetProperty("downloading").GetInt32().ToString(), m.Groups[1].Value);
        Check(diffs, "waiting", e.GetProperty("waiting").GetInt32().ToString(), m.Groups[2].Value);
        Check(diffs, "completed", e.GetProperty("completed").GetInt32().ToString(), m.Groups[3].Value);
        Check(diffs, "failed", e.GetProperty("failed").GetInt32().ToString(), m.Groups[4].Value);
        Check(diffs, "queueCount", e.GetProperty("queueCount").GetInt32().ToString(), qm.Groups[1].Value);
        Check(diffs, "queueMax", settings.Current.MaxQueueItems.ToString(), qm.Groups[2].Value);

        return diffs.Count == 0
            ? ("PASS", $"n={codes.Count} 진행={m.Groups[1]} 대기={m.Groups[2]} 완료={m.Groups[3]} 실패={m.Groups[4]}")
            : ("FAIL", string.Join(" | ", diffs));
    }

    // ── D4. theme-key / D7. theme-parity ───────────────────────────

    private static HashSet<string> LoadThemeKeys(string file)
    {
        var text = File.ReadAllText(Path.Combine(AppDir, "Themes", file));
        var dict = (System.Windows.ResourceDictionary)System.Windows.Markup.XamlReader.Parse(text);
        return dict.Keys.Cast<object>().Select(k => k.ToString()!).ToHashSet();
    }

    private static (string, string) RunThemeKey(JsonElement s, HashSet<string> light, HashSet<string> dark)
    {
        var key = s.GetProperty("key").GetString()!;
        var view = s.GetProperty("view").GetString()!;
        var inLight = light.Contains(key);
        var inDark = dark.Contains(key);
        return inLight && inDark
            ? ("PASS", $"{key} @ {view} (Light+Dark 정의됨, {s.GetProperty("occurrences").GetInt32()}회 사용)")
            : ("FAIL", $"{key} @ {view} — Light={inLight} Dark={inDark}");
    }

    /// <summary>StaticResource Ico* 사용처 ↔ Icons.xaml 정의 — 미정의는 뷰 로드 시 XamlParseException
    /// (IcoExternal 누락으로 [재생] 무반응이었던 사고의 회귀 방지).</summary>
    private static (string, string) RunIconKey(JsonElement s, HashSet<string> icons)
    {
        var key = s.GetProperty("key").GetString()!;
        var view = s.GetProperty("view").GetString()!;
        return icons.Contains(key)
            ? ("PASS", $"{key} @ {view} (Icons.xaml 정의됨)")
            : ("FAIL", $"{key} @ {view} — Icons.xaml에 없음(뷰 로드 시 XamlParseException)");
    }

    private static (string, string) RunThemeParity(JsonElement s, HashSet<string> light, HashSet<string> dark)
    {
        var key = s.GetProperty("key").GetString()!;
        var expect = s.GetProperty("expect").GetString()!;
        var inLight = light.Contains(key);
        var inDark = dark.Contains(key);
        if (expect == "both")
            return inLight && inDark
                ? ("PASS", $"{key} 두 사전 모두 정의")
                : ("FAIL", $"{key} 사전 비대칭 — Light={inLight} Dark={inDark}");
        // planned: 리디자인 1단계에서 추가될 신규 토큰
        if (inLight && inDark)
            return ("PASS", $"{key} 신규 토큰 정의 완료");
        if (inLight || inDark)
            return ("FAIL", $"{key} 신규 토큰이 한쪽 사전에만 있음 — Light={inLight} Dark={inDark}");
        return ("PENDING", $"{key} 신규 토큰 — 리디자인 1단계에서 두 사전에 추가 예정");
    }

    // ── D5. caliburn ───────────────────────────────────────────────

    private static (string, string) RunCaliburn(JsonElement s)
    {
        var sub = s.GetProperty("sub").GetString()!;
        var member = s.GetProperty("member").GetString()!;
        var vmName = s.GetProperty("vm").GetString()!;
        var view = s.GetProperty("view").GetString()!;

        if (sub == "code-behind")
            return ("PASS", $"{member} @ {view} — 코드비하인드 전용(규약 제외 문서화)");

        if (sub == "orphan")
        {
            var declaring = Path.Combine(AppDir, view.Replace('/', Path.DirectorySeparatorChar));
            var referenced = EnumerateSourceFiles()
                .Where(f => !string.Equals(f, declaring, StringComparison.OrdinalIgnoreCase))
                .Any(f => Regex.IsMatch(File.ReadAllText(f), $@"\b{Regex.Escape(member)}\b"));
            return referenced
                ? ("PASS", $"{vmName}.{member} — 참조 확인")
                : ("PASS", $"ISSUE-INLINE {vmName}.{member} — XAML·코드 어디에서도 미참조(고아 액션) → 리디자인에서 제거 또는 배선 결정");
        }

        var type = typeof(ShellViewModel).Assembly
            .GetType($"Multiplatform_Downloader.ViewModels.{vmName}");
        if (type is null)
            return ("FAIL", $"{vmName} 타입 없음");

        bool Exists(Type t) =>
            t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) is not null
            || t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.Name == member);

        var ok = Exists(type);
        if (!ok && sub is "binding-shell" or "attach-shell")
        {
            var cardType = typeof(ShellViewModel).Assembly
                .GetType("Multiplatform_Downloader.ViewModels.DownloadItemViewModel")!;
            ok = Exists(cardType);
        }
        return ok
            ? ("PASS", $"{sub} {member} → {vmName} 멤버 확인 ({view})")
            : ("FAIL", $"{sub} {member} → {vmName}에 없음 ({view}) — Caliburn 규약 붕괴");
    }

    private static IEnumerable<string> EnumerateSourceFiles()
    {
        foreach (var f in Directory.EnumerateFiles(AppDir, "*.xaml", SearchOption.AllDirectories))
            if (!f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                yield return f;
        foreach (var f in Directory.EnumerateFiles(AppDir, "*.cs", SearchOption.AllDirectories))
            if (!f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                yield return f;
    }

    // ── D6. emoji ──────────────────────────────────────────────────

    private static (string, string) RunEmoji(JsonElement s)
    {
        var rel = s.GetProperty("file").GetString()!;
        var ch = s.GetProperty("char").GetString()!;
        var cp = s.GetProperty("codepoint").GetString()!;
        var replacement = s.GetProperty("replacement").GetString()!;
        var path = Path.Combine(AppDir, rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return ("FAIL", $"{rel} 파일 없음(인벤토리 스테일)");
        if (!File.ReadAllText(path).Contains(ch, StringComparison.Ordinal))
            return ("FAIL", $"{cp} '{ch}' — {rel}에서 사라짐(인벤토리 재생성 필요)");
        return replacement == "TBD"
            ? ("PASS", $"ISSUE-INLINE {cp} '{ch}' @ {rel} — 교체 매핑 미정(TBD) → 아이콘 세트에 추가 필요")
            : ("PASS", $"{cp} '{ch}' @ {rel} → {replacement}");
    }

    // ── 공용 ───────────────────────────────────────────────────────

    private static void Check(List<string> diffs, string field, string expect, string actual)
    {
        if (!string.Equals(expect, actual, StringComparison.Ordinal))
            diffs.Add($"{field}: 기대 '{expect}' ≠ 실제 '{actual}'");
    }

    private static void CheckFlag(List<string> diffs, JsonElement e, string field, bool actual)
    {
        var expect = e.GetProperty(field).GetBoolean();
        if (expect != actual)
            diffs.Add($"{field}: 기대 {expect} ≠ 실제 {actual}");
    }

    private sealed class SimSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public bool SettingsFileExisted => true;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>실제 큐처럼 Remove가 목록에서 제거 후 ItemChanged를 발화한다(셸 카드 제거 경로 검증).</summary>
    private sealed class SimQueue : IDownloadQueueService
    {
        private readonly List<DownloadItem> _items = [];
        public IReadOnlyList<DownloadItem> Items => _items;
        public event EventHandler<DownloadItem>? ItemChanged;
        public List<Guid> StartedIds { get; } = [];
        public List<Guid> RemovedIds { get; } = [];

        public void AddAndRaise(DownloadItem item)
        {
            _items.Add(item);
            ItemChanged?.Invoke(this, item);
        }

        public EnqueueResult Enqueue(string urlsText) => new([], [], 0);
        public void Start(Guid id) => StartedIds.Add(id);

        public void Remove(Guid id)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item is null)
                return;
            _items.Remove(item);
            RemovedIds.Add(id);
            ItemChanged?.Invoke(this, item);
        }

        public void StartAll() { }
        public void ChangeFormat(Guid id, string formatId) { }
        public void Cancel(Guid id) { }
        public void Pause(Guid id) { }
        public void Resume(Guid id) { }
        public void Retry(Guid id) { }
        public void PauseAll() { }
        public void ResumeAll() { }
        public void SweepOrphanPartials() { }
        public void RestoreCompleted(QueueItemSnapshot snapshot) { }
    }
}
