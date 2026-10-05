using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inu.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "link", StringComparison.OrdinalIgnoreCase))
    {
        return Fail("Usage: Inu.Linker link <InuProject.json> --lld-link <path> --llvm-nm <path> [--native-root <path>] [--dry-run]");
    }

    if (!InuProject.TryLoad(args[1], out InuProject? project, out string error) || project is null)
    {
        return Fail(error);
    }

    string? lld = GetOption(args, "--lld-link");
    string? llvmNm = GetOption(args, "--llvm-nm");
    if (string.IsNullOrWhiteSpace(lld) || string.IsNullOrWhiteSpace(llvmNm))
    {
        return Fail("lld-link and llvm-nm are required.");
    }

    string nativeRoot = GetOption(args, "--native-root") ?? Path.Combine(Environment.CurrentDirectory, "Artifacts", "Native", "x64");
    bool dryRun = HasOption(args, "--dry-run");
    string manifestPath = Path.Combine(project.OutputDirectory, "Inu.Compile.json");
    if (!File.Exists(manifestPath))
    {
        return Fail($"Compilation manifest not found: {manifestPath}");
    }

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
    JsonElement root = document.RootElement;
    const int SupportedCompilationManifestSchema = 7;
    int schemaVersion = root.GetProperty("schemaVersion").GetInt32();
    if (schemaVersion != SupportedCompilationManifestSchema)
    {
        return Fail($"Unsupported compilation manifest schema: {schemaVersion}. Supported schema: {SupportedCompilationManifestSchema}.");
    }
    string runtimeMode = root.GetProperty("runtimeMode").GetString() ?? string.Empty;
    if (!string.Equals(runtimeMode, "BootstrapToManagedNativeAot", StringComparison.Ordinal))
    {
        return Fail($"Unsupported runtime mode: {runtimeMode}");
    }
    string nativeObject = root.GetProperty("nativeObject").GetString() ?? string.Empty;
    if (!File.Exists(nativeObject))
    {
        return Fail($"ILC native object not found: {nativeObject}");
    }
    if (root.GetProperty("windowsRuntimeLibraries").GetInt32() != 0)
    {
        return Fail("The freestanding Inu runtime must not link Windows runtime libraries.");
    }
    bool nativeDebugSymbols = root.TryGetProperty("nativeDebugSymbols", out JsonElement debugElement) && debugElement.GetBoolean();

    ProcessResult symbols = Capture(llvmNm, ["--defined-only", nativeObject]);
    if (symbols.ExitCode != 0 || !symbols.Output.Contains("InuManagedEntry", StringComparison.Ordinal))
    {
        Console.Error.Write(symbols.Output);
        return Fail("ILC output does not export InuManagedEntry for KMain.");
    }
    if (!symbols.Output.Contains("__ReadyToRunHeader", StringComparison.Ordinal))
    {
        Console.Error.Write(symbols.Output);
        return Fail("ILC output does not define __ReadyToRunHeader; NativeAOT module/static metadata cannot be initialized.");
    }

    string entry = Path.Combine(nativeRoot, "Entry.obj");
    string cpu = Path.Combine(nativeRoot, "Cpu.obj");
    string runtime = Path.Combine(nativeRoot, "Runtime.obj");
    string exceptionHandling = Path.Combine(nativeRoot, "ExceptionHandling.obj");
    string descriptors = Path.Combine(nativeRoot, "Descriptors.obj");
    string interrupts = Path.Combine(nativeRoot, "Interrupts.obj");
    string interruptControllers = Path.Combine(nativeRoot, "InterruptControllers.obj");
    string paging = Path.Combine(nativeRoot, "Paging.obj");
    string syscalls = Path.Combine(nativeRoot, "Syscalls.obj");
    string userMode = Path.Combine(nativeRoot, "UserMode.obj");
    string consoleFont = Path.Combine(nativeRoot, "ConsoleFont.obj");
    string bootLoader = Path.Combine(nativeRoot, "BootLoader.obj");
    foreach (string file in new[] { entry, cpu, runtime, exceptionHandling, descriptors, interrupts, interruptControllers, paging, syscalls, userMode, consoleFont, bootLoader })
    {
        if (!File.Exists(file))
        {
            return Fail($"Native object not found: {file}");
        }
    }

    string output = Path.Combine(project.OutputDirectory, project.Name + ".bin");
    string bootOutput = Path.Combine(project.OutputDirectory, project.Name + ".bootx64.efi");
    string map = Path.Combine(project.OutputDirectory, project.Name + ".map");
    string pdb = Path.Combine(project.OutputDirectory, project.Name + ".pdb");
    List<string> linkArguments =
    [
        "/nologo",
        "/subsystem:native",
        "/machine:x64",
        "/nodefaultlib",
        "/entry:InuKernelEntry",
        "/base:0x0000000001000000",
        "/dynamicbase",
        "/merge:.modules=.rdata",
        "/errorlimit:64",
        $"/out:{output}",
        $"/map:{map}"
    ];
    if (nativeDebugSymbols)
    {
        linkArguments.Add("/debug:full");
        linkArguments.Add($"/pdb:{pdb}");
    }
    linkArguments.AddRange([entry, cpu, runtime, exceptionHandling, descriptors, interrupts, interruptControllers, paging, syscalls, userMode, consoleFont]);

    // 0.0.77: NativeAOT emits module registration and GC-static metadata in
    // archive members that do not necessarily satisfy an ordinary unresolved
    // symbol.  The freestanding Inu host must retain those members so the
    // .modules$I records land between __modules_a/__modules_z and
    // InitializeModules can discover every managed module.
    if (String.Equals(Path.GetExtension(nativeObject), ".lib", StringComparison.OrdinalIgnoreCase))
    {
        linkArguments.Add($"/wholearchive:{nativeObject}");
        Console.WriteLine("[INFO] NativeAOT archive retention: /wholearchive enabled for module/static metadata.");
    }
    else
    {
        linkArguments.Add(nativeObject);
    }

    Console.WriteLine("[INFO] Linking the Inu kernel as a relocatable PE32+ native image (not an EFI application).");
    Console.WriteLine("[INFO] Windows NativeAOT runtime libraries linked: 0");
    if (nativeDebugSymbols)
    {
        Console.WriteLine("[INFO] Native source debugging: CodeView/PDB link enabled.");
    }
    if (dryRun)
    {
        return 0;
    }

    ProcessResult result = Capture(lld, linkArguments);
    Console.Write(result.Output);
    if (result.ExitCode != 0)
    {
        return Fail($"LLD failed with exit code {result.ExitCode}.");
    }
    Console.WriteLine($"[ OK ] Freestanding non-EFI kernel image: {output}");
    Console.WriteLine($"[ OK ] Link map: {map}");
    if (!TryGetPeBaseRelocationDirectory(output, out ulong kernelImageBase, out uint kernelRelocationRva, out uint kernelRelocationSize) ||
        kernelRelocationRva == 0U || kernelRelocationSize < 8U)
    {
        return Fail("KERNEL.BIN has no usable PE base-relocation directory. The FAT32 loader must be able to place the native kernel outside its preferred 0x01000000 address when firmware occupies that range.");
    }
    Console.WriteLine($"[ OK ] Native kernel relocation directory: RVA 0x{kernelRelocationRva:x}, {kernelRelocationSize} byte(s); preferred base 0x{kernelImageBase:x}");

    List<string> bootLinkArguments =
    [
        "/nologo",
        "/subsystem:efi_application",
        "/machine:x64",
        "/nodefaultlib",
        "/entry:InuBootLoaderEntry",
        "/base:0x00400000",
        "/dynamicbase",
        "/errorlimit:64",
        $"/out:{bootOutput}",
        bootLoader
    ];
    ProcessResult bootResult = Capture(lld, bootLinkArguments);
    Console.Write(bootResult.Output);
    if (bootResult.ExitCode != 0)
    {
        return Fail($"UEFI loader link failed with exit code {bootResult.ExitCode}.");
    }
    if (!TryGetPeBaseRelocationDirectory(bootOutput, out ulong bootImageBase, out uint bootRelocationRva, out uint bootRelocationSize) ||
        bootRelocationRva == 0U || bootRelocationSize < 8U)
    {
        return Fail("UEFI loader has no usable PE base-relocation directory. BOOTX64.EFI must remain relocatable so OVMF can load it below the PE preferred image base in small-memory guests.");
    }
    Console.WriteLine($"[ OK ] UEFI FAT32 kernel loader: {bootOutput}");
    Console.WriteLine($"[ OK ] UEFI loader relocation directory: RVA 0x{bootRelocationRva:x}, {bootRelocationSize} byte(s); preferred base 0x{bootImageBase:x}");

    if (nativeDebugSymbols)
    {
        if (!File.Exists(pdb))
        {
            return Fail($"Debug link did not produce the expected native PDB: {pdb}");
        }

        string toolDirectory = Path.GetDirectoryName(Path.GetFullPath(llvmNm)) ?? string.Empty;
        string llvmObjdump = Path.Combine(toolDirectory, "llvm-objdump.exe");
        string llvmSymbolizer = Path.Combine(toolDirectory, "llvm-symbolizer.exe");
        string llvmPdbUtil = Path.Combine(toolDirectory, "llvm-pdbutil.exe");
        if (!File.Exists(llvmObjdump) || !File.Exists(llvmSymbolizer) || !File.Exists(llvmPdbUtil))
        {
            return Fail($"LLVM source-debug tools are required for Debug builds. Expected: {llvmObjdump}, {llvmSymbolizer}, and {llvmPdbUtil}");
        }

        string entryObject = Path.Combine(nativeRoot, "Entry.obj");
        if (!TryGetSymbolAddress(llvmNm, entryObject, "InuDebugImageAnchor", out ulong anchorObjectAddress) ||
            !TryGetSymbolAddress(llvmNm, entryObject, "InuDebugResume", out ulong resumeObjectAddress) ||
            !TryGetSymbolAddress(llvmNm, entryObject, "InuKernelEntry", out ulong entryObjectAddress) ||
            anchorObjectAddress < entryObjectAddress || resumeObjectAddress <= anchorObjectAddress)
        {
            return Fail("Debug Entry.obj does not contain a valid InuDebugImageAnchor/InuDebugResume rendezvous. Rebuild Entry.asm with INU_DEBUG.");
        }

        // The FAT32 loader passes the preloaded system-asset catalogue in volatile R8/R9.
        // InuKernelEntry must save those arguments before entering the debugger
        // rendezvous, so InuDebugImageAnchor is intentionally allowed to sit after
        // (rather than exactly on) the PE entry point. Final PE/COFF images produced by
        // lld-link do not necessarily retain a COFF symbol table that llvm-nm can enumerate.
        // Derive the linked rendezvous addresses from the PE32+ entry point plus the
        // corresponding Entry.obj symbol offsets.
        if (!TryGetPeEntrypointAddress(output, out ulong linkedEntryAddress))
        {
            return Fail("Debug kernel image entry point could not be read from the PE32+ header.");
        }

        ulong anchorAddress;
        ulong resumeAddress;
        try
        {
            anchorAddress = checked(linkedEntryAddress + (anchorObjectAddress - entryObjectAddress));
            resumeAddress = checked(linkedEntryAddress + (resumeObjectAddress - entryObjectAddress));
        }
        catch (OverflowException)
        {
            return Fail("Debug rendezvous symbol offsets overflow the linked kernel address space.");
        }

        string sourceMap = Path.Combine(project.OutputDirectory, "Inu.DebugSymbols.json");
        string kernelSha256;
        string mapSha256;
        using (FileStream kernel = File.OpenRead(output))
            kernelSha256 = Convert.ToHexString(SHA256.HashData(kernel)).ToLowerInvariant();
        using (FileStream linkMap = File.OpenRead(map))
            mapSha256 = Convert.ToHexString(SHA256.HashData(linkMap)).ToLowerInvariant();
        SourceLineEntry[] entries = BuildSourceLineMap(llvmObjdump, llvmSymbolizer, llvmPdbUtil, output, pdb);
        if (entries.Length == 0)
        {
            return Fail("Native PDB contains no source line mappings. ILC Debug compilation must preserve managed sequence points.");
        }

        File.WriteAllText(sourceMap, JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            productVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
            image = Path.GetFullPath(output),
            kernelSha256,
            map = Path.GetFullPath(map),
            mapSha256,
            pdb = Path.GetFullPath(pdb),
            anchor = new
            {
                symbol = "InuDebugImageAnchor",
                linkedAddress = $"0x{anchorAddress:x}",
                resumeSymbol = "InuDebugResume",
                resumeLinkedAddress = $"0x{resumeAddress:x}",
                transport = "qemu-debugcon-0xe9-binary-v1"
            },
            entries,
            producedUtc = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        Console.WriteLine($"[ OK ] Native debug PDB: {pdb}");
        Console.WriteLine($"[ OK ] Source-line debug map: {sourceMap} ({entries.Length} line mapping(s))");
        Console.WriteLine($"[ OK ] Debug relocation rendezvous: anchor 0x{anchorAddress:x}, resume 0x{resumeAddress:x}");
    }

    return 0;
}

