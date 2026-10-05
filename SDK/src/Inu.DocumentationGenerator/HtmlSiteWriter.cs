using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inu.DocumentationGenerator;

internal static class HtmlSiteWriter
{
    internal static void Write(string root, DocumentationConfiguration configuration, IReadOnlyList<ProjectDocumentation> projects)
    {
        string output = Path.GetFullPath(Path.Combine(root, configuration.OutputDirectory));
        if (Directory.Exists(output)) Directory.Delete(output, true);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "assets"));
        Directory.CreateDirectory(Path.Combine(output, "assemblies"));
        Directory.CreateDirectory(Path.Combine(output, "api"));
        Directory.CreateDirectory(Path.Combine(output, "guides"));
        Directory.CreateDirectory(Path.Combine(output, "source"));

        File.WriteAllText(Path.Combine(output, "assets", "site.css"), Css);
        File.WriteAllText(Path.Combine(output, "assets", "site.js"), JavaScript.Replace("__INU_RELEASE__", configuration.Version, StringComparison.Ordinal));
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceFiles = WriteSourceBrowser(root, output, configuration, projects);
        foreach (ProjectDocumentation project in projects)
        {
            WriteAssembly(output, configuration, project, sourceFiles);
            foreach (ApiDocumentation item in project.Items) WriteApi(output, configuration, item);
        }
        CopyGuides(root, output, configuration);
        WriteIndex(output, configuration, projects);
        WriteSearchIndex(output, projects);
    }

    private static void WriteIndex(string output, DocumentationConfiguration config, IReadOnlyList<ProjectDocumentation> projects)
    {
        string cards = string.Join(Environment.NewLine, projects.Select(project =>
            $"<a class=\"card\" href=\"assemblies/{EncodeFile(project.Name)}.html\"><strong>{H(project.Name)}</strong><span>{project.Items.Count} exported API items · {(project.IsToolAssembly ? "tool" : "SDK")}</span></a>"));
        string publicRows = string.Join(Environment.NewLine, projects.SelectMany(project => project.Items.Select(item =>
            $"<tr><td><a href=\"api/{CreateApiFileName(item)}.html\">{H(item.QualifiedName)}</a></td><td><a href=\"assemblies/{EncodeFile(project.Name)}.html\">{H(project.Name)}</a></td><td>{H(item.Kind)}</td><td>{H(Summary(item))}</td></tr>")));
        Int32 publicItemCount = projects.Sum(project => project.Items.Count);
        string body = $"<section class=\"hero\"><p class=\"eyebrow\">VERSION</p><h1>Build a freestanding C# kernel.</h1><p>Offline SDK reference generated from the complete Inu src tree. Every site link is relative, so the complete site remains portable when opened directly with file:// or copied elsewhere.</p><div class=\"actions\"><a href=\"guides/Getting-Started.html\">Get started</a><a href=\"guides/Next-Steps.html\">SDK roadmap</a><a href=\"source/index.html\">Exported SDK source</a></div></section><section id=\"assemblies\"><h2>SDK assemblies</h2><p>{projects.Count} source projects containing {publicItemCount} exported API items are indexed below.</p><div class=\"cards\">{cards}</div></section><section id=\"public-items\"><h2>All exported API items</h2><p>This table contains only declarations explicitly exported to SDK users with the Inu API marker. Ordinary public implementation declarations are intentionally omitted.</p><table><thead><tr><th>API item</th><th>Assembly</th><th>Kind</th><th>Purpose</th></tr></thead><tbody>{publicRows}</tbody></table></section>";
        File.WriteAllText(Path.Combine(output, "index.html"), Page(config, "SDK usage", body, string.Empty));
    }

    private static void WriteAssembly(string output, DocumentationConfiguration config, ProjectDocumentation project, IReadOnlyDictionary<string, IReadOnlyList<string>> sourceFiles)
    {
        string items = string.Join(Environment.NewLine, project.Items.Select(item =>
            $"<tr><td><a href=\"../api/{CreateApiFileName(item)}.html\">{H(item.Name)}</a></td><td>{H(item.Kind)}</td><td>{H(Summary(item))}</td></tr>"));
        string sourceLink = sourceFiles.TryGetValue(project.Name, out IReadOnlyList<string>? files) && files.Count != 0
            ? $"<p><a href=\"../source/index.html#{EncodeFile(project.Name)}\">Browse {files.Count} SDK source files</a></p>" : string.Empty;
        string body = $"<p class=\"eyebrow\">{(project.IsToolAssembly ? "SDK TOOL" : "PUBLIC ASSEMBLY")}</p><h1>{H(project.Name)}</h1><dl><dt>Project</dt><dd>{H(project.ProjectPath)}</dd><dt>Dependencies</dt><dd>{H(project.Dependencies.Count == 0 ? "None" : string.Join(", ", project.Dependencies))}</dd></dl>{sourceLink}<h2>API items</h2><table><thead><tr><th>Name</th><th>Kind</th><th>Purpose</th></tr></thead><tbody>{items}</tbody></table>";
        File.WriteAllText(Path.Combine(output, "assemblies", EncodeFile(project.Name) + ".html"), Page(config, project.Name, body, "../"));
    }

    private static void WriteApi(string output, DocumentationConfiguration config, ApiDocumentation item)
    {
        string sourceUrl = "../source/files/" + SourcePagePath(item.SourcePath) + ".html#L" + item.SourceLine;
        string body = $"<p class=\"eyebrow\">{H(item.Assembly)} · {H(item.Kind)}</p><h1>{H(item.QualifiedName)}</h1><pre><code>{H(item.Signature)}</code></pre>{Section("What it does", item.Summary, item)}{Section("When to use it", item.WhenToUse, item)}{Section("Details", item.Remarks, item)}{Section("Dependencies", item.Dependencies, item)}{Section("Return value", item.Returns, item)}{CodeSection("Example", item.Example)}<h2>Source</h2><p><a href=\"{sourceUrl}\"><code>{H(item.SourcePath)}:{item.SourceLine}</code></a></p>";
        File.WriteAllText(Path.Combine(output, "api", CreateApiFileName(item) + ".html"), Page(config, item.Name, body, "../"));
    }

    private static void CopyGuides(string root, string output, DocumentationConfiguration config)
    {
        string content = Path.Combine(root, "docs", "site-content");
        if (!Directory.Exists(content)) return;
        foreach (string markdown in Directory.EnumerateFiles(content, "*.md", SearchOption.TopDirectoryOnly))
        {
            string title = Path.GetFileNameWithoutExtension(markdown).Replace('-', ' ');
            string html = Markdown(File.ReadAllText(markdown));
            File.WriteAllText(Path.Combine(output, "guides", Path.GetFileNameWithoutExtension(markdown) + ".html"), Page(config, title, html, "../"));
        }
    }

    private static void WriteSearchIndex(string output, IReadOnlyList<ProjectDocumentation> projects)
    {
        object[] entries = projects.SelectMany(project => project.Items.Select(item => new
        {
            title = item.QualifiedName,
            assembly = item.Assembly,
            kind = item.Kind,
            summary = item.Summary,
            url = $"api/{CreateApiFileName(item)}.html"
        })).Cast<object>().ToArray();
        string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(Path.Combine(output, "assets", "search-index.js"), "window.InuSearchIndex=" + json + ";");
    }

    private static string Page(DocumentationConfiguration config, string title, string body, string root) => $"""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{H(title)} · {H(config.Product)}</title><link rel="stylesheet" href="{root}assets/site.css"><script defer src="{root}assets/search-index.js"></script><script defer src="{root}assets/site.js"></script></head><body><header><a class="brand" href="{root}index.html">Inu <span>OS SDK</span></a><nav><a href="{root}guides/Getting-Started.html">Guides</a><a href="{root}index.html#assemblies">API</a></nav><label class="search"><span>Search</span><input id="site-search" data-root="{root}" placeholder="Type a public item"><div id="search-results"></div></label></header><main>{body}</main><footer>Generated from Inu source.</footer></body></html>
""";

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> WriteSourceBrowser(string root, string output, DocumentationConfiguration config, IReadOnlyList<ProjectDocumentation> projects)
    {
        Dictionary<string, IReadOnlyList<string>> result = new(StringComparer.Ordinal);
        StringBuilder index = new("<p class=\"eyebrow\">PORTABLE RELATIVE SOURCE MAP</p><h1>Exported SDK source</h1><p>These pages are copied into the generated site and use relative links only. No repository drive path is required.</p>");
        foreach (ProjectDocumentation project in projects.Where(project => project.Items.Count != 0))
        {
            string projectFile = Path.Combine(root, project.ProjectPath.Replace('/', Path.DirectorySeparatorChar));
            string? projectDirectory = Path.GetDirectoryName(projectFile);
            if (projectDirectory is null || !Directory.Exists(projectDirectory)) continue;
            List<string> files = Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(file => string.Equals(Path.GetExtension(file), ".cs", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(file), ".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList();
            result[project.Name] = files;
            index.Append("<section id=\"").Append(EncodeFile(project.Name)).Append("\"><h2>").Append(H(project.Name)).Append("</h2><div class=\"source-list\">");
            foreach (string relative in files)
            {
                WriteSourcePage(root, output, config, relative);
                index.Append("<a href=\"files/").Append(SourcePagePath(relative)).Append(".html\"><code>").Append(H(relative)).Append("</code></a>");
            }
            index.Append("</div></section>");
        }
        File.WriteAllText(Path.Combine(output, "source", "index.html"), Page(config, "Exported SDK source", index.ToString(), "../"));
        return result;
    }

    private static void WriteSourcePage(string root, string output, DocumentationConfiguration config, string relative)
    {
        string source = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        StringBuilder code = new();
        for (Int32 line = 0; line < lines.Length; line++)
            code.Append("<span class=\"source-line\" id=\"L").Append(line + 1).Append("\"><a class=\"line-number\" href=\"#L").Append(line + 1).Append("\">").Append(line + 1).Append("</a>").Append(H(lines[line])).AppendLine("</span>");
        Int32 depth = relative.Count(ch => ch == '/') + 2;
        string rootPrefix = string.Concat(Enumerable.Repeat("../", depth));
        string body = $"<p class=\"eyebrow\">PUBLIC SDK SOURCE</p><h1>{H(relative)}</h1><p><a href=\"{rootPrefix}source/index.html\">Source index</a></p><pre class=\"source-code\"><code>{code}</code></pre>";
        string target = Path.Combine(output, "source", "files", SourcePagePath(relative).Replace('/', Path.DirectorySeparatorChar) + ".html");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, Page(config, relative, body, rootPrefix));
    }

    private static string SourcePagePath(string relative) => relative.Replace('\\', '/');

    private static string Section(string title, string value, ApiDocumentation? item = null)
    {
        string text = value;
        if (text.Length == 0 && item is not null)
        {
            text = title switch
            {
                "What it does" => Summary(item),
                "When to use it" => $"Use this {item.Kind.ToLowerInvariant()} when working with {item.Assembly} functionality exposed by the Inu SDK.",
                "Details" => $"Declared as {item.Signature}. The authoritative declaration is in {item.SourcePath} at line {item.SourceLine}.",
                "Dependencies" => "No additional API-specific dependency is declared for this item beyond its containing assembly and project references.",
                "Return value" => ReturnSummary(item),
                _ => Summary(item)
            };
        }
        return $"<h2>{H(title)}</h2><p>{H(text)}</p>";
    }

    private static string CodeSection(string title, string value) => value.Length == 0 ? string.Empty : $"<h2>{H(title)}</h2><pre><code>{H(value)}</code></pre>";

    private static string Summary(ApiDocumentation item)
    {
        if (item.Summary.Length != 0) return item.Summary;
        string kind = item.Kind.ToLowerInvariant();
        if (kind == "method") return $"Provides the {item.Name} operation in {item.Assembly}. Its public contract is {item.Signature}.";
        if (kind == "property") return $"Exposes the {item.Name} property from {item.QualifiedName} in {item.Assembly}.";
        if (kind == "enum") return $"Defines the {item.QualifiedName} enumeration used by {item.Assembly}.";
        if (kind == "interface") return $"Defines the public {item.QualifiedName} interface contract provided by {item.Assembly}.";
        return $"Defines the public {kind} {item.QualifiedName} in {item.Assembly}.";
    }

    private static string ReturnSummary(ApiDocumentation item)
    {
        if (item.Returns.Length != 0) return item.Returns;
        string signature = item.Signature;
        if (Regex.IsMatch(signature, @"\bvoid\b", RegexOptions.CultureInvariant)) return "No value is returned.";
        if (Regex.IsMatch(signature, @"\bbool\b|\bBoolean\b", RegexOptions.CultureInvariant)) return "Returns a Boolean result; the operation-specific success conditions are defined by the method contract and signature.";
        return $"Returns the value described by the declared signature: {signature}.";
    }
    private static string H(string value) => WebUtility.HtmlEncode(value);
    private static string EncodeFile(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static string CreateApiFileName(ApiDocumentation item)
    {
        string readable = EncodeFile(item.Name);
        if (readable.Length == 0) readable = "item";
        if (readable.Length > 48) readable = readable[..48].TrimEnd('-');

        string identity = string.Join("|", item.Assembly, item.Kind, item.QualifiedName, item.Signature);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        string suffix = Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
        return $"{readable}-{suffix}";
    }

    private static string Markdown(string text)
    {
        StringBuilder html = new();
        bool code = false;
        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal)) { html.AppendLine(code ? "</code></pre>" : "<pre><code>"); code = !code; continue; }
            if (code) { html.AppendLine(H(raw)); continue; }
            if (line.StartsWith("### ")) html.Append("<h3>").Append(H(line[4..])).AppendLine("</h3>");
            else if (line.StartsWith("## ")) html.Append("<h2>").Append(H(line[3..])).AppendLine("</h2>");
            else if (line.StartsWith("# ")) html.Append("<h1>").Append(H(line[2..])).AppendLine("</h1>");
            else if (line.StartsWith("- ")) html.Append("<p class=\"bullet\">• ").Append(H(line[2..])).AppendLine("</p>");
            else if (line.Length != 0) html.Append("<p>").Append(H(line)).AppendLine("</p>");
        }
        return html.ToString();
    }

    private const string Css = """
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#eaf0ff;background:#07101d;line-height:1.55}*{box-sizing:border-box}body{margin:0}header{position:sticky;top:0;z-index:5;display:flex;align-items:center;gap:2rem;padding:1rem 5vw;background:#07101dee;border-bottom:1px solid #20304a;backdrop-filter:blur(12px)}a{color:#73c7ff;text-decoration:none}.brand{font-weight:800;color:#fff;font-size:1.15rem}.brand span{color:#73c7ff}nav{display:flex;gap:1rem}.search{margin-left:auto;position:relative}.search span{position:absolute;left:-9999px}.search input{width:min(28vw,24rem);padding:.7rem .9rem;border:1px solid #314662;border-radius:.6rem;background:#0d1929;color:#fff}#search-results{position:absolute;right:0;width:32rem;max-width:85vw;background:#0d1929;border:1px solid #314662;border-radius:.6rem;box-shadow:0 1rem 3rem #0008}#search-results a{display:block;padding:.7rem .9rem;border-bottom:1px solid #20304a}main{max-width:76rem;margin:auto;padding:4rem 5vw 7rem}.hero{padding:4rem 0}.hero h1{font-size:clamp(2.6rem,7vw,5.8rem);line-height:.95;max-width:12ch;margin:.2em 0}.hero p{max-width:48rem;font-size:1.2rem;color:#b7c5d9}.eyebrow{letter-spacing:.14em;text-transform:uppercase;color:#73c7ff;font-weight:700}.actions{display:flex;gap:1rem;margin-top:2rem}.actions a,.card{border:1px solid #314662;border-radius:.8rem;padding:1rem 1.2rem;background:#0d1929}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(15rem,1fr));gap:1rem}.card{display:flex;flex-direction:column}.card span{color:#9fafc3;font-size:.9rem}h1{font-size:2.7rem}h2{margin-top:2.4rem}pre{overflow:auto;background:#020711;border:1px solid #20304a;padding:1rem;border-radius:.7rem}code{font-family:Cascadia Code,Consolas,monospace}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:.75rem;border-bottom:1px solid #20304a;vertical-align:top}dt{color:#8da0b9;font-weight:700}dd{margin:0 0 1rem}.bullet{margin:.3rem 0}footer{padding:2rem 5vw;color:#7f90a7;border-top:1px solid #20304a}.source-list{display:flex;flex-direction:column;gap:.35rem}.source-code{padding:0}.source-line{display:block;white-space:pre}.source-line:target{background:#16314a}.line-number{display:inline-block;width:4rem;padding-right:1rem;text-align:right;color:#6f829b;user-select:none}@media(max-width:700px){header{flex-wrap:wrap}.search{order:3;width:100%}.search input{width:100%}nav{margin-left:auto}}
""";

    private const string JavaScript = """
(()=>{const release='__INU_RELEASE__';const applyRelease=()=>{const footer=document.querySelector('footer');if(footer)footer.textContent=`Generated from Inu ${release} source.`;const version=document.querySelector('.hero .eyebrow');if(version&&version.textContent==='VERSION')version.textContent=`VERSION ${release}`};document.addEventListener('DOMContentLoaded',()=>{applyRelease();const input=document.querySelector('#site-search');const box=document.querySelector('#search-results');if(!input||!box)return;const root=input.dataset.root||'';const items=window.InuSearchIndex||[];input.addEventListener('input',()=>{const q=input.value.trim().toLowerCase();box.innerHTML='';if(q.length<2)return;for(const item of items.filter(x=>(x.title+' '+x.assembly+' '+x.summary).toLowerCase().includes(q)).slice(0,8)){const a=document.createElement('a');a.href=root+item.url;a.textContent=item.title+' — '+item.assembly;box.appendChild(a)}})})})();
""";
}
