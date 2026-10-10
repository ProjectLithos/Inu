using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    if (!MaterializeCoderCommands(sdkRoot, output, projectName)) return 1;
    MigrateGeneratedShellSurface(output, projectName);
    RepairMalformedGeneratedStockShellSurface(output, projectName);
    MigrateGeneratedCommandSurface(output, projectName, sdkRoot);
    HideProvidedWorkspaceFolders(output);
    if (!RefreshCentralKernelConsoleSourcePlan(sdkRoot, output)) return 1;

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

static bool FailBool(string message)
{
    Console.Error.WriteLine($"[FAIL] {message}");
    return false;
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

static string SafeProjectSegment(string value)
{
    char[] chars = value.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
    return chars.Length == 0 ? "OS" : new string(chars);
}

static void MigrateGeneratedShellSurface(string output, string projectName)
{
    string root = Path.Combine(output, "Userland", SafeProjectSegment(projectName));
    string file = Path.Combine(root, "Shell.cs");
    if (!File.Exists(file)) return;
    string source = File.ReadAllText(file);
    // Only migrate the stock 0.0.80 shell shape. Hand-written shells are never replaced.
    if (!source.Contains("private static Byte GetPathSeparator()", StringComparison.Ordinal) ||
        !source.Contains("UserlandSystem.Call(UserlandOperation.Get", StringComparison.Ordinal) ||
        !source.Contains("Coder-owned shell behaviour. Configure runs once", StringComparison.Ordinal)) return;

    string ns = "Generated.Userland";
    foreach (string line in source.Split('\n'))
    {
        string t = line.Trim();
        if (t.StartsWith("namespace ", StringComparison.Ordinal) && t.EndsWith(";", StringComparison.Ordinal))
        { ns = t[10..^1].Trim(); break; }
    }
    string prompt = "> ";
    const string promptNeedle = "public const string Prompt = \"";
    int promptStart = source.IndexOf(promptNeedle, StringComparison.Ordinal);
    if (promptStart >= 0)
    {
        promptStart += promptNeedle.Length; int promptEnd = source.IndexOf("\";", promptStart, StringComparison.Ordinal);
        if (promptEnd > promptStart) prompt = source[promptStart..promptEnd];
    }
    string configureBody = "        // Console.Clear();\n        // Console.WriteLine(\"Howdy\");\n        // ConsolePresentation.SetForegroundRgb(232,240,248);\n        // ConsolePresentation.SetBackgroundRgb(9,16,24);\n        // ConsolePresentation.SetCaretMode(ConsoleCaretMode.Blinking);\n        // ConsolePresentation.SetCaretHeightPercent(8);";
    int configure = source.IndexOf("public static void Configure()", StringComparison.Ordinal);
    if (configure >= 0)
    {
        int open = source.IndexOf('{', configure); if (open >= 0)
        {
            int depth = 1, cursor = open + 1;
            while (cursor < source.Length && depth != 0) { if (source[cursor] == '{') depth++; else if (source[cursor] == '}') depth--; cursor++; }
            if (depth == 0) configureBody = source[(open + 1)..(cursor - 1)].Trim('\r','\n');
        }
    }
    configureBody = configureBody.Replace("'\\'", "'\\\\'", StringComparison.Ordinal);
    string escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"");
    string migrated = $"using System;\nusing Inu.Userland.Runtime;\n\nnamespace {ns};\n\n" +
        "/// <summary>Coder-owned shell behaviour. Configure runs once for the lifetime of the shell; Run owns the visible command loop.</summary>\n" +
        "public static class Shell\n{\n" +
        $"    public const string Prompt = \"{escapedPrompt}\";\n\n" +
        "    public static void Configure()\n    {\n" + configureBody + "\n    }\n\n" +
        "    public static void Run()\n    {\n        while (true)\n        {\n" +
        "            Console.Write(Prompt);\n            String input = Console.ReadLine();\n            if (String.IsNullOrWhiteSpace(input)) continue;\n" +
        "            Int32 start=0,end=input.Length; while(start<end&&input[start]==' ')start++; while(end>start&&input[end-1]==' ')end--; if(start==end)continue;\n" +
        "            Int32 split=start;while(split<end&&input[split]!=' ')split++;Int32 argumentStart=split;while(argumentStart<end&&input[argumentStart]==' ')argumentStart++;\n" +
        "            String command=input.Substring(start,split-start);String arguments=argumentStart<end?input.Substring(argumentStart,end-argumentStart):String.Empty;\n" +
        "            String[] candidates=FileSystemPaths.BuildCommandsPath(command);Boolean launched=false;\n" +
        "            for(Int32 index=0;index<candidates.Length;index++)if(Process.TryStart(candidates[index],arguments)){launched=true;break;}\n" +
        "            if(!launched)Console.WriteLine(\"Command not found.\");\n        }\n    }\n}\n";
    File.WriteAllText(file, migrated);
    Console.WriteLine($"[ OK ] Migrated stock shell source to high-level SDK APIs: {file}");
}