static SourceLineEntry[] BuildSourceLineMap(string llvmObjdump, string llvmSymbolizer, string llvmPdbUtil, string image, string pdb)
{
    // PDB CodeView line tables already contain exactly the information Inu
    // needs for source breakpoints: source file, source line and section-relative
    // code address. Reading that table is O(number of sequence points), whereas
    // symbolizing every disassembled instruction is O(number of instructions) and
    // made even modest kernels feed 100,000+ addresses through llvm-symbolizer.
    //
    // llvm-pdbutil dump -l is the primary path. It uses LLVM's native PDB reader,
    // so it does not require DIA and does not need to repeatedly query the PDB.
    if (TryBuildSourceLineMapFromPdb(llvmPdbUtil, image, pdb, out SourceLineEntry[] pdbEntries, out string pdbDiagnostic))
    {
        Console.WriteLine($"[ OK ] Direct PDB source-line extraction: {pdbEntries.Length} mapping(s). No per-instruction symbolization required.");
        return pdbEntries;
    }

    Console.WriteLine($"[WARN] Direct PDB line-table extraction was unavailable: {pdbDiagnostic}");
    Console.WriteLine("[INFO] Falling back to bounded llvm-symbolizer source mapping.");

    ProcessResult disassembly = Capture(llvmObjdump, ["-d", "--no-show-raw-insn", image]);
    if (disassembly.ExitCode != 0)
    {
        throw new InvalidOperationException($"llvm-objdump failed while generating the source debug map: {disassembly.Output}");
    }

    List<string> addresses = [];
    foreach (string line in disassembly.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
    {
        ReadOnlySpan<char> span = line.AsSpan().TrimStart();
        int colon = span.IndexOf(':');
        if (colon <= 0)
        {
            continue;
        }
        ReadOnlySpan<char> token = span[..colon];
        if (ulong.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value))
        {
            addresses.Add($"0x{value:x}");
        }
    }
    if (addresses.Count == 0)
    {
        throw new InvalidOperationException("llvm-objdump did not report any executable instruction addresses.");
    }

    // The fallback is intentionally bounded. A missing/corrupt line table must not
    // make a Debug build spend minutes symbolizing an entire kernel instruction by
    // instruction. For small images retain exact legacy behaviour; for large images
    // fail with a useful diagnostic so the PDB problem can be fixed directly.
    const int MaxFallbackAddresses = 20000;
    if (addresses.Count > MaxFallbackAddresses)
    {
        throw new InvalidOperationException(
            $"The native PDB line table could not be read and the legacy symbolizer fallback would require {addresses.Count} instruction addresses (safety limit {MaxFallbackAddresses}). " +
            "Inu stopped source-map generation instead of appearing to hang. " + pdbDiagnostic);
    }

    ProcessResult symbolizerHelp = Capture(llvmSymbolizer, ["--help"]);
    bool supportsExplicitPdb = symbolizerHelp.Output.Contains("--pdb", StringComparison.Ordinal);
    Console.WriteLine(supportsExplicitPdb
        ? "[INFO] llvm-symbolizer explicit PDB override: supported."
        : "[INFO] llvm-symbolizer explicit PDB override: unavailable; using the EFI CodeView PDB reference.");

    List<string> arguments =
    [
        "--output-style=JSON",
        "--no-inlines",
        $"--obj={image}"
    ];
    if (supportsExplicitPdb)
    {
        arguments.Add($"--pdb={pdb}");
    }

    string symbolizerInput = string.Join(Environment.NewLine, addresses) + Environment.NewLine;
    Console.WriteLine($"[INFO] Fallback source mapping for {addresses.Count} native instruction address(es) (60 second timeout).");
    ProcessResult symbolized = CaptureWithInput(llvmSymbolizer, arguments, symbolizerInput, 60000);
    if (symbolized.TimedOut)
    {
        throw new InvalidOperationException(
            $"llvm-symbolizer timed out after 60 seconds while generating the fallback source debug map for {addresses.Count} addresses. The process was terminated. Output: {symbolized.Output}");
    }
    if (symbolized.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"llvm-symbolizer failed while generating the fallback source debug map (exit {symbolized.ExitCode}): {symbolized.Output}");
    }

    Dictionary<string, SourceLineEntry> unique = new(StringComparer.OrdinalIgnoreCase);
    AddSymbolizerJson(symbolized.Output, unique);
    return unique.Values
        .OrderBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Line)
        .ThenBy(entry => ParseHexAddress(entry.LinkedAddress))
        .ToArray();
}

