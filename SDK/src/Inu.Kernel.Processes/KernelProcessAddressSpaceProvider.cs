using System;
namespace Inu.Kernel.Processes;
public static unsafe class KernelProcessAddressSpaceProvider
{
    public static Boolean Register()=>KernelProcessAddressSpaceServices.Register(&KernelProcesses.ReleaseOwnedImplementation,&KernelProcesses.ReleaseTemporaryImplementation,&KernelProcesses.StoreAllocationImplementation);
}
