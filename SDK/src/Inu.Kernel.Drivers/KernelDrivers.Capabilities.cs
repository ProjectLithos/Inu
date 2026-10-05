using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

public static unsafe partial class KernelDrivers
{
    private static Boolean GrantDeclaredCapabilities(KernelDriverDeviceContext* context,DeviceRecord* d,DriverRecord* r)
    {
        if(context==null)return false;UInt64 declared=r->DeclaredCapabilities;
        // Best-effort pre-grants. Missing resources are not a binding failure:
        // the declaration states what the driver may request, not what must exist.
        GrantGlobalIfDeclared(*context,declared,KernelDriverCapability.PciConfig);
        GrantGlobalIfDeclared(*context,declared,KernelDriverCapability.Timers);
        GrantGlobalIfDeclared(*context,declared,KernelDriverCapability.Networking);
        GrantGlobalIfDeclared(*context,declared,KernelDriverCapability.Filesystem);
        GrantGlobalIfDeclared(*context,declared,KernelDriverCapability.Dma);
        for(Int32 i=0;i<d->ResourceCount;i++)
        {
            KernelDeviceResourceType type=(KernelDeviceResourceType)d->ResourceType[i];UInt64 start=d->ResourceStart[i],length=d->ResourceLength[i];
            if(type==KernelDeviceResourceType.Memory)
            {
                if((declared&(UInt64)KernelDriverCapability.Mmio)!=0UL)TryGrantCapability(*context,new KernelDriverCapabilityRequest(KernelDriverCapability.Mmio,start,length,KernelDriverCapabilityAccess.ReadWrite),out _);
                if((declared&(UInt64)KernelDriverCapability.PhysicalMemory)!=0UL)TryGrantCapability(*context,new KernelDriverCapabilityRequest(KernelDriverCapability.PhysicalMemory,start,length,KernelDriverCapabilityAccess.ReadWrite),out _);
            }
            else if(type==KernelDeviceResourceType.IoPort&&(declared&(UInt64)KernelDriverCapability.PortIo)!=0UL)
                TryGrantCapability(*context,new KernelDriverCapabilityRequest(KernelDriverCapability.PortIo,start,length,KernelDriverCapabilityAccess.ReadWrite),out _);
        }
        if((declared&(UInt64)KernelDriverCapability.Interrupt)!=0UL)TryGrantCapability(*context,new KernelDriverCapabilityRequest(KernelDriverCapability.Interrupt,0UL,0UL,KernelDriverCapabilityAccess.ReadWrite),out _);
        if((declared&(UInt64)KernelDriverCapability.Msi)!=0UL)TryGrantCapability(*context,new KernelDriverCapabilityRequest(KernelDriverCapability.Msi,0UL,0UL,KernelDriverCapabilityAccess.ReadWrite),out _);
        if((declared&(UInt64)KernelDriverCapability.MsiX)!=0UL)TryGrantCapability(*context,new KernelDriverCapabilityRequest(KernelDriverCapability.MsiX,0UL,0UL,KernelDriverCapabilityAccess.ReadWrite),out _);
        return true;
    }

    private static Boolean AllDeclaredCapabilitiesGranted(DeviceRecord* d,UInt64 declared)
    {
        UInt64 granted=0UL;for(Int32 i=0;i<d->GrantCount;i++)granted|=d->GrantCapability[i];return (granted&declared)==declared;
    }

    private static Boolean GrantGlobalIfDeclared(KernelDriverDeviceContext context,UInt64 declared,KernelDriverCapability capability)
    { if((declared&(UInt64)capability)==0UL)return true;return TryGrantCapability(context,new KernelDriverCapabilityRequest(capability,0UL,0UL,KernelDriverCapabilityAccess.ReadWrite),out _); }

    /// <summary>Explicitly grants one declared capability to a bound driver/device pair after kernel policy validation.</summary>
    public static Boolean TryGrantCapability(KernelDriverDeviceContext context,KernelDriverCapabilityRequest request,out KernelDriverCapabilityGrant grant)
    {
        grant=default;if(request.Capability==KernelDriverCapability.Dma&&KernelFaultInjection.ShouldInject(KernelFaultKind.BadDma,"dma",out _))return false;if(!KernelDriverMath.IsValidCapabilityRequest(request)||!TryBound(context.Device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext actual)||actual.Driver.Value!=context.Driver.Value)return false;
        UInt64 bit=(UInt64)request.Capability;if((r->DeclaredCapabilities&bit)!=bit||d->GrantCount>=GrantsPerDevice)return false;
        if(!CapabilityAllowedByDevice(d,request))return false;
        UInt64 token=_nextCapabilityGrantToken++;if(token==0UL)token=_nextCapabilityGrantToken++;Int32 i=d->GrantCount++;d->GrantToken[i]=token;d->GrantCapability[i]=bit;d->GrantStart[i]=request.Start;d->GrantLength[i]=request.Length;d->GrantAccess[i]=(Byte)request.Access;grant=new KernelDriverCapabilityGrant(token,context.Device,context.Driver,request.Capability,request.Start,request.Length,request.Access);return true;
    }

    /// <summary>Validates that an opaque grant is still active and belongs to the supplied binding.</summary>
    public static Boolean ValidateCapabilityGrant(KernelDriverCapabilityGrant grant)
    { return ValidateCapabilityGrant(grant,grant.Capability,grant.Start,grant.Length,grant.Access); }

