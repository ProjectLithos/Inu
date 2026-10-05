using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Drivers;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Ps2;

/// <summary>Owns the legacy i8042 controller, PS/2 keyboard, mouse, and keyboard layout state.</summary>
public static unsafe partial class KernelPs2
{
    /// <summary>Gets the decoded keyboard-input contract implemented by this PS/2 driver.</summary>
    public const UInt32 InputContractVersion = 3U;

    private const UInt16 DataPort=0x60, StatusPort=0x64, CommandPort=0x64;
    private static KernelDriverHandle _driver; private static KernelDeviceHandle _device;
    private static Boolean _initialized,_controller,_keyboard,_mouse;
    private static UInt64 _mousePackets;
    private static Byte _mouseIndex; private static Byte _m0,_m1,_m2;
    private static Ps2MouseState _mouseState;
    private static delegate*<Ps2MouseState, Boolean> _mouseEventHandler;

    /// <summary>Registers the legacy i8042 as a platform device and starts it through the unified driver lifecycle.</summary>
    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if(!KernelDrivers.IsInitialized()&&!KernelDrivers.Initialize())return false;
        KernelDriverMatchRule rule=new(KernelDeviceBus.Platform,true,0x1D1D,true,0x8042,true,0x090000U,0xFF0000U);
        KernelDriverCallbacks callbacks=new(&Probe,&Start,&Stop,&Remove,null);
        KernelDriverCapabilityDeclaration caps=new(KernelDriverCapability.PortIo|KernelDriverCapability.Interrupt);
        if(!KernelDrivers.RegisterDriver("i8042-ps2",rule,callbacks,caps,out _driver))return false;
        KernelDeviceIdentifier id=new(KernelDeviceBus.Platform,0x1D1D,0x8042,0,0,0x090000U,1,0x8042U);
        if(!KernelDrivers.DiscoverDevice(id,default,out _device))return false;
        if(!KernelDrivers.AddResource(_device,new KernelDeviceResource(KernelDeviceResourceType.IoPort,DataPort,1,0))||!KernelDrivers.AddResource(_device,new KernelDeviceResource(KernelDeviceResourceType.IoPort,StatusPort,1,0))||!KernelDrivers.AddResource(_device,new KernelDeviceResource(KernelDeviceResourceType.Interrupt,1,1,0))||!KernelDrivers.AddResource(_device,new KernelDeviceResource(KernelDeviceResourceType.Interrupt,12,1,0)))return false;
        if(!KernelDrivers.MatchAndStartDevice(_device))return false;
        if (!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.KeyboardLayout,&GetLayoutSyscall) || !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.KeyboardLayout,&SetLayoutSyscall)) return false;
        _initialized=true;
        return true;
    }

}
