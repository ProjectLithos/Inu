using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static class UserlandStageCache
{
    private sealed record CacheEntry(int Schema, string Inputs, string Outputs);
    internal static string Fingerprint(IEnumerable<string> paths, IEnumerable<string> values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value + "\0"));
        Add("InuUserlandStageCache:1");
        foreach (string value in values) Add(value);
        foreach (string input in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            Add(input);
            IEnumerable<string> files = File.Exists(input) ? [input] : Directory.Exists(input) ? SourceFiles(input) : [];
            foreach (string file in files.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                Add(file); Add(new FileInfo(file).Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using FileStream stream = File.OpenRead(file);
                byte[] buffer = new byte[65536]; int count;
                while ((count = stream.Read(buffer)) != 0) hash.AppendData(buffer.AsSpan(0, count));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    internal static IEnumerable<string> SourceFiles(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root)) yield return file;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string name = Path.GetFileName(directory);
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase) || name is ".git" or "Artifacts") continue;
            foreach (string file in SourceFiles(directory)) yield return file;
        }
    }
    internal static IEnumerable<string> ProjectInputs(string project, HashSet<string>? seen = null)
    {
        seen ??= new(StringComparer.OrdinalIgnoreCase); project = Path.GetFullPath(project);
        if (!seen.Add(project)) yield break;
        yield return project; string directory = Path.GetDirectoryName(project)!; yield return directory;
        for (DirectoryInfo? parent = new(directory); parent is not null; parent = parent.Parent)
            foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.Config", "global.json" }) yield return Path.Combine(parent.FullName, name);
        if (!File.Exists(project)) yield break;
        XDocument xml = XDocument.Load(project);
        foreach (XElement item in xml.Descendants())
        {
            string? include = (string?)item.Attribute(item.Name.LocalName == "Import" ? "Project" : "Include");
            if (include is null || include.Contains("$(", StringComparison.Ordinal) || include.IndexOfAny(['*', '?']) >= 0) continue;
            string path = Path.GetFullPath(Path.Combine(directory, include));
            if (item.Name.LocalName == "ProjectReference") foreach (string input in ProjectInputs(path, seen)) yield return input;
            else if (item.Name.LocalName is "Compile" or "Import") yield return path;
        }
    }
    internal static int Run(string name, string cacheDirectory, IEnumerable<string> inputs, string[] outputs, IEnumerable<string> arguments, bool force, Func<int> action)
    {
        string[] inputPaths = inputs.ToArray(), args = arguments.ToArray();
        string key = Fingerprint([], new[] { name }.Concat(outputs));
        string cachePath = Path.Combine(cacheDirectory, key + ".json");
        string before = Fingerprint(inputPaths, args);
        bool Ready() => outputs.All(p => File.Exists(p) || Directory.Exists(p) && SourceFiles(p).Any());
        try
        {
            if (!force && Ready() && File.Exists(cachePath))
            {
                CacheEntry? entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(cachePath));
                if (entry is { Schema: 1 } && entry.Inputs == before && entry.Outputs == Fingerprint(outputs, []))
                { Console.WriteLine($"[ OK ] {name}: cached; inputs and outputs are current."); return 0; }
            }
        }
        catch (JsonException) { } // A damaged record is a miss.
        // File.Delete can throw when the parent directory is missing on Windows.
        // Initialise the cache on the first run before invalidating a record.
        Directory.CreateDirectory(cacheDirectory);
        File.Delete(cachePath);
        int result = action(); if (result != 0) return result;
        if (!Ready()) { Console.Error.WriteLine($"[FAIL] {name} did not produce its cache outputs."); return 1; }
        if (before != Fingerprint(inputPaths, args)) { Console.WriteLine($"[INFO] {name}: inputs changed during execution; cache not saved."); return 0; }
        Directory.CreateDirectory(cacheDirectory);
        string temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(new CacheEntry(1, before, Fingerprint(outputs, [])))); File.Move(temporary, cachePath, true); }
        finally { File.Delete(temporary); }
        return 0;
    }
}
