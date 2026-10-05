using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Pci;

public static unsafe partial class KernelPci
{
    /// <summary>Gets one discovered PCI function by discovery index.</summary>
    public static Boolean TryGetDevice(UInt32 index,out PciDeviceInfo info)
    {
        info=default;if(index>=_deviceCount)return false;UInt32 found=0;
        for(UInt32 i=0;i<_deviceCapacity;i++){DeviceRecord* r=_devices+i;if(r->Used==0)continue;if(found++==index){info=Info(r);return true;}}
        return false;
    }

    /// <summary>Finds PCI metadata for a generic driver-framework device handle.</summary>
    public static Boolean TryGetDevice(KernelDeviceHandle handle,out PciDeviceInfo info)
    {
        info=default;if(handle.Value==0U)return false;
        for(UInt32 i=0;i<_deviceCapacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->Handle==handle.Value){info=Info(r);return true;}}
        return false;
    }

    /// <summary>Reads one 8-bit PCI configuration field.</summary>
    public static Boolean TryRead8(PciLocation location,UInt16 offset,out Byte value){value=0;if(!TryRead32(location,(UInt16)(offset&0xFFFC),out UInt32 dword))return false;value=(Byte)(dword>>((offset&3)*8));return true;}
    /// <summary>Reads one 16-bit PCI configuration field.</summary>
    public static Boolean TryRead16(PciLocation location,UInt16 offset,out UInt16 value){value=0;if((offset&1)!=0)return false;if(!TryRead32(location,(UInt16)(offset&0xFFFC),out UInt32 dword))return false;value=(UInt16)(dword>>((offset&2)*8));return true;}
    /// <summary>Reads one 32-bit PCI configuration field through the registered configuration transports.</summary>
    public static Boolean TryRead32(PciLocation location,UInt16 offset,out UInt32 value)
    {
        value=0;if((offset&3)!=0||location.Device>31||location.Function>7)return false;
        return KernelPciConfigurationServices.TryRead32(location,offset,out value);
    }

    /// <summary>Writes one 8-bit PCI configuration field.</summary>
    public static Boolean TryWrite8(PciLocation location,UInt16 offset,Byte value){UInt16 aligned=(UInt16)(offset&0xFFFC);if(!TryRead32(location,aligned,out UInt32 current))return false;Int32 shift=(offset&3)*8;UInt32 mask=0xFFU<<shift;return TryWrite32(location,aligned,(current&~mask)|((UInt32)value<<shift));}
    /// <summary>Writes one 16-bit PCI configuration field.</summary>
    public static Boolean TryWrite16(PciLocation location,UInt16 offset,UInt16 value){if((offset&1)!=0)return false;UInt16 aligned=(UInt16)(offset&0xFFFC);if(!TryRead32(location,aligned,out UInt32 current))return false;Int32 shift=(offset&2)*8;UInt32 mask=0xFFFFU<<shift;return TryWrite32(location,aligned,(current&~mask)|((UInt32)value<<shift));}
    /// <summary>Writes one 32-bit PCI configuration field through the registered configuration transports.</summary>
    public static Boolean TryWrite32(PciLocation location,UInt16 offset,UInt32 value)
    {
        if((offset&3)!=0||location.Device>31||location.Function>7)return false;
        return KernelPciConfigurationServices.TryWrite32(location,offset,value);
    }
}
