using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

public static unsafe partial class KernelDrivers
{
    public static Boolean TryGetDevice(KernelDeviceHandle device,out KernelDeviceIdentifier identifier,out KernelDeviceState state,out KernelDriverHandle driver)
    { identifier=default;state=default;driver=default;if(!TryDevice(device,out DeviceRecord* d))return false;identifier=Identifier(d);state=(KernelDeviceState)d->State;driver=new KernelDriverHandle(d->BoundDriver);return true; }
    public static Boolean TryGetDeviceNode(KernelDeviceHandle device,out KernelDeviceNode node)
    { node=default;if(!TryDevice(device,out DeviceRecord* d))return false;node=new KernelDeviceNode(device,new KernelDeviceHandle(d->Parent),new KernelDeviceHandle(d->FirstChild),new KernelDeviceHandle(d->NextSibling),Identifier(d),(KernelDeviceState)d->State,new KernelDriverHandle(d->BoundDriver),(KernelDriverFailureCode)d->FailureCode);return true; }
    public static Boolean TryGetFirstChild(KernelDeviceHandle parent,out KernelDeviceHandle child){child=default;if(!TryDevice(parent,out DeviceRecord* d)||d->FirstChild==0U)return false;child=new KernelDeviceHandle(d->FirstChild);return true;}
    public static Boolean TryGetNextSibling(KernelDeviceHandle device,out KernelDeviceHandle sibling){sibling=default;if(!TryDevice(device,out DeviceRecord* d)||d->NextSibling==0U)return false;sibling=new KernelDeviceHandle(d->NextSibling);return true;}

    /// <summary>Gets a stable slot-ordered node so tooling can snapshot the exact kernel device tree without bus-specific enumeration.</summary>
    public static Boolean TryGetDeviceNodeByIndex(UInt32 index,out KernelDeviceNode node)
    { node=default;if(!_initialized)return false;UInt32 seen=0U;for(UInt32 i=0U;i<_deviceCapacity;i++){DeviceRecord* d=Device((Int32)i);if(d->Used==0)continue;if(seen++!=index)continue;KernelDeviceHandle h=new(i+1U);return TryGetDeviceNode(h,out node);}return false; }

    /// <summary>Gets a root node by root index. A root has no parent; children are linked through the same node contract.</summary>
    public static Boolean TryGetRootDevice(UInt32 rootIndex,out KernelDeviceNode node)
    { node=default;if(!_initialized)return false;UInt32 seen=0U;for(UInt32 i=0U;i<_deviceCapacity;i++){DeviceRecord* d=Device((Int32)i);if(d->Used==0||d->Parent!=0U)continue;if(seen++!=rootIndex)continue;return TryGetDeviceNode(new KernelDeviceHandle(i+1U),out node);}return false; }

    /// <summary>Returns counts for the six canonical device classes represented by the authoritative tree.</summary>
    public static KernelDeviceTreeSnapshot GetDeviceTreeSnapshot()
    { UInt32 roots=0,pci=0,usb=0,acpi=0,platform=0,virtuals=0,logical=0;if(_initialized)for(UInt32 i=0U;i<_deviceCapacity;i++){DeviceRecord* d=Device((Int32)i);if(d->Used==0)continue;if(d->Parent==0U)roots++;switch((KernelDeviceBus)d->Bus){case KernelDeviceBus.Pci:pci++;break;case KernelDeviceBus.Usb:usb++;break;case KernelDeviceBus.Acpi:acpi++;break;case KernelDeviceBus.Platform:platform++;break;case KernelDeviceBus.Virtual:virtuals++;break;case KernelDeviceBus.Logical:logical++;break;}}return new KernelDeviceTreeSnapshot(_deviceTreeGeneration,_deviceCount,roots,pci,usb,acpi,platform,virtuals,logical); }
    public static Boolean InstallLifecycleSink(delegate*<KernelDriverLifecycleEvent*,Boolean> sink){_lifecycleSink=(UInt64)(void*)sink;return sink!=null;}
    public static KernelDeviceEventQueueStatus GetDeviceEventQueueStatus()=>new(_deviceEventCount,_deviceEventsDropped,_deviceEventSequence);
    public static Boolean TryDequeueDeviceStateEvent(out KernelDeviceStateEvent value)
    { value=default;if(!_initialized||_deviceEventCount==0U||_deviceEvents==null)return false;DeviceEventRecord* e=_deviceEvents+_deviceEventHead;KernelDeviceIdentifier id=new((KernelDeviceBus)e->Bus,e->Vendor,e->Device,e->SubsystemVendor,e->Subsystem,e->ClassCode,e->Revision,e->Location);value=new KernelDeviceStateEvent(e->Sequence,(KernelDeviceStateEventKind)e->Kind,new KernelDeviceHandle(e->Handle),new KernelDeviceHandle(e->Parent),new KernelDriverHandle(e->Driver),id,(KernelDeviceState)e->PreviousState,(KernelDeviceState)e->CurrentState,(KernelDriverFailureCode)e->Failure);Clear((Byte*)e,sizeof(DeviceEventRecord));_deviceEventHead=(_deviceEventHead+1U)%DeviceEventCapacity;_deviceEventCount--;return true; }

