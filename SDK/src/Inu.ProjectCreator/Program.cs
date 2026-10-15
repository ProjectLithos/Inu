using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 1 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
    {
        throw Fail("Usage: Inu.ProjectCreator create [--output <directory>] [--sdk-root <directory>]");
    }

    string sdkRoot = Path.GetFullPath(GetOption(args, "--sdk-root") ?? FindSdkRoot(AppContext.BaseDirectory));
    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string output = Path.GetFullPath(GetOption(args, "--output") ?? Path.Combine(userProfile, "Source", "Repos", "InuKernel"));
    string template = Path.Combine(sdkRoot, "templates", "InuKernel");
    if (!Directory.Exists(template)) throw Fail($"Kernel project template was not found: {template}");

    Directory.CreateDirectory(output);
    foreach (string obsoleteSolution in Directory.EnumerateFiles(output, "*.sln", SearchOption.TopDirectoryOnly).Concat(Directory.EnumerateFiles(output, "*.slnx", SearchOption.TopDirectoryOnly)))
    {
        File.Delete(obsoleteSolution);
        Console.WriteLine($"[ OK ] Removed obsolete Visual Studio solution file: {obsoleteSolution}");
    }
    string? mainProjectPath = ResolveMainProjectPath(output);
    if (mainProjectPath is null) throw new InvalidOperationException("Could not resolve the kernel project path.");
    if (!RemoveSdkOwnedLegacyTrees(output)) throw new InvalidOperationException("Could not remove SDK-owned legacy trees.");
    if (!MaterializeCanonicalUserlandWorkspace(sdkRoot, output)) throw new InvalidOperationException("Could not materialize the canonical userland workspace.");
    if (!MaterializeCanonicalKernelStartupSource(sdkRoot, output)) throw new InvalidOperationException("Could not materialize canonical kernel startup source.");

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
    if (string.IsNullOrWhiteSpace(mainProjectFileName)) throw Fail($"Kernel project filename is invalid: {mainProjectPath}");
    File.Copy(Path.Combine(template, "InuKernel.csproj"), mainProjectPath, true);

    string manifestPath = Path.Combine(output, "InuProject.json");
    string projectName = "MinimalKernel";
    string author = "The DCL Group";
    string targetArchitecture = "x64";
    string kernelModel = "Monolithic";
    string bootProtocol = "Uefi";
    string runtimePack = "Inu.RuntimePack.X64.NativeAot";
    string projectFile = "InuKernel.csproj";
    string projectKind = "operating-system";
    bool testOs = false;
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
            if (root.TryGetProperty("ProjectKind", out JsonElement projectKindElement) && !string.IsNullOrWhiteSpace(projectKindElement.GetString()))
                projectKind = projectKindElement.GetString()!;
            if (root.TryGetProperty("TestOs", out JsonElement testOsElement) && (testOsElement.ValueKind == JsonValueKind.True || testOsElement.ValueKind == JsonValueKind.False))
                testOs = testOsElement.GetBoolean();
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
            throw new InvalidDataException($"Inu configuration is malformed: {configurationPath}");
        }
    }


    string ideConfigurationPath = Path.Combine(output, "Inu.json");
    if (File.Exists(ideConfigurationPath))
    {
        try
        {
            using JsonDocument ideConfigurationDocument = JsonDocument.Parse(File.ReadAllText(ideConfigurationPath));
            JsonElement ideRoot = ideConfigurationDocument.RootElement;
            if (ideRoot.TryGetProperty("projectKind", out JsonElement projectKindElement) && !string.IsNullOrWhiteSpace(projectKindElement.GetString()))
            {
                projectKind = projectKindElement.GetString()!;
                testOs = string.Equals(projectKind, "test-os", StringComparison.OrdinalIgnoreCase);
            }
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
            throw new InvalidDataException($"Kath configuration is malformed: {ideConfigurationPath}");
        }
    }

    if (!RepairTestOsKernelStorageImport(output, projectName, testOs)) throw new InvalidOperationException("Could not repair the Test OS kernel storage import.");
    if (!MaterializeCoderCommands(sdkRoot, output, projectName)) throw new InvalidOperationException("Could not materialize coder commands.");
    // Shell.cs is coder-owned. Never rewrite it during compile/run preparation, even when it
    // still contains markers from an older generated template. Existing shell source is preserved
    // byte-for-byte; new projects receive the current template only when the file is absent.
    EnsureProvidedWorkspaceFoldersVisible(output);
    if (!RefreshCentralKernelConsoleSourcePlan(sdkRoot, output)) throw new InvalidOperationException("Could not refresh the central kernel console source plan.");

    File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
    {
        Name = projectName,
        ProjectKind = projectKind,
        TestOs = testOs,
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


static bool RepairTestOsKernelStorageImport(string output, string projectName, bool testOs)
{
    if (!testOs) return true;

    string kernelPath = Path.Combine(output, "Kernel", projectName, "Kernel.cs");
    if (!File.Exists(kernelPath)) return true;

    try
    {
        string source = File.ReadAllText(kernelPath);
        if (source.Contains("using Inu.Kernel.Storage;", StringComparison.Ordinal)) return true;

        bool usesStorageFacade = source.Contains("FileSystem.", StringComparison.Ordinal) ||
            source.Contains("FileSystemPathPolicy", StringComparison.Ordinal) ||
            source.Contains("FileSystemLogicalPath", StringComparison.Ordinal);
        if (!usesStorageFacade) return true;

        // Test OS projects intentionally exercise every selected SDK facade. Older Test OS
        // kernels were generated before the storage import was part of that profile and are
        // coder-owned, so normal template refresh deliberately preserves them. Repair only
        // this missing import; do not replace or regenerate any kernel policy source.
        string marker = "using Inu.Kernel.Power;";
        if (source.Contains(marker, StringComparison.Ordinal))
            source = source.Replace(marker, marker + Environment.NewLine + "using Inu.Kernel.Storage;", StringComparison.Ordinal);
        else
        {
            int namespaceIndex = source.IndexOf("namespace ", StringComparison.Ordinal);
            if (namespaceIndex < 0) throw Fail($"Test OS kernel uses filesystem APIs but has no namespace declaration to anchor the Inu.Kernel.Storage import: {kernelPath}");
            source = source.Insert(namespaceIndex, "using Inu.Kernel.Storage;" + Environment.NewLine + Environment.NewLine);
        }

        File.WriteAllText(kernelPath, source);
        Console.WriteLine($"[ OK ] Repaired Test OS storage/filesystem SDK import: {kernelPath}");
        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[FAIL] Unable to repair Test OS storage/filesystem import: {ex.Message}");
        return false;
    }
}

static bool RefreshCentralKernelConsoleSourcePlan(string sdkRoot, string output)
{
    string planPath = Path.Combine(output, "Inu.KernelSources.json");
    string targetsPath = Path.Combine(output, "Inu.KernelSources.targets");
    if (!File.Exists(planPath) || !File.Exists(targetsPath)) return true;

    try
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(planPath));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("files", out JsonElement filesElement) || filesElement.ValueKind != JsonValueKind.Array) return true;

        List<(string Source, string Destination, string Project, bool Central)> files = new();
        bool hadMaterializedConsole = false;
        foreach (JsonElement item in filesElement.EnumerateArray())
        {
            string source = item.TryGetProperty("source", out JsonElement sourceElement) ? sourceElement.GetString() ?? string.Empty : string.Empty;
            string destination = item.TryGetProperty("destination", out JsonElement destinationElement) ? destinationElement.GetString() ?? string.Empty : string.Empty;
            string project = item.TryGetProperty("project", out JsonElement projectElement) ? projectElement.GetString() ?? string.Empty : string.Empty;
            bool central = item.TryGetProperty("central", out JsonElement centralElement) && centralElement.ValueKind == JsonValueKind.True;
            if (string.Equals(project, "Inu.Kernel.Console", StringComparison.OrdinalIgnoreCase))
            {
                if (!central) hadMaterializedConsole = true;
                continue;
            }
            if (source.Length != 0 && destination.Length != 0 && project.Length != 0)
                files.Add((source, destination, project, central));
        }

        // 0.0.134 ownership correction: Inu.Kernel.Console is a support dependency of
        // the selected Boot node, not the Boot implementation itself. Older Kath releases
        // accidentally marked it OS-owned, which left existing projects compiling a stale
        // copied console while SDK-owned bootstrap code moved forward. Always link the
        // current central console support source; leave any old physical copy untouched so
        // no coder data is destroyed.
        string consoleRoot = Path.Combine(sdkRoot, "src", "Inu.Kernel.Console");
        if (!Directory.Exists(consoleRoot)) return FailBool($"Kernel console SDK source is missing: {consoleRoot}");
        foreach (string sourcePath in Directory.EnumerateFiles(consoleRoot, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string relative = Path.GetRelativePath(consoleRoot, sourcePath);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase))) continue;
            string canonical = Path.Combine("src", "Inu.Kernel.Console", relative).Replace('\\', '/');
            string destination = Path.Combine("Kernel", "Provided", "Dependencies", "Inu.Kernel.Console", relative).Replace('\\', '/');
            files.Add((canonical, destination, "Inu.Kernel.Console", true));
        }

        List<string> selectedCentralProjects = new();
        string propsPath = Path.Combine(output, "Inu.Configuration.props");
        string propsText = File.Exists(propsPath) ? File.ReadAllText(propsPath) : String.Empty;
        if (propsText.Contains("INU_COMPONENT_STORAGE_NVME_DRIVER", StringComparison.Ordinal)) selectedCentralProjects.Add("Inu.Kernel.Nvme");
        if (propsText.Contains("INU_COMPONENT_STORAGE_AHCI_DRIVER", StringComparison.Ordinal)) selectedCentralProjects.Add("Inu.Kernel.Ahci");
        if (propsText.Contains("INU_COMPONENT_STORAGE_VIRTIO_BLOCK_DRIVER", StringComparison.Ordinal)) selectedCentralProjects.Add("Inu.Kernel.Virtio");
        if (propsText.Contains("INU_COMPONENT_FILESYSTEM_FATFS", StringComparison.Ordinal)) selectedCentralProjects.Add("Inu.Filesystem.FatFs");
        foreach (string selectedProject in selectedCentralProjects)
        {
            string selectedRoot = Path.Combine(sdkRoot, "src", selectedProject);
            if (!Directory.Exists(selectedRoot)) return FailBool($"Selected SDK source project is missing: {selectedRoot}");
            foreach (string sourcePath in Directory.EnumerateFiles(selectedRoot, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string relative = Path.GetRelativePath(selectedRoot, sourcePath);
                if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase))) continue;
                string canonical = Path.Combine("src", selectedProject, relative).Replace('\\', '/');
                string destination = Path.Combine("Kernel", "Provided", "Dependencies", selectedProject, relative).Replace('\\', '/');
                files.Add((canonical, destination, selectedProject, true));
            }
        }

        files = files
            .GroupBy(file => file.Source, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(file => file.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();

        static string Xml(string value) => value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        List<string> targetLines = ["<Project>", "  <ItemGroup>"];
        foreach ((string Source, string Destination, string Project, bool Central) file in files)
        {
            if (file.Central)
                targetLines.Add($"    <Compile Include=\"$(InuSdkRoot)\\{Xml(file.Source.Replace('/', '\\'))}\" Link=\"{Xml(file.Destination.Replace('/', '\\'))}\" />");
            else
                targetLines.Add($"    <Compile Include=\"$(MSBuildThisFileDirectory){Xml(file.Destination.Replace('/', '\\'))}\" />");
        }
        targetLines.Add("  </ItemGroup>");
        targetLines.Add("</Project>");
        targetLines.Add(string.Empty);
        File.WriteAllText(targetsPath, string.Join(Environment.NewLine, targetLines));

        string[] dependencyProjects = root.TryGetProperty("dependencyProjects", out JsonElement dependencyElement) && dependencyElement.ValueKind == JsonValueKind.Array
            ? dependencyElement.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();
        if (!dependencyProjects.Contains("Inu.Kernel.Console", StringComparer.OrdinalIgnoreCase))
            dependencyProjects = dependencyProjects.Append("Inu.Kernel.Console").OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string selectedProject in selectedCentralProjects)
            if (!dependencyProjects.Contains(selectedProject, StringComparer.OrdinalIgnoreCase))
                dependencyProjects = dependencyProjects.Append(selectedProject).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        string note = root.TryGetProperty("note", out JsonElement noteElement) && noteElement.ValueKind == JsonValueKind.String
            ? noteElement.GetString() ?? string.Empty
            : "Selected architectural source is OS-owned. Unselected build-only support source is linked from the single authoritative Inu/SDK/src tree; no duplicate physical copies are generated.";
        File.WriteAllText(planPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            files = files.Select(file => new { source = file.Source, destination = file.Destination, project = file.Project, central = file.Central }).ToArray(),
            dependencyProjects,
            note
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        if (hadMaterializedConsole)
            Console.WriteLine("[ OK ] Refreshed Inu.Kernel.Console from central SDK support source; legacy Boot-owned copy remains untouched but is no longer compiled.");
        return true;
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
    {
        Console.Error.WriteLine($"[FAIL] Could not refresh central kernel console source plan: {ex.Message}");
        return false;
    }
}

static bool FailBool(string message) => throw new InvalidOperationException(message);

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


static bool MaterializeCanonicalKernelStartupSource(string sdkRoot, string output)
{
    string sourceRoot = Path.Combine(sdkRoot, "src", "Inu.Kernel.Bootstrap", "SourceComponents");
    if (!Directory.Exists(sourceRoot))
    {
        Console.Error.WriteLine($"[FAIL] Canonical Inu kernel startup source is missing: {sourceRoot}");
        return false;
    }

    (string Source, string Destination)[] groups =
    [
        (Path.Combine(sourceRoot, "Boot"), Path.Combine(output, "Boot", "Provided", "Startup")),
        (Path.Combine(sourceRoot, "HAL"), Path.Combine(output, "Kernel", "Provided", "HAL")),
        (Path.Combine(sourceRoot, "Startup"), Path.Combine(output, "Kernel", "Provided", "Startup"))
    ];

    foreach ((string sourceDirectory, string destinationDirectory) in groups)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Console.Error.WriteLine($"[FAIL] Canonical Inu source-component directory is missing: {sourceDirectory}");
            return false;
        }
        Directory.CreateDirectory(destinationDirectory);
        foreach (string source in Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, source);
            string destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, true);
        }
    }

    Console.WriteLine("[ OK ] Kernel startup/HAL source materialized from the single Inu/SDK/src authority.");
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
    bool? graphicalStartup = ReadKathGraphicalStartup(output);
    if (graphicalStartup == false)
    {
        foreach (string guiTree in new[] { Path.Combine(destinationRoot, "Desktop"), Path.Combine(destinationRoot, "Login") })
        {
            if (!Directory.Exists(guiTree)) continue;
            Directory.Delete(guiTree, true);
            Console.WriteLine($"[ OK ] Removed unselected graphical userland source tree: {guiTree}");
        }
    }

    // Stock command implementations have one SDK source of reference:
    // SDK/src/Userland/Commands/*.cs. They are materialised only into the coder-owned
    // Userland/<OS>/Commands workspace by MaterializeCoderCommands. Older releases also
    // copied them into Userland/Provided, which made each command appear twice in Kath.
    foreach (string legacyCommandTree in new[]
    {
        Path.Combine(destinationRoot, "Commands"),
        Path.Combine(destinationRoot, "SysInfo")
    })
    {
        if (!Directory.Exists(legacyCommandTree)) continue;
        Directory.Delete(legacyCommandTree, true);
        Console.WriteLine($"[ OK ] Removed duplicate provided command source tree: {legacyCommandTree}");
    }

    foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(sourceRoot, source);
        if (relative.StartsWith("Commands" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("SysInfo" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            (graphicalStartup == false && (relative.StartsWith("Desktop" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                           relative.StartsWith("Login" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))))
        {
            continue;
        }

        string destination = Path.Combine(destinationRoot, relative);
        if (File.Exists(destination)) continue;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, false);
        Console.WriteLine($"[ OK ] Materialized userland workspace file from canonical SDK source: {relative}");
    }
    return true;
}


