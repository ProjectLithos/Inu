using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Inu.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: Inu.ImageBuilder create <InuProject.json> [--kernel <path>] [--bootloader <path>] [--sdk-root <path>] [--ide-root <path>] [--output <path>] [--dry-run]");

    if (!InuProject.TryLoad(args[1], out InuProject? project, out string error) || project is null) return Fail(error);

    string outputDirectory = Path.GetFullPath(project.OutputDirectory);
    string kernelPath = Path.GetFullPath(GetOption(args, "--kernel") ?? Path.Combine(outputDirectory, project.Name + ".bin"));
    string bootLoaderPath = Path.GetFullPath(GetOption(args, "--bootloader") ?? Path.Combine(outputDirectory, project.Name + ".bootx64.efi"));
    string imagePath = Path.GetFullPath(GetOption(args, "--output") ?? Path.Combine(outputDirectory, project.Name + ".img"));
    string projectRoot = Path.GetDirectoryName(Path.GetFullPath(args[1])) ?? Environment.CurrentDirectory;
    string sdkRoot = Path.GetFullPath(GetOption(args, "--sdk-root") ?? Environment.CurrentDirectory);
    string ideRoot = Path.GetFullPath(GetOption(args, "--ide-root") ?? Directory.GetParent(sdkRoot)?.FullName ?? sdkRoot);
    string helpSource = Path.Combine(sdkRoot, "Assets", "System", "Help");
    string wallpapersSource = Path.Combine(sdkRoot, "Assets", "System", "Wallpapers");
    string userlandAppsSource = Path.Combine(outputDirectory, "UserlandApps");
    string shellSource = Path.Combine(userlandAppsSource, "Shell", "SHELL.EXE");
    string commandsSource = Path.Combine(userlandAppsSource, "Commands");
    if (!TryReadGuiApplicationPaths(project.ConfigurationFile, out string desktopVfsPath, out string loginVfsPath, out error)) return Fail(error);
    string? fontSource = ResolveConsoleTrueTypeFont(projectRoot, ideRoot);
    string stagingRoot = Path.Combine(outputDirectory, "BootFiles");
    string stagedLoader = Path.Combine(stagingRoot, "EFI", "BOOT", "BOOTX64.EFI");
    string startupScript = Path.Combine(stagingRoot, "STARTUP.NSH");
    string novaRoot = Path.Combine(stagingRoot, "INU");
    string stagedKernel = Path.Combine(novaRoot, "KERNEL", "KERNEL.BIN");
    string bundlePath = Path.Combine(novaRoot, "SYSTEM", "ASSETS.BIN");
    string compileManifestPath = Path.Combine(outputDirectory, "Inu.Compile.json");
    bool nativeDebugSymbols = false;
    if (File.Exists(compileManifestPath))
    {
        using JsonDocument compileDocument = JsonDocument.Parse(File.ReadAllText(compileManifestPath));
        nativeDebugSymbols = compileDocument.RootElement.TryGetProperty("nativeDebugSymbols", out JsonElement debugElement) && debugElement.GetBoolean();
    }

    Console.WriteLine($"[INFO] UEFI loader  : {bootLoaderPath}");
    Console.WriteLine($"[INFO] Kernel input : {kernelPath} (PE32+ native subsystem, not EFI)");
    Console.WriteLine($"[INFO] System volume: FAT32 /INU (GUI app paths are OS policy: desktop={desktopVfsPath}, login={loginVfsPath})");
    Console.WriteLine($"[INFO] FAT32 image  : {imagePath}");
    if (HasOption(args, "--dry-run")) return 0;

    if (!File.Exists(kernelPath)) return Fail($"Non-EFI Inu kernel not found: {kernelPath}");
    if (!File.Exists(bootLoaderPath)) return Fail($"UEFI FAT32 loader not found: {bootLoaderPath}");
    if (!Directory.Exists(helpSource)) return Fail($"System command help directory not found: {helpSource}");
    if (!Directory.Exists(wallpapersSource)) return Fail($"System wallpaper directory not found: {wallpapersSource}");
    if (!Directory.Exists(userlandAppsSource)) return Fail($"Built userland application directory not found: {userlandAppsSource}");

    try
    {
        Directory.CreateDirectory(outputDirectory);
        if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedLoader)!);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedKernel)!);
        Directory.CreateDirectory(Path.Combine(novaRoot, "SYSTEM", "COMMANDS"));
        Directory.CreateDirectory(Path.Combine(novaRoot, "SYSTEM", "SHELL"));
        Directory.CreateDirectory(Path.Combine(novaRoot, "SYSTEM", "HELP"));
        Directory.CreateDirectory(Path.Combine(novaRoot, "SYSTEM", "FONTS"));
        Directory.CreateDirectory(Path.Combine(novaRoot, "SYSTEM", "WALLPAPERS"));
        File.Copy(bootLoaderPath, stagedLoader, true);
        File.WriteAllText(startupScript, "@echo -off\r\nfs0:\r\n\\EFI\\BOOT\\BOOTX64.EFI\r\n", Encoding.ASCII);
        File.Copy(kernelPath, stagedKernel, true);
        string kernelSha256;
        using (FileStream kernel = File.OpenRead(stagedKernel))
            kernelSha256 = Convert.ToHexString(SHA256.HashData(kernel)).ToLowerInvariant();
        if (nativeDebugSymbols)
        {
            string debugMapPath = Path.Combine(outputDirectory, "Inu.DebugSymbols.json");
            if (!File.Exists(debugMapPath)) return Fail($"Debug image creation requires the native debug map produced by the same link: {debugMapPath}");
            using JsonDocument debugDocument = JsonDocument.Parse(File.ReadAllText(debugMapPath));
            string? linkedKernel = debugDocument.RootElement.TryGetProperty("image", out JsonElement linkedImageElement) ? linkedImageElement.GetString() : null;
            string? linkedKernelSha256 = debugDocument.RootElement.TryGetProperty("kernelSha256", out JsonElement linkedHashElement) ? linkedHashElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(linkedKernel) || !string.Equals(Path.GetFullPath(linkedKernel), Path.GetFullPath(kernelPath), StringComparison.OrdinalIgnoreCase))
                return Fail("Debug symbol map does not identify the kernel being staged into the FAT32 image.");
            if (string.IsNullOrWhiteSpace(linkedKernelSha256) || !string.Equals(linkedKernelSha256, kernelSha256, StringComparison.OrdinalIgnoreCase))
                return Fail("Debug symbol map/kernel hash mismatch. Refusing to create a Debug image from stale native artifacts.");
        }
        if (File.Exists(shellSource))
            File.Copy(shellSource, Path.Combine(novaRoot, "SYSTEM", "SHELL", "SHELL.EXE"), true);
        if (Directory.Exists(commandsSource))
            CopyDirectoryFiles(commandsSource, Path.Combine(novaRoot, "SYSTEM", "COMMANDS"));
        CopyDirectoryFiles(helpSource, Path.Combine(novaRoot, "SYSTEM", "HELP"));
        CopyDirectoryFiles(wallpapersSource, Path.Combine(novaRoot, "SYSTEM", "WALLPAPERS"));
        if (!StageGuiApplication(userlandAppsSource, "INU-DESKTOP.EXE", stagingRoot, desktopVfsPath, out error)) return Fail(error);
        if (!StageGuiApplication(userlandAppsSource, "INU-LOGIN.EXE", stagingRoot, loginVfsPath, out error)) return Fail(error);
        if (string.IsNullOrWhiteSpace(fontSource) || !File.Exists(fontSource))
            return Fail("The generated OS is missing its project-local TrueType console font.");
        File.Copy(fontSource, Path.Combine(novaRoot, "SYSTEM", "FONTS", "CONSOLE.TTF"), true);
        Console.WriteLine("[ OK ] Project TrueType console font staged into the OS image.");
        BuildAssetBundle(novaRoot, bundlePath);

        if (!EfiDiskImage.TryCreate(stagingRoot, imagePath, out bool preservedUserState, out error)) return Fail(error);
        if (preservedUserState) Console.WriteLine("[ OK ] Existing /USERS account, Home and personal settings preserved from the previous image.");

        string manifestPath = Path.Combine(outputDirectory, "Inu.Image.json");
        using FileStream image = File.OpenRead(imagePath);
        string imageSha256 = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        int commandCount = Directory.GetFiles(Path.Combine(novaRoot, "SYSTEM", "COMMANDS")).Length;
        int helpCount = Directory.GetFiles(Path.Combine(novaRoot, "SYSTEM", "HELP")).Length;
        int fontCount = Directory.GetFiles(Path.Combine(novaRoot, "SYSTEM", "FONTS")).Length;
        int wallpaperCount = Directory.GetFiles(Path.Combine(novaRoot, "SYSTEM", "WALLPAPERS")).Length;
        int applicationCount = 2;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            productVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
            project = project.Name,
            architecture = project.TargetArchitecture,
            configuration = nativeDebugSymbols ? "Debug" : "Release",
            nativeDebugSymbols,
            bootProtocol = project.BootProtocol,
            format = "GPT/FAT32 system partition",
            imagePath,
            bootloaderPath = "EFI/BOOT/BOOTX64.EFI",
            shellRecoveryPath = "STARTUP.NSH",
            kernelPath = "INU/KERNEL/KERNEL.BIN",
            desktopApplicationPath = desktopVfsPath,
            loginApplicationPath = loginVfsPath,
            shellPath = "INU/SYSTEM/SHELL/SHELL.EXE",
            commandsPath = "INU/SYSTEM/COMMANDS",
            helpPath = "INU/SYSTEM/HELP",
            fontsPath = "INU/SYSTEM/FONTS",
            wallpapersPath = "INU/SYSTEM/WALLPAPERS",
            assetBundlePath = "INU/SYSTEM/ASSETS.BIN",
            commandCount,
            helpCount,
            fontCount,
            wallpaperCount,
            applicationCount,
            kernelLength = new FileInfo(stagedKernel).Length,
            kernelSha256,
            assetBundleLength = new FileInfo(bundlePath).Length,
            imageLength = new FileInfo(imagePath).Length,
            imageSha256,
            preservedUserState,
            producedUtc = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"[ OK ] UEFI loader staged: EFI\\BOOT\\BOOTX64.EFI");
        Console.WriteLine($"[ OK ] UEFI shell recovery staged: STARTUP.NSH -> \\EFI\\BOOT\\BOOTX64.EFI");
        Console.WriteLine($"[ OK ] Non-EFI kernel staged: INU\\KERNEL\\KERNEL.BIN");
        Console.WriteLine($"[ OK ] FAT32 system layout: ring-3 shell={(File.Exists(shellSource)?1:0)}, /System/Commands contains {commandCount} executable(s), /System/Help contains {helpCount} manuals, /System/Fonts contains {fontCount} font(s), /System/Wallpapers contains {wallpaperCount} wallpaper(s)");
        Console.WriteLine($"[ OK ] Partition asset catalogue: INU\\SYSTEM\\ASSETS.BIN");
        Console.WriteLine($"[ OK ] Bootable GPT/FAT32 image: {imagePath}");
        Console.WriteLine($"[ OK ] Image manifest: {manifestPath}");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
    {
        return Fail(exception.Message);
    }
}

