using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

public static unsafe partial class KernelDrivers
{
    public static Boolean RegisterDriver(KernelDriverMatchRule rule,KernelDriverCallbacks callbacks,out KernelDriverHandle handle)
        => RegisterDriver(null,rule,callbacks,KernelDriverCapabilityDeclaration.None,out handle);
    public static Boolean RegisterDriver(KernelDriverMatchRule rule,KernelDriverCallbacks callbacks,KernelDriverCapabilityDeclaration declaration,out KernelDriverHandle handle)
        => RegisterDriver(null,rule,callbacks,declaration,out handle);
    public static Boolean RegisterDriver(String name,KernelDriverMatchRule rule,KernelDriverCallbacks callbacks,out KernelDriverHandle handle)
        => RegisterDriver(name,rule,callbacks,KernelDriverCapabilityDeclaration.None,out handle);

    /// <summary>Registers a named driver together with the maximum privilege set it may ever request.</summary>
    public static Boolean RegisterDriver(String name,KernelDriverMatchRule rule,KernelDriverCallbacks callbacks,KernelDriverCapabilityDeclaration declaration,out KernelDriverHandle handle)
    {
        handle=default;if(!_initialized||callbacks.Probe==null||callbacks.Start==null||callbacks.Stop==null||callbacks.Remove==null)return false;
        Int32 slot=FindFreeDriver(); if(slot<0){if(!GrowDrivers())return false;slot=FindFreeDriver();if(slot<0)return false;}
        DriverRecord* r=Driver(slot);Clear((Byte*)r,sizeof(DriverRecord));r->Used=1;r->State=(Byte)KernelDriverState.Registered;r->Bus=(Byte)rule.Bus;r->MatchBus=rule.MatchBus?(Byte)1:(Byte)0;r->Vendor=rule.VendorId;r->MatchVendor=rule.MatchVendor?(Byte)1:(Byte)0;r->Device=rule.DeviceId;r->MatchDevice=rule.MatchDevice?(Byte)1:(Byte)0;r->ClassCode=rule.ClassCode;r->ClassMask=rule.ClassMask;
        CopyDriverName(r,name);r->DeclaredCapabilities=(UInt64)declaration.Capabilities;r->Discover=(UInt64)(void*)callbacks.Discover;r->Probe=(UInt64)(void*)callbacks.Probe;r->Bind=(UInt64)(void*)callbacks.Bind;r->Start=(UInt64)(void*)callbacks.Start;r->Stop=(UInt64)(void*)callbacks.Stop;r->Reset=(UInt64)(void*)callbacks.Reset;r->Suspend=(UInt64)(void*)callbacks.Suspend;r->Resume=(UInt64)(void*)callbacks.Resume;r->Remove=(UInt64)(void*)callbacks.Remove;r->Fail=(UInt64)(void*)callbacks.Fail;r->Recover=(UInt64)(void*)callbacks.Recover;r->Interrupt=(UInt64)(void*)callbacks.Interrupt;_driverCount++;handle=new KernelDriverHandle((UInt32)slot+1U);return true;
    }

    public static Boolean UnregisterDriver(KernelDriverHandle handle)
    { if(!TryDriver(handle,out DriverRecord* driver))return false;for(Int32 i=0;i<(Int32)_deviceCapacity;i++){DeviceRecord* d=Device(i);if(d->Used!=0&&d->BoundDriver==handle.Value)return false;}driver->State=(Byte)KernelDriverState.Removing;Clear((Byte*)driver,sizeof(DriverRecord));_driverCount--;return true; }

    public static Boolean RegisterDevice(KernelDeviceIdentifier identifier,out KernelDeviceHandle handle) => DiscoverDevice(identifier,default,out handle);

