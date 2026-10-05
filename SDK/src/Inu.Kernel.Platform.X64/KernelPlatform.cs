using System;
using Inu.Arch.X64;

namespace Inu.Kernel.Platform.X64;

/// <summary>Provides high-level x64 processor initialization behind the canonical architecture boundary.</summary>
public static class KernelPlatform
{
    public static Boolean InitializeDescriptors() => X64ArchitectureBoundary.InitializeDescriptors();
    public static Boolean InitializeInterrupts() => X64ArchitectureBoundary.InitializeInterrupts();
    public static Boolean DisableLegacyPic() => X64ArchitectureBoundary.DisableLegacyPic();
    public static Boolean Halt() => X64ArchitectureBoundary.Halt();
}
