using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Pci;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Owns opaque route lifecycle and dispatch bookkeeping.</summary>
public static unsafe partial class KernelInterruptBroker
{
    /// <summary>Routes a non-PCI legacy GSI directly to a kernel callback through the registered GSI-routing provider.</summary>
    public static Boolean RegisterLegacyGsi(UInt32 gsi, Boolean activeLow, Boolean levelTriggered, delegate*<Byte, UInt64, Boolean> callback, UInt64 cookie, out UInt64 handle)
    {
        handle = 0UL;
        if (!_initialized || !KernelIoApicRouterServices.IsRegistered || callback == null) return false;
        if (_count == _capacity && !Grow()) return false;
        Byte vector = KernelInterruptDispatch.AllocateVector();
        if (vector == 0) return false;
        Route* route = Free();
        if (route == null) { KernelInterruptDispatch.ReleaseVector(vector); return false; }
        route->Used = 1; route->Vector = vector; route->Mechanism = (Byte)KernelInterruptDeliveryMechanism.IoApic; route->Direct = 1;
        route->Source = gsi; route->Target = KernelInterruptAffinityServices.ResolveProcessor(0U); route->Cookie = cookie; route->Callback = (UInt64)(void*)callback;
        route->Handle = ++_nextHandle; if (route->Handle == 0UL) route->Handle = ++_nextHandle;
        if (!KernelInterruptDispatch.Register(vector, &Dispatch, route->Handle))
        { Clear(route); KernelInterruptDispatch.ReleaseVector(vector); return false; }
        KernelDriverInterruptRequest request = new(default, gsi, 0, route->Target, levelTriggered, activeLow, 0UL);
        if (!KernelIoApicRouterServices.TryRoute(&request, vector, route->Target))
        { KernelInterruptDispatch.Unregister(vector); KernelInterruptDispatch.ReleaseVector(vector); Clear(route); return false; }
        _count++; handle = route->Handle; return true;
    }

    /// <summary>Releases a directly registered legacy GSI route.</summary>
    public static Boolean ReleaseLegacyGsi(UInt64 handle)
    {
        if (!_initialized || handle == 0UL) return false;
        for (UInt32 i = 0; i < _capacity; i++)
        {
            Route* route = _routes + i;
            if (route->Used == 0 || route->Direct == 0 || route->Handle != handle) continue;
            KernelIoApicRouterServices.TryMask(route->Source);
            KernelInterruptDispatch.Unregister(route->Vector);
            KernelInterruptDispatch.ReleaseVector(route->Vector);
            Clear(route);
            if (_count != 0U) _count--;
            return true;
        }
        return false;
    }

    public static Boolean TryGetRoute(UInt32 index, out KernelInterruptRouteInfo info)
    {
        info = default;
        if (index >= _count) return false;
        UInt32 found = 0;
        for (UInt32 i = 0; i < _capacity; i++)
        {
            Route* route = _routes + i;
            if (route->Used == 0) continue;
            if (found++ == index)
            {
                info = new(route->Handle, route->Device, route->Vector, (KernelInterruptDeliveryMechanism)route->Mechanism, route->Source, route->Target);
                return true;
            }
        }
        return false;
    }

    private static Boolean Request(KernelDriverInterruptRequest* request, KernelDriverInterruptHandle* handle)
    {
        if (request == null || handle == null || !_initialized) return false;
        if (_count == _capacity && !Grow()) return false;
        Byte vector = KernelInterruptDispatch.AllocateVector();
        if (vector == 0) return false;
        Route* route = Free();
        if (route == null) { KernelInterruptDispatch.ReleaseVector(vector); return false; }

        route->Used = 1; route->Vector = vector; route->Device = request->Device.Value; route->Target = KernelInterruptAffinityServices.ResolveProcessor(request->TargetProcessor);
        route->Source = request->Source; route->Cookie = request->DriverCookie; route->Handle = ++_nextHandle;
        if (route->Handle == 0UL) route->Handle = ++_nextHandle;
        if (!KernelInterruptDispatch.Register(vector, &Dispatch, route->Handle))
        { Clear(route); KernelInterruptDispatch.ReleaseVector(vector); return false; }

        Boolean routed = false;
        if (KernelPci.TryGetDevice(request->Device, out PciDeviceInfo pci) && KernelPciInterruptRouterServices.IsRegistered)
        {
            route->Pci = 1; route->Segment = pci.Location.Segment; route->Bus = pci.Location.Bus; route->PciDevice = pci.Location.Device; route->Function = pci.Location.Function;
            if (KernelPciInterruptRouterServices.TryRoute(pci.Location, request, vector, route->Target, out KernelPciInterruptRouteResult result))
            {
                route->Mechanism = (Byte)result.Mechanism;
                route->Source = result.Source;
                routed = true;
            }
        }
        else if (KernelIoApicRouterServices.IsRegistered && KernelIoApicRouterServices.TryRoute(request, vector, route->Target))
        {
            route->Mechanism = (Byte)KernelInterruptDeliveryMechanism.IoApic;
            routed = true;
        }

        if (!routed)
        {
            KernelInterruptDispatch.Unregister(vector);
            KernelInterruptDispatch.ReleaseVector(vector);
            Clear(route);
            return false;
        }
        _count++;
        *handle = new KernelDriverInterruptHandle(route->Handle);
        return true;
    }

    private static Boolean Release(KernelDriverInterruptHandle handle)
    {
        if (!_initialized || handle.Value == 0UL) return false;
        for (UInt32 i = 0; i < _capacity; i++)
        {
            Route* route = _routes + i;
            if (route->Used == 0 || route->Handle != handle.Value) continue;
            ReleaseHardwareRoute(route);
            KernelInterruptDispatch.Unregister(route->Vector);
            KernelInterruptDispatch.ReleaseVector(route->Vector);
            Clear(route);
            if (_count != 0U) _count--;
            return true;
        }
        return false;
    }

    private static void ReleaseHardwareRoute(Route* route)
    {
        KernelInterruptDeliveryMechanism mechanism = (KernelInterruptDeliveryMechanism)route->Mechanism;
        if (route->Direct != 0 || route->Pci == 0)
        {
            KernelIoApicRouterServices.TryMask(route->Source);
            return;
        }
        PciLocation location = new(route->Segment, route->Bus, route->PciDevice, route->Function);
        KernelPciInterruptRouterServices.TryRelease(location, mechanism, route->Source);
    }

    private static Boolean Dispatch(Byte vector, UInt64 handle)
    {
        for (UInt32 i = 0; i < _capacity; i++)
        {
            Route* route = _routes + i;
            if (route->Used == 0 || route->Handle != handle || route->Vector != vector) continue;
            if (route->Direct != 0 && route->Callback != 0UL)
            {
                delegate*<Byte, UInt64, Boolean> callback = (delegate*<Byte, UInt64, Boolean>)(void*)route->Callback;
                return callback(vector, route->Cookie);
            }
            return KernelDrivers.DispatchInterrupt(new KernelDeviceHandle(route->Device), route->Cookie);
        }
        return false;
    }
}