static void RepairMalformedGeneratedStockShellSurface(string output, string projectName)
{
    string root = Path.Combine(output, "Userland", SafeProjectSegment(projectName));
    string file = Path.Combine(root, "Shell.cs");
    if (!File.Exists(file)) return;
    string source = File.ReadAllText(file);
    if (!source.Contains("Coder-owned shell behaviour. Configure runs once", StringComparison.Ordinal)) return;
    if (!HasUnterminatedCSharpLiteral(source)) return;

    string ns = "Generated.Userland";
    foreach (string line in source.Split('\n'))
    {
        string t = line.Trim();
        if (t.StartsWith("namespace ", StringComparison.Ordinal) && t.EndsWith(";", StringComparison.Ordinal))
        { ns = t[10..^1].Trim(); break; }
    }

    string prompt = "> ";
    const string promptNeedle = "public const string Prompt = \"";
    int promptStart = source.IndexOf(promptNeedle, StringComparison.Ordinal);
    if (promptStart >= 0)
    {
        promptStart += promptNeedle.Length;
        int promptEnd = source.IndexOf("\";", promptStart, StringComparison.Ordinal);
        if (promptEnd > promptStart) prompt = source[promptStart..promptEnd];
    }

    string configureBody = "        // Console.Clear();\n        // Console.WriteLine(\"Howdy\");\n        // ConsolePresentation.SetForegroundRgb(232,240,248);\n        // ConsolePresentation.SetBackgroundRgb(9,16,24);\n        // ConsolePresentation.SetCaretMode(ConsoleCaretMode.Blinking);\n        // ConsolePresentation.SetCaretHeightPercent(8);";
    int configure = source.IndexOf("public static void Configure()", StringComparison.Ordinal);
    if (configure >= 0)
    {
        int open = source.IndexOf('{', configure);
        int run = source.IndexOf("public static void Run()", configure, StringComparison.Ordinal);
        if (open >= 0 && (run < 0 || open < run))
        {
            int depth = 1, cursor = open + 1;
            while (cursor < source.Length && depth != 0)
            {
                if (source[cursor] == '{') depth++;
                else if (source[cursor] == '}') depth--;
                cursor++;
            }
            if (depth == 0 && (run < 0 || cursor <= run))
                configureBody = source[(open + 1)..(cursor - 1)].Trim('\r','\n');
        }
    }

    configureBody = configureBody.Replace("'\\'", "'\\\\'", StringComparison.Ordinal);
    string escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"");
    string repaired = $"using System;\nusing Inu.Userland.Runtime;\n\nnamespace {ns};\n\n" +
        "/// <summary>Coder-owned shell behaviour. Configure runs once for the lifetime of the shell; Run owns the visible command loop.</summary>\n" +
        "public static class Shell\n{\n" +
        $"    public const string Prompt = \"{escapedPrompt}\";\n\n" +
        "    public static void Configure()\n    {\n" + configureBody + "\n    }\n\n" +
        "    public static void Run()\n    {\n        while (true)\n        {\n" +
        "            Console.Write(Prompt);\n            String input = Console.ReadLine();\n            if (String.IsNullOrWhiteSpace(input)) continue;\n" +
        "            Int32 start=0,end=input.Length; while(start<end&&input[start]==' ')start++; while(end>start&&input[end-1]==' ')end--; if(start==end)continue;\n" +
        "            Int32 split=start;while(split<end&&input[split]!=' ')split++;Int32 argumentStart=split;while(argumentStart<end&&input[argumentStart]==' ')argumentStart++;\n" +
        "            String command=input.Substring(start,split-start);String arguments=argumentStart<end?input.Substring(argumentStart,end-argumentStart):String.Empty;\n" +
        "            String[] candidates=FileSystemPaths.BuildCommandsPath(command);Boolean launched=false;\n" +
        "            for(Int32 index=0;index<candidates.Length;index++)if(Process.TryStart(candidates[index],arguments)){launched=true;break;}\n" +
        "            if(!launched)Console.WriteLine(\"Command not found.\");\n        }\n    }\n}\n";
    File.WriteAllText(file, repaired);
    Console.WriteLine($"[ OK ] Repaired malformed stock generated shell source: {file}");
}

