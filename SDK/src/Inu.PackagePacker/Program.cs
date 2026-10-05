using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Inu.PackageFormat;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: Inu.PackagePacker <Inu.Package.json> <payload-directory> <output.zip>");
    return 2;
}

var manifestPath = Path.GetFullPath(args[0]);
var payloadRoot = Path.GetFullPath(args[1]);
var outputPath = Path.GetFullPath(args[2]);
if (!outputPath.EndsWith(InuPackageFormat.ContainerExtension, StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Inu package files use the standard .zip extension.");
if (!Directory.Exists(payloadRoot)) throw new DirectoryNotFoundException(payloadRoot);

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
var manifest = JsonSerializer.Deserialize<InuPackageManifest>(File.ReadAllText(manifestPath), options)
    ?? throw new InvalidDataException("Invalid Inu.Package.json.");
InuPackageArchive.ValidateManifest(manifest);

var files = Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories)
    .Select(path => new { Source = path, Relative = "payload/" + Path.GetRelativePath(payloadRoot, path).Replace('\\', '/') })
    .OrderBy(x => x.Relative, StringComparer.Ordinal)
    .ToArray();
manifest.Files = files.Select(x => new InuPackageFile
{
    Path = x.Relative,
    Length = new FileInfo(x.Source).Length,
    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x.Source))).ToLowerInvariant()
}).ToArray();

var temp = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
try
{
    using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
    {
        var manifestEntry = zip.CreateEntry(InuPackageFormat.ManifestName, CompressionLevel.Optimal);
        using (var writer = new StreamWriter(manifestEntry.Open())) writer.Write(JsonSerializer.Serialize(manifest, options));
        foreach (var file in files)
            zip.CreateEntryFromFile(file.Source, file.Relative, CompressionLevel.Optimal);
    }
    var verification = InuPackageArchive.Verify(temp);
    if (!verification.Success) throw new InvalidDataException("Packed package verification failed: " + string.Join("; ", verification.Errors));
    File.Move(temp, outputPath, true);
    Console.WriteLine($"[ OK ] Inu ZIP package: {outputPath}");
    Console.WriteLine($"[INFO] Type={manifest.Type}, id={manifest.Id}, version={manifest.Version}, payload files={manifest.Files.Length}");
    return 0;
}
finally { if (File.Exists(temp)) File.Delete(temp); }
