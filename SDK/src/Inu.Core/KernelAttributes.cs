namespace Inu.Core;

/// <summary>Marks the managed method that serves as the kernel entry point.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class KernelEntryAttribute : Attribute
{
}

/// <summary>Marks a method that does not return to its caller.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DoesNotReturnAttribute : Attribute
{
}

/// <summary>Represents a Inu semantic version.</summary>
/// <param name="Major">The major version component.</param>
/// <param name="Minor">The minor version component.</param>
/// <param name="Patch">The patch version component.</param>
public readonly record struct VersionInfo(ushort Major, ushort Minor, ushort Patch)
{
    /// <summary>Gets the version of the currently installed Inu SDK.</summary>
    public static VersionInfo Current => new(0, 0, 98);
}
