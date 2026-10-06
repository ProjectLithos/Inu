using System;

namespace Inu.Kernel.Hardware;

/// <summary><inu.api>Coder-facing policy facade for the unified hardware/device tree. Bus-specific driver internals are not exposed by default.</inu.api></summary>
public static class Devices
{
    public static Boolean IsInitialized()=>global::Inu.Kernel.Drivers.KernelDrivers.IsInitialized();
    public static UInt32 GetRegisteredDeviceCount()=>global::Inu.Kernel.Drivers.KernelDrivers.GetCapabilities().RegisteredDevices;
    public static UInt32 GetBoundDeviceCount()=>global::Inu.Kernel.Drivers.KernelDrivers.GetCapabilities().BoundDevices;
    public static UInt32 GetStartedDeviceCount()=>global::Inu.Kernel.Drivers.KernelDrivers.GetCapabilities().StartedDevices;
    public static Boolean StartMatching()=>global::Inu.Kernel.Drivers.KernelDrivers.BindAndStartMatchingDevices();
    public static Boolean PauseAll()=>global::Inu.Kernel.Drivers.KernelDrivers.SuspendStartedDevices();
    public static Boolean ResumeAll()=>global::Inu.Kernel.Drivers.KernelDrivers.ResumeSuspendedDevices();
}