static bool HasUnterminatedCSharpLiteral(string source)
{
    bool inBlockComment=false;
    foreach (string rawLine in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
    {
        bool inString=false, inChar=false, escape=false;
        for (int i=0;i<rawLine.Length;i++)
        {
            char c=rawLine[i]; char next=i+1<rawLine.Length?rawLine[i+1]:'\0';
            if (inBlockComment)
            {
                if (c=='*'&&next=='/') { inBlockComment=false; i++; }
                continue;
            }
            if (inString)
            {
                if (escape) { escape=false; continue; }
                if (c=='\\') { escape=true; continue; }
                if (c=='\"') { inString=false; continue; }
                continue;
            }
            if (inChar)
            {
                if (escape) { escape=false; continue; }
                if (c=='\\') { escape=true; continue; }
                if (c=='\'') { inChar=false; continue; }
                continue;
            }
            if (c=='/'&&next=='/') break;
            if (c=='/'&&next=='*') { inBlockComment=true; i++; continue; }
            if (c=='\"') { inString=true; continue; }
            if (c=='\'') { inChar=true; continue; }
        }
        if (inString||inChar) return true;
    }
    return false;
}

static void MigrateGeneratedCommandSurface(string output, string projectName, string sdkRoot)
{
    string root = Path.Combine(output, "Userland", SafeProjectSegment(projectName), "Commands");
    if (!Directory.Exists(root)) return;

    foreach (string destination in Directory.EnumerateFiles(root, "*.cs", SearchOption.TopDirectoryOnly))
    {
        string current = File.ReadAllText(destination);
        Match currentNamespace = Regex.Match(current, @"\bnamespace\s+(KathInu\.[A-Za-z_][A-Za-z0-9_]*\.Userland\.Commands)\s*;");
        if (!currentNamespace.Success) continue;

        // Kath generated this OS-name namespace for stock commands in older projects.
        // Only the namespace declaration changes; the coder-owned command body is preserved byte-for-byte otherwise.
        string migrated = current.Remove(currentNamespace.Groups[1].Index, currentNamespace.Groups[1].Length)
                                 .Insert(currentNamespace.Groups[1].Index, "Inu.Userland.Commands");
        File.WriteAllText(destination, migrated);
        Console.WriteLine($"[ OK ] Migrated command namespace to reusable Inu.Userland.Commands: {destination}");
    }
}

static bool MaterializeCoderCommands(string sdkRoot, string output, string projectName)
{
    string sourceRoot = Path.Combine(sdkRoot, "src", "Userland", "Commands");
    if (!Directory.Exists(sourceRoot)) return true;
    string safeName = SafeProjectSegment(projectName);
    string destinationRoot = Path.Combine(output, "Userland", safeName, "Commands");
    Directory.CreateDirectory(destinationRoot);
    foreach (string source in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly))
    {
        string destination = Path.Combine(destinationRoot, Path.GetFileName(source));
        if (File.Exists(destination)) continue;
        File.Copy(source, destination, false);
        Console.WriteLine($"[ OK ] Added coder-visible command source: {destination}");
    }
    return true;
}

static void HideProvidedWorkspaceFolders(string output)
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
        Dictionary<string, bool> excludes = new(StringComparer.OrdinalIgnoreCase) { ["**/Provided"] = true };
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

static int Fail(string message)
{
    Console.Error.WriteLine($"[FAIL] {message}");
    return 1;
}
