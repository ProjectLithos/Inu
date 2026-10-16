using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Inu.Host.Services;

public enum SdkExecutionArea { Boot, Kernel, Userland }

public sealed record SdkApiNamespace(string Namespace, SdkExecutionArea Area, IReadOnlyList<string> PublicItems);
public sealed record HostDiagnostic(string Id, string Severity, string Message, int Line, int Column);
public sealed record DiskImageDescriptor(string Name, string Path, long SizeBytes, string FormatHint, DateTime ModifiedUtc);

public static class InuHostServiceCollectionExtensions
{
    public static IServiceCollection AddInuHostServices(this IServiceCollection services, string inuRoot)
    {
        services.AddSingleton(new InuHostEnvironment(Path.GetFullPath(inuRoot)));
        services.AddSingleton<SdkApiCatalogService>();
        services.AddSingleton<RoslynAnalysisService>();
        services.AddSingleton<DiskImageService>();
        services.AddSingleton<HostModuleService>();
        services.AddSingleton<SdkValidationService>();
        return services;
    }
}

public sealed record InuHostEnvironment(string InuRoot)
{
    public string SdkRoot => Path.Combine(InuRoot, "SDK");
    public string DotNet => Path.Combine(InuRoot, ".toolchain", "DotNet", "dotnet.exe");
}

public sealed class HostModuleService(InuHostEnvironment environment)
{
    public int Run(string moduleName, IEnumerable<string> arguments, string configuration = "Release")
    {
        string module = Path.Combine(environment.SdkRoot, "src", moduleName, "bin", "x64", configuration, "net10.0", moduleName + ".dll");
        if (!File.Exists(environment.DotNet)) throw new FileNotFoundException("Inu private .NET host is missing.", environment.DotNet);
        if (!File.Exists(module)) throw new FileNotFoundException($"Inu host module '{moduleName}' has not been built for {configuration}.", module);
        using Process process = new();
        process.StartInfo = new ProcessStartInfo { FileName = environment.DotNet, WorkingDirectory = environment.SdkRoot, UseShellExecute = false };
        process.StartInfo.ArgumentList.Add(module);
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }
}

public sealed class SdkApiCatalogService(InuHostEnvironment environment)
{
    public IReadOnlyList<SdkApiNamespace> Read()
    {
        string contractPath = Path.Combine(environment.SdkRoot, "Inu.ApiContract.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(contractPath));
        var result = new List<SdkApiNamespace>();
        if (!document.RootElement.TryGetProperty("codingApi", out JsonElement codingApi) || !codingApi.TryGetProperty("exports", out JsonElement exports)) return result;
        foreach (JsonElement entry in exports.EnumerateArray())
        {
            if (entry.TryGetProperty("status", out JsonElement status) && status.GetString() == "planned") continue;
            string? ns = entry.TryGetProperty("namespace", out JsonElement nsNode) ? nsNode.GetString() : null;
            string? category = entry.TryGetProperty("category", out JsonElement categoryNode) ? categoryNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(ns) || category is null) continue;
            SdkExecutionArea? area = category switch { "Boot" => SdkExecutionArea.Boot, "Kernel" => SdkExecutionArea.Kernel, "Userland" or "Freestanding .NET" => SdkExecutionArea.Userland, _ => null };
            if (area is null) continue;
            var items = new List<string>();
            if (entry.TryGetProperty("types", out JsonElement types) && types.ValueKind == JsonValueKind.Array)
                items.AddRange(types.EnumerateArray().Select(item => item.GetString()).OfType<string>().Where(item => !string.IsNullOrWhiteSpace(item)));
            result.Add(new SdkApiNamespace(ns, area.Value, items));
        }
        return result.GroupBy(item => (item.Namespace, item.Area)).Select(group => new SdkApiNamespace(group.Key.Namespace, group.Key.Area, group.SelectMany(item => item.PublicItems).Distinct().Order().ToArray())).OrderBy(item => item.Area).ThenBy(item => item.Namespace).ToArray();
    }
}

