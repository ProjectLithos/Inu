using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inu.DocumentationGenerator;

internal static class HtmlSiteWriter
{
    private static readonly string[] AreaOrder = ["Boot", "Kernel", "Userland"];

    internal static void Write(string root, DocumentationConfiguration configuration, IReadOnlyList<ProjectDocumentation> projects)
    {
        string output = Path.GetFullPath(Path.Combine(root, configuration.OutputDirectory));
        Directory.CreateDirectory(output);
        DeleteGenerated(output, "assets");
        DeleteGenerated(output, "api");
        DeleteGenerated(output, "assemblies");
        DeleteFile(output, "index.html");
        DeleteFile(output, "search-index.json");

        Directory.CreateDirectory(Path.Combine(output, "assets"));
        Directory.CreateDirectory(Path.Combine(output, "api"));
        Directory.CreateDirectory(Path.Combine(output, "api", "members"));
        Directory.CreateDirectory(Path.Combine(output, "assemblies"));

        IReadOnlyList<ApiSiteEntry> entries = ReadApiContract(root);
        File.WriteAllText(Path.Combine(output, "assets", "site.css"), Css);
        File.WriteAllText(Path.Combine(output, "assets", "site.js"), JavaScript);

        WriteHome(output, configuration, entries);
        foreach (ApiSiteEntry entry in entries)
        {
            WriteApiPage(output, configuration, entries, entry);
            foreach (string member in entry.Members) WriteMemberPage(output, configuration, entries, entry, member);
        }
        WriteAssemblyIndex(output, configuration, entries);
        foreach (IGrouping<string, ApiSiteEntry> assembly in entries.GroupBy(entry => entry.Assembly, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            WriteAssemblyPage(output, configuration, entries, assembly.Key, assembly.OrderBy(entry => entry.QualifiedName, StringComparer.Ordinal).ToArray());
        WriteSearchIndex(output, entries);
    }

    private static void DeleteGenerated(string output, string name)
    {
        string path = Path.Combine(output, name);
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static void DeleteFile(string output, string name)
    {
        string path = Path.Combine(output, name);
        if (File.Exists(path)) File.Delete(path);
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
            IReadOnlyList<string> members = item.TryGetProperty("members", out JsonElement memberArray)
                ? memberArray.EnumerateArray().Select(member => member.GetString() ?? string.Empty).Where(member => member.Length != 0).ToArray()
                : [];
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
                members,
                ReadMemberExamples(item),
                Optional(item, "example", string.Empty)));
        }
        return entries.OrderBy(entry => AreaOrderFor(entry)).ThenBy(entry => entry.Namespace, StringComparer.Ordinal).ThenBy(entry => entry.Type, StringComparer.Ordinal).ToArray();
    }

