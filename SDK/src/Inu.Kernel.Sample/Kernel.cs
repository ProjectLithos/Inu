using System.Runtime.InteropServices;
using Inu.Architecture.X64;
using Inu.Boot.Contracts;
using Inu.Boot.Memory;
using Inu.Console.Framebuffer;
using Inu.Console.Serial;
using Inu.Core;
using Inu.Primitives;

namespace Inu.Kernel.Sample;

public static class Kernel
{
    private const uint ConsoleFontSize = 16U;

    [KernelEntry]
    public static bool KMain(IBootFramebuffer framebufferBoot, IBootMemoryMapBuffer memoryMap, IUefiMemoryMapKey memoryMapKey, IMemoryDescriptorLayout descriptorLayout)
    {
        SerialConsole serial = new();
        FramebufferConsole framebuffer = new();
        if (!serial.Configure(SerialConfiguration.Com1())) return false;
        if (!framebuffer.Configure(FramebufferConfiguration.Default(ConsoleFontSize))) return false;
        if (!serial.Initialize()) return false;
        if (!framebuffer.Initialize(framebufferBoot)) return false;
        if (!WriteLine(serial, framebuffer, "Inu KMain started.")) return false;
        if (!NativeUefiMemoryMapSource.TryCreate(memoryMap, memoryMapKey, descriptorLayout, out NativeUefiMemoryMapSource? finalMap)) return false;
        if (finalMap is null || finalMap.Count < 1) return false;
        if (!WriteLine(serial, framebuffer, "Final UEFI memory map retained; ExitBootServices succeeded.")) return false;
        if (!WriteLine(serial, framebuffer, "Native UEFI memory-map adapter online.")) return false;
        KernelDiagnosticSink diagnostics = new(serial, framebuffer);
        if (!PlatformInitialization.Initialize(diagnostics)) return false;
        if (!WriteLine(serial, framebuffer, "GDT, TSS, IDT, exceptions, and interrupt-controller contracts online.")) return false;
        if (!AddressSpaceExamples.ValidateStandardLayout()) return false;
        if (!VirtualMemoryExamples.ValidateContracts()) return false;
        if (!WriteLine(serial, framebuffer, "Virtual-memory contracts and x64 page-table codec online.")) return false;
        if (!HeapExamples.ValidateContracts()) return false;
        if (!WriteLine(serial, framebuffer, "Early-allocation and first-fit heap methodologies online.")) return false;
        if (!WriteLine(serial, framebuffer, "CPU halted.")) return false;
        return CPU.Halt();
    }

    [UnmanagedCallersOnly(EntryPoint = "InuManagedEntry")]
    public static unsafe byte NativeEntry(nint bootContextAddress)
    {
        if (bootContextAddress == 0) return 0;
        NativeBootContext* native = (NativeBootContext*)bootContextAddress;
        if (native->Signature != 0x4E59524F41564F4EUL) return 0;

        Framebuffer framebuffer = new(
            new PhysicalAddress(native->FramebufferAddress),
            native->FramebufferSize,
            native->Width,
            native->Height,
            native->PixelsPerScanLine,
            (FramebufferPixelFormat)native->PixelFormat,
            new PixelBitMask(native->RedMask, native->GreenMask, native->BlueMask, native->ReservedMask));
        if (native->FinalMemoryMapFlag != 1 || native->ExitBootServicesStatus != 0) return 0;

        BootFramebufferContext framebufferBoot = new(framebuffer);
        BootMemoryMapBufferContext memoryMap = new(
            new PhysicalAddress(native->FinalMemoryMapAddress),
            native->FinalMemoryMapLength);
        UefiMemoryMapKeyContext memoryMapKey = new(native->FinalMemoryMapKey);
        MemoryDescriptorLayoutContext descriptorLayout = new(
            native->FinalMemoryDescriptorSize,
            native->FinalMemoryDescriptorVersion);
        return KMain(framebufferBoot, memoryMap, memoryMapKey, descriptorLayout) ? (byte)1 : (byte)0;
    }

    private static bool WriteLine(SerialConsole serial, FramebufferConsole framebuffer, ReadOnlySpan<char> text)
    {
        if (!serial.WriteLine(text)) return false;
        return framebuffer.WriteLine(text);
    }

    #pragma warning disable CS0649 // Populated by the native UEFI entry before managed execution.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBootContext
    {
        internal ulong Signature;
        internal ulong FramebufferAddress;
        internal ulong FramebufferSize;
        internal uint Width;
        internal uint Height;
        internal uint PixelsPerScanLine;
        internal uint PixelFormat;
        internal uint RedMask;
        internal uint GreenMask;
        internal uint BlueMask;
        internal uint ReservedMask;
        internal ulong FinalMemoryMapAddress;
        internal ulong FinalMemoryMapLength;
        internal ulong FinalMemoryMapKey;
        internal ulong FinalMemoryDescriptorSize;
        internal uint FinalMemoryDescriptorVersion;
        internal uint FinalMemoryMapCaptureAttempts;
        internal ulong ExitBootServicesStatus;
        internal ulong FinalMemoryMapFlag;
        internal ulong BootstrapPageTableWorkspaceAddress;
        internal ulong BootstrapPageTableWorkspacePages;
        internal ulong AcpiRootPointerAddress;
    }
    #pragma warning restore CS0649
}