static bool TryBuildSourceLineMapFromPdb(string llvmPdbUtil, string image, string pdb, out SourceLineEntry[] entries, out string diagnostic)
{
    entries = [];
    diagnostic = string.Empty;

    if (!TryGetPeSectionLayout(image, out ulong imageBase, out ulong[] sectionRvas, out diagnostic))
    {
        return false;
    }

    ProcessResult result = CaptureWithInput(llvmPdbUtil, ["dump", "-l", pdb], null, 30000);
    if (result.TimedOut)
    {
        diagnostic = "llvm-pdbutil timed out after 30 seconds while reading the CodeView line table.";
        return false;
    }
    if (result.ExitCode != 0)
    {
        diagnostic = $"llvm-pdbutil dump -l failed with exit code {result.ExitCode}: {TrimDiagnostic(result.Output)}";
        return false;
    }

    Dictionary<string, SourceLineEntry> unique = new(StringComparer.OrdinalIgnoreCase);
    string? currentSource = null;
    int currentSection = 0;

    Regex sourceRegex = new(@"^\s*(?<path>.+?)\s+\((?:SHA-256|SHA-1|MD5):\s*[0-9A-Fa-f]+\)\s*$", RegexOptions.CultureInvariant);
    Regex sourceNoChecksumRegex = new(@"^\s*(?<path>.+?)\s+\(no checksum\)\s*$", RegexOptions.CultureInvariant);
    Regex blockRegex = new(@"^\s*(?<segment>[0-9A-Fa-f]{4}):(?<begin>[0-9A-Fa-f]{8})-(?<end>[0-9A-Fa-f]{8}),", RegexOptions.CultureInvariant);
    Regex lineRegex = new(@"(?<!\S)(?<line>\d+|NSI)\s+(?<offset>[0-9A-Fa-f]{8})\s+[ !](?=\s|$)", RegexOptions.CultureInvariant);

    foreach (string raw in result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
    {
        Match source = sourceRegex.Match(raw);
        if (!source.Success)
        {
            source = sourceNoChecksumRegex.Match(raw);
        }
        if (source.Success)
        {
            currentSource = NormalizeSourcePath(source.Groups["path"].Value.Trim());
            continue;
        }

        Match block = blockRegex.Match(raw);
        if (block.Success)
        {
            currentSection = int.Parse(block.Groups["segment"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            continue;
        }

        if (string.IsNullOrWhiteSpace(currentSource) || currentSection <= 0 || currentSection > sectionRvas.Length)
        {
            continue;
        }

        foreach (Match line in lineRegex.Matches(raw))
        {
            if (string.Equals(line.Groups["line"].Value, "NSI", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(line.Groups["line"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lineNumber) ||
                lineNumber <= 0 ||
                !ulong.TryParse(line.Groups["offset"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong sectionOffset))
            {
                continue;
            }

            ulong linkedAddress = checked(imageBase + sectionRvas[currentSection - 1] + sectionOffset);
            string key = currentSource + "|" + lineNumber.ToString(CultureInfo.InvariantCulture);
            if (!unique.TryGetValue(key, out SourceLineEntry? existing) || linkedAddress < ParseHexAddress(existing.LinkedAddress))
            {
                unique[key] = new SourceLineEntry(currentSource, lineNumber, $"0x{linkedAddress:x}");
            }
        }
    }

    if (unique.Count == 0)
    {
        diagnostic = "llvm-pdbutil returned no parseable source line/address records. The NativeAOT PDB may not contain DEBUG_S_LINES records.";
        return false;
    }

    entries = unique.Values
        .OrderBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Line)
        .ThenBy(entry => ParseHexAddress(entry.LinkedAddress))
        .ToArray();
    return true;
}

static bool TryGetPeSectionLayout(string image, out ulong imageBase, out ulong[] sectionRvas, out string diagnostic)
{
    imageBase = 0;
    sectionRvas = [];
    diagnostic = string.Empty;
    try
    {
        using FileStream stream = File.OpenRead(image);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        if (stream.Length < 0x100)
        {
            diagnostic = "EFI image is too small to contain a PE32+ section table.";
            return false;
        }
        stream.Position = 0x3c;
        int peOffset = reader.ReadInt32();
        if (peOffset < 0 || peOffset + 24 > stream.Length)
        {
            diagnostic = "EFI image has an invalid PE header offset.";
            return false;
        }
        stream.Position = peOffset;
        if (reader.ReadUInt32() != 0x00004550)
        {
            diagnostic = "EFI image does not contain a PE signature.";
            return false;
        }
        reader.ReadUInt16(); // Machine
        ushort sectionCount = reader.ReadUInt16();
        stream.Position += 12; // timestamp, symbol table, symbol count
        ushort optionalHeaderSize = reader.ReadUInt16();
        reader.ReadUInt16(); // characteristics
        long optionalHeader = stream.Position;
        if (optionalHeader + optionalHeaderSize > stream.Length || optionalHeaderSize < 32)
        {
            diagnostic = "EFI image has an invalid PE optional header.";
            return false;
        }
        ushort magic = reader.ReadUInt16();
        if (magic != 0x20b)
        {
            diagnostic = $"EFI debug image is not PE32+ (optional-header magic 0x{magic:x}).";
            return false;
        }
        stream.Position = optionalHeader + 24;
        imageBase = reader.ReadUInt64();

        long sectionTable = optionalHeader + optionalHeaderSize;
        if (sectionCount == 0 || sectionTable + (sectionCount * 40L) > stream.Length)
        {
            diagnostic = "EFI image has an invalid PE section table.";
            return false;
        }

        sectionRvas = new ulong[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            stream.Position = sectionTable + (i * 40L) + 12;
            sectionRvas[i] = reader.ReadUInt32();
        }
        return true;
    }
    catch (Exception ex)
    {
        diagnostic = $"Could not read PE section layout: {ex.Message}";
        return false;
    }
}

static string NormalizeSourcePath(string path)
{
    try { return Path.GetFullPath(path); } catch { return path; }
}

static string TrimDiagnostic(string value)
{
    string text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    return text.Length <= 800 ? text : text[..800] + "...";
}

static void AddSymbolizerJson(string output, Dictionary<string, SourceLineEntry> unique)
{
    if (string.IsNullOrWhiteSpace(output))
    {
        return;
    }

    try
    {
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in root.EnumerateArray())
            {
                AddSymbolizerItem(item, unique);
            }
            return;
        }
        if (root.ValueKind == JsonValueKind.Object)
        {
            AddSymbolizerItem(root, unique);
            return;
        }
    }
    catch (JsonException)
    {
        // Capture() combines stdout and stderr. If an LLVM build emits a warning
        // beside otherwise valid JSON, recover the JSON array before falling back
        // to line-oriented parsing.
        int arrayStart = output.IndexOf('[');
        int arrayEnd = output.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            try
            {
                using JsonDocument arrayDocument = JsonDocument.Parse(output[arrayStart..(arrayEnd + 1)]);
                if (arrayDocument.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in arrayDocument.RootElement.EnumerateArray())
                    {
                        AddSymbolizerItem(item, unique);
                    }
                    return;
                }
            }
            catch (JsonException)
            {
                // Continue to the line-oriented fallback below.
            }
        }
    }

    foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            continue;
        }
        try
        {
            using JsonDocument item = JsonDocument.Parse(trimmed);
            AddSymbolizerItem(item.RootElement, unique);
        }
        catch (JsonException)
        {
            // Ignore diagnostic/non-result lines; the caller has already checked
            // llvm-symbolizer's exit code and will report genuine tool failures.
        }
    }
}

