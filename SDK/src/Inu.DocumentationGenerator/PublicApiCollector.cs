using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Inu.DocumentationGenerator;

internal static partial class PublicApiCollector
{
    internal static IReadOnlyList<ProjectDocumentation> Collect(string root, DocumentationConfiguration configuration)
    {
        List<ProjectDocumentation> projects = [];
        string sourceRoot = Path.Combine(root, "src");
        foreach (string projectFile in Directory.EnumerateDirectories(sourceRoot)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly)))
        {
            XDocument project = XDocument.Load(projectFile);
            string name = project.Descendants("AssemblyName").Select(x => x.Value).FirstOrDefault()
                ?? Path.GetFileNameWithoutExtension(projectFile);
            bool isTool = configuration.ToolAssemblies.Contains(name, StringComparer.Ordinal);
            bool isPublic = !isTool;

            string projectDirectory = Path.GetDirectoryName(projectFile)!;
            string[] dependencies = project.Descendants("ProjectReference")
                .Select(x => x.Attribute("Include")?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Path.GetFileNameWithoutExtension(x!))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            List<ApiDocumentation> items = [];
            foreach (string sourceFile in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                items.AddRange(ParseSource(root, name, sourceFile, dependencies));
            }
            projects.Add(new ProjectDocumentation(name, Path.GetRelativePath(root, projectFile), isPublic, isTool, dependencies, items));
        }
        return projects.OrderBy(project => project.Name, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<ApiDocumentation> ParseSource(string root, string assembly, string path, IReadOnlyList<string> projectDependencies)
    {
        string[] lines = File.ReadAllLines(path);
        string currentNamespace = string.Empty;
        List<string> comments = [];
        for (int index = 0; index < lines.Length; index++)
        {
            string trimmed = lines[index].Trim();
            Match namespaceMatch = NamespaceRegex().Match(trimmed);
            if (namespaceMatch.Success) currentNamespace = namespaceMatch.Groups[1].Value;
            if (trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                comments.Add(trimmed[3..].Trim());
                continue;
            }
            if (trimmed.Length == 0 || trimmed.StartsWith("[", StringComparison.Ordinal)) continue;
            if (!trimmed.StartsWith("public ", StringComparison.Ordinal))
            {
                comments.Clear();
                continue;
            }

            Int32 declarationLine = index + 1;
            string signature = ReadSignature(lines, ref index);
            Match declaration = PublicDeclarationRegex().Match(signature);
            if (!declaration.Success)
            {
                comments.Clear();
                continue;
            }

            string kind = DetectKind(signature);
            string name = declaration.Groups[1].Value;
            string xml = string.Join(Environment.NewLine, comments);
            comments.Clear();
            DocumentationText text = ReadDocumentation(xml);
            string qualified = string.IsNullOrEmpty(currentNamespace) ? name : $"{currentNamespace}.{name}";
            string dependencyText = text.Dependencies.Length == 0 ? string.Join(", ", projectDependencies) : text.Dependencies;
            yield return new ApiDocumentation(
                MakeId(assembly, qualified, signature), assembly, currentNamespace, kind, name, qualified, signature,
                text.Summary, text.Remarks, text.WhenToUse, dependencyText, text.Returns, text.Example,
                Path.GetRelativePath(root, path).Replace('\\', '/'), declarationLine);
        }
    }

    private static string ReadSignature(string[] lines, ref int index)
    {
        StringBuilder signature = new(lines[index].Trim());
        while (index + 1 < lines.Length && !signature.ToString().Contains('{') && !signature.ToString().EndsWith(';'))
        {
            index++;
            signature.Append(' ').Append(lines[index].Trim());
            if (signature.Length > 800) break;
        }
        string value = signature.ToString();
        int body = value.IndexOf('{');
        return (body >= 0 ? value[..body] : value).Trim().TrimEnd(';').Trim();
    }

    private static DocumentationText ReadDocumentation(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return DocumentationText.Empty;
        try
        {
            XElement root = XElement.Parse($"<root>{xml}</root>");
            return new DocumentationText(
                Clean(root.Element("summary")?.Value), Clean(root.Element("remarks")?.Value),
                Clean(root.Element("nova.when")?.Value), Clean(root.Element("nova.depends")?.Value),
                Clean(root.Element("returns")?.Value), Clean(root.Element("example")?.Value));
        }
        catch
        {
            return new DocumentationText(Clean(Regex.Replace(xml, "<[^>]+>", " ")), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    private static string DetectKind(string value)
    {
        if (Regex.IsMatch(value, @"\b(class|record|struct|interface|enum)\b")) return "Type";
        if (value.Contains(" event ", StringComparison.Ordinal)) return "Event";
        if (value.Contains('(')) return "Method";
        if (value.Contains("=>", StringComparison.Ordinal) || value.Contains("{ get", StringComparison.Ordinal)) return "Property";
        return "Field";
    }

    private static string MakeId(string assembly, string qualified, string signature)
    {
        string raw = $"{assembly}-{qualified}-{signature}".ToLowerInvariant();
        return Regex.Replace(raw, "[^a-z0-9]+", "-").Trim('-');
    }

    private static string Clean(string? value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    [GeneratedRegex(@"^(?:namespace)\s+([A-Za-z_][A-Za-z0-9_.]*)")]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"^public\s+(?:(?:static|sealed|abstract|readonly|unsafe|partial|required|virtual|override|new)\s+)*(?:(?:class|record|struct|interface|enum)\s+)?(?:[A-Za-z_][A-Za-z0-9_<>,.?\[\]\s:*]*\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*(?:[({=;:]|$)")]
    private static partial Regex PublicDeclarationRegex();

    private sealed record DocumentationText(string Summary, string Remarks, string WhenToUse, string Dependencies, string Returns, string Example)
    {
        internal static DocumentationText Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
