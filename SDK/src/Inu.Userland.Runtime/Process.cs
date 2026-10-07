using System;
using System.Text;

namespace Inu.Userland.Runtime;

/// <summary><inu.api>High-level process-launch helpers for ordinary ring-3 applications. The native process syscall transport is hidden behind this surface.</inu.api></summary>
public static unsafe class Process
{
    /// <summary><inu.api>Attempts to start an executable with raw argument text.</inu.api></summary>
    public static Boolean TryStart(String path,String arguments)
    {
        if(path==null||path.Length==0||path.Length>1024)return false;if(arguments==null)arguments=String.Empty;if(arguments.Length>2048)return false;
        Byte* p=stackalloc Byte[path.Length];Byte* a=stackalloc Byte[arguments.Length==0?1:arguments.Length];
        for(Int32 i=0;i<path.Length;i++){Char c=path[i];if(c>0x7F)return false;p[i]=(Byte)c;}
        for(Int32 i=0;i<arguments.Length;i++){Char c=arguments[i];if(c>0x7F)return false;a[i]=(Byte)c;}
        return UserlandProcess.SpawnAscii(p,(UInt32)path.Length,a,(UInt32)arguments.Length,null,0U)>0L;
    }
}

/// <summary><inu.api>High-level command-line information supplied to the current process.</inu.api></summary>
public static unsafe class CommandLine
{
    /// <summary><inu.api>Returns the raw argument text supplied by the parent process.</inu.api></summary>
    public static String GetRawArguments()
    {
        Byte* bytes=stackalloc Byte[2048];Int32 length=UserlandArguments.ReadRaw(bytes,2048U);if(length<=0)return String.Empty;
        StringBuilder result=new StringBuilder(length);for(Int32 i=0;i<length;i++)result.Append((Char)bytes[i]);return result.ToString();
    }
}
