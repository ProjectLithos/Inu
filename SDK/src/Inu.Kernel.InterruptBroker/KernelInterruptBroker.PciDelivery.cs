using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Pci;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Optional PCI MSI-X/MSI/INTx interrupt-delivery provider.</summary>
public static unsafe class KernelPciMessageSignaledRouter
{
    private static Boolean _initialized;

    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!KernelPciInterruptRouterServices.Register(&TryRoutePci, &ReleaseHardwareRoute)) return false;
        _initialized = true;
        return true;
    }

    private static Boolean TryRoutePci(PciLocation location, KernelDriverInterruptRequest* request, Byte vector, UInt32 targetProcessor, KernelPciInterruptRouteResult* result)
    {
        if (request == null || result == null) return false;
        UInt32 apicId = KernelInterruptAffinityServices.ResolveApicId(targetProcessor);
        UInt64 address = KernelInterruptBrokerMath.CreateMsiAddress(apicId);
        UInt16 data = KernelInterruptBrokerMath.CreateMsiData(vector);
        Boolean hasMsix = KernelPci.TryGetMsixCapability(location, out PciMsixCapability msix) && msix.TableSize != 0U;
        Boolean hasMsi = KernelPci.TryGetMsiCapability(location, out _);
        Boolean hasIoApic = KernelIoApicRouterServices.IsRegistered;
        KernelInterruptDeliveryMechanism preferred = KernelInterruptBrokerMath.SelectPciMechanism(hasMsix, hasMsi, hasIoApic);

        if (preferred == KernelInterruptDeliveryMechanism.MsiX && KernelPci.TryProgramMsix(location, 0, address, data))
        { result->Mechanism = KernelInterruptDeliveryMechanism.MsiX; return true; }
        if (preferred != KernelInterruptDeliveryMechanism.IoApic && hasMsi && KernelPci.TryProgramMsi(location, address, data))
        { result->Mechanism = KernelInterruptDeliveryMechanism.Msi; return true; }
        if (hasIoApic && TryRouteIntx(location, request, vector, targetProcessor, out UInt32 gsi))
        { result->Mechanism = KernelInterruptDeliveryMechanism.IoApic; result->Source = gsi; return true; }
        return false;
    }

    private static Boolean TryRouteIntx(PciLocation location, KernelDriverInterruptRequest* request, Byte vector, UInt32 targetProcessor, out UInt32 gsi)
    {
        gsi = 0U;
        if (!KernelPci.TryRead8(location, 0x3D, out Byte pin) || pin == 0 || !KernelPci.TryRead8(location, 0x3C, out Byte line) || line == 0xFF) return false;
        gsi = line;
        KernelDriverInterruptRequest gsiRequest = new(request->Device, gsi, request->Priority, request->TargetProcessor, request->LevelTriggered, request->ActiveLow, request->DriverCookie);
        return KernelIoApicRouterServices.TryRoute(&gsiRequest, vector, targetProcessor) && KernelPci.SetIntxDisabled(location, false);
    }

    private static Boolean ReleaseHardwareRoute(PciLocation location, KernelInterruptDeliveryMechanism mechanism, UInt32 source)
    {
        if (mechanism == KernelInterruptDeliveryMechanism.Msi) return KernelPci.TryDisableMsi(location);
        if (mechanism == KernelInterruptDeliveryMechanism.MsiX) return KernelPci.TryDisableMsix(location);
        if (mechanism == KernelInterruptDeliveryMechanism.IoApic) return KernelIoApicRouterServices.TryMask(source);
        return true;
    }
}
