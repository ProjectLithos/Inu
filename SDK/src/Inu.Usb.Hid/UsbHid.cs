using System;
using Inu.Bus.Usb;
using Inu.Kernel.Drivers;
namespace Inu.Usb.Hid;

public readonly struct UsbHidKeyboardEvent
{
    public UsbHidKeyboardEvent(Byte usage,Boolean pressed,Char character,Byte modifiers){Usage=usage;Pressed=pressed;Character=character;Modifiers=modifiers;}
    public Byte Usage{get;} public Boolean Pressed{get;} public Char Character{get;} public Byte Modifiers{get;}
}
public readonly struct UsbHidMouseState
{
    public UsbHidMouseState(Int32 x,Int32 y,Int32 wheel,Byte buttons){X=x;Y=y;Wheel=wheel;Buttons=buttons;}
    public Int32 X{get;} public Int32 Y{get;} public Int32 Wheel{get;} public Byte Buttons{get;}
}
public readonly struct UsbHidCapabilities
{
    public UsbHidCapabilities(Boolean initialized,UInt32 keyboards,UInt32 mice,UInt64 keyboardReports,UInt64 mouseReports){Initialized=initialized;Keyboards=keyboards;Mice=mice;KeyboardReports=keyboardReports;MouseReports=mouseReports;}
    public Boolean Initialized{get;} public UInt32 Keyboards{get;} public UInt32 Mice{get;} public UInt64 KeyboardReports{get;} public UInt64 MouseReports{get;}
}

/// <summary>Owns generic USB HID transport and delegates protocol interpretation to optional HID components.</summary>
public static unsafe partial class UsbHid
{
    private struct HidRec{internal Byte Used,Device,Endpoint,Protocol,MaxPacket;internal UInt32 Generic;internal fixed Byte Previous[8];}
    private const Int32 Max=32;
    private static HidRec* _r;
    private static Inu.Kernel.Heap.KernelHeapAllocation _a;
    private static Boolean _initialized;
    private static KernelDriverHandle _driver;
    private static UInt32 _keyboards,_mice;

    public static Boolean Initialize()
    {
        if(_initialized)return true;
        if(!Inu.Kernel.Heap.KernelHeap.TryAllocate((UInt64)Max*(UInt64)sizeof(HidRec),64,true,out _a))return false;
        _r=(HidRec*)(nuint)_a.Address;
        KernelDriverMatchRule rule=new(KernelDeviceBus.Usb,true,0,false,0,false,0x030100U,0xFFFF00U);
        KernelDriverCallbacks cb=new(&Probe,&Start,&Stop,&Remove,null);
        if(!KernelDrivers.RegisterDriver("usb-hid",rule,cb,out _driver)){Inu.Kernel.Heap.KernelHeap.TryRelease(_a);return false;}
        _initialized=true;
        return Discover();
    }

    public static Boolean Discover()
    {
        if(!_initialized)return false;
        UInt32 count=KernelUsbBus.GetInterfaceCount();
        for(UInt32 i=0;i<count;i++)
        {
            if(!KernelUsbBus.TryGetInterface(i,out UsbInterfaceInfo inf)||inf.Descriptor.Class!=(Byte)UsbClassCode.Hid||inf.Descriptor.SubClass!=1||inf.DeviceNode.Value==0U)continue;
            if(!KernelDrivers.MatchAndStartDevice(inf.DeviceNode)&&FindGeneric(inf.DeviceNode)<0)return false;
        }
        return true;
    }

    public static Boolean Service()
    {
        if(!_initialized)return false;
        Byte* report=stackalloc Byte[64];
        for(Int32 i=0;i<Max;i++)
        {
            HidRec* r=_r+i;if(r->Used==0)continue;
            for(Int32 z=0;z<64;z++)report[z]=0;
            if(!KernelUsbBus.InterruptTransfer(new UsbDeviceHandle(r->Device),r->Endpoint,report,r->MaxPacket,out UInt32 got))continue;
            if(r->Protocol==1&&got>=8)
            {
                Byte* previous=r->Previous;UsbHidKeyboardServices.ProcessReport(previous,report,got);
            }
            else if(r->Protocol==2&&got>=3) UsbHidMouseServices.ProcessReport(report,got);
        }
        return true;
    }

