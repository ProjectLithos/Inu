using System.Text.Json;

namespace Inu.DocumentationGenerator;

internal sealed record DocumentationFinding(
    string Assembly,
    string QualifiedName,
    string Kind,
    string Field,
    string Message);

internal static class DocumentationAudit
{
    internal static IReadOnlyList<DocumentationFinding> Create(
        IReadOnlyList<ProjectDocumentation> projects,
        DocumentationConfiguration configuration)
    {
        List<DocumentationFinding> findings = [];
        foreach (ApiDocumentation item in projects
            .Where(project => project.IsPublicAssembly)
            .SelectMany(project => project.Items))
        {
            AddWhenMissing(findings, item, "summary", item.Summary,
                $"Missing summary: {item.Assembly}::{item.QualifiedName}");
            AddWhenMissing(findings, item, "whenToUse", item.WhenToUse,
                $"Missing <nova.when>: {item.Assembly}::{item.QualifiedName}");
            AddWhenMissing(findings, item, "dependencies", item.Dependencies,
                $"Missing dependency information: {item.Assembly}::{item.QualifiedName}");
            if (item.Kind == "Method" && !item.Signature.Contains(" void ", StringComparison.Ordinal))
            {
                AddWhenMissing(findings, item, "returns", item.Returns,
                    $"Missing return documentation: {item.Assembly}::{item.QualifiedName}");
            }
            if (configuration.RequireExampleForPublicMethods && item.Kind == "Method")
            {
                AddWhenMissing(findings, item, "example", item.Example,
                    $"Missing example: {item.Assembly}::{item.QualifiedName}");
            }
        }
        return findings;
    }

    internal static void Write(
        string root,
        DocumentationConfiguration configuration,
        IReadOnlyList<ProjectDocumentation> projects,
        IReadOnlyList<DocumentationFinding> findings)
    {
        string directory = Path.Combine(root, "Artifacts", "Documentation");
        Directory.CreateDirectory(directory);
        object report = new
        {
            schemaVersion = 1,
            product = configuration.Product,
            version = configuration.Version,
            generatedUtc = DateTimeOffset.UtcNow,
            publicAssemblies = projects.Count(project => project.IsPublicAssembly),
            publicItems = projects.Where(project => project.IsPublicAssembly).Sum(project => project.Items.Count),
            documentedItems = projects.Where(project => project.IsPublicAssembly)
                .SelectMany(project => project.Items)
                .Count(item => item.Summary.Length != 0 && item.WhenToUse.Length != 0 && item.Dependencies.Length != 0),
            findings
        };
        string path = Path.Combine(directory, "PublicApiAudit.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AddWhenMissing(
        List<DocumentationFinding> findings,
        ApiDocumentation item,
        string field,
        string value,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new DocumentationFinding(item.Assembly, item.QualifiedName, item.Kind, field, message));
        }
    }
}
