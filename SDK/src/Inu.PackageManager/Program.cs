using System.IO.Compression;
using System.Text.Json;
using Inu.PackageFormat;

namespace Inu.PackageManager;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static int Main(string[] args)
    {
        if (args.Length == 0) return Usage();
        var command = args[0].ToLowerInvariant();
        var root = GetOption(args, "--root") ?? Path.Combine(Environment.CurrentDirectory, "InuSystem");
        try
        {
            return command switch
            {
                "verify" when args.Length >= 2 => Verify(args[1]),
                "inspect" when args.Length >= 2 => Inspect(args[1]),
                "install" when args.Length >= 2 => Install(args[1], root),
                "uninstall" when args.Length >= 2 => Uninstall(args[1], root),
                "list" => List(root),
                _ => Usage()
            };
        }
        catch (Exception ex) { Console.Error.WriteLine("[FAIL] " + ex.Message); return 1; }
    }

    private static int Usage()
    {
        Console.WriteLine("Inu.PackageManager verify <package.zip>");
        Console.WriteLine("Inu.PackageManager inspect <package.zip>");
        Console.WriteLine("Inu.PackageManager install <package.zip> [--root <system-root>]");
        Console.WriteLine("Inu.PackageManager uninstall <package-id> [--root <system-root>]");
        Console.WriteLine("Inu.PackageManager list [--root <system-root>]");
        return 2;
    }

    private static int Verify(string path)
    {
        var result = InuPackageArchive.Verify(Path.GetFullPath(path));
        foreach (var error in result.Errors) Console.Error.WriteLine("[FAIL] " + error);
        if (!result.Success) return 1;
        Console.WriteLine($"[ OK ] Verified Inu package {result.Manifest!.Id} {result.Manifest.Version} ({result.Manifest.Type}).");
        return 0;
    }

    private static int Inspect(string path)
    {
        var result = InuPackageArchive.Verify(Path.GetFullPath(path));
        if (!result.Success) { foreach (var error in result.Errors) Console.Error.WriteLine("[FAIL] " + error); return 1; }
        Console.WriteLine(JsonSerializer.Serialize(result.Manifest, JsonOptions));
        return 0;
    }

    private static int Install(string path, string systemRoot)
    {
        path = Path.GetFullPath(path); systemRoot = Path.GetFullPath(systemRoot);
        var verify = InuPackageArchive.Verify(path);
        if (!verify.Success) throw new InvalidDataException(string.Join("; ", verify.Errors));
        var manifest = verify.Manifest!;
        var db = LoadDatabase(systemRoot);
        foreach (var dependency in manifest.Dependencies.Where(d => !d.Optional))
        {
            var installed = db.Packages.FirstOrDefault(p => p.Id.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase));
            if (installed is null || !InuVersionConstraint.Matches(installed.Version, dependency.Version))
                throw new InvalidOperationException($"Unresolved dependency: {dependency.Id} {dependency.Version}");
        }
        if (manifest.Type == InuPackageKind.KernelExtension && manifest.Signing.State is not ("signed" or "trusted"))
            throw new InvalidOperationException("Kernel extensions require signed/trusted package policy.");

        var txRoot = Path.Combine(systemRoot, "System", "Packages", "transactions", Guid.NewGuid().ToString("N"));
        var stage = Path.Combine(txRoot, "stage");
        var previousBackup = Path.Combine(txRoot, "previous");
        var final = Path.Combine(systemRoot, "System", "Packages", "installed", manifest.Id, manifest.Version);
        var previous = db.Packages.FirstOrDefault(p => p.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase));
        if (previous is not null && previous.Version.Equals(manifest.Version, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Package {manifest.Id} {manifest.Version} is already installed.");
        Directory.CreateDirectory(stage);
        var newTreeCommitted = false;
        var previousTreeMoved = false;
        try
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var file in manifest.Files)
            {
                var entry = archive.GetEntry(InuPackageArchive.NormalizeEntryPath(file.Path)) ?? throw new InvalidDataException($"Missing {file.Path}");
                var rel = InuPackageArchive.NormalizeEntryPath(file.Path);
                var dest = Path.GetFullPath(Path.Combine(stage, rel.Replace('/', Path.DirectorySeparatorChar)));
                if (!dest.StartsWith(Path.GetFullPath(stage) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package attempted path escape.");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!); entry.ExtractToFile(dest, true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            if (Directory.Exists(final)) throw new IOException($"Install target already exists: {final}");
            if (previous is not null && Directory.Exists(previous.InstallPath))
            {
                Directory.Move(previous.InstallPath, previousBackup);
                previousTreeMoved = true;
            }
            Directory.Move(stage, final); // atomic within the same volume
            newTreeCommitted = true;
            db.Packages.RemoveAll(p => p.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase));
            db.Packages.Add(new InstalledPackage { Id = manifest.Id, Name = manifest.Name, Version = manifest.Version, Type = manifest.Type.ToString(), InstallPath = final, Dependencies = manifest.Dependencies });
            SaveDatabaseAtomic(systemRoot, db);
            if (previousTreeMoved && Directory.Exists(previousBackup)) Directory.Delete(previousBackup, true);
            Console.WriteLine($"[ OK ] Installed {manifest.Id} {manifest.Version} transactionally.");
            return 0;
        }
        catch
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            if (newTreeCommitted && Directory.Exists(final)) Directory.Delete(final, true);
            if (previousTreeMoved && previous is not null && Directory.Exists(previousBackup) && !Directory.Exists(previous.InstallPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(previous.InstallPath)!);
                Directory.Move(previousBackup, previous.InstallPath);
            }
            throw;
        }
        finally { if (Directory.Exists(txRoot)) Directory.Delete(txRoot, true); }
    }

    private static int Uninstall(string id, string systemRoot)
    {
        systemRoot = Path.GetFullPath(systemRoot); var db = LoadDatabase(systemRoot);
        var target = db.Packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Package is not installed: {id}");
        var blockers = db.Packages.Where(p => p.Dependencies.Any(d => !d.Optional && d.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).Select(p => p.Id).ToArray();
        if (blockers.Length != 0) throw new InvalidOperationException("Package is required by: " + string.Join(", ", blockers));
        var trash = Path.Combine(systemRoot, "System", "Packages", "transactions", "remove-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(trash)!);
        if (Directory.Exists(target.InstallPath)) Directory.Move(target.InstallPath, trash);
        try
        {
            db.Packages.Remove(target); SaveDatabaseAtomic(systemRoot, db);
            if (Directory.Exists(trash)) Directory.Delete(trash, true);
            Console.WriteLine($"[ OK ] Removed {id}."); return 0;
        }
        catch { if (Directory.Exists(trash) && !Directory.Exists(target.InstallPath)) Directory.Move(trash, target.InstallPath); throw; }
    }

    private static int List(string root)
    {
        var db = LoadDatabase(Path.GetFullPath(root));
        foreach (var package in db.Packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)) Console.WriteLine($"{package.Id}\t{package.Version}\t{package.Type}");
        return 0;
    }

    private static PackageDatabase LoadDatabase(string root)
    {
        var path = DatabasePath(root); if (!File.Exists(path)) return new();
        return JsonSerializer.Deserialize<PackageDatabase>(File.ReadAllText(path), JsonOptions) ?? new();
    }

    private static void SaveDatabaseAtomic(string root, PackageDatabase db)
    {
        var path = DatabasePath(root); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N"); File.WriteAllText(temp, JsonSerializer.Serialize(db, JsonOptions)); File.Move(temp, path, true);
    }

    private static string DatabasePath(string root) => Path.Combine(root, "System", "Packages", "database.json");
    private static string? GetOption(string[] values, string name) { for (var i = 0; i + 1 < values.Length; i++) if (values[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return values[i + 1]; return null; }

    private sealed class PackageDatabase { public int SchemaVersion { get; set; } = 1; public List<InstalledPackage> Packages { get; set; } = []; }
    private sealed class InstalledPackage { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string Version { get; set; } = ""; public string Type { get; set; } = ""; public string InstallPath { get; set; } = ""; public InuPackageDependency[] Dependencies { get; set; } = []; }
}
