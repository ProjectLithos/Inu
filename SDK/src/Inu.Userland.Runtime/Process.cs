using System;
using System.Text;

namespace Inu.Userland.Runtime;

/// <summary><inu.api>High-level process-launch helpers for ordinary ring-3 applications. The native process syscall transport is hidden behind this surface.</inu.api></summary>
public static unsafe class Process
{
    /// <summary><inu.api>Attempts to start an executable with raw argument text. A bare executable name is resolved against the configured CommandsPath(s); an absolute path is used directly.</inu.api></summary>
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

    /// <summary><inu.api>Returns the process arguments as an ordinary C# string array. Double quotes group whitespace into one argument and are removed from the resulting values.</inu.api></summary>
    public static String[] GetArguments()
    {
        String raw=GetRawArguments();if(String.IsNullOrWhiteSpace(raw))return Array.Empty<String>();
        Int32 count=CountArguments(raw);String[] args=new String[count];Int32 index=0,argument=0;
        while(index<raw.Length&&argument<count)
        {
            while(index<raw.Length&&IsSpace(raw[index]))index++;if(index>=raw.Length)break;
            StringBuilder value=new StringBuilder();Boolean quoted=false;
            while(index<raw.Length)
            {
                Char c=raw[index++];
                if(c=='"'){quoted=!quoted;continue;}
                if(!quoted&&IsSpace(c))break;
                value.Append(c);
            }
            args[argument++]=value.ToString();
        }
        return args;
    }

    private static Int32 CountArguments(String raw)
    {
        Int32 count=0,index=0;while(index<raw.Length){while(index<raw.Length&&IsSpace(raw[index]))index++;if(index>=raw.Length)break;count++;Boolean quoted=false;while(index<raw.Length){Char c=raw[index++];if(c=='"'){quoted=!quoted;continue;}if(!quoted&&IsSpace(c))break;}}return count;
    }

    private static Boolean IsSpace(Char value)=>value==' '||value=='\t';
}
