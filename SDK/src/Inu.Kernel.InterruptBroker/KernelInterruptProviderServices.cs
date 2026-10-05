using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Pci;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Result returned by an optional PCI interrupt-delivery provider.</summary>
public struct KernelPciInterruptRouteResult
{
    public KernelInterruptDeliveryMechanism Mechanism;
    public UInt32 Source;
}

/// <summary>Parent-owned contract used by the broker to reach an optional I/O APIC router.</summary>
public static unsafe class KernelIoApicRouterServices
{
    private static delegate*<KernelDriverInterruptRequest*, Byte, UInt32, Boolean> _route;
    private static delegate*<UInt32, Boolean> _mask;

    public static Boolean IsRegistered => _route != null && _mask != null;

    public static Boolean Register(delegate*<KernelDriverInterruptRequest*, Byte, UInt32, Boolean> route, delegate*<UInt32, Boolean> mask)
    {
        if (route == null || mask == null || IsRegistered) return false;
        _route = route;
        _mask = mask;
        return true;
    }

    public static Boolean TryRoute(KernelDriverInterruptRequest* request, Byte vector, UInt32 targetProcessor) =>
        _route != null && request != null && _route(request, vector, targetProcessor);

    public static Boolean TryMask(UInt32 gsi) => _mask != null && _mask(gsi);
}

/// <summary>Parent-owned contract used by the broker to reach an optional PCI MSI/MSI-X/INTx delivery provider.</summary>
public static unsafe class KernelPciInterruptRouterServices
{
    private static delegate*<PciLocation, KernelDriverInterruptRequest*, Byte, UInt32, KernelPciInterruptRouteResult*, Boolean> _route;
    private static delegate*<PciLocation, KernelInterruptDeliveryMechanism, UInt32, Boolean> _release;

    public static Boolean IsRegistered => _route != null && _release != null;

    public static Boolean Register(
        delegate*<PciLocation, KernelDriverInterruptRequest*, Byte, UInt32, KernelPciInterruptRouteResult*, Boolean> route,
        delegate*<PciLocation, KernelInterruptDeliveryMechanism, UInt32, Boolean> release)
    {
        if (route == null || release == null || IsRegistered) return false;
        _route = route;
        _release = release;
        return true;
    }

    public static Boolean TryRoute(PciLocation location, KernelDriverInterruptRequest* request, Byte vector, UInt32 targetProcessor, out KernelPciInterruptRouteResult result)
    {
        result = default;
        if (_route == null || request == null) return false;
        KernelPciInterruptRouteResult local = default;
        Boolean routed = _route(location, request, vector, targetProcessor, &local);
        result = local;
        return routed;
    }

    public static Boolean TryRelease(PciLocation location, KernelInterruptDeliveryMechanism mechanism, UInt32 source) =>
        _release != null && _release(location, mechanism, source);
}

/// <summary>Parent-owned contract for optional interrupt CPU/APIC affinity policy.</summary>
public static unsafe class KernelInterruptAffinityServices
{
    private static delegate*<UInt32, UInt32> _resolveProcessor;
    private static delegate*<UInt32, UInt32> _resolveApicId;

    public static Boolean IsRegistered => _resolveProcessor != null && _resolveApicId != null;

    public static Boolean Register(delegate*<UInt32, UInt32> resolveProcessor, delegate*<UInt32, UInt32> resolveApicId)
    {
        if (resolveProcessor == null || resolveApicId == null || IsRegistered) return false;
        _resolveProcessor = resolveProcessor;
        _resolveApicId = resolveApicId;
        return true;
    }

    public static UInt32 ResolveProcessor(UInt32 requested) => _resolveProcessor != null ? _resolveProcessor(requested) : requested;
    public static UInt32 ResolveApicId(UInt32 processor) => _resolveApicId != null ? _resolveApicId(processor) : Native.GetCurrentApicId();
}
