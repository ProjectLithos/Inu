using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Inu.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length > 0 && string.Equals(args[0], "watch-diagnostics", StringComparison.OrdinalIgnoreCase))
    {
        return WatchDiagnostics(args);
    }
    if (args.Length < 2 || !string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
    {
        return Fail("Usage: Inu.QemuLauncher run <InuProject.json> --qemu <path> --image <path> [--ovmf-code <path>] [--ovmf-vars <path>] [--timeout-seconds <value>] [--cpus <n>] [--memory-mib <n>] [--storage virtio-block|ahci|nvme] [--network virtio-net|e1000|none] [--graphics virtio-gpu|gop] [--usb xhci|none] [--run-directory <path>] [--require-marker <text>] [--accept-and-stop] [--dry-run]");
    }

    if (!InuProject.TryLoad(args[1], out InuProject? project, out string error) || project is null)
    {
        return Fail(error);
    }

    string? qemuOption = GetOption(args, "--qemu") ?? Environment.GetEnvironmentVariable("INU_QEMU_X64");
    string? imageOption = GetOption(args, "--image");
    if (string.IsNullOrWhiteSpace(qemuOption) || string.IsNullOrWhiteSpace(imageOption))
    {
        return Fail("QEMU and image paths are required.");
    }

    string qemu = Path.GetFullPath(qemuOption);
    string imagePath = Path.GetFullPath(imageOption);
    string? ovmfCodeOption = GetOption(args, "--ovmf-code") ?? Environment.GetEnvironmentVariable("INU_OVMF_CODE");
    string? ovmfVarsOption = GetOption(args, "--ovmf-vars") ?? Environment.GetEnvironmentVariable("INU_OVMF_VARS");
    string? ovmfCode = ResolveFirmware(qemu, ovmfCodeOption, ["edk2-x86_64-code.fd", "OVMF_CODE.fd"]);
    string? ovmfVars = ResolveFirmware(qemu, ovmfVarsOption, ["edk2-i386-vars.fd", "edk2-x86_64-vars.fd", "OVMF_VARS.fd"]);
    int timeoutSeconds;
    try
    {
        timeoutSeconds = ParseTimeout(GetOption(args, "--timeout-seconds"));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Fail(exception.Message);
    }

    int qemuProcessorCount;
    int memoryMiB;
    string storageTarget;
    string networkTarget;
    string graphicsTarget;
    string usbTarget;
    try
    {
        qemuProcessorCount = ResolveQemuProcessorCount(project, GetOption(args, "--cpus"));
        memoryMiB = ParseBoundedInt(GetOption(args, "--memory-mib"), 512, 128, 65536, "--memory-mib");
        storageTarget = ParseChoice(GetOption(args, "--storage"), "virtio-block", ["virtio-block", "ahci", "nvme"], "--storage");
        networkTarget = ParseChoice(GetOption(args, "--network"), "none", ["none", "virtio-net", "e1000"], "--network");
        graphicsTarget = ParseChoice(GetOption(args, "--graphics"), "virtio-gpu", ["virtio-gpu", "gop"], "--graphics");
        usbTarget = ParseChoice(GetOption(args, "--usb"), "none", ["none", "xhci"], "--usb");
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Fail(exception.Message);
    }
    catch (ArgumentException exception)
    {
        return Fail(exception.Message);
    }
    bool acceptAndStop = HasOption(args, "--accept-and-stop");
    string[] requiredMarkers = GetOptions(args, "--require-marker").ToArray();

    string outputDirectory = Path.GetFullPath(project.OutputDirectory);
    string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
    string? runDirectoryOption = GetOption(args, "--run-directory");
    string runDirectory = string.IsNullOrWhiteSpace(runDirectoryOption) ? Path.Combine(outputDirectory, "Runs", runId) : Path.GetFullPath(runDirectoryOption);
    // The project image is the persistent writable system/user disk. QEMU must use it directly
    // so /USERS survives a normal rerun. Keep a pre-run snapshot in the run directory for
    // diagnostics instead of booting a disposable copy that would lose account changes.
    string runImage = imagePath;
    string preRunImageSnapshot = Path.Combine(runDirectory, "boot-image-before-run.img");
    string variableStore = Path.Combine(runDirectory, "OVMF_VARS.fd");
    string serialLog = Path.Combine(runDirectory, "serial.log");
    string qemuPidFile = Path.Combine(runDirectory, "qemu.pid");
    string qemuStdoutLog = Path.Combine(runDirectory, "qemu.stdout.log");
    string qemuStderrLog = Path.Combine(runDirectory, "qemu.stderr.log");
    string qemuDebugLog = Path.Combine(runDirectory, "qemu-diagnostics.log");
    string qemuDebugConLog = Path.Combine(runDirectory, "qemu-debugcon.log");
    string diagnosticReport = Path.Combine(runDirectory, "failure-diagnostics.txt");
    string latestDiagnosticReport = Path.Combine(outputDirectory, "failure-diagnostics-latest.txt");
    string runManifest = Path.Combine(outputDirectory, "Inu.Run.json");
    string qemuStarterScript = Path.Combine(runDirectory, "Launch-Qemu.ps1");
    string diagnosticWatcherScript = Path.Combine(runDirectory, "Watch-Qemu-Diagnostics.ps1");
    string diagnosticWatcherStdout = Path.Combine(runDirectory, "diagnostic-watcher.stdout.log");
    string diagnosticWatcherStderr = Path.Combine(runDirectory, "diagnostic-watcher.stderr.log");
    int qmpPort = ReserveLoopbackPort();
    if (qmpPort <= 0) return Fail("Unable to reserve a loopback QMP diagnostics port.");

    if (string.IsNullOrWhiteSpace(ovmfCode) || string.IsNullOrWhiteSpace(ovmfVars))
    {
        return Fail("x64 OVMF firmware was not found. Run Install-InuToolchain.bat or pass --ovmf-code and --ovmf-vars.");
    }

    int hostLogicalProcessorCount = Environment.ProcessorCount;
    string[] qemuArguments = BuildArguments(ovmfCode, variableStore, runImage, serialLog, qemuPidFile, qemuProcessorCount, memoryMiB, storageTarget, networkTarget, graphicsTarget, usbTarget, qemuDebugLog, qemuDebugConLog, qmpPort);
    Console.WriteLine($"[INFO] QEMU executable: {qemu}");
    Console.WriteLine($"[INFO] OVMF code     : {ovmfCode}");
    Console.WriteLine($"[INFO] OVMF variables: {ovmfVars}");
    Console.WriteLine($"[INFO] Boot image    : {imagePath}");
    Console.WriteLine($"[INFO] QEMU serial log: {serialLog}");
    Console.WriteLine($"[INFO] QEMU diagnostic log: {qemuDebugLog}");
    Console.WriteLine($"[INFO] QEMU debugcon log: {qemuDebugConLog}");
    Console.WriteLine($"[INFO] QEMU stop report: {diagnosticReport}");
    Console.WriteLine($"[INFO] QMP diagnostics: 127.0.0.1:{qmpPort}");
    Console.WriteLine($"[INFO] Host CPUs     : {hostLogicalProcessorCount} logical processor(s)");
    Console.WriteLine($"[INFO] QEMU CPUs     : {qemuProcessorCount} logical processor(s)");
    Console.WriteLine($"[INFO] QEMU RAM      : {memoryMiB} MiB");
    Console.WriteLine($"[INFO] QEMU storage  : {storageTarget}");
    Console.WriteLine($"[INFO] QEMU network  : {networkTarget}");
    Console.WriteLine($"[INFO] QEMU graphics : {graphicsTarget}");
    Console.WriteLine($"[INFO] QEMU USB      : {usbTarget}");
    Console.WriteLine($"[INFO] {Quote(qemu)} {string.Join(" ", qemuArguments.Select(Quote))}");
    if (HasOption(args, "--dry-run"))
    {
        return 0;
    }

    if (!File.Exists(qemu)) return Fail($"QEMU executable not found: {qemu}");
    if (!File.Exists(imagePath)) return Fail($"Boot image not found: {imagePath}");
    if (!File.Exists(ovmfCode)) return Fail($"OVMF code firmware not found: {ovmfCode}");
    if (!File.Exists(ovmfVars)) return Fail($"OVMF variable-store template not found: {ovmfVars}");

    int qemuPid = 0;
    try
    {
        Directory.CreateDirectory(runDirectory);
        if (File.Exists(runManifest))
        {
            try
            {
                using JsonDocument previousRunDocument=JsonDocument.Parse(File.ReadAllText(runManifest));
                if(previousRunDocument.RootElement.TryGetProperty("qemuProcessId",out JsonElement previousPidElement)&&previousPidElement.TryGetInt32(out int previousPid)&&previousPid>0&&IsProcessAlive(previousPid))
                    return Fail($"A previous accepted Inu QEMU session is still running (PID {previousPid}). Close it before starting another run so both VMs cannot write the same persistent FAT32 image.");
            }
            catch(JsonException){ }
        }
        File.Copy(imagePath, preRunImageSnapshot, true);
        File.Copy(ovmfVars, variableStore, true);
        if (File.Exists(serialLog)) File.Delete(serialLog);
        if (File.Exists(qemuDebugLog)) File.Delete(qemuDebugLog);
        if (File.Exists(qemuDebugConLog)) File.Delete(qemuDebugConLog);
        if (File.Exists(diagnosticReport)) File.Delete(diagnosticReport);
        if (File.Exists(latestDiagnosticReport)) File.Delete(latestDiagnosticReport);
        // The shared manifest describes only a successfully accepted live run. Remove any
        // previous value before this run starts so failed/incomplete launches can never leave
        // an older QEMU session looking current to Kath or other SDK consumers.
        if (File.Exists(runManifest)) File.Delete(runManifest);

        // Launch QEMU through a short-lived run-local PowerShell script. Start-Process
        // gives Windows the executable path and argument list separately, so paths such as
        // C:\Program Files\qemu\qemu-system-x86_64.exe do not depend on cmd.exe START
        // quotation/errorlevel semantics. QEMU's stdout and stderr are ordinary files and
        // therefore cannot keep Kath's build-capture pipes open after acceptance succeeds.
        string powerShell = ResolvePowerShell();
        File.WriteAllText(qemuStarterScript, BuildDetachedQemuScript(qemu, qemuArguments, qemuStdoutLog, qemuStderrLog));
        using (Process starter = new())
        {
            // The short-lived PowerShell starter must not inherit this launcher's captured
            // stdout/stderr handles. Build-Inu reads those handles to EOF after this process
            // exits; if QEMU inherits either handle, successful runtime acceptance is not
            // reported until the long-lived QEMU process finally closes. ShellExecute gives
            // the starter an independent standard-handle set while its script still redirects
            // QEMU stdout/stderr to the run-local files below.
            starter.StartInfo = new ProcessStartInfo(powerShell)
            {
                UseShellExecute = true,
                WorkingDirectory = runDirectory,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            starter.StartInfo.ArgumentList.Add("-NoLogo");
            starter.StartInfo.ArgumentList.Add("-NoProfile");
            starter.StartInfo.ArgumentList.Add("-NonInteractive");
            starter.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            starter.StartInfo.ArgumentList.Add("Bypass");
            starter.StartInfo.ArgumentList.Add("-File");
            starter.StartInfo.ArgumentList.Add(qemuStarterScript);
            if (!starter.Start()) return Fail($"QEMU detached starter failed to start. Starter: {qemuStarterScript}");
            if (!starter.WaitForExit(5000))
            {
                try { starter.Kill(true); } catch { }
                return Fail($"QEMU detached starter did not return within 5 seconds. Starter: {qemuStarterScript}");
            }
        }

        // The starter process exit code is deliberately not treated as QEMU's status.
        // The authoritative proof is QEMU's own -pidfile followed by a live-process lookup.
        qemuPid = WaitForQemuPid(qemuPidFile, 5000);
        if (qemuPid <= 0) return Fail($"QEMU did not publish its PID. PID file: {qemuPidFile}; diagnostics: {qemuStderrLog}");
        if (!IsProcessAlive(qemuPid))
        {
            return Fail($"QEMU exited before runtime acceptance started. PID: {qemuPid}; diagnostics: {qemuStderrLog}");
        }

        Console.WriteLine($"[ OK ] QEMU started without -S. Process ID: {qemuPid}");
        const int gcStressGraceSeconds = 60;
        const int managedProgressGraceSeconds = 15;
        const string gcRunMarker = "NOBT:GC:RUN";
        const string gcOkMarker = "NOBT:GC:OK";
        Stopwatch acceptanceClock = Stopwatch.StartNew();
        TimeSpan acceptanceDeadline = TimeSpan.FromSeconds(timeoutSeconds);
        bool gcStressGraceActivated = false;
        bool gcStressCompleted = false;
        bool managedProgressGraceActivated = false;
        string serialText = string.Empty;
        while (acceptanceClock.Elapsed < acceptanceDeadline)
        {
            if (!IsProcessAlive(qemuPid))
            {
                // Preserve the same diagnostic contract as the timeout path: an
                // early reset/triple-fault must print the serial tail before the
                // launcher reports failure so the last kernel breadcrumb is visible
                // directly in the IDE output. The launcher deliberately does not
                // read ExitCode because QEMU was started by the detached starter.
                serialText = ReadSharedText(serialLog);
                string? crashDumpPath = TryMaterializeSerialCrashDump(serialText, runDirectory, project.Name, project.TargetArchitecture);
                PrintSerialTail(serialText);
                return Fail($"QEMU exited before runtime acceptance completed. PID: {qemuPid}. Serial log: {serialLog}; diagnostics: {qemuStderrLog}" + (crashDumpPath is null ? string.Empty : $"; crash dump: {crashDumpPath}"));
            }

            serialText = ReadSharedText(serialLog);
            if (serialText.Contains("[INU:CRASHDUMP]", StringComparison.Ordinal) || serialText.Contains("[INU:PANIC]", StringComparison.Ordinal))
            {
                string? crashDumpPath = TryMaterializeSerialCrashDump(serialText, runDirectory, project.Name, project.TargetArchitecture);
                PrintSerialTail(serialText);
                TryStop(qemuPid);
                return Fail("Inu reported a kernel panic during runtime acceptance." + (crashDumpPath is null ? $" Serial log: {serialLog}" : $" Crash dump: {crashDumpPath}"));
            }
            if (serialText.Contains("NOMNG:RETURN:FAIL", StringComparison.Ordinal) ||
                serialText.Contains("NOKMAIN:FAIL:", StringComparison.Ordinal) ||
                serialText.Contains("NOBT:FAIL:", StringComparison.Ordinal))
            {
                PersistLatestSerial(outputDirectory, serialText);
                PrintSerialTail(serialText);
                TryStop(qemuPid);
                return Fail($"Inu reported an explicit kernel startup failure during QEMU runtime acceptance. See the serial tail for the failing stage. Serial log: {serialLog}");
            }
            const string ttfReadyMarker = "[[INU:TTF_READY]]";
            const string interactiveReadyMarker = "[[INU:INTERACTIVE_READY]]";
            bool ttfReady = serialText.Contains(ttfReadyMarker, StringComparison.Ordinal);
            bool interactiveReady = serialText.Contains(interactiveReadyMarker, StringComparison.Ordinal);
            bool gcStressRunning = serialText.Contains(gcRunMarker, StringComparison.Ordinal);
            bool gcStressReady = serialText.Contains(gcOkMarker, StringComparison.Ordinal);
            bool managedConformanceProgress =
                serialText.Contains("EH:1890", StringComparison.Ordinal);

            // 0.0.109: the GVM acceptance probe is intentionally heavily instrumented.
            // Under TCG that serial traffic can consume a material part of the base boot
            // window. Once both class-GVM calls have completed, allow the new interface-GVM
            // and dynamic-dictionary gates to finish before collection conformance. Grant one
            // bounded post-progress window; unlike the mature-GC allowance this is never repeated.
            if (managedConformanceProgress && !ttfReady && !managedProgressGraceActivated)
            {
                managedProgressGraceActivated = true;
                // 0.0.106: this is an allowance in addition to the existing boot
                // deadline. 0.0.104/0.0.105 incorrectly used elapsed+grace, which
                // often equalled the original 20-second deadline when EH:1890 arrived
                // early. The launcher announced an extension without extending QEMU's
                // lifetime. Keep this additive and one-shot.
                acceptanceDeadline += TimeSpan.FromSeconds(managedProgressGraceSeconds);
                Console.WriteLine($"[INFO] Managed conformance is still advancing through interface-GVM/dynamic-dictionary checks after class-GVM completion; extending the current acceptance deadline by {managedProgressGraceSeconds} seconds before graphics readiness.");
            }

            if (gcStressRunning && !gcStressReady && !gcStressGraceActivated)
            {
                gcStressGraceActivated = true;
                acceptanceDeadline += TimeSpan.FromSeconds(gcStressGraceSeconds);
                Console.WriteLine($"[INFO] Inu mature-GC stress gate detected; allowing up to {gcStressGraceSeconds} additional seconds for the mandatory QEMU stress run.");
            }

            if (gcStressReady && !gcStressCompleted)
            {
                gcStressCompleted = true;
                TimeSpan postGcDeadline = acceptanceClock.Elapsed + TimeSpan.FromSeconds(timeoutSeconds);
                if (postGcDeadline > acceptanceDeadline) acceptanceDeadline = postGcDeadline;
                Console.WriteLine($"[ OK ] Inu mature-GC stress gate completed; interactive readiness retains a {timeoutSeconds}-second bounded window.");
            }

            if (ttfReady && interactiveReady)
            {
                Thread.Sleep(500);
                if (!IsProcessAlive(qemuPid))
                {
                    return Fail($"QEMU exited after the interactive command prompt appeared. PID: {qemuPid}; diagnostics: {qemuStderrLog}");
                }

                serialText = ReadSharedText(serialLog);
                int readyIndex = serialText.LastIndexOf(interactiveReadyMarker, StringComparison.Ordinal);
                string postReadyText = readyIndex >= 0 ? serialText[(readyIndex + interactiveReadyMarker.Length)..] : string.Empty;
                if (postReadyText.Contains("NOKMAIN:FAIL:", StringComparison.Ordinal) ||
                    postReadyText.Contains("[INU:PANIC]", StringComparison.Ordinal))
                {
                    string? crashDumpPath = TryMaterializeSerialCrashDump(serialText, runDirectory, project.Name, project.TargetArchitecture);
                    PrintSerialTail(serialText);
                    TryStop(qemuPid);
                    return Fail($"Inu reported a runtime failure after the interactive-ready marker. Serial log: {serialLog}" + (crashDumpPath is null ? string.Empty : $"; crash dump: {crashDumpPath}"));
                }

                foreach (string requiredMarker in requiredMarkers)
                {
                    if (!serialText.Contains(requiredMarker, StringComparison.Ordinal))
                    {
                        PrintSerialTail(serialText);
                        TryStop(qemuPid);
                        return Fail($"Required QEMU acceptance marker was not observed: {requiredMarker}. Serial log: {serialLog}");
                    }
                }

                string latestSerialLog = Path.Combine(outputDirectory, "serial.log");
                File.WriteAllText(latestSerialLog, serialText);
                File.WriteAllText(runManifest, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    productVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
                    project = project.Name,
                    runId,
                    runDirectory,
                    qemuProcessId = qemuPid,
                    qemuExecutable = qemu,
                    hostLogicalProcessorCount,
                    qemuProcessorCount,
                    memoryMiB,
                    storageTarget,
                    networkTarget,
                    graphicsTarget,
                    usbTarget,
                    requiredMarkers,
                    acceptAndStop,
                    ovmfCode,
                    ovmfVariableTemplate = ovmfVars,
                    variableStore,
                    bootImage = runImage,
                    preRunImageSnapshot,
                    persistentWritableImage = true,
                    serialLog,
                    latestSerialLog,
                    qemuDebugLog,
                    qemuDebugConLog,
                    diagnosticReport,
                    latestDiagnosticReport,
                    qmpHost = "127.0.0.1",
                    qmpPort,
                    managedKMainConfirmed = true,
                    trueTypeConsoleConfirmed = true,
                    interactiveConsoleConfirmed = true,
                    qemuRemainedOpen = !acceptAndStop,
                    acceptedUtc = DateTimeOffset.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true }));

                Console.WriteLine("[ OK ] Managed KMain execution confirmed.");
                Console.WriteLine("[ OK ] TrueType graphics console confirmed.");
                Console.WriteLine("[ OK ] Interactive command prompt confirmed.");
                Console.WriteLine($"[ OK ] QEMU remains open indefinitely. Process ID: {qemuPid}");
                Console.WriteLine($"[ OK ] Serial output captured: {latestSerialLog}");
                Console.WriteLine($"[ OK ] Live run directory: {runDirectory}");
                if (acceptAndStop)
                {
                    TryStop(qemuPid);
                    Console.WriteLine("[ OK ] QEMU acceptance completed and the matrix/test instance was stopped cleanly.");
                    return 0;
                }
                if (StartDiagnosticWatcher(qemuPid, qmpPort, serialLog, qemuDebugLog, qemuDebugConLog, qemuStderrLog, diagnosticReport, latestDiagnosticReport, diagnosticWatcherScript, diagnosticWatcherStdout, diagnosticWatcherStderr))
                {
                    Console.WriteLine($"[ OK ] QEMU stop-state watcher armed. If QEMU enters [Stopped], Kath will ingest: {diagnosticReport}");
                }
                else
                {
                    Console.WriteLine($"[WARN] QEMU stop-state watcher could not be started. Manual logs remain in: {runDirectory}");
                }
                return 0;
            }

            Thread.Sleep(100);
        }

        serialText = ReadSharedText(serialLog);
        string? timeoutCrashDumpPath = TryMaterializeSerialCrashDump(serialText, runDirectory, project.Name, project.TargetArchitecture);
        if(timeoutCrashDumpPath is not null) Console.WriteLine($"[INFO] Crash dump materialized from serial panic record: {timeoutCrashDumpPath}");
        PersistLatestSerial(outputDirectory, serialText);
        PrintSerialDiagnosticSummary(serialText);
        PrintSerialTail(serialText);
        TryStop(qemuPid);
        if (string.IsNullOrEmpty(serialText))
        {
            return Fail($"Timed out before any Inu serial output appeared after {timeoutSeconds} seconds. Serial log: {serialLog}");
        }
        if (serialText.Contains("EH:188C", StringComparison.Ordinal) &&
            !serialText.Contains("EH:1890", StringComparison.Ordinal))
        {
            return Fail($"Timed out during managed class-GVM conformance before the graphics-console readiness stage. Base boot window: {timeoutSeconds}s. See the managed-conformance last-stage diagnostic above. Serial log: {serialLog}");
        }
        int gvmCompletedIndex = serialText.LastIndexOf("EH:1890", StringComparison.Ordinal);
        int interfaceGvmCompletedIndex = serialText.LastIndexOf("EH:1896", StringComparison.Ordinal);
        if (gvmCompletedIndex >= 0 && interfaceGvmCompletedIndex < gvmCompletedIndex)
        {
            return Fail($"Timed out during managed interface-GVM/dynamic-dictionary conformance after class-GVM success. Base boot window: {timeoutSeconds}s; bounded post-GVM progress allowance: {managedProgressGraceSeconds}s. See the managed-conformance last-stage diagnostic above. Serial log: {serialLog}");
        }
        int collectionGateIndex = serialText.LastIndexOf("EH:B4", StringComparison.Ordinal);
        if (interfaceGvmCompletedIndex >= 0 &&
            collectionGateIndex > interfaceGvmCompletedIndex &&
            !serialText.Contains("EH:B5", StringComparison.Ordinal))
        {
            return Fail($"Timed out during managed collection conformance after interface-GVM success. Base boot window: {timeoutSeconds}s; bounded post-GVM progress allowance: {managedProgressGraceSeconds}s. See the managed-conformance last-stage diagnostic above. Serial log: {serialLog}");
        }
        if (!serialText.Contains("[[INU:TTF_READY]]", StringComparison.Ordinal))
        {
            return Fail($"Timed out before Inu confirmed the TrueType graphics console after the bounded boot/progress windows. Serial log: {serialLog}");
        }
        if (serialText.Contains(gcRunMarker, StringComparison.Ordinal) && !serialText.Contains(gcOkMarker, StringComparison.Ordinal))
        {
            return Fail($"Timed out before Inu completed the mature-GC stress gate. Base boot window: {timeoutSeconds}s; GC stress allowance: {gcStressGraceSeconds}s. Serial log: {serialLog}");
        }
        if (!serialText.Contains("[[INU:INTERACTIVE_READY]]", StringComparison.Ordinal))
        {
            return Fail($"Timed out before Inu emitted the interactive-ready marker after the bounded boot/GC acceptance windows. Serial log: {serialLog}");
        }
        return Fail($"Inu readiness markers were observed, but runtime acceptance did not complete within the bounded boot/GC acceptance windows. Serial log: {serialLog}");
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
    {
        if (qemuPid > 0) TryStop(qemuPid);
        return Fail(exception.Message);
    }
}


