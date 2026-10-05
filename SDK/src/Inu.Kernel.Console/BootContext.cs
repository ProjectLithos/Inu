using System;

namespace Inu.Kernel.Console;

/// <summary>Minimal kernel-entry boot boundary. Concrete boot information is exposed only through independently selectable capability interfaces.</summary>
public interface IBootContext
{
    Boolean IsAvailable();
}

#pragma warning disable CS0649 // Populated by the native boot entry before managed execution.
internal struct NativeBootHandoffLayout
{
    internal UInt64 Signature;
    internal UInt64 FramebufferAddress;
    internal UInt64 FramebufferSize;
    internal UInt32 Width;
    internal UInt32 Height;
    internal UInt32 PixelsPerScanLine;
    internal UInt32 PixelFormat;
    internal UInt32 RedMask;
    internal UInt32 GreenMask;
    internal UInt32 BlueMask;
    internal UInt32 ReservedMask;
    internal UInt64 FinalMemoryMapAddress;
    internal UInt64 FinalMemoryMapLength;
    internal UInt64 FinalMemoryMapKey;
    internal UInt64 FinalMemoryDescriptorSize;
    internal UInt32 FinalMemoryDescriptorVersion;
    internal UInt32 FinalMemoryMapCaptureAttempts;
    internal UInt64 ExitBootServicesStatus;
    internal UInt64 FinalMemoryMapFlag;
    internal UInt64 BootstrapPageTableWorkspaceAddress;
    internal UInt64 BootstrapPageTableWorkspacePages;
    internal UInt64 AcpiRootPointerAddress;
    internal UInt64 ApplicationProcessorTrampolineAddress;
    internal UInt64 ApplicationProcessorTrampolinePages;
    internal UInt64 SystemAssetBundleAddress;
    internal UInt64 SystemAssetBundleLength;
    internal UInt64 KernelImageBase;
}
#pragma warning restore CS0649

/// <summary>Native boot hand-off root. Each optional capability is implemented by a separate partial source file.</summary>
public readonly unsafe partial struct NativeBootContext : IBootContext
{
    private readonly UInt64 _nativeAddress;
    public NativeBootContext(UInt64 nativeAddress) => _nativeAddress = nativeAddress;
    internal NativeBootHandoffLayout* GetCSharpontext() => (NativeBootHandoffLayout*)_nativeAddress;
    public Boolean IsAvailable()
    {
        NativeBootHandoffLayout* context = GetCSharpontext();
        return context != null && context->Signature == 0x4E59524F41564F4EUL;
    }
}
