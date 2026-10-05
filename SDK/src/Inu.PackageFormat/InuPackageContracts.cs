using System.Text.Json.Serialization;

namespace Inu.PackageFormat;

public static class InuPackageFormat
{
    public const string ContainerExtension = ".zip";
    public const string ManifestName = "Inu.Package.json";
    public const string Format = "inu-package-v1";
    public const int SchemaVersion = 1;
}

[JsonConverter(typeof(JsonStringEnumConverter<InuPackageKind>))]
public enum InuPackageKind
{
    Application,
    Driver,
    Library,
    Service,
    KernelExtension
}

public sealed class InuPackageManifest
{
    public string Format { get; set; } = InuPackageFormat.Format;
    public int SchemaVersion { get; set; } = InuPackageFormat.SchemaVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public InuPackageKind Type { get; set; }
    public string Publisher { get; set; } = "";
    public string[] Architectures { get; set; } = ["any"];
    public InuPackageRequirements Requires { get; set; } = new();
    public InuPackageDependency[] Dependencies { get; set; } = [];
    public string[] Capabilities { get; set; } = [];
    public InuPackageFile[] Files { get; set; } = [];
    public InuPackageInstall Install { get; set; } = new();
    public InuPackageSignature Signing { get; set; } = new();
}

public sealed class InuPackageRequirements
{
    public string MinimumInuVersion { get; set; } = "0.0.0";
    public string SdkApiVersion { get; set; } = "1.0";
    public string AbiVersion { get; set; } = "1.0";
}

public sealed class InuPackageDependency
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "*";
    public bool Optional { get; set; }
}

public sealed class InuPackageFile
{
    public string Path { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class InuPackageInstall
{
    public string Entry { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string ServiceStartup { get; set; } = "manual";
    public string LibraryKind { get; set; } = "";
}

public sealed class InuPackageSignature
{
    public string State { get; set; } = "unsigned";
    public string Algorithm { get; set; } = "";
    public string SignerId { get; set; } = "";
    public string ManifestDigest { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed record InuPackageInspection(InuPackageManifest Manifest, IReadOnlyList<string> Entries);

public sealed record InuPackageVerification(bool Success, InuPackageManifest? Manifest, IReadOnlyList<string> Errors)
{
    public static InuPackageVerification Failed(params string[] errors) => new(false, null, errors);
}
