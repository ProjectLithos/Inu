using System.Diagnostics;
using System.Text.Json;

namespace Inu.Cli;

internal static class Program
{
    private static readonly string Version = GetProductVersion();

    private static string GetProductVersion()
    {
        System.Version? assemblyVersion = typeof(Program).Assembly.GetName().Version;
        return assemblyVersion is null
            ? "0.0.0"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                PrintHelp();
                return 0;
            }

            if (args[0] is "-v" or "--version" or "version")
            {
                Console.WriteLine(Version);
                return 0;
            }

            string root = ResolveRoot(args);
            string command = args[0].ToLowerInvariant();
            string[] tail = args.Skip(1).ToArray();

            switch (command)
            {
                case "build":
                    Build(root, args.Contains("--force-rebuild", StringComparer.OrdinalIgnoreCase));
                    return 0;
                case "toolchain" when tail.Length > 0 && tail[0].Equals("ensure", StringComparison.OrdinalIgnoreCase):
                    ToolchainEnsure(root);
                    return 0;
                case "os":
                    return RunOperatingSystemCommand(root, tail);

                // One host entry point for every SDK host-side operation.  The implementation
                // modules remain separately maintainable inside SDK/src, but Kath and SDK build
                // scripts invoke only inu.exe.  Users therefore have one stable host contract.
                case "project":
                    return RunHostModule(root, "Inu.ProjectCreator", tail);
                case "compile":
                    return RunHostModule(root, "Inu.ManagedCompiler", Prepend("compile", tail));
                case "link":
                    return RunHostModule(root, "Inu.Linker", Prepend("link", tail));
                case "image":
                    return RunHostModule(root, "Inu.ImageBuilder", tail);
                case "userland":
                    return RunHostModule(root, "Inu.UserlandCompiler", Prepend("compile", tail));
                case "qemu":
                    return RunHostModule(root, "Inu.QemuLauncher", tail);
                default:
                    throw new ArgumentException($"Unknown Inu command: {args[0]}", nameof(args));
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[FAIL] {exception.Message}");
            return 1;
        }
    }

    private static int RunOperatingSystemCommand(string inuRoot, string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Usage: inu os build|run <InuProject.json> [--configuration Debug|Release] [--force-rebuild] [--no-run]");
        string action = args[0].ToLowerInvariant();
        if (action is not ("build" or "run")) throw new ArgumentException("inu os supports only build or run.");
        string project = Path.GetFullPath(args[1]);
        string configuration = "Release";
        bool forceRebuild = false;
        bool noRun = false;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--configuration", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new ArgumentException("--configuration requires Debug or Release.");
                configuration = args[++i];
                if (configuration is not ("Debug" or "Release")) throw new ArgumentException("--configuration must be Debug or Release.");
            }
            else if (args[i].Equals("--force-rebuild", StringComparison.OrdinalIgnoreCase)) forceRebuild = true;
            else if (args[i].Equals("--no-run", StringComparison.OrdinalIgnoreCase)) noRun = true;
            else if (args[i].Equals("--root", StringComparison.OrdinalIgnoreCase)) i++;
            else throw new ArgumentException($"Unknown inu os option: {args[i]}");
        }

        string buildScript = Path.Combine(inuRoot, "SDK", "Build-Inu.ps1");
        if (!File.Exists(buildScript)) throw new FileNotFoundException("Inu SDK build script is missing.", buildScript);
        if (!File.Exists(project)) throw new FileNotFoundException("Inu project manifest is missing.", project);

        List<string> ps = new()
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", buildScript,
            "-Project", project,
            "-Configuration", configuration
        };
        if (forceRebuild) ps.Add("-ForceRebuild");
        if (noRun || action == "build") ps.Add("-NoRun");
        else ps.Add("-Run");
        return RunProcess("powershell.exe", ps, Path.GetDirectoryName(project) ?? inuRoot);
    }

    private static string[] Prepend(string value, string[] args)
    {
        string[] result = new string[args.Length + 1];
        result[0] = value;
        Array.Copy(args, 0, result, 1, args.Length);
        return result;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Inu " + Version);
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  inu build [--root <path>] [--force-rebuild]");
        Console.WriteLine("  inu os build <InuProject.json> [--configuration Debug|Release] [--force-rebuild]");
        Console.WriteLine("  inu os run <InuProject.json> [--configuration Debug|Release] [--force-rebuild]");
        Console.WriteLine("  inu os build <InuProject.json> --no-run   (build only; used by Kath Debug)");
        Console.WriteLine("  inu project create [options]");
        Console.WriteLine("  inu compile <InuProject.json> [options]");
        Console.WriteLine("  inu link <InuProject.json> [options]");
        Console.WriteLine("  inu image create <InuProject.json> [options]");
        Console.WriteLine("  inu userland <InuProject.json> [options]");
        Console.WriteLine("  inu qemu run <InuProject.json> [options]");
        Console.WriteLine("  inu toolchain ensure [--root <path>]");
        Console.WriteLine("  inu version        (aliases: --version, -v)");
        Console.WriteLine("  inu help           (aliases: --help, -h)");
        Console.WriteLine();
        Console.WriteLine("Host module selection:");
        Console.WriteLine("  --host-configuration Debug|Release  (default: Release)");
    }

    private static string ResolveRoot(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i].Equals("--root", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(args[i + 1]);

        string? env = Environment.GetEnvironmentVariable("INU_ROOT");
        if (!string.IsNullOrWhiteSpace(env))
            return Path.GetFullPath(env);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        if (dir.Name.Equals("Bin", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
            return dir.Parent.FullName;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
    }

    private static int RunHostModule(string inuRoot, string moduleName, string[] originalArgs)
    {
        string configuration = "Release";
        List<string> forwarded = new();
        for (int i = 0; i < originalArgs.Length; i++)
        {
            if (originalArgs[i].Equals("--host-configuration", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= originalArgs.Length) throw new ArgumentException("--host-configuration requires Debug or Release.");
                configuration = originalArgs[++i];
                if (configuration is not ("Debug" or "Release")) throw new ArgumentException("--host-configuration must be Debug or Release.");
                continue;
            }
            if (originalArgs[i].Equals("--root", StringComparison.OrdinalIgnoreCase))
            {
                i++; // root is a host-only option and is not forwarded to SDK modules.
                continue;
            }
            forwarded.Add(originalArgs[i]);
        }

        string sdkRoot = Path.Combine(inuRoot, "SDK");
        string dotnet = Path.Combine(inuRoot, ".toolchain", "DotNet", "dotnet.exe");
        string module = Path.Combine(sdkRoot, "src", moduleName, "bin", "x64", configuration, "net10.0", moduleName + ".dll");
        if (!File.Exists(dotnet)) throw new FileNotFoundException("Inu private .NET host is missing.", dotnet);
        if (!File.Exists(module)) throw new FileNotFoundException($"Inu host module '{moduleName}' has not been built for {configuration}.", module);

        return RunProcess(dotnet, new[] { module }.Concat(forwarded), sdkRoot);
    }

    private static int RunProcess(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static void ToolchainEnsure(string root)
    {
        string dotnet = Path.Combine(root, ".toolchain", "DotNet", "dotnet.exe");
        string clang = Path.Combine(root, ".toolchain", "LLVM", "bin", "clang.exe");
        string linker = Path.Combine(root, ".toolchain", "LLVM", "bin", "lld-link.exe");

        foreach (string path in new[] { dotnet, clang, linker })
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required private tool is missing.", path);
            Console.WriteLine($"[ OK ] {path}");
        }
    }

    private static void Build(string root, bool forceRebuild)
    {
        Console.WriteLine($"[INFO] Inu {Version}");
        Console.WriteLine($"[INFO] Root: {root}");

        foreach (string relative in new[]
        {
            Path.Combine("src", "Common", "Inu.Cli"),
            Path.Combine("src", "Language", "Input"),
            Path.Combine("src", "Language", "Output"),
            "targets",
            "toolchains"
        })
        {
            string path = Path.Combine(root, relative);
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Required Inu source directory is missing: {path}");
            Console.WriteLine($"[ OK ] {relative}");
        }

        string? dclgRoot = Directory.GetParent(root)?.FullName;
        if (string.IsNullOrWhiteSpace(dclgRoot)) throw new InvalidOperationException("Inu must be installed under the Kath&Inu root beside Kath.");
        string kathRoot = Path.Combine(dclgRoot, "Kath");
        string kathBuild = Path.Combine(kathRoot, "Build-Kath.ps1");
        string kathLauncherProject = Path.Combine(kathRoot, "src", "Kath.Launcher", "Kath.Launcher.csproj");
        if (!File.Exists(kathBuild) || !File.Exists(kathLauncherProject)) throw new DirectoryNotFoundException($"The complete Kath source is missing from: {kathRoot}");

        ToolchainEnsure(root);

        string kathBin = Path.Combine(kathRoot, "Bin");
        Directory.CreateDirectory(kathBin);
        string stableKath = Path.Combine(kathBin, "Kath.exe");
        Console.WriteLine("[INFO] Checking Kath build stages...");
        Run("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{kathBuild}\"" + (forceRebuild ? " -ForceRebuild" : ""), kathRoot);

        string electronMain = Path.Combine(kathRoot, "applications", "electron", "lib", "backend", "electron-main.js");
        if (!File.Exists(electronMain)) throw new FileNotFoundException("Kath build reported success but the Electron backend was not produced.", electronMain);
        if (!File.Exists(stableKath)) throw new FileNotFoundException("Kath build did not produce the stable launcher.", stableKath);

        string? kathFileVersion = FileVersionInfo.GetVersionInfo(stableKath).FileVersion;
        string expectedKathFileVersion = Version + ".0";
        if (!string.Equals(kathFileVersion, expectedKathFileVersion, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(stableKath);
            throw new InvalidOperationException($"Kath launcher version mismatch. Expected {expectedKathFileVersion}, got {kathFileVersion ?? "<none>"}.");
        }

        Console.WriteLine($"[ OK ] Kath application: {electronMain}");
        Console.WriteLine($"[ OK ] Kath launcher: {stableKath} ({kathFileVersion})");
        Console.WriteLine("[ OK ] Inu and Kath build completed.");
    }

    private static void Run(string fileName, string arguments, string workingDirectory)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"Process '{fileName}' failed with exit code {process.ExitCode}.");
    }
}
