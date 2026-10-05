using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Kernel.InterruptBroker;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Processes;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Drivers;
using Inu.Kernel.Storage;
using Inu.Kernel.Networking;
using Inu.Kernel.Graphics;
using Inu.Kernel.Ps2;
using Inu.Usb.Hid;
using Inu.Kernel.Time;
using Inu.Kernel.Acpi;
using Inu.Kernel.Smp;

namespace Inu.Kernel.Bootstrap;

/// <summary>
/// Runtime bridge between the versioned public subsystem contracts and the concrete
/// kernel implementation.  Boot does not declare the kernel ready until every formal
/// boundary has been resolved to a live implementation or an explicitly degraded one.
/// </summary>
public static class KernelSubsystemRuntime
{
    public const UInt32 SubsystemCount=13U;

    public static Boolean ValidateAll(out UInt32 readyCount,out UInt32 degradedCount)
    {
        readyCount=0U;degradedCount=0U;
        for(UInt32 raw=1U;raw<=SubsystemCount;raw++)
        {
            if(!TryGetStatus((KernelSubsystemId)(Byte)raw,out KernelSubsystemStatus status))return false;
            if(!status.IsCompatible(KernelSubsystemContractVersion.Major,KernelSubsystemContractVersion.Minor))return false;
            if(status.State==KernelSubsystemState.Ready)readyCount++;
            else if(status.State==KernelSubsystemState.Degraded)degradedCount++;
            else return false;
        }
        return readyCount+degradedCount==SubsystemCount;
    }

    public static Boolean TryGetStatus(KernelSubsystemId id,out KernelSubsystemStatus status)
    {
        status=default;
        switch(id)
        {
            case KernelSubsystemId.Memory:
                return Status(id,KernelPhysicalMemory.IsInitialized()&&KernelVirtualMemory.IsInitialized()&&KernelAddressSpace.IsInitialized()&&KernelHeap.IsInitialized(),0x0FUL,out status);
            case KernelSubsystemId.Interrupts:
                { KernelInterruptBrokerCapabilities c=KernelInterruptBroker.GetCapabilities();UInt64 caps=(c.LocalApic?1UL:0UL)|(c.IoApic?2UL:0UL)|(c.X2Apic?4UL:0UL)|(c.Msi?8UL:0UL)|(c.MsiX?16UL:0UL);return Status(id,KernelInterruptBroker.IsInitialized(),caps,out status); }
            case KernelSubsystemId.Scheduler:
                { KernelSchedulerCapabilities c=KernelScheduler.GetCapabilities();return Status(id,KernelScheduler.IsInitialized(),c.ProcessorCount,out status); }
            case KernelSubsystemId.Processes:
                return Status(id,KernelProcesses.IsInitialized(),KernelProcesses.GetActiveProcessCount(),out status);
            case KernelSubsystemId.Syscalls:
                return Status(id,KernelSystemCalls.IsInitialized(),1UL,out status);
            case KernelSubsystemId.Drivers:
                { KernelDriverCapabilities c=KernelDrivers.GetCapabilities();UInt64 caps=((UInt64)c.RegisteredDrivers<<32)|c.RegisteredDevices;return Status(id,KernelDrivers.IsInitialized(),caps,out status); }
            case KernelSubsystemId.Filesystem:
                { KernelStorageCapabilities c=KernelStorage.GetCapabilities();UInt64 caps=((UInt64)c.Mounts<<32)|c.Volumes;return Status(id,KernelStorage.IsInitialized(),caps,out status); }
            case KernelSubsystemId.Networking:
                { KernelNetworkCapabilities c=KernelNetworking.GetCapabilities();UInt64 caps=((UInt64)c.Interfaces<<32)|c.Sockets;return Status(id,KernelNetworking.IsInitialized(),caps,out status); }
            case KernelSubsystemId.Graphics:
                { KernelGraphicsCapabilities c=KernelGraphics.GetCapabilities();return Status(id,KernelGraphics.IsInitialized(),c.Displays,out status); }
            case KernelSubsystemId.Input:
                { Ps2Capabilities ps2=KernelPs2.GetCapabilities();Boolean usb=UsbHid.IsInitialized();UInt64 caps=(ps2.Keyboard?1UL:0UL)|(ps2.Mouse?2UL:0UL)|(usb?4UL:0UL);return Status(id,KernelPs2.IsInitialized()||usb,caps,out status); }
            case KernelSubsystemId.Time:
                { KernelTimeCapabilities c=KernelTime.GetCapabilities();UInt64 caps=(c.HasHpet?1UL:0UL)|(c.HasTsc?2UL:0UL)|(c.HasInvariantTsc?4UL:0UL)|(c.HasLocalApicTimer?8UL:0UL);return Status(id,KernelTime.IsInitialized,caps,out status); }
            case KernelSubsystemId.Power:
                { AcpiPowerCapabilities c=KernelAcpiPowerServices.GetCapabilities();UInt64 caps=(c.ResetAvailable?1UL:0UL)|(c.ShutdownAvailable?2UL:0UL)|(c.PowerButtonAvailable?4UL:0UL);return Status(id,c.Initialized,caps,out status); }
            case KernelSubsystemId.Smp:
                { KernelSmpCapabilities c=KernelSmp.GetCapabilities();UInt64 caps=((UInt64)c.OnlineProcessorCount<<32)|c.ProcessorCount;return Status(id,KernelSmp.IsInitialized(),caps,out status); }
            default:return false;
        }
    }

    private static Boolean Status(KernelSubsystemId id,Boolean ready,UInt64 capabilities,out KernelSubsystemStatus status)
    {
        status=new KernelSubsystemStatus(id,ready?KernelSubsystemState.Ready:KernelSubsystemState.Failed,KernelSubsystemContractVersion.Major,KernelSubsystemContractVersion.Minor,capabilities);
        return true;
    }
}
