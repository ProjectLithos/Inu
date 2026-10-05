using System;

namespace Inu.Bus.Usb;

/// <summary>Dependency-resolved registry for optional USB enumeration.</summary>
public static unsafe class KernelUsbEnumerationServices
{
    private static delegate*<UsbHostHandle,Byte,Byte,UsbSpeed,UsbDeviceHandle*,Boolean> _enumerate;
    public static Boolean IsAvailable=>_enumerate!=null;
    public static Boolean Register(delegate*<UsbHostHandle,Byte,Byte,UsbSpeed,UsbDeviceHandle*,Boolean> enumerate){if(enumerate==null||_enumerate!=null)return false;_enumerate=enumerate;return true;}
    internal static Boolean Enumerate(UsbHostHandle host,Byte parentAddress,Byte port,UsbSpeed speed,out UsbDeviceHandle device){device=default;if(_enumerate==null)return false;UsbDeviceHandle value=default;if(!_enumerate(host,parentAddress,port,speed,&value))return false;device=value;return true;}
}
