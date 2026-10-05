namespace Inu.Core;

/// <summary>Exposes the stable, versioned public contracts implemented by this Inu SDK.</summary>
public static class InuSdkContract
{
    /// <summary>Gets the Inu SDK product release version.</summary>
    public const string SdkVersion = "0.0.64";

    /// <summary>Gets the stable Inu public API contract version.</summary>
    public const string ApiVersion = "3.0";

    /// <summary>Gets the overall Inu binary ABI contract version.</summary>
    public const string AbiVersion = "1.0";

    /// <summary>Gets the Inu kernel ABI contract version.</summary>
    public const string KernelAbiVersion = "1.0";

    /// <summary>Gets the Inu driver ABI contract version.</summary>
    public const string DriverAbiVersion = "1.0";

    /// <summary>Gets the Inu system-call ABI contract version.</summary>
    public const string SyscallAbiVersion = "2.0";

    /// <summary>Gets the Inu debugger ABI contract version.</summary>
    public const string DebugAbiVersion = "1.0";

    /// <summary>Gets the Inu crash-dump ABI contract version.</summary>
    public const string CrashDumpAbiVersion = "1.0";

    /// <summary>Gets the Inu kernel-heap diagnostics ABI contract version.</summary>
    public const string HeapDiagnosticsAbiVersion = "1.0";

    /// <summary>Returns true when a consumer API major version is compatible with this SDK.</summary>
    /// <param name="majorVersion">The public API major version required by the consumer.</param>
    /// <returns><see langword="true"/> when the requested major version is supported; otherwise <see langword="false"/>.</returns>
    public static bool IsApiCompatible(int majorVersion) => majorVersion == 1;

    /// <summary>Returns true when a driver ABI major version is compatible with this SDK.</summary>
    /// <param name="majorVersion">The driver ABI major version required by the driver.</param>
    /// <returns><see langword="true"/> when the requested driver ABI major version is supported; otherwise <see langword="false"/>.</returns>
    public static bool IsDriverAbiCompatible(int majorVersion) => majorVersion == 1;

    /// <summary>Returns true when a crash-dump ABI major version can be opened by this SDK.</summary>
    public static bool IsCrashDumpAbiCompatible(int majorVersion) => majorVersion == 1;
}
