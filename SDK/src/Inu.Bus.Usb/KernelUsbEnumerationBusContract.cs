using System;

namespace Inu.Bus.Usb;

/// <summary>Explicit bus/registry contract consumed by optional USB enumeration components.</summary>
public static unsafe class KernelUsbEnumerationBusContract
{
    private static delegate*<UsbHostHandle,Byte,Byte,UsbSpeed,Byte,Boolean> _prepare;
    private static delegate*<UsbHostHandle,Byte,Byte,UsbSetupPacket*,Byte*,UInt32,UInt32*,Boolean> _control;
    private static delegate*<Byte> _nextAddress;
    private static delegate*<UsbHostHandle,Byte,Byte,Byte,UsbSpeed,UsbDeviceDescriptor,Byte,Byte,UsbDeviceHandle*,Boolean> _addDevice;
    private static delegate*<UsbHostHandle,UsbDeviceHandle,Byte*,UInt32,Boolean> _parseInterfaces;
    private static delegate*<UsbDeviceHandle,Boolean> _removeDevice;
    private static delegate*<Byte*> _scratch;
    private static delegate*<UInt32> _scratchBytes;
    private static delegate*<void> _enumerated;
    private static delegate*<Boolean> _failure;

    internal static Boolean Register(
        delegate*<UsbHostHandle,Byte,Byte,UsbSpeed,Byte,Boolean> prepare,
        delegate*<UsbHostHandle,Byte,Byte,UsbSetupPacket*,Byte*,UInt32,UInt32*,Boolean> control,
        delegate*<Byte> nextAddress,
        delegate*<UsbHostHandle,Byte,Byte,Byte,UsbSpeed,UsbDeviceDescriptor,Byte,Byte,UsbDeviceHandle*,Boolean> addDevice,
        delegate*<UsbHostHandle,UsbDeviceHandle,Byte*,UInt32,Boolean> parseInterfaces,
        delegate*<UsbDeviceHandle,Boolean> removeDevice,
        delegate*<Byte*> scratch,
        delegate*<UInt32> scratchBytes,
        delegate*<void> enumerated,
        delegate*<Boolean> failure)
    {
        if(prepare==null||control==null||nextAddress==null||addDevice==null||parseInterfaces==null||removeDevice==null||scratch==null||scratchBytes==null||enumerated==null||failure==null)return false;
        if(_prepare!=null)return true;
        _prepare=prepare;_control=control;_nextAddress=nextAddress;_addDevice=addDevice;_parseInterfaces=parseInterfaces;_removeDevice=removeDevice;_scratch=scratch;_scratchBytes=scratchBytes;_enumerated=enumerated;_failure=failure;return true;
    }
    public static Boolean Prepare(UsbHostHandle host,Byte parentAddress,Byte port,UsbSpeed speed,Byte address)=>_prepare!=null&&_prepare(host,parentAddress,port,speed,address);
    public static Boolean Control(UsbHostHandle host,Byte address,Byte endpoint,UsbSetupPacket* setup,Byte* buffer,UInt32 bytes,UInt32* transferred)=>_control!=null&&_control(host,address,endpoint,setup,buffer,bytes,transferred);
    public static Byte NextAddress()=>_nextAddress==null?(Byte)0:_nextAddress();
    public static Boolean AddDevice(UsbHostHandle host,Byte address,Byte parentAddress,Byte port,UsbSpeed speed,UsbDeviceDescriptor descriptor,Byte maxPacket0,Byte configuration,out UsbDeviceHandle device){device=default;if(_addDevice==null)return false;UsbDeviceHandle value=default;if(!_addDevice(host,address,parentAddress,port,speed,descriptor,maxPacket0,configuration,&value))return false;device=value;return true;}
    public static Boolean ParseInterfaces(UsbHostHandle host,UsbDeviceHandle device,Byte* data,UInt32 bytes)=>_parseInterfaces!=null&&_parseInterfaces(host,device,data,bytes);
    public static Boolean RemoveDevice(UsbDeviceHandle device)=>_removeDevice!=null&&_removeDevice(device);
    public static Byte* Scratch()=>_scratch==null?null:_scratch();
    public static UInt32 ScratchBytes()=>_scratchBytes==null?0U:_scratchBytes();
    public static void MarkEnumerated(){if(_enumerated!=null)_enumerated();}
    public static Boolean Fail()=>_failure!=null&&_failure();
}