static void AddSymbolizerItem(JsonElement itemRoot, Dictionary<string, SourceLineEntry> unique)
{
    string addressText = itemRoot.TryGetProperty("Address", out JsonElement addressElement)
        ? addressElement.GetString() ?? string.Empty
        : string.Empty;
    if (string.IsNullOrWhiteSpace(addressText) ||
        !itemRoot.TryGetProperty("Symbol", out JsonElement symbolsElement) ||
        symbolsElement.ValueKind != JsonValueKind.Array ||
        symbolsElement.GetArrayLength() == 0)
    {
        return;
    }

    JsonElement symbol = symbolsElement[0];
    string sourcePath = symbol.TryGetProperty("FileName", out JsonElement fileElement)
        ? fileElement.GetString() ?? string.Empty
        : string.Empty;
    int lineNumber = symbol.TryGetProperty("Line", out JsonElement lineElement) && lineElement.TryGetInt32(out int parsedLine)
        ? parsedLine
        : 0;
    if (lineNumber <= 0 || string.IsNullOrWhiteSpace(sourcePath) || sourcePath == "??")
    {
        return;
    }

    ulong address = ParseHexAddress(addressText);
    string normalizedPath;
    try
    {
        normalizedPath = Path.GetFullPath(sourcePath);
    }
    catch (Exception)
    {
        return;
    }

    string key = $"{normalizedPath}|{lineNumber}";
    if (!unique.TryGetValue(key, out SourceLineEntry? existing) || address < ParseHexAddress(existing.LinkedAddress))
    {
        unique[key] = new SourceLineEntry(normalizedPath, lineNumber, $"0x{address:x}");
    }
}