    private static void WriteHome(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries)
    {
        string sidebar = Sidebar(entries, string.Empty, null);
        string body = $"""
<section class="hero" id="introduction">
  <p class="eyebrow">INU SDK {H(config.Version)}</p>
  <h1>Build the operating system you mean to build.</h1>
  <p>Inu exists so an OS developer can work in normal C# while still owning the design of the boot path, kernel and userland. The SDK supplies freestanding runtime support, small replaceable source components and hardware/OS contracts; Kath composes only what the OS selects and leaves coder-owned source such as <code>Kernel.cs</code> under the developer's control.</p>
</section>
<section id="why"><h2>Why Inu exists</h2><p>Most operating-system frameworks begin by deciding the kernel architecture, boot flow and subsystem boundaries for the developer. Inu is intended to do the opposite: provide the difficult low-level substrate without making those architectural choices permanent. A project may be monolithic, microkernel, hybrid or custom; SDK components can be used, replaced, materialised and edited as the OS grows.</p></section>
<section id="use"><h2>What the SDK should be used for</h2><div class="three"><article><h3>Boot</h3><p>Receive and normalise the machine state handed over by firmware or a loader, retain only the capabilities the OS needs, then transfer control into the OS.</p></article><article><h3>Kernel</h3><p>Compose memory, scheduling, drivers, storage, graphics, networking, security and other selected mechanisms in coder-owned source rather than through a fixed hidden kernel.</p></article><article><h3>Userland</h3><p>Run isolated applications and commands against ordinary managed APIs while Inu handles the kernel transport beneath the public SDK surface.</p></article></div></section>
<section id="how"><h2>How to use this Help</h2><ol><li>Choose <strong>Boot</strong>, <strong>Kernel</strong> or <strong>Userland</strong> in the left column.</li><li>Expand a <code>using …;</code> namespace to see the public SDK items available in that area.</li><li>Select a type or API surface to see its public members, availability, implementation notes and examples.</li><li>Use those APIs from the coder-owned project source. If you need to own or replace an SDK implementation, materialise that component into the OS instead of editing a second hidden copy.</li></ol></section>
<section id="expansion"><h2>How Inu is intended to expand</h2><p>The current working platform is primarily <strong>x64 with UEFI</strong>, but neither is intended to define the SDK permanently. New hardware should be added behind the same driver/capability contracts so an OS can select NVMe, AHCI, VirtIO, physical devices or future controllers without changing unrelated source. Boot support should grow beyond UEFI through independent boot capability providers; the boot contracts already identify UEFI, Limine and Multiboot2 as protocol identities, while non-UEFI boot paths still require their concrete implementations. Architecture-specific code should remain behind architecture/HAL contracts so additional targets such as Arm64 or RISC-V can be introduced without leaking x64 assumptions into generic OS code.</p><p>The same rule applies to every subsystem: add a new implementation beside an interface or capability boundary, let Kath expose it as a selectable component, and keep the OS author's composition visible in source.</p></section>
<section id="boundary"><h2>Public API boundary</h2><p>This Help is generated from <code>Inu.ApiContract.json</code>, the explicit coding surface intended for SDK users. Public implementation details that are not exported through that contract are deliberately omitted. Ring-3 programs use the documented userland APIs rather than calling the Get/Set/Event transport directly.</p></section>
""";
        File.WriteAllText(Path.Combine(output, "index.html"), Page(config, entries, "Inu SDK Help", body, string.Empty, sidebar));
    }

    private static void WriteApiPage(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries, ApiSiteEntry entry)
    {
        string rows = entry.Members.Count == 0
            ? "<tr><td colspan=\"2\">This exported type has no separately listed public members.</td></tr>"
            : string.Join(Environment.NewLine, entry.Members.Select(member => $"<tr><td><a href=\"members/{MemberFile(entry, member)}.html\"><code>{H(member)}</code></a></td><td>{H(MemberKind(member, entry.Kind))}</td></tr>"));
        string example = entry.Example.Length == 0 ? "// This API does not require a separate example." : entry.Example;
        string body = $"""
<p class="eyebrow">{H(PrimaryArea(entry))} · using {H(entry.Namespace)};</p>
<h1>{H(entry.QualifiedName)}</h1>
<p class="lede">{H(entry.Notes)}</p>
<section class="metadata"><div><span>Area</span><strong>{H(PrimaryArea(entry))}</strong></div><div><span>Using</span><strong><code>using {H(entry.Namespace)};</code></strong></div><div><span>Assembly/source</span><strong>{H(entry.Assembly)}</strong></div><div><span>Available in</span><strong>{H(entry.Availability)}</strong></div></section>
<h2>Public items</h2><table><thead><tr><th>Member</th><th>Kind</th></tr></thead><tbody>{rows}</tbody></table>
<h2>How it is provided</h2><p>{H(entry.Implementation)}</p>
<h2>Example</h2><pre><code>{H(example)}</code></pre>
""";
        File.WriteAllText(Path.Combine(output, "api", ApiFile(entry) + ".html"), Page(config, entries, entry.QualifiedName, body, "../", Sidebar(entries, "../", entry)));
    }

