using System.Text.Json;

namespace Inu.DocumentationGenerator;

internal static class ConfigurationReader
{
    internal static DocumentationConfiguration Read(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return new DocumentationConfiguration(
            RequiredString(root, "product"),
            RequiredString(root, "version"),
            RequiredString(root, "outputDirectory"),
            ReadStrings(root, "publicAssemblies"),
            ReadStrings(root, "toolAssemblies"),
            root.GetProperty("requireDocumentationForPublicItems").GetBoolean(),
            root.GetProperty("requireExampleForPublicMethods").GetBoolean());
    }

    private static string RequiredString(JsonElement element, string name)
    {
        string? value = element.GetProperty(name).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Documentation configuration property '{name}' is required.")
            : value;
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement element, string name) =>
        element.GetProperty(name).EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => item.Length != 0)
            .ToArray();
}
