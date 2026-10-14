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


        switch (args[0].ToLowerInvariant())
        {
            case "build":
                Build(root, args.Contains("--force-rebuild", StringComparer.OrdinalIgnoreCase));
                return 0;
            case "toolchain" when args.Length > 1 && args[1].Equals("ensure", StringComparison.OrdinalIgnoreCase):
                ToolchainEnsure(root);
                return 0;
            default:
                throw new ArgumentException($"Unknown Inu command: {args[0]}", nameof(args));
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Inu " + Version);
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  inu build [--root <path>] [--force-rebuild]");
        Console.WriteLine("  inu toolchain ensure [--root <path>]");
        Console.WriteLine("  inu version        (aliases: --version, -v)");
        Console.WriteLine("  inu help           (aliases: --help, -h)");
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

    private static void ToolchainEnsure(string root)
    {
        string dotnet = Path.Combine(root, ".toolchain", "DotNet", "dotnet.exe");
        string clang = Path.Combine(root, ".toolchain", "LLVM", "bin", "clang.exe");
        string linker = Path.Combine(root, ".toolchain", "LLVM", "bin", "lld-link.exe");

        foreach (string path in new[] { dotnet, clang, linker })
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Required private tool is missing.", path);
            }
            else
            {
                Console.WriteLine($"[ OK ] {path}");
            }
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
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Required Inu source directory is missing: {path}");
            }
            Console.WriteLine($"[ OK ] {relative}");
        }

        string? dclgRoot = Directory.GetParent(root)?.FullName;
        if (string.IsNullOrWhiteSpace(dclgRoot))
        {
            throw new InvalidOperationException("Inu must be installed under the Kath&Inu root beside Kath.");
        }
        string kathRoot = Path.Combine(dclgRoot, "Kath");
        string kathBuild = Path.Combine(kathRoot, "Build-Kath.ps1");
        string kathLauncherProject = Path.Combine(kathRoot, "src", "Kath.Launcher", "Kath.Launcher.csproj");
        if (!File.Exists(kathBuild) || !File.Exists(kathLauncherProject))
        {
            throw new DirectoryNotFoundException($"The complete Kath source is missing from: {kathRoot}");
        }

        ToolchainEnsure(root);

        string dotnet = Path.Combine(root, ".toolchain", "DotNet", "dotnet.exe");
        string bin = Path.Combine(root, "Bin");
        Directory.CreateDirectory(bin);

        string kathBin = Path.Combine(kathRoot, "Bin");
        Directory.CreateDirectory(kathBin);
        string stableKath = Path.Combine(kathBin, "Kath.exe");
        Console.WriteLine("[INFO] Checking Kath build stages...");
        Run("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{kathBuild}\"" + (forceRebuild ? " -ForceRebuild" : ""), kathRoot);

        string electronMain = Path.Combine(kathRoot, "applications", "electron", "lib", "backend", "electron-main.js");
        if (!File.Exists(electronMain))
        {
            throw new FileNotFoundException("Kath build reported success but the Electron backend was not produced.", electronMain);
        }

        if (!File.Exists(stableKath))
        {
            throw new FileNotFoundException("Kath build did not produce the stable launcher.", stableKath);
        }

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
        return;
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
