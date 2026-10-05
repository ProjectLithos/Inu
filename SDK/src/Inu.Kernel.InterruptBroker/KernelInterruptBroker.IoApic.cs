using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Pci;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Optional I/O APIC discovery, MMIO mapping, GSI routing and masking provider.</summary>
public static unsafe class KernelIoApicRouter
{
    private struct IoApicMap
    {
        internal UInt64 VirtualAddress;
        internal UInt32 BaseGsi, MaximumGsi;
    }

    private static KernelHeapAllocation _allocation;
    private static IoApicMap* _ioApics;
    private static UInt32 _ioApicCount;
    private static Boolean _initialized;

    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!MapIoApics()) return false;
        if (_ioApicCount == 0U) { _initialized = true; return true; }
        if (!KernelIoApicRouterServices.Register(&TryRouteGsi, &MaskGsi)) return false;
        _initialized = true;
        return true;
    }

    public static Boolean IsAvailable() => KernelIoApicRouterServices.IsRegistered;

    private static Boolean TryRouteGsi(KernelDriverInterruptRequest* request, Byte vector, UInt32 targetProcessor)
    {
        if (request == null || !TryFindIoApic(request->Source, out UInt64 io, out UInt32 baseGsi)) return false;
        UInt32 pin = request->Source - baseGsi;
        UInt32 low = vector;
        if (request->ActiveLow) low |= 1U << 13;
        if (request->LevelTriggered) low |= 1U << 15;
        UInt32 high = (KernelInterruptAffinityServices.ResolveApicId(targetProcessor) & 0xFFU) << 24;
        return IoWrite(io, 0x11U + pin * 2U, high) && IoWrite(io, 0x10U + pin * 2U, low);
    }

    private static Boolean MaskGsi(UInt32 gsi)
    {
        if (!TryFindIoApic(gsi, out UInt64 io, out UInt32 baseGsi)) return false;
        UInt32 pin = gsi - baseGsi;
        if (!IoRead(io, 0x10U + pin * 2U, out UInt32 low)) return false;
        return IoWrite(io, 0x10U + pin * 2U, low | (1U << 16));
    }

    private static Boolean TryFindIoApic(UInt32 gsi, out UInt64 mapped, out UInt32 baseGsi)
    {
        mapped = 0; baseGsi = 0;
        for (UInt32 i = 0; i < _ioApicCount; i++)
        {
            IoApicMap* apic = _ioApics + i;
            if (gsi < apic->BaseGsi || gsi > apic->MaximumGsi) continue;
            mapped = apic->VirtualAddress; baseGsi = apic->BaseGsi; return true;
        }
        return false;
    }

    private static Boolean MapIoApics()
    {
        UInt32 advertised = KernelAcpi.GetIoApicCount();
        _ioApicCount = 0U;
        if (advertised == 0U) return true;
        if (!KernelHeap.TryAllocate((UInt64)advertised * (UInt64)sizeof(IoApicMap), 16UL, true, out _allocation)) return false;
        _ioApics = (IoApicMap*)(nuint)_allocation.Address;
        for (UInt32 i = 0; i < advertised; i++)
        {
            if (!KernelAcpi.TryGetIoApic(i, out AcpiIoApicInfo info)) continue;
            if (!KernelPci.TryMapMmio(info.Address, 0x20UL, out UInt64 mapped)) continue;
            if (!IoRead(mapped, 1U, out UInt32 version)) continue;
            UInt32 maximumRedirectionEntry = (version >> 16) & 0xFFU;
            IoApicMap* entry = _ioApics + _ioApicCount++;
            entry->VirtualAddress = mapped;
            entry->BaseGsi = info.GlobalSystemInterruptBase;
            entry->MaximumGsi = info.GlobalSystemInterruptBase + maximumRedirectionEntry;
        }
        return true;
    }

    private static Boolean IoRead(UInt64 io, UInt32 index, out UInt32 value)
    {
        value = 0;
        if (!Inu.Kernel.Internal.X64.Native.WriteMmio32(io, index)) return false;
        value = Inu.Kernel.Internal.X64.Native.ReadMmio32(io + 0x10UL);
        return true;
    }

    private static Boolean IoWrite(UInt64 io, UInt32 index, UInt32 value) =>
        Inu.Kernel.Internal.X64.Native.WriteMmio32(io, index) && Inu.Kernel.Internal.X64.Native.WriteMmio32(io + 0x10UL, value);
}