static bool TryGetPeBaseRelocationDirectory(string image, out ulong imageBase, out uint relocationRva, out uint relocationSize)
{
    imageBase = 0;
    relocationRva = 0;
    relocationSize = 0;
    try
    {
        using FileStream stream = File.OpenRead(image);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        if (stream.Length < 0x100) return false;
        stream.Position = 0;
        if (reader.ReadUInt16() != 0x5A4D) return false;
        stream.Position = 0x3C;
        uint peOffset = reader.ReadUInt32();
        if (peOffset > stream.Length - 24) return false;
        stream.Position = peOffset;
        if (reader.ReadUInt32() != 0x00004550) return false;
        stream.Position = peOffset + 4 + 20;
        long optionalHeader = stream.Position;
        if (reader.ReadUInt16() != 0x20B) return false;
        stream.Position = optionalHeader + 24;
        imageBase = reader.ReadUInt64();
        stream.Position = optionalHeader + 108;
        uint directoryCount = reader.ReadUInt32();
        if (directoryCount <= 5U) return false;
        stream.Position = optionalHeader + 112 + (5 * 8);
        relocationRva = reader.ReadUInt32();
        relocationSize = reader.ReadUInt32();
        return true;
    }
    catch (Exception)
    {
        imageBase = 0;
        relocationRva = 0;
        relocationSize = 0;
        return false;
    }
}

