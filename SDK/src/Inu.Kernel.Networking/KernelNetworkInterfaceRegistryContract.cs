using System;

namespace Inu.Kernel.Networking;

/// <summary>Language-neutral interface-registry contract surface consumed by optional networking components.</summary>
public static unsafe class KernelNetworkInterfaceRegistryContract
{
    private static delegate*<KernelNetworkInterfaceHandle,Boolean> _contains;

    internal static Boolean Register(delegate*<KernelNetworkInterfaceHandle,Boolean> contains)
    {
        if(contains==null)return false;
        if(_contains!=null)return true;
        _contains=contains;
        return true;
    }

    public static Boolean Contains(KernelNetworkInterfaceHandle handle)=>_contains!=null&&_contains(handle);
}