static void CopyDirectoryFiles(string source, string destination)
{
    foreach (string file in Directory.GetFiles(source).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
}

static void BuildAssetBundle(string novaRoot, string bundlePath)
{
    var entries = new List<(string Path,string File,uint Flags)>();
    foreach ((string path,uint flags) in new[] { ("SYSTEM/SHELL",1U),("SYSTEM/COMMANDS",1U),("SYSTEM/HELP",2U),("SYSTEM/FONTS",4U),("SYSTEM/WALLPAPERS",8U) })
    {
        string fullDir=Path.Combine(novaRoot,path.Replace('/',Path.DirectorySeparatorChar));
        foreach(string file in Directory.GetFiles(fullDir).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase))
            entries.Add(($"{path}/{Path.GetFileName(file).ToUpperInvariant()}",file,flags));
    }
    using FileStream stream=new(bundlePath,FileMode.Create,FileAccess.Write,FileShare.None);
    using BinaryWriter writer=new(stream,Encoding.ASCII,leaveOpen:false);
    writer.Write(Encoding.ASCII.GetBytes("NOVASSET"));writer.Write(1U);writer.Write((uint)entries.Count);
    foreach(var entry in entries)
    {
        byte[] path=Encoding.ASCII.GetBytes(entry.Path);byte[] data=File.ReadAllBytes(entry.File);
        writer.Write((uint)path.Length);writer.Write((uint)data.Length);writer.Write(entry.Flags);writer.Write(0U);writer.Write(path);writer.Write(data);
    }
}