static bool TryGetPeEntrypointAddress(string image, out ulong address)
{
    address = 0;
    try
    {
        using FileStream stream = File.OpenRead(image);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        if (stream.Length < 0x40)
        {
            return false;
        }

        stream.Position = 0;
        if (reader.ReadUInt16() != 0x5A4D) // MZ
        {
            return false;
        }

        stream.Position = 0x3C;
        uint peOffset = reader.ReadUInt32();
        if (peOffset > stream.Length - 4 - 20)
        {
            return false;
        }

        stream.Position = peOffset;
        if (reader.ReadUInt32() != 0x00004550) // PE\0\0
        {
            return false;
        }

        stream.Position = peOffset + 4 + 20;
        ushort magic = reader.ReadUInt16();
        if (magic != 0x20B) // PE32+
        {
            return false;
        }

        stream.Position = peOffset + 4 + 20 + 16;
        uint entryPointRva = reader.ReadUInt32();
        stream.Position = peOffset + 4 + 20 + 24;
        ulong imageBase = reader.ReadUInt64();
        address = checked(imageBase + entryPointRva);
        return true;
    }
    catch (Exception)
    {
        return false;
    }
}

static bool TryGetSymbolAddress(string llvmNm, string image, string symbolName, out ulong address)
{
    address = 0;
    ProcessResult result = Capture(llvmNm, ["--defined-only", image]);
    if (result.ExitCode != 0)
    {
        return false;
    }
    foreach (string line in result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
    {
        if (!line.Contains(symbolName, StringComparison.Ordinal))
        {
            continue;
        }
        string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string token = part.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? part[2..] : part;
            if (ulong.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address))
            {
                return true;
            }
        }
    }
    return false;
}