    /// <summary>Discovers a device and adds it to the authoritative PCI/USB/ACPI/platform/virtual/logical device tree.</summary>
    public static Boolean DiscoverDevice(KernelDeviceIdentifier identifier,KernelDeviceHandle parent,out KernelDeviceHandle handle)
    {
        handle=default;if(!_initialized||identifier.Bus==KernelDeviceBus.Unknown)return false;if(parent.Value!=0U&&!TryDevice(parent,out _))return false;Int32 slot=FindFreeDevice();if(slot<0){if(!GrowDevices())return false;slot=FindFreeDevice();if(slot<0)return false;}
        DeviceRecord* d=Device(slot);Clear((Byte*)d,sizeof(DeviceRecord));d->Used=1;d->State=(Byte)KernelDeviceState.Discovered;d->Bus=(Byte)identifier.Bus;d->Vendor=identifier.VendorId;d->Device=identifier.DeviceId;d->SubsystemVendor=identifier.SubsystemVendorId;d->Subsystem=identifier.SubsystemId;d->ClassCode=identifier.ClassCode;d->Revision=identifier.Revision;d->Location=identifier.Location;d->Parent=parent.Value;_deviceCount++;_deviceTreeGeneration++;handle=new KernelDeviceHandle((UInt32)slot+1U);
        if(parent.Value!=0U){DeviceRecord* p=Device((Int32)parent.Value-1);d->NextSibling=p->FirstChild;p->FirstChild=handle.Value;}EmitLifecycle(handle,default,KernelDriverLifecycleStage.Discover,KernelDeviceState.Registered,KernelDeviceState.Discovered,KernelDriverFailureCode.None);EmitDeviceStateEvent(KernelDeviceStateEventKind.Arrived,handle,d,default,KernelDeviceState.Registered,KernelDeviceState.Discovered,KernelDriverFailureCode.None);return true;
    }

    public static Boolean AddResource(KernelDeviceHandle device,KernelDeviceResource resource)
    { if(!TryDevice(device,out DeviceRecord* d)||!KernelDriverMath.IsValidResource(resource)||(d->State!=(Byte)KernelDeviceState.Registered&&d->State!=(Byte)KernelDeviceState.Discovered))return false;Int32 i=d->ResourceCount;if(i>=ResourcesPerDevice)return false;d->ResourceType[i]=(Byte)resource.Type;d->ResourceStart[i]=resource.Start;d->ResourceLength[i]=resource.Length;d->ResourceFlags[i]=resource.Flags;d->ResourceCount++;_deviceTreeGeneration++;return true; }
    public static Boolean TryGetResource(KernelDeviceHandle device,UInt32 resourceIndex,out KernelDeviceResource resource)
    { resource=default;if(!TryDevice(device,out DeviceRecord* d)||resourceIndex>=d->ResourceCount)return false;Int32 i=(Int32)resourceIndex;resource=new KernelDeviceResource((KernelDeviceResourceType)d->ResourceType[i],d->ResourceStart[i],d->ResourceLength[i],d->ResourceFlags[i]);return true; }

    /// <summary>Adds or replaces one bounded property on the authoritative device node before driver binding.</summary>
    public static Boolean SetProperty(KernelDeviceHandle device,KernelDeviceProperty property)
    { if(property.Key==KernelDevicePropertyKey.None||!TryDevice(device,out DeviceRecord* d)||d->State==(Byte)KernelDeviceState.Removing||d->State==(Byte)KernelDeviceState.Removed)return false;for(Int32 i=0;i<d->PropertyCount;i++)if(d->PropertyKey[i]==(UInt32)property.Key){d->PropertyValue0[i]=property.Value0;d->PropertyValue1[i]=property.Value1;_deviceTreeGeneration++;return true;}Int32 slot=d->PropertyCount;if(slot>=PropertiesPerDevice)return false;d->PropertyKey[slot]=(UInt32)property.Key;d->PropertyValue0[slot]=property.Value0;d->PropertyValue1[slot]=property.Value1;d->PropertyCount++;_deviceTreeGeneration++;return true; }
    public static Boolean TryGetProperty(KernelDeviceHandle device,KernelDevicePropertyKey key,out KernelDeviceProperty property)
    { property=default;if(key==KernelDevicePropertyKey.None||!TryDevice(device,out DeviceRecord* d))return false;for(Int32 i=0;i<d->PropertyCount;i++)if(d->PropertyKey[i]==(UInt32)key){property=new KernelDeviceProperty(key,d->PropertyValue0[i],d->PropertyValue1[i]);return true;}return false; }
    public static Boolean TryGetPropertyByIndex(KernelDeviceHandle device,UInt32 propertyIndex,out KernelDeviceProperty property)
    { property=default;if(!TryDevice(device,out DeviceRecord* d)||propertyIndex>=d->PropertyCount)return false;Int32 i=(Int32)propertyIndex;property=new KernelDeviceProperty((KernelDevicePropertyKey)d->PropertyKey[i],d->PropertyValue0[i],d->PropertyValue1[i]);return true; }

}