static bool HasOption(ReadOnlySpan<string> args, string option)
{
    foreach (string value in args) if (string.Equals(value, option, StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}
static string? GetOption(ReadOnlySpan<string> args, string name)
{
    for (int index = 0; index + 1 < args.Length; index++) if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
    return null;
}
static bool TryReadGuiApplicationPaths(string configurationPath, out string desktopPath, out string loginPath, out string error)
{
    desktopPath = "/BIN/INU-DESKTOP.EXE";
    loginPath = "/BIN/INU-LOGIN.EXE";
    error = string.Empty;
    if (!File.Exists(configurationPath)) return true;
    try
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("guiDesktopPath", out JsonElement desktop) && !string.IsNullOrWhiteSpace(desktop.GetString())) desktopPath = desktop.GetString()!;
        if (root.TryGetProperty("guiLoginPath", out JsonElement login) && !string.IsNullOrWhiteSpace(login.GetString())) loginPath = login.GetString()!;
        if (!IsSafeVfsApplicationPath(desktopPath) || !IsSafeVfsApplicationPath(loginPath))
        {
            error = "GUI application paths must be absolute VFS paths below / and may not contain '.' or '..' segments.";
            return false;
        }
        return true;
    }
    catch (Exception exception) when (exception is IOException or JsonException)
    {
        error = $"Could not read GUI application paths from {configurationPath}: {exception.Message}";
        return false;
    }
}

