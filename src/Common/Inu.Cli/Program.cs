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


        return args[0].ToLowerInvariant() switch
        {
            "build" => Build(root, args.Contains("--force-rebuild", StringComparer.OrdinalIgnoreCase)),
            "toolchain" when args.Length > 1 &&
                args[1].Equals("ensure", StringComparison.OrdinalIgnoreCase)
                => ToolchainEnsure(root),
            _ => Unknown(args[0])
        };
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Inu " + Version);
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  inu build [--root <path>] [--force-rebuild]");
        Console.WriteLine("  inu toolchain ensure [--root <path>]");
        Console.WriteLine("  inu --version");
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"[FAIL] Unknown Inu command: {command}");
        return 2;
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

    private static int ToolchainEnsure(string root)
    {
        string dotnet = Path.Combine(root, ".toolchain", "DotNet", "dotnet.exe");
        string clang = Path.Combine(root, ".toolchain", "LLVM", "bin", "clang.exe");
        string linker = Path.Combine(root, ".toolchain", "LLVM", "bin", "lld-link.exe");

        bool ok = true;
        foreach (string path in new[] { dotnet, clang, linker })
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[FAIL] Required private tool is missing: {path}");
                ok = false;
            }
            else
            {
                Console.WriteLine($"[ OK ] {path}");
            }
        }

        return ok ? 0 : 1;
    }

    private static int Build(string root, bool forceRebuild)
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
            if (!Directory.Exists(path))
            {
                Console.Error.WriteLine($"[FAIL] Required Inu source directory is missing: {path}");
                return 1;
            }
            Console.WriteLine($"[ OK ] {relative}");
        }

        string? dclgRoot = Directory.GetParent(root)?.FullName;
        if (string.IsNullOrWhiteSpace(dclgRoot))
        {
            Console.Error.WriteLine("[FAIL] Inu must be installed under the Kath&Inu root beside Kath.");
            return 1;
        }
        string kathRoot = Path.Combine(dclgRoot, "Kath");
        string kathBuild = Path.Combine(kathRoot, "Build-Kath.ps1");
        string kathLauncherProject = Path.Combine(kathRoot, "src", "Kath.Launcher", "Kath.Launcher.csproj");
        if (!File.Exists(kathBuild) || !File.Exists(kathLauncherProject))
        {
            Console.Error.WriteLine($"[FAIL] The complete Kath source is missing from: {kathRoot}");
            return 1;
        }

        int tools = ToolchainEnsure(root);
        if (tools != 0)
            return tools;

        string dotnet = Path.Combine(root, ".toolchain", "DotNet", "dotnet.exe");
        string bin = Path.Combine(root, "Bin");
        Directory.CreateDirectory(bin);

        string kathBin = Path.Combine(kathRoot, "Bin");
        Directory.CreateDirectory(kathBin);
        string stableKath = Path.Combine(kathBin, "Kath.exe");
        Console.WriteLine("[INFO] Checking Kath build stages...");
        int result = Run("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{kathBuild}\"" + (forceRebuild ? " -ForceRebuild" : ""), kathRoot);
        if (result != 0)
            return result;

        string electronMain = Path.Combine(kathRoot, "applications", "electron", "lib", "backend", "electron-main.js");
        if (!File.Exists(electronMain))
        {
            Console.Error.WriteLine("[FAIL] Kath build reported success but the Electron backend was not produced.");
            return 1;
        }

        if (!File.Exists(stableKath))
        {
            Console.Error.WriteLine("[FAIL] Kath build did not produce the stable launcher.");
            return 1;
        }

        string? kathFileVersion = FileVersionInfo.GetVersionInfo(stableKath).FileVersion;
        string expectedKathFileVersion = Version + ".0";
        if (!string.Equals(kathFileVersion, expectedKathFileVersion, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"[FAIL] Kath launcher version mismatch. Expected {expectedKathFileVersion}, got {kathFileVersion ?? "<none>"}.");
            File.Delete(stableKath);
            return 1;
        }

        Console.WriteLine($"[ OK ] Kath application: {electronMain}");
        Console.WriteLine($"[ OK ] Kath launcher: {stableKath} ({kathFileVersion})");
        Console.WriteLine("[ OK ] Inu and Kath build completed.");
        return 0;
    }

    private static int Run(string fileName, string arguments, string workingDirectory)
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
        return process.ExitCode;
    }
}