public sealed class RoslynAnalysisService
{
    public IReadOnlyList<HostDiagnostic> Analyze(string source, char? activePathSeparator = null)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var diagnostics = tree.GetDiagnostics().Select(d =>
        {
            FileLinePositionSpan span = d.Location.GetLineSpan();
            return new HostDiagnostic(d.Id, d.Severity.ToString(), d.GetMessage(), span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
        }).ToList();
        if (activePathSeparator is char separator)
        {
            foreach (var literal in tree.GetRoot().DescendantTokens().Where(token => token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralToken)))
            {
                string value = literal.ValueText;
                for (int i = 0; i < value.Length; i++)
                {
                    char current = value[i];
                    // {name} is a logical-path placeholder, not a path separator.
                    if (current is '{' or '}') continue;
                    if (!IsSeparatorLike(current) || current == separator) continue;
                    FileLinePositionSpan span = tree.GetLineSpan(literal.Span);
                    diagnostics.Add(new HostDiagnostic("INU1007", "Error", $"Path uses '{current}' but the active kernel path separator is '{separator}'.", span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1));
                }
            }
        }
        return diagnostics;
    }

    private static bool IsSeparatorLike(char value)
    {
        if (value == 0 || value > 0x7f || char.IsLetterOrDigit(value)) return false;
        return value is not ('.' or '_' or '-' or ' ' or '{' or '}');
    }
}

public sealed class DiskImageService(InuHostEnvironment environment)
{
    public IReadOnlyList<DiskImageDescriptor> List(string projectRoot)
    {
        var roots = new[] { Path.GetFullPath(projectRoot), Path.Combine(environment.SdkRoot, "Artifacts") };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".img", ".raw", ".iso", ".vhd", ".vhdx", ".bin" };
        var result = new List<DiskImageDescriptor>();
        foreach (string root in roots.Where(Directory.Exists))
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            if (!extensions.Contains(ext)) continue;
            FileInfo info = new(file);
            if (ext.Equals(".bin", StringComparison.OrdinalIgnoreCase) && info.Length < 1024 * 1024) continue;
            result.Add(new DiskImageDescriptor(info.Name, info.FullName, info.Length, DetectFormat(info.FullName, ext), info.LastWriteTimeUtc));
        }
        return result.OrderByDescending(item => item.ModifiedUtc).ThenBy(item => item.Name).ToArray();
    }

    private static string DetectFormat(string path, string extension)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> bytes = stackalloc byte[520];
        int read = stream.Read(bytes);
        if (read >= 520 && System.Text.Encoding.ASCII.GetString(bytes.Slice(512, 8)) == "EFI PART") return "GPT disk image";
        if (read >= 512 && bytes[510] == 0x55 && bytes[511] == 0xaa) return "MBR/raw disk image";
        return extension.ToLowerInvariant() switch { ".iso" => "ISO image", ".vhd" => "VHD", ".vhdx" => "VHDX", _ => "Raw disk image" };
    }
}

public sealed record SdkValidationResult(bool Success, IReadOnlyList<string> Errors);

public sealed class SdkValidationService(InuHostEnvironment environment)
{
    public SdkValidationResult Validate()
    {
        var errors = new List<string>();
        string manifestPath = Path.Combine(environment.SdkRoot, "Inu-SourceManifest.json");
        if (!File.Exists(manifestPath)) errors.Add($"SDK source manifest is missing: {manifestPath}");
        else
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (document.RootElement.TryGetProperty("Files", out JsonElement files) || document.RootElement.TryGetProperty("files", out files))
                {
                    foreach (JsonElement entry in files.EnumerateArray())
                    {
                        string? relative = entry.TryGetProperty("Path", out JsonElement pathNode) ? pathNode.GetString() : entry.TryGetProperty("path", out pathNode) ? pathNode.GetString() : null;
                        string? expected = entry.TryGetProperty("Sha256", out JsonElement hashNode) ? hashNode.GetString() : entry.TryGetProperty("sha256", out hashNode) ? hashNode.GetString() : null;
                        if (string.IsNullOrWhiteSpace(relative) || string.IsNullOrWhiteSpace(expected)) continue;
                        string full = Path.Combine(environment.SdkRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(full)) { errors.Add($"Manifest file is missing: {relative}"); continue; }
                        string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(full)));
                        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) errors.Add($"Manifest hash mismatch: {relative}");
                    }
                }
            }
            catch (Exception ex) { errors.Add("SDK source manifest could not be validated: " + ex.Message); }
        }
        try { _ = JsonDocument.Parse(File.ReadAllText(Path.Combine(environment.SdkRoot, "Inu.ApiContract.json"))); }
        catch (Exception ex) { errors.Add("SDK API contract is invalid: " + ex.Message); }
        return new SdkValidationResult(errors.Count == 0, errors);
    }
}
