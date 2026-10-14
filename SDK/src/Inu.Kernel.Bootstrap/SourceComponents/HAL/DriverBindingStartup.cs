using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Runs matching/binding only after all selected driver families have registered.</summary>
public static class DriverBindingStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_DRIVERS
        return KernelDrivers.BindAndStartMatchingDevices();
#else
        return true;
#endif
    }
}
