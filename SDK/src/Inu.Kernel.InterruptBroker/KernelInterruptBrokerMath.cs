using System;
namespace Inu.Kernel.InterruptBroker;
/// <summary>Contains deterministic interrupt-broker policy calculations that are independent of hardware access.</summary>
public static class KernelInterruptBrokerMath
{
    /// <summary>Selects the preferred PCI interrupt mechanism without exposing that decision to the device driver.</summary>
    public static KernelInterruptDeliveryMechanism SelectPciMechanism(Boolean msiX,Boolean msi,Boolean ioApic)
    {if(msiX)return KernelInterruptDeliveryMechanism.MsiX;if(msi)return KernelInterruptDeliveryMechanism.Msi;if(ioApic)return KernelInterruptDeliveryMechanism.IoApic;return KernelInterruptDeliveryMechanism.None;}
    /// <summary>Builds the xAPIC-compatible MSI message address for an APIC destination identifier.</summary>
    public static UInt64 CreateMsiAddress(UInt32 apicId)=>0xFEE00000UL|((UInt64)(apicId&0xFFU)<<12);
    /// <summary>Builds the fixed-delivery MSI data payload for one allocated IDT vector.</summary>
    public static UInt16 CreateMsiData(Byte vector)=>(UInt16)vector;
}