static int WatchDiagnostics(string[] args)
{
    if (!int.TryParse(GetOption(args, "--pid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int qemuPid) || qemuPid <= 0 ||
        !int.TryParse(GetOption(args, "--qmp-port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int qmpPort) || qmpPort <= 0)
    {
        return Fail("watch-diagnostics requires --pid and --qmp-port.");
    }

    string? serialLog = GetOption(args, "--serial");
    string? qemuDebugLog = GetOption(args, "--qemu-debug");
    string? debugConLog = GetOption(args, "--debugcon");
    string? stderrLog = GetOption(args, "--stderr");
    string? reportPath = GetOption(args, "--report");
    string? latestReportPath = GetOption(args, "--latest-report");
    if (string.IsNullOrWhiteSpace(serialLog) || string.IsNullOrWhiteSpace(qemuDebugLog) ||
        string.IsNullOrWhiteSpace(debugConLog) || string.IsNullOrWhiteSpace(stderrLog) ||
        string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(latestReportPath))
    {
        return Fail("watch-diagnostics is missing one or more diagnostic file paths.");
    }

    string lastKnownStatus = "unknown";
    int consecutiveQmpFailures = 0;
    while (IsProcessAlive(qemuPid))
    {
        (bool success, string status, string registers, string cpus, string error) = QueryQmpSnapshot(qmpPort, false);
        if (success)
        {
            consecutiveQmpFailures = 0;
            lastKnownStatus = status;
            if (!string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
            {
                Thread.Sleep(150);
                var confirmation = QueryQmpSnapshot(qmpPort, false);
                if (!confirmation.Success || !string.Equals(confirmation.Status, "running", StringComparison.OrdinalIgnoreCase))
                {
                    CaptureDiagnosticReport(qemuPid, qmpPort, confirmation.Success ? confirmation.Status : status, serialLog, qemuDebugLog, debugConLog, stderrLog, reportPath, latestReportPath);
                    return 0;
                }
            }
        }
        else
        {
            consecutiveQmpFailures++;
            if (consecutiveQmpFailures >= 20 && !IsProcessAlive(qemuPid)) break;
        }
        Thread.Sleep(250);
    }

    CaptureDiagnosticReport(qemuPid, qmpPort, "process-exited (last QMP status: " + lastKnownStatus + ")", serialLog, qemuDebugLog, debugConLog, stderrLog, reportPath, latestReportPath);
    return 0;
}

static bool StartDiagnosticWatcher(int qemuPid, int qmpPort, string serialLog, string qemuDebugLog, string debugConLog, string stderrLog, string reportPath, string latestReportPath, string watcherScript, string watcherStdout, string watcherStderr)
{
    try
    {
        string? dotnet = Environment.ProcessPath;
        string launcherDll = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrWhiteSpace(dotnet) || string.IsNullOrWhiteSpace(launcherDll) || !File.Exists(dotnet) || !File.Exists(launcherDll)) return false;
        string[] watcherArguments =
        [
            launcherDll, "watch-diagnostics",
            "--pid", qemuPid.ToString(CultureInfo.InvariantCulture),
            "--qmp-port", qmpPort.ToString(CultureInfo.InvariantCulture),
            "--serial", serialLog,
            "--qemu-debug", qemuDebugLog,
            "--debugcon", debugConLog,
            "--stderr", stderrLog,
            "--report", reportPath,
            "--latest-report", latestReportPath
        ];
        string argumentLine = string.Join(" ", watcherArguments.Select(QuoteWindowsArgument));
        string script = "$ErrorActionPreference = 'Stop'\r\n" +
                        $"$exe = {QuoteForPowerShell(dotnet)}\r\n" +
                        $"$arguments = {QuoteForPowerShell(argumentLine)}\r\n" +
                        $"$stdout = {QuoteForPowerShell(watcherStdout)}\r\n" +
                        $"$stderr = {QuoteForPowerShell(watcherStderr)}\r\n" +
                        "Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr | Out-Null\r\n";
        File.WriteAllText(watcherScript, script);
        string powerShell = ResolvePowerShell();
        using Process starter = new();
        // Isolate the diagnostic-watcher's starter for the same reason as the QEMU
        // starter above. The watcher intentionally lives as long as QEMU; it must never
        // retain the parent launcher's captured stdout/stderr handles and delay acceptance.
        starter.StartInfo = new ProcessStartInfo(powerShell)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(watcherScript) ?? Environment.CurrentDirectory,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        starter.StartInfo.ArgumentList.Add("-NoLogo");
        starter.StartInfo.ArgumentList.Add("-NoProfile");
        starter.StartInfo.ArgumentList.Add("-NonInteractive");
        starter.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        starter.StartInfo.ArgumentList.Add("Bypass");
        starter.StartInfo.ArgumentList.Add("-File");
        starter.StartInfo.ArgumentList.Add(watcherScript);
        if (!starter.Start()) return false;
        if (!starter.WaitForExit(5000)) { try { starter.Kill(true); } catch { } return false; }
        return starter.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

static int ReserveLoopbackPort()
{
    try
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    catch
    {
        return 0;
    }
}

static (bool Success, string Status, string Registers, string Cpus, string Error) QueryQmpSnapshot(int port, bool includeDetails)
{
    try
    {
        using TcpClient client = new();
        Task connect = client.ConnectAsync(IPAddress.Loopback, port);
        if (!connect.Wait(1000) || !client.Connected) return (false, "unavailable", string.Empty, string.Empty, "QMP connect timed out");
        using NetworkStream stream = client.GetStream();
        stream.ReadTimeout = 1500;
        stream.WriteTimeout = 1500;
        using StreamReader reader = new(stream, Encoding.UTF8, false, 4096, true);
        using StreamWriter writer = new(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true, NewLine = "\r\n" };

        string? greeting = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(greeting) || !greeting.Contains("\"QMP\"", StringComparison.Ordinal))
            return (false, "unavailable", string.Empty, string.Empty, "QMP greeting was not received");
        writer.WriteLine("{\"execute\":\"qmp_capabilities\"}");
        if (!TryReadQmpReturn(reader, out _, out string capabilityError))
            return (false, "unavailable", string.Empty, string.Empty, capabilityError);

        writer.WriteLine("{\"execute\":\"query-status\"}");
        if (!TryReadQmpReturn(reader, out string statusReturn, out string statusError))
            return (false, "unavailable", string.Empty, string.Empty, statusError);
        string status = "unknown";
        using (JsonDocument statusDocument = JsonDocument.Parse(statusReturn))
        {
            JsonElement root = statusDocument.RootElement;
            if (root.TryGetProperty("return", out JsonElement returned) && returned.ValueKind == JsonValueKind.Object && returned.TryGetProperty("status", out JsonElement statusElement))
                status = statusElement.GetString() ?? "unknown";
        }

        string registers = string.Empty;
        string cpus = string.Empty;
        if (includeDetails)
        {
            registers = QueryHmp(reader, writer, "info registers");
            cpus = QueryHmp(reader, writer, "info cpus");
        }
        return (true, status, registers, cpus, string.Empty);
    }
    catch (Exception exception) when (exception is IOException or SocketException or InvalidOperationException or JsonException or AggregateException)
    {
        return (false, "unavailable", string.Empty, string.Empty, exception.Message);
    }
}

static string QueryHmp(StreamReader reader, StreamWriter writer, string command)
{
    string encodedCommand = JsonSerializer.Serialize(command);
    writer.WriteLine("{\"execute\":\"human-monitor-command\",\"arguments\":{\"command-line\":" + encodedCommand + "}}");
    if (!TryReadQmpReturn(reader, out string response, out string error)) return "<QMP/HMP error: " + error + ">";
    try
    {
        using JsonDocument document = JsonDocument.Parse(response);
        if (document.RootElement.TryGetProperty("return", out JsonElement returned)) return returned.GetString() ?? returned.ToString();
    }
    catch (JsonException exception)
    {
        return "<invalid HMP response: " + exception.Message + ">";
    }
    return "<no HMP result>";
}

static bool TryReadQmpReturn(StreamReader reader, out string response, out string error)
{
    response = string.Empty;
    error = string.Empty;
    for (int attempt = 0; attempt < 64; attempt++)
    {
        string? line = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("event", out _)) continue;
            if (root.TryGetProperty("return", out _)) { response = line; return true; }
            if (root.TryGetProperty("error", out JsonElement qmpError)) { error = qmpError.ToString(); return false; }
        }
        catch (JsonException)
        {
        }
    }
    error = "QMP response limit reached";
    return false;
}

static string? TryMaterializeSerialCrashDump(string serialText, string runDirectory, string projectName, string architecture)
{
    const string marker = "[INU:CRASHDUMP]";
    int markerIndex = serialText.LastIndexOf(marker, StringComparison.Ordinal);
    if (markerIndex < 0) return null;
    int lineEnd = serialText.IndexOf('\n', markerIndex);
    string line = (lineEnd < 0 ? serialText[markerIndex..] : serialText[markerIndex..lineEnd]).Trim();
    Dictionary<string,string> fields = new(StringComparer.OrdinalIgnoreCase);
    foreach (string token in line[marker.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        int equals = token.IndexOf('=');
        if (equals <= 0 || equals == token.Length - 1) continue;
        fields[token[..equals]] = token[(equals + 1)..];
    }

    static ulong Number(Dictionary<string,string> map, string key)
    {
        if (!map.TryGetValue(key, out string? text)) return 0UL;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex) ? hex : 0UL;
        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value) ? value : 0UL;
    }
    static string Hex(ulong value) => "0x" + value.ToString("X16", CultureInfo.InvariantCulture);

    ulong code=Number(fields,"code"),cpu=Number(fields,"cpu"),thread=Number(fields,"thread"),process=Number(fields,"process");
    ulong rip=Number(fields,"rip"),rsp=Number(fields,"rsp"),rbp=Number(fields,"rbp"),rflags=Number(fields,"rflags"),cr3=Number(fields,"cr3");
    int frameCount=(int)Math.Min(8UL,Number(fields,"frames"));
    List<object> frames=new();
    for(int i=0;i<frameCount;i++){ulong ip=Number(fields,"f"+i.ToString(CultureInfo.InvariantCulture));if(ip!=0UL)frames.Add(new { index=i, instructionPointer=Hex(ip) });}
    object[] registers =
    [
        new { name="rip", value=Hex(rip) }, new { name="rsp", value=Hex(rsp) }, new { name="rbp", value=Hex(rbp) },
        new { name="rflags", value=Hex(rflags) }, new { name="cr3", value=Hex(cr3) }
    ];
    string createdUtc=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture);
    string serialTail=serialText.Length<=65536?serialText:serialText[^65536..];
    object[] processData=process==0UL?Array.Empty<object>():new object[]{new { processId=process, current=true }};
    object unavailable(string note)=>new { version=1, available=false, data=new { }, note };
    var document = new
    {
        magic="NOCD", format="Inu Crash Dump", formatVersion=new { major=1, minor=1 }, architecture,
        createdUtc, producer=new { product="Inu.QemuLauncher", version=Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown" }, project=new { name=projectName },
        sections=new Dictionary<string,object>
        {
            ["cpuState"]=new { version=1, available=true, data=new { architecture, cpuIndex=cpu, threadId=thread, processId=process, instructionPointer=Hex(rip), stackPointer=Hex(rsp), framePointer=Hex(rbp), flags=Hex(rflags), pageTableRoot=Hex(cr3) } },
            ["registers"]=new { version=1, available=true, data=registers },
            ["stack"]=new { version=1, available=frames.Count>0, data=new { frames } },
            ["pageTables"]=new { version=1, available=cr3!=0UL, data=new { cr3=Hex(cr3), decoded=false }, note="Serial panic capture records CR3; Kath can decode page tables when a stopped debug session is available." },
            ["processes"]=new { version=1, available=process!=0UL, data=processData },
            ["modules"]=unavailable("Module enumeration requires a debug-map/session capture."),
            ["heap"]=unavailable("Heap diagnostics were not exported in the allocation-free panic record."),
            ["memoryRanges"]=unavailable("Memory range capture requires QMP/debugger memory access."),
            ["panic"]=new { version=1, available=true, data=new { reason="kernel panic", code, message="Captured from allocation-free serial panic record." } },
            ["drivers"]=unavailable("Live driver-state export was not available in the serial panic record."),
            ["telemetry"]=new { version=1, available=true, data=new { serialTail }, note="NOCD 1.1 standard telemetry section containing the bounded recent serial/telemetry tail." }
        }
    };
    try
    {
        Directory.CreateDirectory(runDirectory);
        string path=Path.Combine(runDirectory,"Inu-Crash-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+".nodump.json");
        File.WriteAllText(path,JsonSerializer.Serialize(document,new JsonSerializerOptions{WriteIndented=true}));
        return path;
    }
    catch (IOException) { return null; }
    catch (UnauthorizedAccessException) { return null; }
}

static void CaptureDiagnosticReport(int qemuPid, int qmpPort, string observedStatus, string serialLog, string qemuDebugLog, string debugConLog, string stderrLog, string reportPath, string latestReportPath)
{
    try
    {
        (bool qmpOk, string qmpStatus, string registers, string cpus, string qmpError) = QueryQmpSnapshot(qmpPort, true);
        StringBuilder report = new();
        report.AppendLine("Inu QEMU stop-state diagnostics " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"));
        report.AppendLine("capturedUtc: " + DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        report.AppendLine("qemuPid: " + qemuPid.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("processAlive: " + IsProcessAlive(qemuPid));
        report.AppendLine("observedStatus: " + observedStatus);
        report.AppendLine("qmpStatus: " + (qmpOk ? qmpStatus : "unavailable"));
        if (!qmpOk) report.AppendLine("qmpError: " + qmpError);
        report.AppendLine();
        report.AppendLine("=== QEMU CPU registers (HMP info registers) ===");
        report.AppendLine(string.IsNullOrWhiteSpace(registers) ? "<unavailable>" : registers.TrimEnd());
        report.AppendLine();
        report.AppendLine("=== QEMU CPUs (HMP info cpus) ===");
        report.AppendLine(string.IsNullOrWhiteSpace(cpus) ? "<unavailable>" : cpus.TrimEnd());
        report.AppendLine();
        report.AppendLine("=== Inu serial tail (last 256 KiB) ===");
        report.AppendLine(ReadTextTail(serialLog, 262144));
        report.AppendLine();
        report.AppendLine("=== QEMU exception/triple-fault chain (filtered from -d int,guest_errors,cpu_reset) ===");
        report.AppendLine(ReadQemuExceptionTail(qemuDebugLog, 256));
        report.AppendLine();
        report.AppendLine("=== QEMU diagnostic log tail (int,guest_errors,cpu_reset; last 256 KiB) ===");
        report.AppendLine(ReadTextTail(qemuDebugLog, 262144));
        report.AppendLine();
        report.AppendLine("=== QEMU stderr tail (last 64 KiB) ===");
        report.AppendLine(ReadTextTail(stderrLog, 65536));
        report.AppendLine();
        report.AppendLine("=== isa-debugcon 0xE9 tail (hex, last 512 bytes) ===");
        report.AppendLine(ReadBinaryTailHex(debugConLog, 512));
        report.AppendLine();
        report.AppendLine("=== Source files ===");
        report.AppendLine("serial: " + serialLog);
        report.AppendLine("qemuDiagnostics: " + qemuDebugLog);
        report.AppendLine("debugcon: " + debugConLog);
        report.AppendLine("qemuStderr: " + stderrLog);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Environment.CurrentDirectory);
        File.WriteAllText(reportPath, report.ToString());
        Directory.CreateDirectory(Path.GetDirectoryName(latestReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllText(latestReportPath, report.ToString());
    }
    catch
    {
    }
}

static string ReadTextTail(string path, int maximumCharacters)
{
    string text = ReadSharedText(path);
    if (string.IsNullOrEmpty(text)) return "<empty or unavailable>";
    return text.Length <= maximumCharacters ? text : text[^maximumCharacters..];
}

static string ReadQemuExceptionTail(string path, int maximumLines)
{
    string text = ReadSharedText(path);
    if (string.IsNullOrEmpty(text)) return "<empty or unavailable>";
    string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    List<string> selected = new();
    foreach (string line in lines)
    {
        string lower = line.ToLowerInvariant();
        if (lower.Contains("triple fault", StringComparison.Ordinal) ||
            lower.Contains("check_exception", StringComparison.Ordinal) ||
            lower.Contains("exception_index", StringComparison.Ordinal) ||
            lower.Contains("v=0e", StringComparison.Ordinal) || lower.Contains("v=08", StringComparison.Ordinal) ||
            lower.Contains("v=0d", StringComparison.Ordinal) || lower.Contains("v=0c", StringComparison.Ordinal) ||
            lower.Contains("v=06", StringComparison.Ordinal) || lower.Contains("v=0b", StringComparison.Ordinal) ||
            lower.Contains("v=0a", StringComparison.Ordinal))
            selected.Add(line);
    }
    if (selected.Count == 0) return "<no filtered exception lines>";
    int start = Math.Max(0, selected.Count - maximumLines);
    return string.Join(Environment.NewLine, selected.Skip(start));
}

static string ReadBinaryTailHex(string path, int maximumBytes)
{
    try
    {
        if (!File.Exists(path)) return "<empty or unavailable>";
        byte[] bytes = File.ReadAllBytes(path);
        int start = Math.Max(0, bytes.Length - maximumBytes);
        StringBuilder builder = new();
        for (int i = start; i < bytes.Length; i++)
        {
            if (builder.Length != 0) builder.Append(' ');
            builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }
        return builder.Length == 0 ? "<empty>" : builder.ToString();
    }
    catch (IOException)
    {
        return "<unavailable>";
    }
    catch (UnauthorizedAccessException)
    {
        return "<unavailable>";
    }
}

static string[] BuildArguments(string ovmfCode, string variableStore, string runImage, string serialLog, string qemuPidFile, int qemuProcessorCount, int memoryMiB, string storageTarget, string networkTarget, string graphicsTarget, string usbTarget, string qemuDebugLog, string qemuDebugConLog, int qmpPort)
{
    List<string> arguments =
    [
        "-machine", "q35",
        "-accel", "tcg,thread=multi",
        "-cpu", "max",
        "-smp", qemuProcessorCount.ToString(CultureInfo.InvariantCulture),
        "-m", memoryMiB.ToString(CultureInfo.InvariantCulture) + "M",
        "-display", "sdl",
        "-drive", $"if=pflash,format=raw,unit=0,readonly=on,file={EscapeDriveValue(ovmfCode)}",
        "-drive", $"if=pflash,format=raw,unit=1,file={EscapeDriveValue(variableStore)}",
        "-drive", $"if=none,format=raw,file={EscapeDriveValue(runImage)},id=boot"
    ];

    switch (storageTarget)
    {
        case "virtio-block": arguments.AddRange(["-device", "virtio-blk-pci,disable-legacy=on,drive=boot,bootindex=0"]); break;
        case "ahci":
            arguments.AddRange(["-device", "ich9-ahci,id=inu-ahci"]);
            arguments.AddRange(["-device", "ide-hd,drive=boot,bus=inu-ahci.0,bootindex=0"]);
            break;
        case "nvme": arguments.AddRange(["-device", "nvme,drive=boot,serial=INU0001,bootindex=0"]); break;
        default: throw new ArgumentException("Unsupported storage target: " + storageTarget);
    }

    switch (networkTarget)
    {
        case "none": arguments.AddRange(["-nic", "none"]); break;
        case "virtio-net": arguments.AddRange(["-netdev", "user,id=net0", "-device", "virtio-net-pci,netdev=net0"]); break;
        case "e1000": arguments.AddRange(["-netdev", "user,id=net0", "-device", "e1000,netdev=net0"]); break;
        default: throw new ArgumentException("Unsupported network target: " + networkTarget);
    }

    if (graphicsTarget == "virtio-gpu") arguments.AddRange(["-device", "virtio-vga"]);
    else arguments.AddRange(["-device", "VGA"]);
    if (usbTarget == "xhci") arguments.AddRange(["-device", "qemu-xhci,id=xhci"]);

    arguments.AddRange([
        "-boot", "order=c,menu=off,strict=on",
        "-pidfile", qemuPidFile,
        "-serial", $"file:{serialLog}",
        "-D", qemuDebugLog,
        "-d", "int,guest_errors,cpu_reset",
        "-debugcon", $"file:{qemuDebugConLog}",
        "-global", "isa-debugcon.iobase=0xe9",
        "-qmp", $"tcp:127.0.0.1:{qmpPort},server=on,wait=off",
        "-monitor", "none",
        "-no-reboot",
        "-no-shutdown"
    ]);
    return arguments.ToArray();
}

static string BuildDetachedQemuScript(string qemu, string[] qemuArguments, string stdoutLog, string stderrLog)
{
    // Windows PowerShell 5.1 joins Start-Process -ArgumentList array elements back
    // into a single C# / NativeAOTommand line without preserving the element quoting.
    // Supply one already-quoted Windows command line instead so an argument such as
    // if=pflash,...,file=C:\Program Files\qemu\... remains one argv element.
    string argumentLine = string.Join(" ", qemuArguments.Select(QuoteWindowsArgument));
    return "$ErrorActionPreference = 'Stop'\r\n" +
           $"$qemu = {QuoteForPowerShell(qemu)}\r\n" +
           $"$qemuArgumentLine = {QuoteForPowerShell(argumentLine)}\r\n" +
           $"$stdout = {QuoteForPowerShell(stdoutLog)}\r\n" +
           $"$stderr = {QuoteForPowerShell(stderrLog)}\r\n" +
           "Start-Process -FilePath $qemu -ArgumentList $qemuArgumentLine -RedirectStandardOutput $stdout -RedirectStandardError $stderr | Out-Null\r\n";
}


static string QuoteWindowsArgument(string value)
{
    if (value.Length == 0) return "\"\"";
    if (!value.Any(char.IsWhiteSpace) && !value.Contains('\"')) return value;

    System.Text.StringBuilder builder = new();
    builder.Append('\"');
    int backslashes = 0;
    foreach (char ch in value)
    {
        if (ch == '\\')
        {
            backslashes++;
            continue;
        }

        if (ch == '\"')
        {
            builder.Append('\\', backslashes * 2 + 1);
            builder.Append('\"');
            backslashes = 0;
            continue;
        }

        builder.Append('\\', backslashes);
        backslashes = 0;
        builder.Append(ch);
    }

    // Backslashes before the closing quote must be doubled.
    builder.Append('\\', backslashes * 2);
    builder.Append('\"');
    return builder.ToString();
}

static string ResolvePowerShell()
{
    string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    if (!string.IsNullOrWhiteSpace(systemRoot))
    {
        string windowsPowerShell = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(windowsPowerShell)) return windowsPowerShell;
    }
    return "powershell.exe";
}


static int WaitForQemuPid(string pidFile, int timeoutMilliseconds)
{
    Stopwatch clock = Stopwatch.StartNew();
    while (clock.ElapsedMilliseconds < timeoutMilliseconds)
    {
        try
        {
            if (File.Exists(pidFile))
            {
                string text = File.ReadAllText(pidFile).Trim();
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) && pid > 0) return pid;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        Thread.Sleep(50);
    }
    return 0;
}

static string QuoteForPowerShell(string value)
{
    // Single-quoted PowerShell literals are exact apart from an embedded apostrophe,
    // represented by two apostrophes. QEMU arguments are passed as an array to
    // Start-Process instead of being reparsed by cmd.exe.
    return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}

static int ResolveQemuProcessorCount(InuProject project, string? explicitValue)
{
    if (!string.IsNullOrWhiteSpace(explicitValue) && int.TryParse(explicitValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int explicitCount))
    {
        if (explicitCount < 1 || explicitCount > 256) throw new ArgumentOutOfRangeException(nameof(explicitValue), "--cpus must be between 1 and 256.");
        return explicitCount;
    }
    string? environment = Environment.GetEnvironmentVariable("INU_TARGET_CPUS");
    if (!string.IsNullOrWhiteSpace(environment) && int.TryParse(environment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested))
    {
        if (requested < 1 || requested > 256) throw new InvalidOperationException("INU_TARGET_CPUS must be between 1 and 256.");
        return requested;
    }
    if (project.QemuCpuCount < 1 || project.QemuCpuCount > 256) throw new InvalidOperationException("InuProject.json QemuCpuCount must be between 1 and 256.");
    return project.QemuCpuCount;
}

static int ParseBoundedInt(string? value, int defaultValue, int minimum, int maximum, string optionName)
{
    if (string.IsNullOrWhiteSpace(value)) return defaultValue;
    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < minimum || parsed > maximum)
        throw new ArgumentOutOfRangeException(optionName, $"{optionName} must be between {minimum} and {maximum}.");
    return parsed;
}

static string ParseChoice(string? value, string defaultValue, string[] choices, string optionName)
{
    string resolved = string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();
    if (!choices.Contains(resolved, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException($"{optionName} must be one of: {string.Join(", ", choices)}.");
    return resolved;
}

static string? ResolveFirmware(string qemu, string? explicitPath, string[] names)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return Path.GetFullPath(explicitPath);
    }

    string qemuDirectory = Path.GetDirectoryName(qemu) ?? Environment.CurrentDirectory;
    string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    List<string> roots =
    [
        qemuDirectory,
        Path.Combine(qemuDirectory, "share"),
        Path.Combine(qemuDirectory, "share", "qemu"),
        Path.GetFullPath(Path.Combine(qemuDirectory, "..", "share")),
        Path.GetFullPath(Path.Combine(qemuDirectory, "..", "share", "qemu")),
        Path.Combine(programFiles, "qemu"),
        Path.Combine(programFilesX86, "qemu"),
        Path.Combine(localAppData, "Programs", "qemu")
    ];

    foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (!Directory.Exists(root)) continue;
        foreach (string name in names)
        {
            string direct = Path.Combine(root, name);
            if (File.Exists(direct)) return Path.GetFullPath(direct);
            try
            {
                string? recursive = Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(recursive)) return Path.GetFullPath(recursive);
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
    return null;
}

static int ParseTimeout(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return 20;
    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds < 5 || seconds > 300)
    {
        throw new ArgumentOutOfRangeException(nameof(value), "Boot timeout must be between 5 and 300 seconds.");
    }
    return seconds;
}


static void PersistLatestSerial(string outputDirectory, string serialText)
{
    if (string.IsNullOrEmpty(serialText)) return;
    try
    {
        File.WriteAllText(Path.Combine(outputDirectory, "serial.log"), serialText);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}


static void PrintSerialDiagnosticSummary(string serialText)
{
    if (string.IsNullOrEmpty(serialText)) return;
    string[] interestingPrefixes =
    [
        "NOBT:MODULES",
        "NOBT:FAIL:MODULE",
        "EH:8B",
        // 0.0.103: InuEhTrace now preserves full diagnostic identifiers. The old
        // 0x193..0x198 TypeManager probes therefore appear as 193..198 instead of
        // being truncated to their low byte (93..98).
        "EH:193",
        "EV:194=",
        "EV:195=",
        "EV:196=",
        "EV:198=",
        "EH:197",
        "EV:8C=",
        "EV:8D=",
        "EV:8E=",
        "EV:8F=",
        "EV:80=",
        "EV:81=",
        // Fine-grained generic/GVM conformance and TypeLoader diagnostics.
        "EH:188",
        "EV:188",
        "EH:189",
        "EV:189",
        "EH:190",
        "EV:190",
        "EH:191",
        "EV:191",
        "EH:192",
        "EV:192",
        "EH:193",
        "EV:193",
        "EH:194",
        "EV:194",
        "EH:195",
        "EV:195",
        "EH:196",
        "EV:196",
        "EH:197",
        "EV:197",
        "EH:198",
        "EV:198",
        "EH:199",
        "EV:199",
        "EH:19A",
        "EV:19A",
        "EH:19B",
        "EV:19B",
        "EH:19C",
        "EV:19C",
        "EH:19D",
        "EV:19D",
        // 0.0.104 collection-entry diagnostics. These are emitted before the older
        // 0x1820+ markers so a timeout immediately after EH:B4 is attributable.
        "EH:18B",
        "EV:18B",
        "EH:18C",
        "EV:18C",
        // 0.0.107 List<T>.Enumerator return-boundary diagnostics emitted from CoreLib.
        "EH:18D",
        "EV:18D",
        // 0.0.109 Dictionary<TKey,TValue>.Enumerator return-boundary diagnostics
        // plus collision-free collection throw breadcrumbs.
        "EH:182",
        "EV:182",
        "EH:183",
        "EV:183",
        "EH:184",
        "EV:184",
        "EH:185",
        "EV:185",
        "EH:186",
        "EV:186",
        "EH:187",
        "EV:187",
        "EH:18E",
        "EV:18E",
        "EH:18F",
        "EV:18F",
        // 0.0.136 shared generic activation/default-constructor diagnostics.
        "EH:1A",
        "EV:1A",
        "EH:B4",
        "EH:B5",
        "GC conformance passed/failed:",
        "NOBT:GC:ROOTMAP",
        "NOBT:GC:RUN",
        "NOBT:GC:OK"
    ];

    Console.Error.WriteLine("[INFO] QEMU serial diagnostic summary follows:");
    using StringReader reader = new(serialText);
    string? line;
    while ((line = reader.ReadLine()) != null)
    {
        string trimmed = line.Trim();
        foreach (string prefix in interestingPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(trimmed);
                break;
            }
        }
    }
    PrintManagedConformanceDiagnosticInterpretation(serialText);
    Console.Error.WriteLine("[INFO] End QEMU serial diagnostic summary.");
}

static void PrintManagedConformanceDiagnosticInterpretation(string serialText)
{
    // Keep this host-side map deliberately small and stable. The raw EH/EV lines above remain
    // authoritative; this adds an immediate description of the furthest managed-conformance milestone reached.
    (string Marker, string Description)[] stages =
    [
        ("EH:1880", "generic runtime checks entered"),
        ("EH:188A", "generic delegate checks completed"),
        ("EH:188B", "class-GVM target allocated"),
        ("EH:188C", "exact Int32 class-GVM call entered"),
        ("EV:188D=", "exact Int32 class-GVM call returned"),
        ("EH:188E", "shared-reference class-GVM call entered"),
        ("EV:188F=", "shared-reference class-GVM call returned"),
        ("EH:1890", "both class-GVM calls completed"),
        ("EH:1891", "interface-GVM conformance entered"),
        ("EH:189A", "direct interface-GVM implementation body entered"),
        ("EH:189B", "inherited interface-GVM override body entered"),
        ("EH:189C", "variance-compatible interface-GVM implementation body entered"),
        ("EH:189D", "shared GVM Method/MethodDictionary cell target body entered"),
        ("EH:189E", "shared GVM AllocateObject/InterfaceCall cell target body entered"),
        ("EH:189F", "shared GVM non-generic constrained-call target body entered"),
        ("EH:18A0", "shared GVM generic-constrained-call target body entered"),
        ("EH:18A1", "shared GVM default-constructor dictionary target body entered"),
        ("EH:18A2", "shared GVM GC-static dictionary target body entered"),
        ("EH:18A3", "shared GVM non-GC-static dictionary target body entered"),
        ("EH:18A4", "exact closed generic static bases materialisation entered"),
        ("EH:18A5", "exact closed generic static bases materialisation completed"),
        ("EH:18A6", "shared GVM SZ-array TypeHandle target body entered"),
        ("EH:18A7", "shared GVM multidimensional-array TypeHandle target body entered"),
        ("EH:18A8", "shared GVM pointer TypeHandle target body entered"),
        ("EH:18A9", "shared GVM function-pointer TypeHandle target body entered"),
        ("EH:1A00", "Activator.CreateInstance<T> entered"),
        ("EV:1A01=", "Activator requested MethodTable"),
        ("EV:1A02=", "Activator default-constructor pointer obtained"),
        ("EV:1A03=", "Activator missing-constructor marker obtained"),
        ("EV:1A04=", "Activator value-type branch classification"),
        ("EH:1A05", "Activator value-type constructor call entered"),
        ("EH:1A06", "Activator value-type constructor call returned"),
        ("EH:1A07", "Activator value-type CreateInstance returning"),
        ("EH:1A08", "Activator detected missing default constructor"),
        ("EH:1A09", "Activator reference-type constructor validated"),
        ("EV:1A0A=", "Activator allocator pointer obtained"),
        ("EH:1A0B", "Activator allocator call entered"),
        ("EV:1A0C=", "Activator allocation MethodTable argument"),
        ("EH:1A0D", "Activator allocator call returned"),
        ("EH:1A0E", "Activator constructor call entered"),
        ("EH:1A0F", "Activator constructor call returned"),
        ("EH:1A20", "dictionary allocator wrapper entered"),
        ("EV:1A21=", "dictionary allocator MethodTable argument"),
        ("EV:1A22=", "dictionary allocator returned object pointer"),
        ("EH:1A23", "dictionary allocator wrapper returning"),
        ("EH:1A24", "dictionary finalizable allocator wrapper entered"),
        ("EV:1A25=", "dictionary finalizable allocator MethodTable argument"),
        ("EV:1A26=", "dictionary finalizable allocator returned object pointer"),
        ("EH:1A27", "dictionary finalizable allocator wrapper returning"),
        ("EH:1A30", "DefaultConstructor InvokeMap lookup entered"),
        ("EV:1A31=", "DefaultConstructor requested type"),
        ("EV:1A32=", "DefaultConstructor lookup hash"),
        ("EV:1A33=", "DefaultConstructor keyed InvokeMap candidate flags"),
        ("EV:1A34=", "DefaultConstructor candidate type"),
        ("EV:1A35=", "DefaultConstructor candidate canonical compatibility"),
        ("EV:1A36=", "DefaultConstructor candidate entry point"),
        ("EH:1A37", "DefaultConstructor keyed lookup missed; full diagnostic scan entered"),
        ("EV:1A38=", "DefaultConstructor missing-marker fallback pointer"),
        ("EH:1A39", "DefaultConstructor resolved through keyed InvokeMap lookup"),
        ("EH:1A3A", "DefaultConstructor resolved through full InvokeMap fallback scan"),
        ("EH:1A3B", "DefaultConstructor not present in InvokeMap"),
        ("EV:1A3C=", "DefaultConstructor loaded-module count"),
        ("EV:1A3D=", "DefaultConstructor module index"),
        ("EV:1A3E=", "DefaultConstructor module has InvokeMap"),
        ("EV:1A3F=", "DefaultConstructor CommonFixups initialised"),
        ("EV:1A42=", "DefaultConstructor keyed candidate method cookie"),
        ("EV:1A43=", "DefaultConstructor keyed candidate count"),
        ("EV:1A44=", "DefaultConstructor raw InvokeMap candidate flags"),
        ("EV:1A45=", "DefaultConstructor raw InvokeMap method cookie"),
        ("EV:1A46=", "DefaultConstructor raw InvokeMap candidate type"),
        ("EV:1A47=", "DefaultConstructor raw candidate canonical compatibility"),
        ("EV:1A48=", "DefaultConstructor total InvokeMap entries scanned"),
        ("EH:1A40", "DefaultConstructorProbe constructor body entered"),
        ("EH:1A41", "DefaultConstructorProbe constructor body completed"),
        ("EV:1892=", "exact Int32 interface-GVM call returned"),
        ("EV:1893=", "shared-reference interface-GVM call returned"),
        ("EV:1898=", "inherited class-override interface-GVM call returned"),
        ("EV:1894=", "variance-compatible interface-GVM call returned"),
        ("EH:1896", "interface-GVM and dynamic-dictionary conformance completed"),
        ("EH:1897", "generic RuntimeHelpers conformance completed"),
        ("EH:B4", "collection conformance entered"),
        ("EH:18B0", "collection runtime checks entered"),
        ("EH:18B1", "List<Int32> construction entered"),
        ("EH:18B2", "List<Int32> constructed"),
        ("EH:18B3", "first List<Int32>.Add completed"),
        ("EV:18B4=", "initial List<Int32> population completed"),
        ("EV:18B5=", "initial List<Int32> capacity observed"),
        ("EH:18B6", "initial List<Int32> indexing checks completed"),
        ("EH:18B7", "List<Int32>.Insert completed"),
        ("EH:18B8", "List<Int32> Contains/IndexOf checks completed"),
        ("EH:18B9", "List<Int32>.Remove completed"),
        ("EH:18BA", "List<Int32>.RemoveAt completed"),
        ("EH:18BB", "List<Int32> CopyTo destination allocated"),
        ("EH:18BC", "List<Int32>.CopyTo completed"),
        ("EH:18BD", "List<Int32>.ToArray completed"),
        ("EH:18BE", "direct List<Int32> enumerator acquisition entered"),
        ("EH:18D0", "List<T>.GetEnumerator managed body entered"),
        ("EV:18D1=", "List<T>.GetEnumerator observed source Count"),
        ("EV:18D2=", "List<T>.GetEnumerator observed source version"),
        ("EV:18D3=", "List<T>.GetEnumerator observed source Capacity"),
        ("EH:18D4", "List<T>.Enumerator constructor entered"),
        ("EH:18D5", "List<T>.Enumerator list reference assigned"),
        ("EV:18D6=", "List<T>.Enumerator captured version"),
        ("EV:18D7=", "List<T>.Enumerator constructor observed source Count"),
        ("EV:18D8=", "List<T>.Enumerator constructor observed source Capacity"),
        ("EH:18D9", "List<T>.Enumerator constructor reached return boundary"),
        ("EH:18BF", "direct List<Int32> enumerator acquired by caller"),
        ("EH:18C0", "direct List<Int32> enumerator first MoveNext completed"),
        ("EV:18C1=", "direct List<Int32> enumeration completed"),
        ("EV:18C2=", "direct List<Int32> enumeration sum computed"),
        ("EH:18C3", "List<Probe> construction entered"),
        ("EH:18C4", "List<Probe> constructed"),
        ("EH:18C5", "shared-reference List<Probe> enumeration checks completed"),
        ("EH:1820", "List<Int32> invalidation checks entered"),
        ("EH:1823", "List<Int32> invalidation interface source prepared"),
        ("EH:1824", "List<Int32> invalidation interface enumerator acquired"),
        ("EH:1825", "List<Int32> invalidation first MoveNext entered"),
        ("EV:1826=", "List<Int32> invalidation first MoveNext returned"),
        ("EV:1827=", "List<Int32> invalidation first Current observed"),
        ("EH:1828", "List<Int32> invalidation first element verified"),
        ("EH:1829", "List<Int32> mutation before invalidation entered"),
        ("EV:182A=", "List<Int32> count after invalidating mutation"),
        ("EH:182B", "List<Int32> invalidating mutation completed"),
        ("EH:182C", "List<Int32> invalidation exception probe entered"),
        ("EH:1832", "collection invalidation EH helper entered"),
        ("EH:18F0", "collection modified throw emitted"),
        ("EH:1834", "collection invalidation InvalidOperationException caught"),
        ("EV:182D=", "collection invalidation throw result observed"),
        ("EH:182E", "collection invalidation throw confirmed"),
        ("EH:182F", "collection invalidation disposal entered"),
        ("EH:1830", "collection invalidation enumerator disposed"),
        ("EV:1831=", "collection invalidation final result observed"),
        ("EH:1840", "Dictionary<Int32,Int32> construction entered"),
        ("EH:1841", "Dictionary<Int32,Int32> constructed"),
        ("EV:1842=", "Dictionary<Int32,Int32> population progress"),
        ("EV:1843=", "Dictionary<Int32,Int32> population completed"),
        ("EH:1844", "Dictionary<Int32,Int32>.TryGetValue entered"),
        ("EV:1845=", "Dictionary<Int32,Int32>.TryGetValue result"),
        ("EV:1846=", "Dictionary<Int32,Int32>.TryGetValue value"),
        ("EH:1847", "Dictionary<Int32,Int32> mutation/removal checks completed"),
        ("EH:184A", "direct Dictionary<Int32,Int32> enumerator acquisition entered"),
        ("EH:18E0", "Dictionary<TKey,TValue>.GetEnumerator managed body entered"),
        ("EV:18E1=", "Dictionary<TKey,TValue>.GetEnumerator observed Count"),
        ("EV:18E2=", "Dictionary<TKey,TValue>.GetEnumerator observed used-entry count"),
        ("EV:18E3=", "Dictionary<TKey,TValue>.GetEnumerator observed version"),
        ("EV:18E4=", "Dictionary<TKey,TValue>.GetEnumerator observed entry capacity"),
        ("EV:18E5=", "Dictionary<TKey,TValue>.GetEnumerator observed bucket capacity"),
        ("EH:18E6", "Dictionary<TKey,TValue>.Enumerator constructor entered"),
        ("EH:18E7", "Dictionary<TKey,TValue>.Enumerator dictionary reference assigned"),
        ("EV:18E8=", "Dictionary<TKey,TValue>.Enumerator captured version"),
        ("EH:18E9", "Dictionary<TKey,TValue>.Enumerator index initialised"),
        ("EH:18EA", "Dictionary<TKey,TValue>.Enumerator current-value initialisation entered"),
        ("EH:18EB", "Dictionary<TKey,TValue>.Enumerator current-value initialisation completed"),
        ("EV:18EC=", "Dictionary<TKey,TValue>.Enumerator constructor observed Count"),
        ("EV:18ED=", "Dictionary<TKey,TValue>.Enumerator constructor observed used-entry count"),
        ("EH:18EE", "Dictionary<TKey,TValue>.Enumerator constructor reached return boundary"),
        ("EH:184B", "direct Dictionary<Int32,Int32> enumerator acquired by caller"),
        ("EH:184C", "direct Dictionary<Int32,Int32> enumeration loop entered"),
        ("EV:184D=", "direct Dictionary<Int32,Int32> enumeration iteration index"),
        ("EV:184E=", "direct Dictionary<Int32,Int32>.MoveNext result"),
        ("EV:184F=", "direct Dictionary<Int32,Int32> current key observed"),
        ("EV:1848=", "direct Dictionary<Int32,Int32> enumeration count completed"),
        ("EV:1849=", "direct Dictionary<Int32,Int32> key sum completed"),
        ("EH:18F1", "duplicate dictionary key throw emitted"),
        ("EH:18F2", "dictionary key-not-found throw emitted"),
        ("EH:1850", "Dictionary<String,Probe> construction entered"),
        ("EH:1860", "Queue<Int32> checks entered"),
        ("EH:1870", "Stack<Probe> checks entered"),
        ("EH:B5", "collection conformance completed"),
        ("EH:1900", "TypeLoaderExports.GVMLookupForSlot entered"),
        ("EH:1904", "GVM cache hit"),
        ("EH:1906", "GVM cache miss; resolver entered"),
        ("EH:1908", "GVM result cached and returned"),
        ("EH:1910", "TypeLoader GVM resolution entered"),
        ("EH:191C", "exact generic method pointer selected"),
        ("EH:1917", "shared generic fat function pointer created"),
        ("EH:1918", "GVM template lookup failed"),
        ("EH:1919", "GVM dictionary lookup failed"),
        ("EH:191E", "class-GVM implementation lookup failed"),
        ("EH:1920", "class-GVM metadata lookup entered"),
        ("EH:192E", "class-GVM implementation metadata matched"),
        ("EH:192F", "class-GVM implementation metadata not found"),
        ("EH:1930", "exact-instantiation lookup entered"),
        ("EH:193A", "exact-instantiation function pointer found"),
        ("EH:193F", "exact-instantiation function pointer not found"),
        ("EH:1940", "shared generic template lookup entered"),
        ("EV:19D0=", "canonical-template candidate matched owner/token/arity"),
        ("EV:19D1=", "canonical-template argument compatibility score"),
        ("EV:19D2=", "canonical-template encoded generic argument"),
        ("EV:19D3=", "requested concrete generic argument"),
        ("EV:19D4=", "canonical-template candidate selected"),
        ("EV:19D5=", "selected canonical-template method-entry offset"),
        ("EV:19D6=", "selected canonical-template method flags"),
        ("EH:19DE", "no ABI-compatible canonical template found"),
        ("EH:19DF", "ABI-compatible canonical template selected"),
        ("EH:194D", "shared generic template function pointer found"),
        ("EH:194F", "shared generic template not found"),
        ("EH:1950", "generic method dictionary lookup entered"),
        ("EH:195A", "static generic method dictionary found"),
        ("EH:195F", "static generic method dictionary not found"),
        ("EH:19B0", ".NET 10 dynamic GenericMethodDictionary materialisation entered"),
        ("EV:19B1=", "generic method template NativeLayout token selected"),
        ("EV:19B2=", "dynamic GenericMethodDictionary cell count decoded"),
        ("EV:19B3=", "dynamic GenericMethodDictionary fixup kind decoded"),
        ("EV:19B4=", "unsupported dynamic GenericMethodDictionary fixup kind"),
        ("EV:19B5=", "dynamic GenericMethodDictionary cell materialised"),
        ("EV:19B6=", "dynamic GenericMethodDictionary first-cell pointer"),
        ("EV:19B7=", "dynamic dictionary resolved type/constraint operand"),
        ("EV:19B8=", "dynamic dictionary resolved slot operand"),
        ("EV:19B9=", "dynamic dictionary resolved method declaring type"),
        ("EV:19BA=", "dynamic dictionary resolved method token"),
        ("EV:19BB=", "dynamic MethodLdToken generic argument count"),
        ("EV:19BC=", "dynamic StaticData kind or FieldLdToken metadata handle"),
        ("EV:19BD=", "dynamic default-constructor or thread-static pointer"),
        ("EV:1A50=", "StaticData requested constructed type"),
        ("EV:1A52=", "StaticData type-manager handle"),
        ("EV:1A56=", "StaticData lookup hash"),
        ("EV:1A57=", "StaticData StaticsInfo candidate type"),
        ("EV:1A58=", "StaticData StaticsInfo candidate count"),
        ("EV:1A5A=", "StaticData kind"),
        ("EV:1A5E=", "StaticData NativeStatics index"),
        ("EV:1A5F=", "StaticData resolved base pointer"),
        ("EH:1A51", "StaticData type handle was null"),
        ("EH:1A53", "StaticData TypeManager was unavailable"),
        ("EH:1A54", "StaticData StaticsInfoHashtable was unavailable"),
        ("EH:1A55", "StaticData NativeReferences/NativeStatics tables were unavailable"),
        ("EH:1A59", "StaticData exact type was absent from StaticsInfoHashtable"),
        ("EH:1A5B", "StaticData had no statics-info entry"),
        ("EH:1A5C", "StaticData kind was invalid"),
        ("EH:1A5D", "StaticData requested bag entry was absent"),
        ("EH:19BE", "dynamic GenericMethodDictionary materialisation failed"),
        ("EH:19BF", "dynamic GenericMethodDictionary materialisation completed"),
        ("EH:19C0", "interface-GVM metadata lookup entered"),
        ("EV:19C1=", "interface-GVM concrete implementation type resolved"),
        ("EV:19C2=", "interface-GVM implementation method token resolved"),
        ("EH:19CD", "interface-GVM implementation metadata matched"),
        ("EH:19CE", "interface-GVM default-method diamond detected"),
        ("EH:19CF", "interface-GVM implementation metadata not found or reabstracted"),
        ("EH:1970", "NativeAOT reflection-map blob lookup entered"),
        ("EV:1972=", "ReadyToRun reflection-map section ID selected"),
        ("EV:1973=", "RhGetModuleSection returned blob address"),
        ("EV:1974=", "RhGetModuleSection returned blob length"),
        ("EH:1975", "reflection-map blob section was absent/invalid"),
        ("EH:1976", "reflection-map blob section loaded"),
        ("EH:1980", "NativeHashtable load entered"),
        ("EV:1985=", "NativeHashtable header read"),
        ("EV:1986=", "NativeHashtable bucket shift decoded"),
        ("EV:1987=", "NativeHashtable entry-index size decoded"),
        ("EH:1988", "allocation-free NativeReader value initialized"),
        ("EH:1989", "NativeParser initialized"),
        ("EH:198A", "NativeHashtable constructed"),
        ("EH:198E", "NativeHashtable header rejected"),
        ("EH:19A0", "class-GVM bucket-bound lookup entered"),
        ("EV:19A1=", "class-GVM hashtable bucket selected"),
        ("EV:19A2=", "class-GVM bucket start offset decoded"),
        ("EV:19A3=", "class-GVM bucket end offset decoded"),
        ("EH:19A4", "class-GVM bucket enumerator constructed")
    ];

    int furthestPosition = -1;
    string? furthestMarker = null;
    string? furthestDescription = null;
    foreach ((string marker, string description) in stages)
    {
        int position = serialText.LastIndexOf(marker, StringComparison.Ordinal);
        if (position <= furthestPosition) continue;
        furthestPosition = position;
        furthestMarker = marker.EndsWith("=", StringComparison.Ordinal) ? marker[..^1] : marker;
        furthestDescription = description;
    }

    if (furthestPosition >= 0)
        Console.Error.WriteLine($"[INFO] Managed conformance last stage: {furthestMarker} - {furthestDescription}.");
}

static void PrintSerialTail(string serialText)
{
    if (string.IsNullOrEmpty(serialText))
    {
        Console.Error.WriteLine("[INFO] QEMU serial log is empty.");
        return;
    }
    const int maximumTailCharacters = 4096;
    string tail = serialText.Length <= maximumTailCharacters ? serialText : serialText[^maximumTailCharacters..];
    Console.Error.WriteLine("[INFO] QEMU serial tail follows:");
    Console.Error.WriteLine(tail);
    Console.Error.WriteLine("[INFO] End QEMU serial tail.");
}

static string ReadSharedText(string path)
{
    if (!File.Exists(path)) return string.Empty;
    try
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
    catch (IOException)
    {
        return string.Empty;
    }
}

static bool IsProcessAlive(int processId)
{
    if (processId <= 0) return false;
    try
    {
        using Process process = Process.GetProcessById(processId);
        return !process.HasExited;
    }
    catch (ArgumentException)
    {
        return false;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return false;
    }
}

static bool TryStop(int processId)
{
    if (processId <= 0) return true;
    try
    {
        using Process process = Process.GetProcessById(processId);
        if (!process.HasExited) process.Kill(true);
        return true;
    }
    catch (ArgumentException)
    {
        return true;
    }
    catch
    {
        return false;
    }
}

static string EscapeDriveValue(string value) => value.Replace(",", ",,", StringComparison.Ordinal);
static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
static bool HasOption(ReadOnlySpan<string> args, string option)
{
    foreach (string value in args) if (string.Equals(value, option, StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}
static List<string> GetOptions(string[] args, string name)
{
    List<string> values = new();
    for (int index = 0; index + 1 < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) values.Add(args[index + 1]);
    }
    return values;
}
static string? GetOption(ReadOnlySpan<string> args, string name)
{
    for (int index = 0; index + 1 < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[index + 1];
        }
    }
    return null;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"[FAIL] {message}");
    return 1;
}
