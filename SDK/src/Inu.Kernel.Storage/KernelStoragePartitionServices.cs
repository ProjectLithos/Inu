using System;

namespace Inu.Kernel.Storage;

/// <summary>Dependency-resolved registry for optional partition discovery.</summary>
public static unsafe class KernelStoragePartitionServices
{
    private static delegate*<KernelStorageDeviceHandle,Byte*,UInt32,UInt32*,Boolean> _discover;
    public static Boolean IsAvailable=>_discover!=null;
    public static Boolean Register(delegate*<KernelStorageDeviceHandle,Byte*,UInt32,UInt32*,Boolean> discover){if(discover==null||_discover!=null)return false;_discover=discover;return true;}
    internal static Boolean Discover(KernelStorageDeviceHandle device,Byte* scratch,UInt32 scratchBytes,out UInt32 discovered){discovered=0U;if(_discover==null)return false;UInt32 value=0U;if(!_discover(device,scratch,scratchBytes,&value))return false;discovered=value;return true;}
}
