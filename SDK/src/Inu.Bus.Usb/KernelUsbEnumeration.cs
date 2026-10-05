using System;

namespace Inu.Bus.Usb;

/// <summary>Optional USB device-enumeration state machine.</summary>
public static unsafe class KernelUsbEnumeration
{
    public static Boolean Initialize()=>KernelUsbEnumerationServices.Register(&EnumerateService);
    private static Boolean EnumerateService(UsbHostHandle host,Byte parentAddress,Byte port,UsbSpeed speed,UsbDeviceHandle* device){if(device==null)return false;UsbDeviceHandle value=default;if(!Enumerate(host,parentAddress,port,speed,out value))return false;*device=value;return true;}
    public static Boolean Enumerate(UsbHostHandle host,Byte parentAddress,Byte port,UsbSpeed speed,out UsbDeviceHandle device)
    {
        device=default;if(port==0)return KernelUsbEnumerationBusContract.Fail();Byte address=KernelUsbEnumerationBusContract.NextAddress();if(address==0)return KernelUsbEnumerationBusContract.Fail();if(!KernelUsbEnumerationBusContract.Prepare(host,parentAddress,port,speed,address))return KernelUsbEnumerationBusContract.Fail();
        Byte* scratch=KernelUsbEnumerationBusContract.Scratch();UInt32 scratchBytes=KernelUsbEnumerationBusContract.ScratchBytes();if(scratch==null||scratchBytes<18U)return KernelUsbEnumerationBusContract.Fail();for(UInt32 i=0;i<scratchBytes;i++)scratch[i]=0;
        UInt32 got=0;UsbSetupPacket get8=new(0x80,6,0x0100,0,8);if(!KernelUsbEnumerationBusContract.Control(host,0,0,&get8,scratch,8,&got)||got<8)return KernelUsbEnumerationBusContract.Fail();Byte mps=scratch[7];UsbSetupPacket setAddress=new(0,5,address,0,0);if(!KernelUsbEnumerationBusContract.Control(host,0,0,&setAddress,null,0,&got))return KernelUsbEnumerationBusContract.Fail();
        UsbSetupPacket getDevice=new(0x80,6,0x0100,0,18);if(!KernelUsbEnumerationBusContract.Control(host,address,0,&getDevice,scratch,18,&got)||!UsbDescriptorParser.TryParseDevice(scratch,got,out UsbDeviceDescriptor dd))return KernelUsbEnumerationBusContract.Fail();UsbSetupPacket getCfg9=new(0x80,6,0x0200,0,9);if(!KernelUsbEnumerationBusContract.Control(host,address,0,&getCfg9,scratch,9,&got)||got<9)return KernelUsbEnumerationBusContract.Fail();UInt16 total=UsbDescriptorParser.Read16(scratch+2);if(total<9||total>scratchBytes)return KernelUsbEnumerationBusContract.Fail();
        UsbSetupPacket getCfg=new(0x80,6,0x0200,0,total);if(!KernelUsbEnumerationBusContract.Control(host,address,0,&getCfg,scratch,total,&got)||got<total)return KernelUsbEnumerationBusContract.Fail();Byte configuration=scratch[5];UsbSetupPacket setCfg=new(0,9,configuration,0,0);if(!KernelUsbEnumerationBusContract.Control(host,address,0,&setCfg,null,0,&got))return KernelUsbEnumerationBusContract.Fail();if(!KernelUsbEnumerationBusContract.AddDevice(host,address,parentAddress,port,speed,dd,mps,configuration,out device))return KernelUsbEnumerationBusContract.Fail();if(!KernelUsbEnumerationBusContract.ParseInterfaces(host,device,scratch,total)){KernelUsbEnumerationBusContract.RemoveDevice(device);return KernelUsbEnumerationBusContract.Fail();}KernelUsbEnumerationBusContract.MarkEnumerated();return true;
    }
}
