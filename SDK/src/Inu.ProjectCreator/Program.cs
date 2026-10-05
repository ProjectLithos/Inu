using System.Linq;
using System.Text.Json;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 1 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
    {
        return Fail("Usage: Inu.ProjectCreator create [--output <directory>] [--sdk-root <directory>]");
    }

    string sdkRoot = Path.GetFullPath(GetOption(args, "--sdk-root") ?? FindSdkRoot(AppContext.BaseDirectory));
    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string output = Path.GetFullPath(GetOption(args, "--output") ?? Path.Combine(userProfile, "Source", "Repos", "InuKernel"));
    string template = Path.Combine(sdkRoot, "templates", "InuKernel");
    if (!Directory.Exists(template)) return Fail($"Kernel project template was not found: {template}");

    Directory.CreateDirectory(output);
    foreach (string obsoleteSolution in Directory.EnumerateFiles(output, "*.sln", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(output, "*.slnx", SearchOption.TopDirectoryOnly)))
    {
        File.Delete(obsoleteSolution);
        Console.WriteLine($"[ OK ] Removed obsolete Visual Studio solution file: {obsoleteSolution}");
    }
    string? mainProjectPath = ResolveMainProjectPath(output);
    if (mainProjectPath is null) return 1;
    if (!RemoveSdkOwnedLegacyTrees(output)) return 1;
    if (!MaterializeCanonicalUserlandWorkspace(sdkRoot, output)) return 1;

    // SDK-owned implementation remains canonical under $(InuSdkRoot)\src. Generated projects
    // carry only project-owned scaffolding plus user-editable workspace projects.

    foreach (string source in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(template, source);
        bool isConfigurationFile = string.Equals(relative, "Inu.Configuration.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relative, "Inu.Configuration.props", StringComparison.OrdinalIgnoreCase);
        if (isConfigurationFile && File.Exists(Path.Combine(output, relative))) continue;
        if (string.Equals(relative, "InuProject.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relative, "InuKernel.csproj", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        // Kath&Inu has only Boot, Kernel and Userland execution partitions.
        if (string.Equals(relative, Path.Combine("Kernel", "Kernel.cs"), StringComparison.OrdinalIgnoreCase))
            continue;

        string destination;
        if (relative.StartsWith("Boot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            destination = Path.Combine(output, "Boot", "Provided", "Startup", Path.GetRelativePath("Boot", relative));
        else if (relative.StartsWith("HAL" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            destination = Path.Combine(output, "Kernel", "Provided", "HAL", Path.GetRelativePath("HAL", relative));
        else if (relative.StartsWith("Startup" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            destination = Path.Combine(output, "Kernel", "Provided", "Startup", Path.GetRelativePath("Startup", relative));
        else
            destination = Path.Combine(output, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        // Kernel, Boot and HAL scaffolding refresh with the SDK. User-authored extension
        // projects and materialized userland workspace projects remain protected.
        bool generatedShellTree = relative.StartsWith(Path.Combine("Userland", "Shell") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        bool userOwnedTree = relative.StartsWith("KernelProjects" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            (relative.StartsWith("Userland" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !generatedShellTree);

        if (userOwnedTree && File.Exists(destination)) continue;

        File.Copy(source, destination, true);
    }

    string mainProjectFileName = Path.GetFileName(mainProjectPath);
    if (string.IsNullOrWhiteSpace(mainProjectFileName)) return Fail($"Kernel project filename is invalid: {mainProjectPath}");
    File.Copy(Path.Combine(template, "InuKernel.csproj"), mainProjectPath, true);

    string manifestPath = Path.Combine(output, "InuProject.json");
    string projectName = "MinimalKernel";
    string author = "The DCL Group";
    string targetArchitecture = "x64";
    string kernelModel = "Monolithic";
    string bootProtocol = "Uefi";
    string runtimePack = "Inu.RuntimePack.X64.NativeAot";
    string projectFile = "InuKernel.csproj";
    string[] workAreas = new[] { "HAL", "Drivers", "Storage", "Filesystems", "Networking", "USB", "Input", "Processes", "Scheduler", "System Calls", "Security", "Diagnostics" };

    if (File.Exists(manifestPath))
    {
        try
        {
            using JsonDocument existingManifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = existingManifest.RootElement;
            if (root.TryGetProperty("Name", out JsonElement nameElement) && !string.IsNullOrWhiteSpace(nameElement.GetString()))
                projectName = nameElement.GetString()!;
            if (root.TryGetProperty("Author", out JsonElement authorElement) && !string.IsNullOrWhiteSpace(authorElement.GetString()))
                author = authorElement.GetString()!;
            if (root.TryGetProperty("TargetArchitecture", out JsonElement architectureElement) && !string.IsNullOrWhiteSpace(architectureElement.GetString()))
                targetArchitecture = architectureElement.GetString()!;
            if (root.TryGetProperty("KernelModel", out JsonElement modelElement) && !string.IsNullOrWhiteSpace(modelElement.GetString()))
                kernelModel = modelElement.GetString()!;
            if (root.TryGetProperty("BootProtocol", out JsonElement bootElement) && !string.IsNullOrWhiteSpace(bootElement.GetString()))
                bootProtocol = bootElement.GetString()!;
            if (root.TryGetProperty("RuntimePack", out JsonElement runtimeElement) && !string.IsNullOrWhiteSpace(runtimeElement.GetString()))
                runtimePack = runtimeElement.GetString()!;
            if (root.TryGetProperty("ProjectFile", out JsonElement projectElement) && !string.IsNullOrWhiteSpace(projectElement.GetString()))
            {
                string existingProjectFile = projectElement.GetString()!;
                string normalizedProjectFile = existingProjectFile.Replace('\\','/');
                if (!normalizedProjectFile.StartsWith("System/Inu.Kernel.Entry.", StringComparison.OrdinalIgnoreCase))
                    projectFile = existingProjectFile;
            }
            if (root.TryGetProperty("WorkAreas", out JsonElement workElement) && workElement.ValueKind == JsonValueKind.Array)
                workAreas = workElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }
        catch (JsonException)
        {
            // A malformed manifest is replaced with safe defaults below.
        }
    }

    // Inu.Configuration.json remains compatibility metadata.
    // Kath projects additionally carry Inu.json; it is the IDE-authoritative
    // configuration and is applied after compatibility metadata so stale manifests/props
    // can never silently revert Microkernel or Hybrid to Monolithic during refresh.
    string configurationPath = Path.Combine(output, "Inu.Configuration.json");
    string[] developmentAreas = Array.Empty<string>();
    if (File.Exists(configurationPath))
    {
        try
        {
            using JsonDocument configurationDocument = JsonDocument.Parse(File.ReadAllText(configurationPath));
            JsonElement configurationRoot = configurationDocument.RootElement;
            if (configurationRoot.TryGetProperty("Architecture", out JsonElement architectureElement) && !string.IsNullOrWhiteSpace(architectureElement.GetString()))
                targetArchitecture = architectureElement.GetString()!;
            if (configurationRoot.TryGetProperty("KernelModel", out JsonElement modelElement) && !string.IsNullOrWhiteSpace(modelElement.GetString()))
                kernelModel = modelElement.GetString()!;
            if (configurationRoot.TryGetProperty("BootProtocol", out JsonElement bootElement) && !string.IsNullOrWhiteSpace(bootElement.GetString()))
                bootProtocol = bootElement.GetString()!;
            if (configurationRoot.TryGetProperty("WorkAreas", out JsonElement developmentElement) && developmentElement.ValueKind == JsonValueKind.Array)
                developmentAreas = developmentElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            string[] availableAreas = new[] { "Shell", "GUI", "Drivers", "HAL", "Audio", "Filesystems", "Storage", "Networking", "USB", "Input", "Processes", "Scheduler", "System Calls", "Security", "Diagnostics", "Tests" };
            workAreas = availableAreas.Where(area => !developmentAreas.Contains(area, StringComparer.OrdinalIgnoreCase)).ToArray();
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"[FAIL] Inu configuration is malformed: {configurationPath}");
            return 1;
        }
    }


    string ideConfigurationPath = Path.Combine(output, "Inu.json");
    if (File.Exists(ideConfigurationPath))
    {
        try
        {
            using JsonDocument ideConfigurationDocument = JsonDocument.Parse(File.ReadAllText(ideConfigurationPath));
            JsonElement ideRoot = ideConfigurationDocument.RootElement;
            if (ideRoot.TryGetProperty("targetArchitecture", out JsonElement architectureElement) && !string.IsNullOrWhiteSpace(architectureElement.GetString()))
            {
                string configured = architectureElement.GetString()!;
                targetArchitecture = string.Equals(configured, "x86_64", StringComparison.OrdinalIgnoreCase) ? "x64" : configured;
            }
            if (ideRoot.TryGetProperty("kernelArchitecture", out JsonElement modelElement) && !string.IsNullOrWhiteSpace(modelElement.GetString()))
            {
                string configured = modelElement.GetString()!;
                kernelModel = string.Equals(configured, "microkernel", StringComparison.OrdinalIgnoreCase) ? "Microkernel"
                    : string.Equals(configured, "hybrid", StringComparison.OrdinalIgnoreCase) ? "Hybrid"
                    : string.Equals(configured, "custom", StringComparison.OrdinalIgnoreCase) ? "Custom"
                    : "Monolithic";
            }
            if (ideRoot.TryGetProperty("bootArchitecture", out JsonElement bootElement) && !string.IsNullOrWhiteSpace(bootElement.GetString()))
            {
                string configured = bootElement.GetString()!;
                bootProtocol = string.Equals(configured, "uefi", StringComparison.OrdinalIgnoreCase) ? "Uefi" : configured;
            }
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"[FAIL] Kath configuration is malformed: {ideConfigurationPath}");
            return 1;
        }
    }

    File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
    {
        Name = projectName,
        Author = author,
        ProjectFile = projectFile,
        TargetArchitecture = targetArchitecture,
        BootProtocol = bootProtocol,
        KernelEntry = "KMain",
        RuntimePack = runtimePack,
        OutputDirectory = Path.Combine(sdkRoot, "Artifacts", "MinimalKernel"),
        KernelModel = kernelModel,
        ConfigurationFile = "Inu.Configuration.json",
        WorkAreas = workAreas,
        DevelopmentAreas = developmentAreas
    }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);


    Console.WriteLine($"[ OK ] C# kernel project: {output}");
    Console.WriteLine($"[ OK ] Kernel project  : {mainProjectPath}");
    Console.WriteLine($"[ OK ] Project manifest: {manifestPath}");
    return 0;
}

static string? ResolveMainProjectPath(string output)
{
    string canonical = Path.Combine(output, "InuKernel.csproj");
    string[] candidates = Directory.EnumerateFiles(output, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();

    // InuKernel.csproj is the authoritative OS/kernel root. Auxiliary projects
    // may legitimately live at the project root (for example application-format or
    // generated support libraries) and must not be mistaken for a second kernel.
    if (File.Exists(canonical))
    {
        string[] auxiliary = candidates
            .Where(candidate => !string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(canonical), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (auxiliary.Length > 0)
            Console.WriteLine($"[INFO] Root kernel project: {Path.GetFileName(canonical)}; ignoring {auxiliary.Length} top-level auxiliary project(s): {string.Join(", ", auxiliary.Select(candidate => Path.GetFileName(candidate)))}");
        return canonical;
    }

    if (candidates.Length == 0) return canonical;
    if (candidates.Length == 1) return candidates[0];
    Console.Error.WriteLine($"[FAIL] More than one root kernel project exists in {output} and InuKernel.csproj is missing: {string.Join(", ", candidates.Select(candidate => Path.GetFileName(candidate)))}");
    return null;
}


static bool RemoveSdkOwnedLegacyTrees(string output)
{
    // System/Commands is now an editable operating-system command workspace.  Older generated
    // projects used System/ for SDK implementation mirrors, so clean those legacy children while
    // preserving Commands and every user edit beneath it.
    string systemRoot = Path.Combine(output, "System");
    if (Directory.Exists(systemRoot))
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(systemRoot))
        {
            if (Directory.Exists(entry) && string.Equals(Path.GetFileName(entry), "Commands", StringComparison.OrdinalIgnoreCase)) continue;
            if (Directory.Exists(entry)) Directory.Delete(entry, true);
            else File.Delete(entry);
            Console.WriteLine($"[ OK ] Removed legacy SDK System mirror entry: {entry}");
        }
    }

    string[] sdkOwnedTrees =
    [
        "Sdk",
        "HAL",
        "Startup",
        Path.Combine("Kernel", "Console"),
        Path.Combine("Userland", "Shell"),
        "Console",
        "Runtime"
    ];

    foreach (string relative in sdkOwnedTrees)
    {
        string path = Path.Combine(output, relative);
        if (!Directory.Exists(path)) continue;
        Directory.Delete(path, true);
        Console.WriteLine($"[ OK ] Removed legacy SDK implementation mirror: {path}");
    }

    string bootRoot = Path.Combine(output, "Boot");
    if (Directory.Exists(bootRoot))
    {
        foreach (string legacyBootSource in Directory.EnumerateFiles(bootRoot, "*.cs", SearchOption.TopDirectoryOnly))
        {
            File.Delete(legacyBootSource);
            Console.WriteLine($"[ OK ] Removed legacy root Boot source: {legacyBootSource}");
        }
    }

    // Kernel\Kernel.cs may contain coder edits from the old layout, so preserve the file
    // but exclude it from compilation. Kernel\<OSName>\Kernel.cs is authoritative now.
    return true;
}


static bool MaterializeCanonicalUserlandWorkspace(string sdkRoot, string output)
{
    string sourceRoot = Path.Combine(sdkRoot, "src", "Userland");
    if (!Directory.Exists(sourceRoot))
    {
        Console.Error.WriteLine($"[FAIL] Canonical Inu userland workspace source is missing: {sourceRoot}");
        return false;
    }

    string destinationRoot = Path.Combine(output, "Userland", "Provided");
    foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(sourceRoot, source);
        string destination = Path.Combine(destinationRoot, relative);
        if (File.Exists(destination)) continue;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, false);
        Console.WriteLine($"[ OK ] Materialized userland workspace file from canonical SDK source: {relative}");
    }
    return true;
}

static string FindSdkRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Inu.SdkManifest.json"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Inu SDK root was not found.");
}

static string? GetOption(ReadOnlySpan<string> args, string name)
{
    for (int index = 0; index + 1 < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
    }
    return null;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"[FAIL] {message}");
    return 1;
}
