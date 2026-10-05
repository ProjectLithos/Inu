using Inu.DocumentationGenerator;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Inu.DocumentationGenerator generate [--root <repository>] [--configuration <file>] [--validate]");
    return 0;
}
if (!string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unknown command: {args[0]}");
    return 2;
}

string root = FindRepositoryRoot(GetOption(args, "--root") ?? Directory.GetCurrentDirectory());
string configPath = GetOption(args, "--configuration") ?? Path.Combine(root, "docs", "Inu.Documentation.json");
bool validate = args.Contains("--validate", StringComparer.OrdinalIgnoreCase);
DocumentationConfiguration configuration = ConfigurationReader.Read(Path.GetFullPath(configPath));
IReadOnlyList<ProjectDocumentation> projects = PublicApiCollector.Collect(root, configuration);
IReadOnlyList<DocumentationFinding> findings = DocumentationAudit.Create(projects, configuration);
DocumentationAudit.Write(root, configuration, projects, findings);
ApiCompatibilityWriter.Write(root, configuration, projects);

List<string> failures = [];
if (validate && configuration.RequireDocumentationForPublicItems)
{
    failures.AddRange(findings.Select(finding => finding.Message));
}
HtmlSiteWriter.Write(root, configuration, projects);
Console.WriteLine($"[ OK ] Generated Inu SDK usage site with {projects.Count} assemblies and {projects.Sum(project => project.Items.Count)} public items.");
Console.WriteLine($@"[INFO] Public API documentation audit: {findings.Count} finding(s). See Artifacts\Documentation\PublicApiAudit.json and Inu.ApiCompatibility.json.");
if (failures.Count == 0) return 0;
foreach (string failure in failures) Console.Error.WriteLine($"[FAIL] {failure}");
return 1;

static string? GetOption(string[] arguments, string name)
{
    int index = Array.FindIndex(arguments, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(Path.GetFullPath(start));
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Inu repository root was not found.");
}