static bool IsSafeVfsApplicationPath(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value[0] != '/') return false;
    string[] parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) return false;
    foreach (string part in parts) if (part is "." or ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
    return true;
}

static bool StageGuiApplication(string sourceDirectory, string sourceName, string fileSystemRoot, string vfsPath, out string error)
{
    error = string.Empty;
    string source = Path.Combine(sourceDirectory, sourceName);
    if (!File.Exists(source)) { error = $"Built GUI application not found: {source}"; return false; }
    string[] parts = vfsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    string destination = fileSystemRoot;
    foreach (string part in parts) destination = Path.Combine(destination, part);
    string fullRoot = Path.GetFullPath(fileSystemRoot) + Path.DirectorySeparatorChar;
    string fullDestination = Path.GetFullPath(destination);
    if (!fullDestination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) { error = $"GUI application path escapes the FAT filesystem root: {vfsPath}"; return false; }
    Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
    File.Copy(source, fullDestination, true);
    Console.WriteLine($"[ OK ] GUI application staged: {vfsPath}");
    return true;
}

static string? ResolveConsoleTrueTypeFont(string projectRoot, string ideRoot)
{
    List<string> candidates = new()
    {
        Path.Combine(projectRoot, "Kernel", "Provided", "Assets", "Fonts", "TrueType", "Console.ttf"),
        Path.Combine(ideRoot, "Assets", "Fonts", "TrueType", "DejaVuSansMono.ttf"),
        Path.Combine(ideRoot, "Assets", "Fonts", "TrueType", "Console.ttf")
    };

    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrWhiteSpace(localAppData))
    {
        string userFonts = Path.Combine(localAppData, "Microsoft", "Windows", "Fonts");
        candidates.Add(Path.Combine(userFonts, "CascadiaMono.ttf"));
        candidates.Add(Path.Combine(userFonts, "CascadiaCode.ttf"));
    }

    string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    if (!string.IsNullOrWhiteSpace(windows))
    {
        string fonts = Path.Combine(windows, "Fonts");
        candidates.Add(Path.Combine(fonts, "consola.ttf"));
        candidates.Add(Path.Combine(fonts, "lucon.ttf"));
        candidates.Add(Path.Combine(fonts, "cour.ttf"));
    }

    foreach (string candidate in candidates)
        if (File.Exists(candidate)) return candidate;
    return null;
}

static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
