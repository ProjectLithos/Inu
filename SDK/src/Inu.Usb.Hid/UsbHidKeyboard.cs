using System;
namespace Inu.Usb.Hid;

/// <summary>Optional USB boot-keyboard protocol decoder. It owns decoding and event delivery, not USB transport.</summary>
public static unsafe class UsbHidKeyboard
{
    private static Boolean _initialized;
    private static UInt64 _reports;
    public static Boolean Initialize(){if(_initialized)return true;if(!UsbHidKeyboardServices.Register(&Process,&Translate,&GetReportCount))return false;_initialized=true;return true;}
    public static Boolean IsInitialized()=>_initialized;
    private static UInt64 GetReportCount()=>_reports;
    private static Boolean Process(Byte* previous,Byte* report,UInt32 length)
    {
        if(length<8)return false;Boolean shift=(report[0]&0x22)!=0;
        for(Int32 q=2;q<8;q++){Byte u=previous[q];if(u==0)continue;Boolean present=false;for(Int32 i=2;i<8;i++)if(report[i]==u)present=true;if(!present)UsbHidKeyboardServices.Dispatch(new UsbHidKeyboardEvent(u,false,'\0',report[0]));}
        for(Int32 i=2;i<8;i++){Byte u=report[i];if(u==0)continue;Boolean old=false;for(Int32 q=2;q<8;q++)if(previous[q]==u)old=true;if(!old)UsbHidKeyboardServices.Dispatch(new UsbHidKeyboardEvent(u,true,Translate(u,shift),report[0]));}
        for(Int32 i=0;i<8;i++)previous[i]=report[i];_reports++;return true;
    }
    private static Char Translate(Byte u,Boolean shift)
    {if(u>=4&&u<=29){Char c=(Char)('a'+u-4);return shift?(Char)(c-'a'+'A'):c;}if(u>=30&&u<=38){const String normal="123456789";const String shifted="!@#$%^&*(";return shift?shifted[u-30]:normal[u-30];}if(u==39)return shift?')':'0';if(u==40)return '\n';if(u==42)return '\b';if(u==43)return '\t';if(u==44)return ' ';if(u==45)return shift?'_':'-';if(u==46)return shift?'+':'=';if(u==47)return shift?'{':'[';if(u==48)return shift?'}':']';if(u==49)return shift?'|':'\\';if(u==51)return shift?':':';';if(u==52)return shift?'\"':'\'';if(u==53)return shift?'~':'`';if(u==54)return shift?'<':',';if(u==55)return shift?'>':'.';if(u==56)return shift?'?':'/';return '\0';}
}
