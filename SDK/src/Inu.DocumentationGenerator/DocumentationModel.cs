namespace Inu.DocumentationGenerator;

internal sealed record DocumentationConfiguration(
    string Product,
    string Version,
    string OutputDirectory,
    IReadOnlyList<string> PublicAssemblies,
    IReadOnlyList<string> ToolAssemblies,
    bool RequireDocumentationForPublicItems,
    bool RequireExampleForPublicMethods);

internal sealed record ProjectDocumentation(
    string Name,
    string ProjectPath,
    bool IsPublicAssembly,
    bool IsToolAssembly,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<ApiDocumentation> Items);

internal sealed record ApiDocumentation(
    string Id,
    string Assembly,
    string Namespace,
    string Kind,
    string Name,
    string QualifiedName,
    string Signature,
    string Summary,
    string Remarks,
    string WhenToUse,
    string Dependencies,
    string Returns,
    string Example,
    string SourcePath,
    int SourceLine);
