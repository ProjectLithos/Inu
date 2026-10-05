using System;

namespace Inu.Kernel.Drivers;

public static class KernelDriverMath
{
    public static Boolean Matches(KernelDriverMatchRule rule, KernelDeviceIdentifier device)
    { if(rule.MatchBus&&rule.Bus!=device.Bus)return false; if(rule.MatchVendor&&rule.VendorId!=device.VendorId)return false; if(rule.MatchDevice&&rule.DeviceId!=device.DeviceId)return false; return rule.ClassMask==0U||(rule.ClassCode&rule.ClassMask)==(device.ClassCode&rule.ClassMask); }
    public static Boolean IsValidResource(KernelDeviceResource resource)
    { if(resource.Type==KernelDeviceResourceType.None)return false; if(resource.Length==0UL)return resource.Type==KernelDeviceResourceType.Interrupt||resource.Type==KernelDeviceResourceType.Dma; return resource.Start<=UInt64.MaxValue-(resource.Length-1UL); }
    public static Boolean IsValidInterruptRequest(KernelDriverInterruptRequest request) => request.Device.Value!=0U&&request.Priority<=15U;
    public static Boolean IsSingleCapability(KernelDriverCapability capability) { UInt64 value=(UInt64)capability; return value!=0UL&&(value&(value-1UL))==0UL&&value<=((UInt64)KernelDriverCapability.Filesystem); }
    public static Boolean IsValidCapabilityRequest(KernelDriverCapabilityRequest request)
    {
        if(!IsSingleCapability(request.Capability)||request.Access==KernelDriverCapabilityAccess.None)return false;
        Boolean ranged=request.Capability==KernelDriverCapability.Mmio||request.Capability==KernelDriverCapability.PortIo||request.Capability==KernelDriverCapability.PhysicalMemory;
        if(!ranged)return request.Start==0UL&&request.Length==0UL;
        if(request.Length==0UL)return false;
        return request.Start<=UInt64.MaxValue-(request.Length-1UL);
    }
    public static Boolean RangeContains(UInt64 outerStart,UInt64 outerLength,UInt64 innerStart,UInt64 innerLength)
    { if(outerLength==0UL||innerLength==0UL)return false;if(outerStart>UInt64.MaxValue-(outerLength-1UL)||innerStart>UInt64.MaxValue-(innerLength-1UL))return false;UInt64 outerEnd=outerStart+outerLength-1UL;UInt64 innerEnd=innerStart+innerLength-1UL;return innerStart>=outerStart&&innerEnd<=outerEnd; }
    /// <summary>Returns whether an issued grant access mode covers the requested operation.</summary>
    public static Boolean AccessCovers(KernelDriverCapabilityAccess granted,KernelDriverCapabilityAccess requested)
    {
        if(granted==KernelDriverCapabilityAccess.None||requested==KernelDriverCapabilityAccess.None)return false;
        if(granted==requested)return true;
        return granted==KernelDriverCapabilityAccess.ReadWrite&&(requested==KernelDriverCapabilityAccess.Read||requested==KernelDriverCapabilityAccess.Write);
    }
    public static Boolean IsValidOptions(KernelDriverFrameworkOptions options)
    {
        if(options.InitialDriverCapacity==0U||options.InitialDeviceCapacity==0U)return false;
        if(options.InitialDriverCapacity>Int32.MaxValue||options.InitialDeviceCapacity>Int32.MaxValue)return false;
        if(options.MaximumDriverCapacity<options.InitialDriverCapacity||options.MaximumDeviceCapacity<options.InitialDeviceCapacity)return false;
        return true;
    }
    public static UInt32 NextCapacity(UInt32 current, UInt32 maximum)
    {
        if(current>=maximum)return current;
        UInt64 doubled=(UInt64)current*2UL; UInt64 result=doubled>maximum?maximum:doubled;
        if(result>Int32.MaxValue)result=Int32.MaxValue;
        return (UInt32)result;
    }
}
