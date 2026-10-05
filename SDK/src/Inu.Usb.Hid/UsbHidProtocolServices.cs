using System;
namespace Inu.Usb.Hid;

/// <summary>Dependency-resolved keyboard protocol service used by the generic HID transport.</summary>
public static unsafe class UsbHidKeyboardServices
{
    private static UInt64 _process,_translate,_reportCount,_handler;
    public static UInt64 ReportCount => _reportCount==0?0UL:((delegate*<UInt64>)(void*)_reportCount)();
    public static Boolean Register(delegate*<Byte*,Byte*,UInt32,Boolean> process,delegate*<Byte,Boolean,Char> translate,delegate*<UInt64> reportCount)
    {if(process==null||translate==null||reportCount==null||_process!=0)return false;_process=(UInt64)(void*)process;_translate=(UInt64)(void*)translate;_reportCount=(UInt64)(void*)reportCount;return true;}
    public static Boolean ProcessReport(Byte* previous,Byte* report,UInt32 length)=>_process==0||((delegate*<Byte*,Byte*,UInt32,Boolean>)(void*)_process)(previous,report,length);
    public static Boolean SetEventHandler(delegate*<UsbHidKeyboardEvent,Boolean> handler){if(_process==0)return false;_handler=(UInt64)(void*)handler;return true;}
    public static Boolean Dispatch(UsbHidKeyboardEvent value)=>_handler==0||((delegate*<UsbHidKeyboardEvent,Boolean>)(void*)_handler)(value);
    public static Char TranslateUsage(Byte usage,Boolean shift)=>_translate==0?'\0':((delegate*<Byte,Boolean,Char>)(void*)_translate)(usage,shift);
}

/// <summary>Dependency-resolved mouse protocol service used by the generic HID transport.</summary>
public static unsafe class UsbHidMouseServices
{
    private static UInt64 _process,_getState,_reportCount,_handler;
    public static UInt64 ReportCount => _reportCount==0?0UL:((delegate*<UInt64>)(void*)_reportCount)();
    public static Boolean Register(delegate*<Byte*,UInt32,Boolean> process,delegate*<UsbHidMouseState> getState,delegate*<UInt64> reportCount)
    {if(process==null||getState==null||reportCount==null||_process!=0)return false;_process=(UInt64)(void*)process;_getState=(UInt64)(void*)getState;_reportCount=(UInt64)(void*)reportCount;return true;}
    public static Boolean ProcessReport(Byte* report,UInt32 length)=>_process==0||((delegate*<Byte*,UInt32,Boolean>)(void*)_process)(report,length);
    public static Boolean SetEventHandler(delegate*<UsbHidMouseState,Boolean> handler){if(_process==0)return false;_handler=(UInt64)(void*)handler;return true;}
    public static Boolean Dispatch(UsbHidMouseState value)=>_handler==0||((delegate*<UsbHidMouseState,Boolean>)(void*)_handler)(value);
    public static UsbHidMouseState GetState()=>_getState==0?default:((delegate*<UsbHidMouseState>)(void*)_getState)();
}
