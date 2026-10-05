using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Pci;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Owns opaque driver interrupt routes and dispatches hardware delivery through registered providers.</summary>
public static unsafe partial class KernelInterruptBroker
{
    private const UInt32 ApicBaseMsr = 0x1BU;
    private const UInt64 ApicEnabled = 1UL << 11;
    private const UInt64 X2ApicEnabled = 1UL << 10;

    private struct Route
    {
        internal Byte Used, Vector, Mechanism, Direct, Pci;
        internal UInt32 Device, Source, Target;
        internal UInt64 Handle, Cookie, Callback;
        internal UInt16 Segment;
        internal Byte Bus, PciDevice, Function;
    }

    private static Route* _routes;
    private static KernelHeapAllocation _allocation;
    private static UInt32 _capacity, _count;
    private static UInt64 _nextHandle;
    private static Boolean _initialized, _localApic, _x2Apic;

    /// <summary>Installs the opaque driver interrupt broker. Hardware delivery is supplied by registered optional providers.</summary>
    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!KernelDrivers.IsInitialized() || !KernelInterruptDispatch.IsInitialized() || !KernelHeap.IsInitialized() ||
            !KernelAddressSpace.IsInitialized() || !KernelPci.IsInitialized()) return false;
        if (!Allocate(32U, out _allocation, out _routes)) return false;
        _capacity = 32U;

        UInt64 apic = Native.ReadModelSpecificRegister(ApicBaseMsr);
        _localApic = (apic & ApicEnabled) != 0UL;
        _x2Apic = (apic & X2ApicEnabled) != 0UL;
        if (!_localApic) return false;

        if (!KernelDrivers.InstallInterruptBroker(&Request, &Release)) return false;
        _initialized = true;
        return true;
    }

    public static Boolean IsInitialized() => _initialized;

    public static KernelInterruptBrokerCapabilities GetCapabilities() =>
        new(_initialized, _localApic, KernelIoApicRouterServices.IsRegistered, _x2Apic,
            KernelPciInterruptRouterServices.IsRegistered, KernelPciInterruptRouterServices.IsRegistered, _count);
}