    /// <summary>Gets a snapshot of one registered driver by handle.</summary>
    public static Boolean TryGetDriverInfo(KernelDriverHandle driver,out KernelDriverInfo info)
    { info=default;if(!TryDriver(driver,out DriverRecord* r))return false;info=new KernelDriverInfo(driver,(KernelDriverState)r->State,Rule(r),(KernelDriverCapability)r->DeclaredCapabilities,r->NameLength);return true; }
    /// <summary>Reads one ASCII byte from a driver's stable display name without allocating a managed string.</summary>
    public static Boolean TryGetDriverNameByte(KernelDriverHandle driver,UInt32 index,out Byte value)
    { value=0;if(!TryDriver(driver,out DriverRecord* r)||index>=r->NameLength)return false;value=r->Name[index];return true; }

    /// <summary>Gets the privilege declaration registered for a driver.</summary>
    public static Boolean TryGetDeclaredCapabilities(KernelDriverHandle driver,out KernelDriverCapabilityDeclaration declaration)
    { declaration=default;if(!TryDriver(driver,out DriverRecord* r))return false;declaration=new KernelDriverCapabilityDeclaration((KernelDriverCapability)r->DeclaredCapabilities);return true; }


    /// <summary>Gets whether a bound driver currently owns a live grant for the requested capability.</summary>
    public static Boolean HasCapabilityGrant(KernelDriverDeviceContext context,KernelDriverCapability capability)
    {
        if(!KernelDriverMath.IsSingleCapability(capability)||!TryBound(context.Device,out DeviceRecord* d,out _,out KernelDriverDeviceContext actual)||actual.Driver.Value!=context.Driver.Value)return false;
        UInt64 bit=(UInt64)capability;for(Int32 i=0;i<d->GrantCount;i++)if((d->GrantCapability[i]&bit)==bit)return true;return false;
    }

    /// <summary>Returns one live grant owned by a bound driver for a requested capability.</summary>
    public static Boolean TryGetCapabilityGrant(KernelDriverDeviceContext context,KernelDriverCapability capability,out KernelDriverCapabilityGrant grant)
    {
        grant=default;if(!KernelDriverMath.IsSingleCapability(capability)||!TryBound(context.Device,out DeviceRecord* d,out _,out KernelDriverDeviceContext actual)||actual.Driver.Value!=context.Driver.Value)return false;
        UInt64 bit=(UInt64)capability;for(Int32 i=0;i<d->GrantCount;i++)if((d->GrantCapability[i]&bit)==bit){grant=new KernelDriverCapabilityGrant(d->GrantToken[i],context.Device,context.Driver,capability,d->GrantStart[i],d->GrantLength[i],(KernelDriverCapabilityAccess)d->GrantAccess[i]);return true;}return false;
    }

    /// <summary>Applies kernel policy to the complete declaration when a driver binds. Declarations are ceilings; grants are the actual authority.</summary>
}
