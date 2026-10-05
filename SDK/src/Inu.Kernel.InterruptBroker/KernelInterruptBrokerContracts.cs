using System;
namespace Inu.Kernel.InterruptBroker;
/// <summary>Identifies the hardware delivery mechanism selected internally by the opaque interrupt broker.</summary>
public enum KernelInterruptDeliveryMechanism : Byte { None=0,IoApic=1,Msi=2,MsiX=3,LocalApic=4,X2Apic=5 }
/// <summary>Reports the platform mechanisms and current opaque interrupt routes owned by the broker.</summary>
public readonly struct KernelInterruptBrokerCapabilities
{
    public KernelInterruptBrokerCapabilities(Boolean initialized,Boolean localApic,Boolean ioApic,Boolean x2Apic,Boolean msi,Boolean msiX,UInt32 activeRoutes)
    {Initialized=initialized;LocalApic=localApic;IoApic=ioApic;X2Apic=x2Apic;Msi=msi;MsiX=msiX;ActiveRoutes=activeRoutes;}
    public Boolean Initialized { get; }
    public Boolean LocalApic { get; }
    public Boolean IoApic { get; }
    public Boolean X2Apic { get; }
    public Boolean Msi { get; }
    public Boolean MsiX { get; }
    public UInt32 ActiveRoutes { get; }
}
/// <summary>Provides diagnostic information about an opaque route without requiring device drivers to know its mechanism.</summary>
public readonly struct KernelInterruptRouteInfo
{
    public KernelInterruptRouteInfo(UInt64 handle,UInt32 device,Byte vector,KernelInterruptDeliveryMechanism mechanism,UInt32 source,UInt32 targetProcessor)
    {Handle=handle;Device=device;Vector=vector;Mechanism=mechanism;Source=source;TargetProcessor=targetProcessor;}
    public UInt64 Handle { get; }
    public UInt32 Device { get; }
    public Byte Vector { get; }
    public KernelInterruptDeliveryMechanism Mechanism { get; }
    public UInt32 Source { get; }
    public UInt32 TargetProcessor { get; }
}
