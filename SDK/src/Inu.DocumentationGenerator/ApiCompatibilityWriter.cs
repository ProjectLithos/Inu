using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Inu.DocumentationGenerator;

internal static class ApiCompatibilityWriter
{
    internal static void Write(string root, DocumentationConfiguration configuration, IReadOnlyList<ProjectDocumentation> projects)
    {
        var assemblies = projects.Where(project => project.IsPublicAssembly)
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .Select(project => new
            {
                name = project.Name,
                project = project.ProjectPath,
                dependencies = project.Dependencies,
                items = project.Items.OrderBy(item => item.QualifiedName, StringComparer.Ordinal)
                    .ThenBy(item => item.Signature, StringComparer.Ordinal)
                    .Select(item => new
                    {
                        id = item.Id,
                        item.Namespace,
                        item.Kind,
                        item.Name,
                        item.QualifiedName,
                        item.Signature,
                        signatureHash = Hash(item.Signature),
                        item.SourcePath,
                        item.SourceLine
                    }).ToArray()
            }).ToArray();

        string directory = Path.Combine(root, "Artifacts", "Documentation");
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "Inu.ApiCompatibility.json");
        string manifestPath = Path.Combine(root, "Inu.SdkManifest.json");
        string apiVersion = "1.0";
        string abiVersion = "1.0";
        if (File.Exists(manifestPath))
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (manifest.RootElement.TryGetProperty("apiVersion", out JsonElement api)) apiVersion = api.GetString() ?? apiVersion;
            if (manifest.RootElement.TryGetProperty("abiVersion", out JsonElement abi)) abiVersion = abi.GetString() ?? abiVersion;
        }
        string publicApiFingerprint = Hash(string.Join("\n", assemblies.SelectMany(a => a.items).Select(i => $"{i.QualifiedName}|{i.Signature}").OrderBy(x => x, StringComparer.Ordinal)));
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            product = configuration.Product,
            version = configuration.Version,
            apiVersion,
            abiVersion,
            compatibilityPolicy = "additive-within-major",
            publicApiFingerprint,
            generatedUtc = DateTimeOffset.UtcNow,
            assemblies
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