    private static void WriteMemberPage(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries, ApiSiteEntry entry, string member)
    {
        string example = entry.MemberExamples.TryGetValue(member, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : entry.Example;
        if (string.IsNullOrWhiteSpace(example)) example = "// Use this member through the documented API surface.";
        string body = $"""
<p class="eyebrow">{H(PrimaryArea(entry))} · {H(entry.Namespace)}</p>
<p><a href="../{ApiFile(entry)}.html">← {H(entry.QualifiedName)}</a></p>
<h1>{H(MemberQualifiedName(entry, member))}</h1>
<section class="metadata"><div><span>Using</span><strong><code>using {H(entry.Namespace)};</code></strong></div><div><span>Kind</span><strong>{H(MemberKind(member, entry.Kind))}</strong></div><div><span>Type/API</span><strong>{H(entry.QualifiedName)}</strong></div><div><span>Availability</span><strong>{H(entry.Availability)}</strong></div></section>
<h2>Signature / item</h2><pre><code>{H(member)}</code></pre>
<h2>Example</h2><pre><code>{H(example)}</code></pre>
""";
        File.WriteAllText(Path.Combine(output, "api", "members", MemberFile(entry, member) + ".html"), Page(config, entries, MemberQualifiedName(entry, member), body, "../../", Sidebar(entries, "../../", entry)));
    }

    private static string Sidebar(IReadOnlyList<ApiSiteEntry> entries, string root, ApiSiteEntry? current)
    {
        StringBuilder sidebar = new();
        sidebar.Append("<aside class=\"sidebar\"><a class=\"help-home\" href=\"").Append(root).Append("index.html\">Inu SDK Help</a><nav class=\"intro-links\"><a href=\"").Append(root).Append("index.html#introduction\">Introduction</a><a href=\"").Append(root).Append("index.html#why\">Why Inu exists</a><a href=\"").Append(root).Append("index.html#how\">How to use it</a><a href=\"").Append(root).Append("index.html#expansion\">Expansion</a></nav>");
        foreach (string area in AreaOrder)
        {
            sidebar.Append("<section class=\"area\"><h2>").Append(area).Append("</h2>");
            foreach (IGrouping<string, ApiSiteEntry> ns in EntriesForArea(entries, area).GroupBy(e => e.Namespace, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                bool open = current is not null && ns.Key == current.Namespace && IsInArea(current, area);
                sidebar.Append("<details class=\"using-group\"").Append(open ? " open" : string.Empty).Append("><summary><code>using ").Append(H(ns.Key)).Append(";</code></summary><div>");
                foreach (ApiSiteEntry entry in ns.OrderBy(e => e.Type, StringComparer.Ordinal))
                    sidebar.Append("<a class=\"").Append(ReferenceEquals(current, entry) ? "active" : string.Empty).Append("\" href=\"").Append(root).Append("api/").Append(ApiFile(entry)).Append(".html\">").Append(H(entry.Type)).Append("</a>");
                sidebar.Append("</div></details>");
            }
            sidebar.Append("</section>");
        }
        sidebar.Append("</aside>");
        return sidebar.ToString();
    }

    private static IEnumerable<ApiSiteEntry> EntriesForArea(IReadOnlyList<ApiSiteEntry> entries, string area) => entries.Where(entry => IsInArea(entry, area) && entry.Status != "legacy");

    private static bool IsInArea(ApiSiteEntry entry, string area)
    {
        if (entry.Category == area) return true;
        if (entry.Category != "Freestanding .NET") return false;
        if (entry.Availability.Contains("kernel and ring-3 userland", StringComparison.OrdinalIgnoreCase)) return true;
        return area == "Userland" && entry.Availability.Contains("ring-3 userland", StringComparison.OrdinalIgnoreCase);
    }

    private static string PrimaryArea(ApiSiteEntry entry) => entry.Category switch
    {
        "Boot" => "Boot",
        "Kernel" => "Kernel",
        "Userland" => "Userland",
        "Freestanding .NET" when entry.Availability.Contains("ring-3 userland", StringComparison.OrdinalIgnoreCase) && !entry.Availability.Contains("kernel", StringComparison.OrdinalIgnoreCase) => "Userland",
        "Freestanding .NET" => "Boot / Kernel / Userland",
        _ => entry.Category
    };

    private static int AreaOrderFor(ApiSiteEntry entry) => entry.Category switch { "Boot" => 0, "Kernel" => 1, "Userland" => 2, "Freestanding .NET" => 3, _ => 9 };

    private static void WriteAssemblyIndex(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries)
    {
        string cards = string.Join(Environment.NewLine, entries.GroupBy(e => e.Assembly, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"<a class=\"card\" href=\"{Slug(g.Key)}.html\"><strong>{H(g.Key)}</strong><span>{g.Count()} exported API surface{(g.Count() == 1 ? string.Empty : "s")}</span></a>"));
        File.WriteAllText(Path.Combine(output, "assemblies", "index.html"), Page(config, entries, "Assemblies", $"<h1>Assemblies and source components</h1><div class=\"cards\">{cards}</div>", "../", Sidebar(entries, "../", null)));
    }

    private static void WriteAssemblyPage(string output, DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries, string assembly, IReadOnlyList<ApiSiteEntry> assemblyEntries)
    {
        string cards = string.Join(Environment.NewLine, assemblyEntries.Select(entry => $"<a class=\"card\" href=\"../api/{ApiFile(entry)}.html\"><strong>{H(entry.QualifiedName)}</strong><span>{H(PrimaryArea(entry))}</span></a>"));
        File.WriteAllText(Path.Combine(output, "assemblies", Slug(assembly) + ".html"), Page(config, entries, assembly, $"<h1>{H(assembly)}</h1><div class=\"cards\">{cards}</div>", "../", Sidebar(entries, "../", null)));
    }

    private static void WriteSearchIndex(string output, IReadOnlyList<ApiSiteEntry> entries)
    {
        object[] index = entries.Select(entry => new { title = entry.QualifiedName, @namespace = entry.Namespace, area = PrimaryArea(entry), assembly = entry.Assembly, members = string.Join(" ", entry.Members), url = "api/" + ApiFile(entry) + ".html" }).ToArray();
        string json = JsonSerializer.Serialize(index);
        File.WriteAllText(Path.Combine(output, "search-index.json"), json);
        File.WriteAllText(Path.Combine(output, "assets", "search-index.js"), "window.InuSearchIndex=" + json + ";");
    }

    private static string Page(DocumentationConfiguration config, IReadOnlyList<ApiSiteEntry> entries, string title, string body, string root, string sidebar) => $"""
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{H(title)} · {H(config.Product)}</title><link rel="stylesheet" href="{root}assets/site.css"><script defer src="{root}assets/search-index.js"></script><script defer src="{root}assets/site.js"></script></head><body><div class="layout">{sidebar}<div class="page"><header><div><strong>Inu SDK {H(config.Version)}</strong><span>Boot · Kernel · Userland</span></div><label class="search"><span>Search</span><input id="site-search" data-root="{root}" placeholder="Search namespaces, types or members"><div id="search-results"></div></label></header><main>{body}</main><footer>Inu SDK {H(config.Version)} · explicit coder-facing API contract</footer></div></div></body></html>
""";

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

    private static string MemberKind(string member, string pageKind)
    {
        if (pageKind.Contains("Primitive", StringComparison.OrdinalIgnoreCase)) return "Primitive/value type";
        if (member.Contains('(')) return member.StartsWith(pageKind + "(", StringComparison.Ordinal) ? "Constructor" : "Method";
        if (pageKind.Contains("Enum", StringComparison.OrdinalIgnoreCase)) return "Enum value";
        if (pageKind.Contains("Interface", StringComparison.OrdinalIgnoreCase)) return "Property / method";
        return "Property / value";
    }

    private static IReadOnlyDictionary<string, string> ReadMemberExamples(JsonElement item)
    {
        Dictionary<string, string> examples = new(StringComparer.Ordinal);
        if (!item.TryGetProperty("memberExamples", out JsonElement map) || map.ValueKind != JsonValueKind.Object) return examples;
        foreach (JsonProperty property in map.EnumerateObject()) if (property.Value.ValueKind == JsonValueKind.String) examples[property.Name] = property.Value.GetString() ?? string.Empty;
        return examples;
    }

    private static string Required(JsonElement item, string name) => item.GetProperty(name).GetString() ?? throw new InvalidDataException($"API contract property '{name}' is required.");
    private static string Optional(JsonElement item, string name, string fallback) => item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    private static string H(string value) => WebUtility.HtmlEncode(value);
    private static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private sealed record ApiSiteEntry(string Namespace, string Type, string Assembly, string Kind, string Category, string Availability, string Status, string CompiledInto, string Implementation, string Notes, IReadOnlyList<string> Members, IReadOnlyDictionary<string, string> MemberExamples, string Example)
    {
        internal string QualifiedName => Namespace + "." + Type;
    }

    private const string Css = """
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#e9eef6;background:#071019;line-height:1.55}*{box-sizing:border-box}body{margin:0}a{color:#72d3ff;text-decoration:none}.layout{display:grid;grid-template-columns:21rem minmax(0,1fr);min-height:100vh}.sidebar{position:sticky;top:0;height:100vh;overflow:auto;background:#0a1520;border-right:1px solid #243446;padding:1.1rem}.help-home{display:block;color:#fff;font-size:1.25rem;font-weight:800;padding:.55rem}.intro-links{display:grid;gap:.2rem;margin:.7rem 0 1.2rem}.intro-links a,.using-group a{padding:.42rem .55rem;border-radius:.45rem}.intro-links a:hover,.using-group a:hover,.using-group a.active{background:#142536;color:#fff}.area{border-top:1px solid #223244;padding-top:.75rem;margin-top:.8rem}.area h2{font-size:.82rem;text-transform:uppercase;letter-spacing:.14em;color:#7fd8ff;margin:.35rem .55rem}.using-group{margin:.25rem 0}.using-group summary{cursor:pointer;padding:.5rem .55rem;border-radius:.45rem;color:#cbd7e5}.using-group summary:hover{background:#101f2e}.using-group>div{display:grid;padding:.2rem 0 .45rem .7rem}.using-group a{font-size:.91rem;color:#9fb3c9}.page{min-width:0}.page>header{position:sticky;top:0;z-index:10;display:flex;align-items:center;justify-content:space-between;gap:2rem;padding:1rem 3vw;background:#071019f2;border-bottom:1px solid #243446;backdrop-filter:blur(12px)}header strong,header span{display:block}header span{font-size:.82rem;color:#8fa2b6}.search{position:relative}.search>span{position:absolute;left:-9999px}.search input{width:min(32vw,30rem);padding:.7rem .85rem;border:1px solid #334a61;border-radius:.65rem;background:#0d1a27;color:#fff}#search-results{position:absolute;right:0;top:3rem;width:35rem;max-width:80vw;background:#0d1a27;border:1px solid #334a61;border-radius:.7rem;box-shadow:0 1rem 3rem #0009;overflow:hidden}#search-results a{display:block;padding:.7rem .9rem;border-bottom:1px solid #223244}main{max-width:76rem;padding:3.2rem 5vw 6rem}.hero{padding:2rem 0}.hero h1,h1{font-size:clamp(2.4rem,5vw,4.8rem);line-height:1.02;letter-spacing:-.04em;margin:.2em 0}.hero p,.lede{font-size:1.12rem;color:#b8c5d5;max-width:60rem}.eyebrow{font-size:.77rem;letter-spacing:.14em;text-transform:uppercase;color:#74d6ff;font-weight:800}.three{display:grid;grid-template-columns:repeat(3,1fr);gap:1rem}.three article,.metadata div,.card{border:1px solid #263a4e;background:#0c1824;border-radius:.8rem;padding:1rem}.three h3{margin-top:0}.metadata{display:grid;grid-template-columns:repeat(2,1fr);gap:.8rem;margin:1.5rem 0}.metadata span{display:block;color:#8da1b7;font-size:.82rem}.metadata strong{display:block;margin-top:.25rem;overflow-wrap:anywhere}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(15rem,1fr));gap:1rem}.card{display:flex;flex-direction:column}.card strong{color:#fff}.card span{color:#9eb1c5}table{width:100%;border-collapse:collapse;margin:1rem 0}th,td{text-align:left;padding:.8rem;border-bottom:1px solid #263a4e;vertical-align:top}th{color:#9eb1c5}pre{overflow:auto;background:#03070b;border:1px solid #263a4e;border-radius:.8rem;padding:1rem}code{font-family:Cascadia Code,Consolas,monospace}section{margin-bottom:2.8rem}footer{padding:1.2rem 5vw 3rem;color:#8194a8}@media(max-width:950px){.layout{grid-template-columns:1fr}.sidebar{position:relative;height:auto;border-right:0;border-bottom:1px solid #243446}.area{display:block}.page>header{position:relative}.search input{width:45vw}.three{grid-template-columns:1fr}}@media(max-width:560px){.page>header{align-items:flex-start;flex-direction:column}.search,.search input{width:100%}.metadata{grid-template-columns:1fr}main{padding-top:2rem}}
""";

    private const string JavaScript = """
(()=>{document.addEventListener('DOMContentLoaded',()=>{const input=document.querySelector('#site-search');const box=document.querySelector('#search-results');if(!input||!box)return;const root=input.dataset.root||'';const items=window.InuSearchIndex||[];input.addEventListener('input',()=>{const q=input.value.trim().toLowerCase();box.innerHTML='';if(q.length<2)return;for(const item of items.filter(x=>(x.title+' '+x.namespace+' '+x.area+' '+x.assembly+' '+x.members).toLowerCase().includes(q)).slice(0,14)){const a=document.createElement('a');a.href=root+item.url;a.textContent=item.area+' · '+item.title;box.appendChild(a)}})})})();
""";
}
