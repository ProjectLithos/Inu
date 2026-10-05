using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.InterruptBroker;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Ps2;
using Inu.Kernel.Processes;
using Inu.Kernel.Drivers;
using Inu.Kernel.Storage;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
#if INU_COMPONENT_STORAGE_NVME_DRIVER
using Inu.Kernel.Nvme;
#endif
#if INU_COMPONENT_STORAGE_AHCI_DRIVER
using Inu.Kernel.Ahci;
#endif
using Inu.Kernel.Virtio;
using Inu.Kernel.Virtio.Gpu;
using Inu.Kernel.Graphics;
using Inu.Kernel.Gui;
using Inu.Kernel.Audio;
#if INU_COMPONENT_NETWORK_E1000_DRIVER
using Inu.Kernel.E1000;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
using Inu.Kernel.Rtl8168;
#endif
using Inu.Bus.Usb;
using Inu.Usb.Xhci;
using Inu.Usb.Hid;
using Inu.Usb.MassStorage;
using Inu.Usb.Hub;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap;

public static unsafe partial class Kernel
{
private static Boolean ServiceNetworkAdapters(UInt64 cookie)
    {
        Boolean ok=true;
        if (KernelVirtio.IsInitialized()) ok=KernelVirtio.ServiceAll()&ok;
#if INU_COMPONENT_NETWORK_E1000_DRIVER
        if (KernelE1000.IsInitialized()) ok=KernelE1000.ServiceAll()&ok;
#endif
#if INU_COMPONENT_NETWORK_RTL8168_DRIVER
        if (KernelRtl8168.IsInitialized()) ok=KernelRtl8168.ServiceAll()&ok;
#endif
        return ok;
    }
}
