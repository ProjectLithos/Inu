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
        Directory.CreateDirectory(Path.Combine(output, "api"));
        Directory.CreateDirectory(Path.Combine(output, "api", "members"));
        Directory.CreateDirectory(Path.Combine(output, "assemblies"));

        IReadOnlyList<ApiSiteEntry> entries = ReadApiContract(root);
        File.WriteAllText(Path.Combine(output, "assets", "site.css"), Css);
        File.WriteAllText(Path.Combine(output, "assets", "site.js"), JavaScript.Replace("__INU_RELEASE__", configuration.Version, StringComparison.Ordinal));

        WriteHome(output, configuration, entries);
        WriteApiIndex(output, configuration, entries);
        WriteAssemblyIndex(output, configuration, entries);
        foreach (ApiSiteEntry entry in entries)
        {
            WriteApiPage(output, configuration, entry);
            foreach (string member in entry.Members) WriteMemberPage(output, configuration, entry, member);
        }
        foreach (IGrouping<string, ApiSiteEntry> assembly in entries.GroupBy(entry => entry.Assembly, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            WriteAssemblyPage(output, configuration, assembly.Key, assembly.OrderBy(entry => entry.QualifiedName, StringComparer.Ordinal).ToArray());
        WriteSearchIndex(output, entries);
    }

    private static IReadOnlyList<ApiSiteEntry> ReadApiContract(string root)
    {
        string path = Path.Combine(root, "Inu.ApiContract.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        List<ApiSiteEntry> entries = [];
        foreach (JsonElement item in document.RootElement.GetProperty("codingApi").GetProperty("exports").EnumerateArray())
        {
            string ns = Required(item, "namespace");
            string type = Required(item, "type");
            entries.Add(new ApiSiteEntry(
                ns,
                type,
                Required(item, "assembly"),
                Required(item, "kind"),
                Required(item, "category"),
                Required(item, "availability"),
                Optional(item, "status", "recommended"),
                Optional(item, "compiledInto", Required(item, "assembly")),
                Optional(item, "implementation", string.Empty),
                Optional(item, "notes", string.Empty),
                item.GetProperty("members").EnumerateArray().Select(member => member.GetString() ?? string.Empty).Where(member => member.Length != 0).ToArray(),
                ReadMemberExamples(item),
                Optional(item, "example", string.Empty)));
        }
        foreach (ApiSiteEntry entry in entries)
            foreach (string member in entry.Members)
                if (!entry.MemberExamples.TryGetValue(member, out string? example) || string.IsNullOrWhiteSpace(example))
                    throw new InvalidDataException($"API member '{entry.QualifiedName}.{member}' requires a member-specific documentation example.");
        return entries.OrderBy(entry => CategoryOrder(entry.Category)).ThenBy(entry => entry.QualifiedName, StringComparer.Ordinal).ToArray();
    }

    private static void WriteHome(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries)
    {
        string categories = string.Join(Environment.NewLine, new[] { "Freestanding .NET", "Userland", "Kernel" }.Select(category =>
        {
            ApiSiteEntry[] categoryEntries = entries.Where(entry => entry.Category == category && entry.Status != "legacy").ToArray();
            string cards = string.Join(Environment.NewLine, categoryEntries.Take(8).Select(entry => ApiCard(entry, string.Empty)));
            return $"<section><div class=\"section-heading\"><div><p class=\"eyebrow\">{H(category)}</p><h2>{H(CategoryTitle(category))}</h2></div><a href=\"api/index.html#{Slug(category)}\">View all {categoryEntries.Length}</a></div><div class=\"cards\">{cards}</div></section>";
        }));
        int legacyCount = entries.Count(entry => entry.Status == "legacy");
        string body = $"""
<section class="hero">
  <p class="eyebrow">INU SDK {H(config.Version)} · CURRENT API</p>
  <h1>The API your OS code actually uses.</h1>
  <p>This reference is generated from the current Inu public API contract. It documents the freestanding .NET surface, high-level ring-3 APIs, and coder-facing kernel APIs. Native Get/Set/Event transport is intentionally not a programming surface for SDK users.</p>
  <div class="actions"><a class="primary" href="api/index.html">Browse the API</a><a href="assemblies/index.html">Browse assemblies</a></div>
</section>
<section class="facts"><div><strong>{entries.Count}</strong><span>type/API overview pages</span></div><div><strong>{entries.Sum(entry => entry.Members.Count)}</strong><span>individual member pages</span></div><div><strong>{entries.Select(entry => entry.Assembly).Distinct(StringComparer.Ordinal).Count()}</strong><span>assemblies/source components</span></div><div><strong>{legacyCount}</strong><span>legacy compatibility page{(legacyCount == 1 ? string.Empty : "s")}</span></div></section>
{categories}
<section class="boundary"><p class="eyebrow">BOUNDARY</p><h2>You write against the SDK, not the syscall transport.</h2><p>For ring-3 code, use <code>System.Console</code>, <code>System.IO</code>, <code>FileSystemPaths</code>, <code>Process</code>, <code>CommandLine</code>, and <code>SystemInformation</code>. Inu translates those operations to the kernel internally.</p></section>
""";
        File.WriteAllText(Path.Combine(output, "index.html"), Page(config, "API reference", body, string.Empty));
    }

    private static void WriteApiIndex(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries)
    {
        StringBuilder body = new();
        body.Append("<p class=\"eyebrow\">CURRENT PUBLIC SURFACE</p><h1>API index</h1><p>Every entry below is a coder-facing API overview. Every listed public member links to its own page with its exact signature and a member-specific usage example.</p>");
        foreach (string category in new[] { "Freestanding .NET", "Userland", "Kernel" })
        {
            body.Append("<section id=\"").Append(Slug(category)).Append("\"><div class=\"section-heading\"><div><p class=\"eyebrow\">").Append(H(category)).Append("</p><h2>").Append(H(CategoryTitle(category))).Append("</h2></div></div><div class=\"cards\">");
            foreach (ApiSiteEntry entry in entries.Where(entry => entry.Category == category && entry.Status != "legacy")) body.Append(ApiCard(entry, "../"));
            body.Append("</div></section>");
        }
        ApiSiteEntry[] legacy = entries.Where(entry => entry.Status == "legacy").ToArray();
        if (legacy.Length != 0)
        {
            body.Append("<section id=\"legacy\"><div class=\"section-heading\"><div><p class=\"eyebrow\">LEGACY</p><h2>Compatibility APIs</h2></div></div><p>These remain present for existing source but are not the recommended surface for new SDK code.</p><div class=\"cards\">");
            foreach (ApiSiteEntry entry in legacy) body.Append(ApiCard(entry, "../"));
            body.Append("</div></section>");
        }
        File.WriteAllText(Path.Combine(output, "api", "index.html"), Page(config, "API index", body.ToString(), "../"));
    }

    private static void WriteApiPage(string output, DocumentationConfiguration config, ApiSiteEntry entry)
    {
        string rows = string.Join(Environment.NewLine, entry.Members.Select(member => $"<tr><td><a href=\"members/{MemberFile(entry, member)}.html\"><code>{H(member)}</code></a></td><td>{H(MemberKind(member, entry.Kind))}</td></tr>"));
        string example = entry.Example.Length == 0 ? "// No example is currently defined." : entry.Example;
        string status = entry.Status == "legacy" ? "<span class=\"badge legacy\">Legacy compatibility</span>" : "<span class=\"badge\">Recommended</span>";
        string body = $"""
<p class="eyebrow">{H(entry.Category)} API</p>
<div class="title-row"><div><h1>{H(entry.QualifiedName)}</h1><p class="lede">{H(entry.Notes)}</p></div>{status}</div>
<section class="metadata">
  <div><span>Assembly / source component</span><strong><a href="../assemblies/{Slug(entry.Assembly)}.html">{H(entry.Assembly)}.dll</a></strong></div>
  <div><span>Namespace</span><strong>{H(entry.Namespace)}</strong></div>
  <div><span>API kind</span><strong>{H(entry.Kind)}</strong></div>
  <div><span>Available in</span><strong>{H(entry.Availability)}</strong></div>
  <div class="wide"><span>Compiled into / loaded as</span><strong>{H(entry.CompiledInto)}</strong></div>
</section>
<h2>Public API</h2>
<p>These are the members Inu currently documents as part of this public surface.</p>
<table><thead><tr><th>Member</th><th>Kind</th></tr></thead><tbody>{rows}</tbody></table>
<h2>How it is provided</h2><p>{H(entry.Implementation)}</p>
<h2>Example</h2><pre><code>{H(example)}</code></pre>
""";
        File.WriteAllText(Path.Combine(output, "api", ApiFile(entry) + ".html"), Page(config, entry.QualifiedName, body, "../"));
    }

    private static void WriteMemberPage(string output, DocumentationConfiguration config, ApiSiteEntry entry, string member)
    {
        string kind = MemberKind(member, entry.Kind);
        string status = entry.Status == "legacy" ? "<span class=\"badge legacy\">Legacy compatibility</span>" : "<span class=\"badge\">Recommended</span>";
        string example = entry.MemberExamples.TryGetValue(member, out string? specific) && specific.Length != 0 ? specific : entry.Example;
        string body = $"""
<p class="eyebrow">{H(entry.Category)} · {H(kind)}</p>
<div class="title-row"><div><h1>{H(MemberQualifiedName(entry, member))}</h1><p class="lede">Public member of <a href="../{ApiFile(entry)}.html"><code>{H(entry.QualifiedName)}</code></a>.</p></div>{status}</div>
<section class="metadata">
  <div><span>Assembly / source component</span><strong><a href="../../assemblies/{Slug(entry.Assembly)}.html">{H(entry.Assembly)}.dll</a></strong></div>
  <div><span>Namespace</span><strong>{H(entry.Namespace)}</strong></div>
  <div><span>Owning API</span><strong><a href="../{ApiFile(entry)}.html">{H(entry.QualifiedName)}</a></strong></div>
  <div><span>Member kind</span><strong>{H(kind)}</strong></div>
  <div><span>Available in</span><strong>{H(entry.Availability)}</strong></div>
  <div class="wide"><span>Compiled into / loaded as</span><strong>{H(entry.CompiledInto)}</strong></div>
</section>
<h2>Signature</h2><pre><code>{H(member)}</code></pre>
<h2>How to use it</h2><pre><code>{H(example)}</code></pre>
<h2>How it is provided</h2><p>{H(entry.Implementation)}</p>
""";
        File.WriteAllText(Path.Combine(output, "api", "members", MemberFile(entry, member) + ".html"), Page(config, MemberQualifiedName(entry, member), body, "../../"));
    }

    private static void WriteAssemblyIndex(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries)
    {
        string cards = string.Join(Environment.NewLine, entries.GroupBy(entry => entry.Assembly, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group =>
            $"<a class=\"card\" href=\"{Slug(group.Key)}.html\"><span class=\"card-kicker\">ASSEMBLY / COMPONENT</span><strong>{H(group.Key)}.dll</strong><span>{group.Count()} API page{(group.Count() == 1 ? string.Empty : "s")} · {group.Sum(entry => entry.Members.Count)} members</span></a>"));
        string body = $"<p class=\"eyebrow\">ASSEMBLIES</p><h1>Assemblies and source components</h1><p>Inu kernel components are copied as source into the generated kernel. Their logical SDK assembly/component name is shown here together with the final compilation target on each API page.</p><div class=\"cards\">{cards}</div>";
        File.WriteAllText(Path.Combine(output, "assemblies", "index.html"), Page(config, "Assemblies", body, "../"));
    }

    private static void WriteAssemblyPage(string output, DocumentationConfiguration config, string assembly, IReadOnlyList<ApiSiteEntry> entries)
    {
        string rows = string.Join(Environment.NewLine, entries.Select(entry => $"<tr><td><a href=\"../api/{ApiFile(entry)}.html\">{H(entry.QualifiedName)}</a></td><td>{H(entry.Kind)}</td><td>{H(entry.Availability)}</td><td>{entry.Members.Count}</td></tr>"));
        string compiled = string.Join("; ", entries.Select(entry => entry.CompiledInto).Distinct(StringComparer.Ordinal));
        string body = $"<p class=\"eyebrow\">ASSEMBLY / SOURCE COMPONENT</p><h1>{H(assembly)}.dll</h1><section class=\"metadata\"><div class=\"wide\"><span>Current compilation/load target</span><strong>{H(compiled)}</strong></div><div><span>API pages</span><strong>{entries.Count}</strong></div><div><span>Documented members</span><strong>{entries.Sum(entry => entry.Members.Count)}</strong></div></section><h2>Public API pages</h2><table><thead><tr><th>API</th><th>Kind</th><th>Availability</th><th>Members</th></tr></thead><tbody>{rows}</tbody></table>";
        File.WriteAllText(Path.Combine(output, "assemblies", Slug(assembly) + ".html"), Page(config, assembly, body, "../"));
    }

    private static void WriteSearchIndex(string output, IReadOnlyList<ApiSiteEntry> entries)
    {
        object[] items = entries.Select(entry => new { title = entry.QualifiedName, assembly = entry.Assembly, kind = entry.Kind, category = entry.Category, members = string.Join(" ", entry.Members), url = "api/" + ApiFile(entry) + ".html" })
            .Concat(entries.SelectMany(entry => entry.Members.Select(member => new { title = MemberQualifiedName(entry, member), assembly = entry.Assembly, kind = MemberKind(member, entry.Kind), category = entry.Category, members = member, url = "api/members/" + MemberFile(entry, member) + ".html" }))).ToArray();
        string json = JsonSerializer.Serialize(items);
        File.WriteAllText(Path.Combine(output, "search-index.json"), json);
        File.WriteAllText(Path.Combine(output, "assets", "search-index.js"), "window.InuSearchIndex=" + json + ";");
    }

    private static string ApiCard(ApiSiteEntry entry, string root) => $"<a class=\"card\" href=\"{root}api/{ApiFile(entry)}.html\"><span class=\"card-kicker\">{H(entry.Kind)}</span><strong>{H(entry.QualifiedName)}</strong><span>{H(entry.Assembly)} · {entry.Members.Count} public member{(entry.Members.Count == 1 ? string.Empty : "s")}</span></a>";
    private static string ApiFile(ApiSiteEntry entry)
    {
        string readable = Slug(entry.Namespace + "-" + entry.Type);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(entry.Namespace + "|" + entry.Type));
        return readable + "-" + Convert.ToHexString(digest.AsSpan(0, 4)).ToLowerInvariant();
    }
    private static string MemberFile(ApiSiteEntry entry, string member)
    {
        string readable = Slug(entry.Namespace + "-" + entry.Type + "-" + member);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(entry.Namespace + "|" + entry.Type + "|" + member));
        return readable + "-" + Convert.ToHexString(digest.AsSpan(0, 4)).ToLowerInvariant();
    }
    private static string MemberQualifiedName(ApiSiteEntry entry, string member)
    {
        string head = member.Contains('(') ? member[..member.IndexOf('(')] : member;
        if (head.Contains('.')) return entry.Namespace + "." + member;
        return entry.QualifiedName + "." + member;
    }
    private static IReadOnlyDictionary<string, string> ReadMemberExamples(JsonElement item)
    {
        Dictionary<string, string> examples = new(StringComparer.Ordinal);
        if (!item.TryGetProperty("memberExamples", out JsonElement map) || map.ValueKind != JsonValueKind.Object) return examples;
        foreach (JsonProperty property in map.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.String) examples[property.Name] = property.Value.GetString() ?? string.Empty;
        return examples;
    }
    private static string CategoryTitle(string category) => category switch { "Freestanding .NET" => "Freestanding .NET API", "Userland" => "Ring-3 userland API", "Kernel" => "Kernel coder API", _ => category };
    private static int CategoryOrder(string category) => category switch { "Freestanding .NET" => 0, "Userland" => 1, "Kernel" => 2, _ => 9 };
    private static string MemberKind(string member, string pageKind)
    {
        if (pageKind.Contains("Primitive", StringComparison.OrdinalIgnoreCase)) return "Primitive/value type";
        if (member.Contains('(')) return member.StartsWith("FileSystemPathPolicy(", StringComparison.Ordinal) ? "Constructor" : "Method";
        if (member.Contains('.') && (pageKind.Contains("Enum", StringComparison.OrdinalIgnoreCase) || member.Contains("KernelCpuRole.", StringComparison.Ordinal))) return "Enum value";
        if (pageKind.Contains("Interfaces", StringComparison.OrdinalIgnoreCase) || pageKind == "Interfaces and classes") return "Type";
        return "Property / value";
    }
    private static string Required(JsonElement item, string name) => item.GetProperty(name).GetString() ?? throw new InvalidDataException($"API contract property '{name}' is required.");
    private static string Optional(JsonElement item, string name, string fallback) => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    private static string H(string value) => WebUtility.HtmlEncode(value);
    private static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static string Page(DocumentationConfiguration config, string title, string body, string root) => $"""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{H(title)} · {H(config.Product)}</title><link rel="stylesheet" href="{root}assets/site.css"><script defer src="{root}assets/search-index.js"></script><script defer src="{root}assets/site.js"></script></head><body>
<header><a class="brand" href="{root}index.html">Inu <span>SDK</span></a><nav><a href="{root}api/index.html">API</a><a href="{root}assemblies/index.html">Assemblies</a></nav><label class="search"><span>Search API</span><input id="site-search" data-root="{root}" placeholder="Search type, function or assembly"><div id="search-results"></div></label></header>
<main>{body}</main><footer>Inu SDK {H(config.Version)} · current source/API contract</footer></body></html>
""";

    private sealed record ApiSiteEntry(string Namespace, string Type, string Assembly, string Kind, string Category, string Availability, string Status, string CompiledInto, string Implementation, string Notes, IReadOnlyList<string> Members, IReadOnlyDictionary<string, string> MemberExamples, string Example)
    {
        internal string QualifiedName => Namespace + "." + Type;
    }

    private const string Css = """
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#e8eef8;background:#08101a;line-height:1.55}*{box-sizing:border-box}body{margin:0}a{color:#6fd0ff;text-decoration:none}header{position:sticky;top:0;z-index:10;display:flex;align-items:center;gap:2rem;padding:1rem 5vw;background:#08101af2;border-bottom:1px solid #223044;backdrop-filter:blur(14px)}.brand{font-weight:800;color:#fff;font-size:1.2rem}.brand span{color:#6fd0ff}nav{display:flex;gap:1.1rem}.search{margin-left:auto;position:relative}.search>span{position:absolute;left:-9999px}.search input{width:min(30vw,28rem);padding:.75rem .9rem;border:1px solid #33465e;border-radius:.65rem;background:#0e1a28;color:#fff}#search-results{position:absolute;right:0;top:3rem;width:34rem;max-width:88vw;background:#0e1a28;border:1px solid #33465e;border-radius:.7rem;box-shadow:0 1rem 3rem #0009;overflow:hidden}#search-results a{display:block;padding:.75rem .9rem;border-bottom:1px solid #223044}main{max-width:80rem;margin:auto;padding:4rem 5vw 7rem}.hero{padding:3.5rem 0 2rem}.hero h1{font-size:clamp(3rem,7vw,6rem);line-height:.95;letter-spacing:-.04em;max-width:13ch;margin:.15em 0}.hero>p:not(.eyebrow){max-width:52rem;font-size:1.2rem;color:#b8c5d6}.eyebrow,.card-kicker{letter-spacing:.13em;text-transform:uppercase;color:#6fd0ff;font-size:.78rem;font-weight:800}.actions{display:flex;gap:.9rem;margin-top:2rem}.actions a{padding:.85rem 1.1rem;border:1px solid #33465e;border-radius:.7rem}.actions .primary{background:#6fd0ff;color:#07101b;border-color:#6fd0ff;font-weight:800}.facts{display:grid;grid-template-columns:repeat(4,1fr);gap:1rem;margin:1rem 0 4rem}.facts div,.metadata div{padding:1rem;border:1px solid #26374d;border-radius:.8rem;background:#0c1724}.facts strong{display:block;font-size:2rem}.facts span,.metadata span{display:block;color:#8fa2b8;font-size:.85rem}.section-heading{display:flex;align-items:end;justify-content:space-between;gap:1rem;margin-top:4rem}.section-heading h2{margin:.15rem 0}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(16rem,1fr));gap:1rem}.card{display:flex;flex-direction:column;gap:.35rem;padding:1.1rem;border:1px solid #26374d;border-radius:.8rem;background:#0c1724}.card strong{font-size:1.05rem;color:#fff}.card span:last-child{color:#9eb0c4;font-size:.9rem}.boundary{margin-top:5rem;padding:2rem;border:1px solid #314a66;border-radius:1rem;background:#0d1c2b}h1{font-size:clamp(2.2rem,5vw,4rem);line-height:1.05;letter-spacing:-.03em}h2{margin-top:2.8rem}.lede{max-width:58rem;color:#b8c5d6;font-size:1.08rem}.title-row{display:flex;justify-content:space-between;gap:1rem;align-items:flex-start}.badge{display:inline-block;padding:.35rem .6rem;border-radius:999px;background:#153650;color:#83d9ff;font-size:.78rem;font-weight:800;white-space:nowrap}.badge.legacy{background:#3b2e19;color:#ffd88e}.metadata{display:grid;grid-template-columns:repeat(4,1fr);gap:.8rem;margin:2rem 0}.metadata .wide{grid-column:span 2}.metadata strong{display:block;margin-top:.25rem;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse;margin:1rem 0}th,td{text-align:left;padding:.8rem;border-bottom:1px solid #26374d;vertical-align:top}th{color:#9eb0c4;font-size:.85rem}pre{overflow:auto;background:#03070c;border:1px solid #26374d;padding:1.1rem;border-radius:.8rem}code{font-family:Cascadia Code,Consolas,monospace}@media(max-width:800px){header{flex-wrap:wrap}.search{order:3;width:100%}.search input{width:100%}.facts{grid-template-columns:repeat(2,1fr)}.metadata{grid-template-columns:1fr 1fr}.metadata .wide{grid-column:span 2}.title-row{display:block}.badge{margin-bottom:1rem}}@media(max-width:520px){main{padding-top:2rem}.facts,.metadata{grid-template-columns:1fr}.metadata .wide{grid-column:span 1}}
""";

    private const string JavaScript = """
(()=>{document.addEventListener('DOMContentLoaded',()=>{const input=document.querySelector('#site-search');const box=document.querySelector('#search-results');if(!input||!box)return;const root=input.dataset.root||'';const items=window.InuSearchIndex||[];input.addEventListener('input',()=>{const q=input.value.trim().toLowerCase();box.innerHTML='';if(q.length<2)return;for(const item of items.filter(x=>(x.title+' '+x.assembly+' '+x.kind+' '+x.category+' '+x.members).toLowerCase().includes(q)).slice(0,10)){const a=document.createElement('a');a.href=root+item.url;a.textContent=item.title+' — '+item.assembly;box.appendChild(a)}})})})();
""";
}