    public static Boolean IsInitialized()=>_initialized;
    public static Boolean SetKeyboardEventHandler(delegate*<UsbHidKeyboardEvent,Boolean> handler)=>UsbHidKeyboardServices.SetEventHandler(handler);
    public static Boolean SetMouseEventHandler(delegate*<UsbHidMouseState,Boolean> handler)=>UsbHidMouseServices.SetEventHandler(handler);
    public static UsbHidCapabilities GetCapabilities()=>new(_initialized,_keyboards,_mice,UsbHidKeyboardServices.ReportCount,UsbHidMouseServices.ReportCount);
    public static UsbHidMouseState GetMouseState()=>UsbHidMouseServices.GetState();
    public static Char TranslateBootUsage(Byte usage,Boolean shift)=>UsbHidKeyboardServices.TranslateUsage(usage,shift);

    private static Boolean Probe(KernelDriverDeviceContext* c)
    {
        if(c==null||!KernelUsbBus.TryGetInterface(c->Device,out UsbInterfaceInfo inf)||inf.Descriptor.Class!=(Byte)UsbClassCode.Hid||inf.Descriptor.SubClass!=1)return false;
        for(UInt32 e=0;e<4;e++)if(inf.TryGetEndpoint(e,out UsbEndpointDescriptor x)&&x.In&&x.TransferType==UsbTransferType.Interrupt)return true;
        return false;
    }

    private static Boolean Start(KernelDriverDeviceContext* c)
    {
        if(c==null||FindGeneric(c->Device)>=0||!KernelUsbBus.TryGetInterface(c->Device,out UsbInterfaceInfo inf))return c!=null&&FindGeneric(c->Device)>=0;
        UsbEndpointDescriptor ep=default;Boolean found=false;
        for(UInt32 e=0;e<4;e++){if(inf.TryGetEndpoint(e,out UsbEndpointDescriptor x)&&x.In&&x.TransferType==UsbTransferType.Interrupt){ep=x;found=true;break;}}
        if(!found)return false;
        Int32 s=Free();if(s<0)return false;
        HidRec* r=_r+s;r->Used=1;r->Device=(Byte)inf.Device.Value;r->Endpoint=ep.Address;r->Protocol=inf.Descriptor.Protocol;r->MaxPacket=(Byte)(ep.MaximumPacketSize>64?64:ep.MaximumPacketSize);r->Generic=c->Device.Value;
        UsbSetupPacket setProtocol=new(0x21,11,0,inf.Descriptor.Number,0);KernelUsbBus.ControlTransfer(inf.Device,setProtocol,null,0,out _);
        UsbSetupPacket setIdle=new(0x21,10,0,inf.Descriptor.Number,0);KernelUsbBus.ControlTransfer(inf.Device,setIdle,null,0,out _);
        if(r->Protocol==1)_keyboards++;else if(r->Protocol==2)_mice++;
        return true;
    }

    private static Boolean Stop(KernelDriverDeviceContext* c)
    {
        if(c==null)return false;Int32 i=FindGeneric(c->Device);if(i<0)return true;
        HidRec* r=_r+i;if(r->Protocol==1&&_keyboards>0U)_keyboards--;else if(r->Protocol==2&&_mice>0U)_mice--;
        Clear((Byte*)r,(UInt32)sizeof(HidRec));return true;
    }
    private static Boolean Remove(KernelDriverDeviceContext* c)=>Stop(c);
    private static Int32 FindGeneric(KernelDeviceHandle h){for(Int32 i=0;i<Max;i++)if((_r+i)->Used!=0&&(_r+i)->Generic==h.Value)return i;return -1;}
    private static Int32 Free(){for(Int32 i=0;i<Max;i++)if((_r+i)->Used==0)return i;return -1;}
    private static void Clear(Byte* p,UInt32 n){for(UInt32 i=0;i<n;i++)p[i]=0;}
}
