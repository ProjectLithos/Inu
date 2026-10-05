using System;

namespace Inu.Kernel.Virtio;

/// <summary>Provides allocation-free VirtIO PCI identification and split-queue sizing calculations.</summary>
public static class VirtioMath
{
    /// <summary>Resolves modern and transitional VirtIO PCI identifiers to a supported device type.</summary>
    public static VirtioDeviceType IdentifyDeviceType(UInt16 deviceId,UInt16 subsystemId)
    {if(deviceId>=0x1040U&&deviceId<=0x107FU){UInt16 type=(UInt16)(deviceId-0x1040U);return type<=4U?(VirtioDeviceType)type:VirtioDeviceType.Unknown;}if(deviceId>=0x1000U&&deviceId<=0x103FU&&subsystemId<=4U)return (VirtioDeviceType)subsystemId;return VirtioDeviceType.Unknown;}
    /// <summary>Selects the largest power-of-two queue size not exceeding both the device maximum and requested size.</summary>
    public static UInt16 SelectQueueSize(UInt16 maximum,UInt16 requested){UInt16 limit=maximum<requested?maximum:requested;if(limit==0U)return (UInt16)0;UInt16 result=(UInt16)1;while(result<=limit/2U)result=(UInt16)(result*2U);return result;}
    /// <summary>Computes the byte size of a split virtqueue containing descriptors, available ring, and used ring.</summary>
    public static UInt64 SplitQueueBytes(UInt16 size){if(size==0U)return 0UL;UInt64 descriptors=(UInt64)size*16UL;UInt64 available=6UL+(UInt64)size*2UL;UInt64 usedOffset=(descriptors+available+3UL)&~3UL;return usedOffset+6UL+(UInt64)size*8UL;}
}