static bool? ReadKathGraphicalStartup(string output)
{
    string path = Path.Combine(output, "Inu.json");
    if (!File.Exists(path)) return null;
    try
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("startupModel", out JsonElement startup) && startup.ValueKind == JsonValueKind.String)
        {
            string value = startup.GetString() ?? string.Empty;
            return string.Equals(value, "gui", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "cli-gui", StringComparison.OrdinalIgnoreCase);
        }
        if (root.TryGetProperty("gui", out JsonElement gui) && gui.ValueKind == JsonValueKind.String)
            return !string.Equals(gui.GetString(), "none", StringComparison.OrdinalIgnoreCase);
        return false;
    }
    catch (JsonException)
    {
        return null;
    }
}

static string SafeProjectSegment(string value)
{
    char[] chars = value.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
    return chars.Length == 0 ? "OS" : new string(chars);
}

// Shell.cs is userland source and is generated by Kath's project generator.
// ProjectCreator must not migrate or repair user-owned shell files during SDK refresh.

// Existing OS-owned command files are never rewritten during project refresh.
// New projects receive the canonical command template through MaterializeCoderCommands.

static bool MaterializeCoderCommands(string sdkRoot, string output, string projectName)
{
    string sourceRoot = Path.Combine(sdkRoot, "src", "Userland", "Commands");
    if (!Directory.Exists(sourceRoot)) return true;

    // These hashes identify the unmodified 0.0.141 stock command files. They are metadata,
    // not alternate command implementations. If a generated project still has one of these
    // exact files, refresh it from the single canonical SDK source so existing projects gain
    // the normal human-readable formatting. Any coder edit changes the hash and is preserved.
    Dictionary<string, string> legacyStockHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cd.cs"] = "25D671C1207ACB2AB858A87E23FB6AEAAD0162AB150CC01445E8EF3440CEF06C",
        ["Clear.cs"] = "15FCB1D9569B4A33ED217C48EEF661359E8FA7917EC9568D60B78D993568656B",
        ["Console.cs"] = "A1CEA1E0ABD89082113AFCCA8B31A2F48C7AD2E1F7EEB4B43AFBF108A4840FE2",
        ["Del.cs"] = "0ABAA467AEC82DC507B0588A3DF783BB1625D4E1CDE3F88C454385AA763954C1",
        ["Dir.cs"] = "7D8135BBAA2C0D451512197340D2700A625F2654382877FE5E6C8E168D1E263D",
        ["Echo.cs"] = "988AAF7DDE592B559959625119F05BBD87366A26B28EB830C998505E0C54D744",
        ["Mkdir.cs"] = "DAED009E340552BCEA19D16E762DAB7D05C3AC7FA189D265E0A3E0C75D4452DC",
        ["Pwd.cs"] = "0CEB763BC00AFD64B25C18D2A4913E330E2B6F9ED787680C69D73F41C32FD3FB",
        ["Rmdir.cs"] = "631A84C0BA3F0C98042F0C4971AE815281A547CD1899CCCF56BD18EC887521F3",
        ["SysInfo.cs"] = "A46E39376354D46A3F7974359B54A1179A1CC3D60507684A332B7BFCBDE5D1BE",
        ["Type.cs"] = "E71FF2D4CD995261E72280B5D9625986F92E324EE30014F004A9F531304CEEE2",
        ["View.cs"] = "FB1503C0D52615EEB1CF2F48A0D42EDF47DFD446831B8531D9706F0E03DCD5"
    };

    string safeName = SafeProjectSegment(projectName);
    string destinationRoot = Path.Combine(output, "Userland", safeName, "Commands");
    Directory.CreateDirectory(destinationRoot);

    foreach (string source in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly))
    {
        string fileName = Path.GetFileName(source);
        string destination = Path.Combine(destinationRoot, fileName);

        if (File.Exists(destination))
        {
            string destinationHash = Sha256File(destination);
            bool isLegacyStock = legacyStockHashes.TryGetValue(fileName, out string? legacyHash) &&
                                 string.Equals(destinationHash, legacyHash, StringComparison.OrdinalIgnoreCase);
            bool isBrokenDirStock = string.Equals(fileName, "Dir.cs", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(destinationHash, "180E5634A08D7C06850846C3DD6F986F0991F9C8C7B60C2CD775B5FED46F80D5", StringComparison.OrdinalIgnoreCase);
            if (isLegacyStock || isBrokenDirStock)
            {
                File.Copy(source, destination, true);
                Console.WriteLine($"[ OK ] Refreshed unmodified stock command from canonical source: {destination}");
            }
            continue;
        }

        File.Copy(source, destination, false);
        Console.WriteLine($"[ OK ] Added coder-visible command source: {destination}");
    }

    return true;
}

static string Sha256File(string path)
{
    return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

static void EnsureProvidedWorkspaceFoldersVisible(string output)
{
    try
    {
        string directory = Path.Combine(output, ".theia");
        string file = Path.Combine(directory, "settings.json");
        Dictionary<string, object?> settings = new(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(file))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                foreach (JsonProperty property in doc.RootElement.EnumerateObject()) settings[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText());
            }
            catch { }
        }
        Dictionary<string, bool> excludes = new(StringComparer.OrdinalIgnoreCase);
        if (settings.TryGetValue("files.exclude", out object? existing) && existing is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.True || property.Value.ValueKind == JsonValueKind.False)
                    excludes[property.Name] = property.Value.GetBoolean();
        }
        excludes.Remove("**/Provided");
        settings["files.exclude"] = excludes;
        Directory.CreateDirectory(directory);
        File.WriteAllText(file, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
    catch { }
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

static Exception Fail(string message) => new InvalidOperationException(message);
