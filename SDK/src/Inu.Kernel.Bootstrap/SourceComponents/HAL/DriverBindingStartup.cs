using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Runs matching/binding only after all selected driver families have registered.</summary>
public static class DriverBindingStartup
{
    public static Boolean Initialize()
    {
#if INU_KERNELAREA_DRIVERS
        if (!KernelDrivers.BindAndStartMatchingDevices())
        {
            global::Inu.Kernel.Console.KernelConsole.WriteHostControl("DRIVER_BINDING_FAILED");
            return false;
        }
        return true;
#else
        return true;
#endif
    }
}
