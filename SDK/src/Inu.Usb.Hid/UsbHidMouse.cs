using System;
namespace Inu.Usb.Hid;

/// <summary>Optional USB boot-mouse protocol decoder. It owns pointer state and event delivery, not USB transport.</summary>
public static unsafe class UsbHidMouse
{
    private static Boolean _initialized;
    private static UInt64 _reports;
    private static UsbHidMouseState _state;
    public static Boolean Initialize(){if(_initialized)return true;if(!UsbHidMouseServices.Register(&Process,&GetState,&GetReportCount))return false;_initialized=true;return true;}
    public static Boolean IsInitialized()=>_initialized;
    private static UsbHidMouseState GetState()=>_state;
    private static UInt64 GetReportCount()=>_reports;
    private static Boolean Process(Byte* report,UInt32 length)
    {if(length<3)return false;Int32 dx=(SByte)report[1],dy=(SByte)report[2],wheel=length>3?(SByte)report[3]:0;_state=new UsbHidMouseState(_state.X+dx,_state.Y+dy,_state.Wheel+wheel,report[0]);_reports++;return UsbHidMouseServices.Dispatch(_state);}
}