static ulong ParseHexAddress(string value)
{
    string token = value.Trim();
    if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        token = token[2..];
    }
    return ulong.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong address) ? address : 0;
}

static ProcessResult Capture(string executable, IEnumerable<string> arguments) =>
    CaptureWithInput(executable, arguments, null, 0);

static ProcessResult CaptureWithInput(string executable, IEnumerable<string> arguments, string? standardInput, int timeoutMilliseconds = 0)
{
    using Process process = new();
    process.StartInfo = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        RedirectStandardInput = standardInput is not null,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (string argument in arguments)
    {
        process.StartInfo.ArgumentList.Add(argument);
    }
    if (!process.Start())
    {
        return new ProcessResult(-1, string.Empty, false);
    }

    // Begin draining output before sending input. This prevents a child process that
    // emits diagnostics/results while reading stdin from deadlocking against full OS
    // pipe buffers. It is particularly important for llvm-symbolizer + NativeAOT PDBs.
    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
    Task<string> stderr = process.StandardError.ReadToEndAsync();
    Task? input = null;
    if (standardInput is not null)
    {
        input = Task.Run(async () =>
        {
            try
            {
                await process.StandardInput.WriteAsync(standardInput);
                await process.StandardInput.FlushAsync();
            }
            catch (IOException)
            {
                // If the child exits while input is still being written, preserve its
                // stdout/stderr and exit code instead of masking the useful diagnostic
                // with a broken-pipe exception.
            }
            finally
            {
                try { process.StandardInput.Close(); } catch (Exception) { }
            }
        });
    }

    bool timedOut = false;
    if (timeoutMilliseconds > 0)
    {
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            timedOut = true;
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            process.WaitForExit();
        }
    }
    else
    {
        process.WaitForExit();
    }

    if (input is not null)
    {
        try { input.Wait(5000); } catch (AggregateException) { }
    }
    Task.WaitAll(stdout, stderr);
    return new ProcessResult(timedOut ? -2 : process.ExitCode, stdout.Result + stderr.Result, timedOut);
}

static bool HasOption(ReadOnlySpan<string> args, string option)
{
    foreach (string value in args) if (string.Equals(value, option, StringComparison.OrdinalIgnoreCase)) return true;
    return false;
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

sealed record SourceLineEntry(string SourcePath, int Line, string LinkedAddress);
readonly record struct ProcessResult(int ExitCode, string Output, bool TimedOut);