    /// <summary>Validates a grant for a concrete operation, including capability kind, range and read/write authority.</summary>
    public static Boolean ValidateCapabilityGrant(KernelDriverCapabilityGrant grant,KernelDriverCapability requiredCapability,UInt64 start,UInt64 length,KernelDriverCapabilityAccess requiredAccess)
    {
        if(!grant.IsValid||!KernelDriverMath.IsSingleCapability(requiredCapability)||!KernelDriverMath.AccessCovers(grant.Access,requiredAccess)||grant.Capability!=requiredCapability||!TryDevice(grant.Device,out DeviceRecord* d)||d->BoundDriver!=grant.Driver.Value)return false;
        Boolean ranged=requiredCapability==KernelDriverCapability.Mmio||requiredCapability==KernelDriverCapability.PortIo||requiredCapability==KernelDriverCapability.PhysicalMemory;
        if(ranged&&!KernelDriverMath.RangeContains(grant.Start,grant.Length,start,length))return false;if(!ranged&&(start!=0UL||length!=0UL))return false;
        for(Int32 i=0;i<d->GrantCount;i++)if(d->GrantToken[i]==grant.Token&&d->GrantCapability[i]==(UInt64)grant.Capability&&d->GrantStart[i]==grant.Start&&d->GrantLength[i]==grant.Length&&d->GrantAccess[i]==(Byte)grant.Access)return true;return false;
    }

    /// <summary>Returns a live grant that authorizes a specific operation for the bound driver.</summary>
    public static Boolean TryGetCapabilityGrant(KernelDriverDeviceContext context,KernelDriverCapability capability,UInt64 start,UInt64 length,KernelDriverCapabilityAccess access,out KernelDriverCapabilityGrant grant)
    {
        grant=default;if(!KernelDriverMath.IsSingleCapability(capability)||!TryBound(context.Device,out DeviceRecord* d,out _,out KernelDriverDeviceContext actual)||actual.Driver.Value!=context.Driver.Value)return false;
        UInt64 bit=(UInt64)capability;for(Int32 i=0;i<d->GrantCount;i++){if((d->GrantCapability[i]&bit)!=bit)continue;KernelDriverCapabilityGrant candidate=new(d->GrantToken[i],context.Device,context.Driver,capability,d->GrantStart[i],d->GrantLength[i],(KernelDriverCapabilityAccess)d->GrantAccess[i]);if(ValidateCapabilityGrant(candidate,capability,start,length,access)){grant=candidate;return true;}}return false;
    }

    /// <summary>Revokes a previously issued capability token.</summary>
    public static Boolean RevokeCapability(KernelDriverCapabilityGrant grant)
    { if(!grant.IsValid||!TryDevice(grant.Device,out DeviceRecord* d)||d->BoundDriver!=grant.Driver.Value)return false;for(Int32 i=0;i<d->GrantCount;i++){if(d->GrantToken[i]!=grant.Token)continue;Int32 last=d->GrantCount-1;d->GrantToken[i]=d->GrantToken[last];d->GrantCapability[i]=d->GrantCapability[last];d->GrantStart[i]=d->GrantStart[last];d->GrantLength[i]=d->GrantLength[last];d->GrantAccess[i]=d->GrantAccess[last];d->GrantToken[last]=0UL;d->GrantCount--;return true;}return false; }

    private static void RevokeAllCapabilities(DeviceRecord* d)
    {
        if(d==null)return;for(Int32 i=0;i<d->GrantCount;i++){d->GrantToken[i]=0UL;d->GrantCapability[i]=0UL;d->GrantStart[i]=0UL;d->GrantLength[i]=0UL;d->GrantAccess[i]=0;}d->GrantCount=0;
    }

    private static Boolean CapabilityAllowedByDevice(DeviceRecord* d,KernelDriverCapabilityRequest request)
    {
        if(request.Capability==KernelDriverCapability.PciConfig)return d->Bus==(Byte)KernelDeviceBus.Pci;
        if(request.Capability==KernelDriverCapability.Mmio)return HasResourceRange(d,KernelDeviceResourceType.Memory,request.Start,request.Length);
        if(request.Capability==KernelDriverCapability.PortIo)return HasResourceRange(d,KernelDeviceResourceType.IoPort,request.Start,request.Length);
        if(request.Capability==KernelDriverCapability.Dma)return HasResourceType(d,KernelDeviceResourceType.Dma);
        if(request.Capability==KernelDriverCapability.Interrupt||request.Capability==KernelDriverCapability.Msi||request.Capability==KernelDriverCapability.MsiX)return HasResourceType(d,KernelDeviceResourceType.Interrupt);
        if(request.Capability==KernelDriverCapability.PhysicalMemory)return HasResourceRange(d,KernelDeviceResourceType.Memory,request.Start,request.Length);
        return request.Capability==KernelDriverCapability.Timers||request.Capability==KernelDriverCapability.Networking||request.Capability==KernelDriverCapability.Filesystem;
    }
    private static Boolean HasResourceType(DeviceRecord* d,KernelDeviceResourceType type){for(Int32 i=0;i<d->ResourceCount;i++)if(d->ResourceType[i]==(Byte)type)return true;return false;}
    private static Boolean HasResourceRange(DeviceRecord* d,KernelDeviceResourceType type,UInt64 start,UInt64 length){for(Int32 i=0;i<d->ResourceCount;i++)if(d->ResourceType[i]==(Byte)type&&KernelDriverMath.RangeContains(d->ResourceStart[i],d->ResourceLength[i],start,length))return true;return false;}

}
