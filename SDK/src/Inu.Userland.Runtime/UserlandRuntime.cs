using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;

namespace Inu.Userland.Runtime;

public enum UserlandOperation : Byte { Get=0, Set=1, Event=2 }
public static class UserlandError { public const Int64 NotPermitted=-1L,NotFound=-2L,Fault=-14L,Busy=-16L,InvalidArgument=-22L,NotImplemented=-38L; }

public unsafe struct UserlandMessage
{
    public const UInt64 SerializedBytes=144UL;
    public UInt64 Version,ByteSize,AppNameAddress,AppNameLength,ProcessId,MessageAddress,MessageLength,DataAddress,DataLength,OutputAddress,OutputCapacity,CorrelationId,Flags,Value0,Value1,Value2,Value3,Capability;
}

internal static unsafe class Native
{
#pragma warning disable CS0626
    [MethodImpl(MethodImplOptions.InternalCall)][RuntimeImport("*","InuUserSyscall")]
    internal static extern Int64 Syscall(UInt64 operation,UInt64 envelope);
    [MethodImpl(MethodImplOptions.InternalCall)][RuntimeImport("*","InuUserPause")]
    internal static extern void Pause();
#pragma warning restore CS0626
}

public static unsafe class UserlandSystem
{
    private static UInt64 _pid;
    private const String AppName="userland";

    public static Int64 Call(UserlandOperation operation,String message,Byte* data,UInt64 dataLength,Byte* output,UInt64 outputCapacity,UInt64 value0=0UL,UInt64 value1=0UL,UInt64 value2=0UL,UInt64 value3=0UL)
    {
        if(message==null||message.Length==0||message.Length>96)return UserlandError.InvalidArgument;
        // Only the identity query may use PID zero. Bootstrap once before any
        // ordinary call so commands and custom entry points carry their real PID.
        if(_pid==0UL&&message!="process.id.current"&&ProcessId()==0UL)return UserlandError.NotPermitted;
        Byte* app=stackalloc Byte[64];Byte* msg=stackalloc Byte[96];
        UInt64 an=CopyAscii(AppName,app,64U),mn=CopyAscii(message,msg,96U);if(an==0UL||mn==0UL)return UserlandError.InvalidArgument;
        UserlandMessage envelope=default;envelope.Version=1UL;envelope.ByteSize=UserlandMessage.SerializedBytes;envelope.AppNameAddress=(UInt64)(nuint)app;envelope.AppNameLength=an;envelope.ProcessId=_pid;envelope.MessageAddress=(UInt64)(nuint)msg;envelope.MessageLength=mn;envelope.DataAddress=(UInt64)(nuint)data;envelope.DataLength=dataLength;envelope.OutputAddress=(UInt64)(nuint)output;envelope.OutputCapacity=outputCapacity;envelope.Value0=value0;envelope.Value1=value1;envelope.Value2=value2;envelope.Value3=value3;
        Int64 result=Native.Syscall((UInt64)(Byte)operation,(UInt64)(nuint)(&envelope));
        if(_pid==0UL&&message=="process.id.current"&&result>0L)_pid=(UInt64)result;
        return result;
    }

    public static UInt64 ProcessId(){if(_pid!=0UL)return _pid;Int64 value=Call(UserlandOperation.Get,"process.id.current",null,0UL,null,0UL);if(value>0L)_pid=(UInt64)value;return _pid;}
    public static void Pause()=>Native.Pause();
    internal static UInt64 CopyAscii(String text,Byte* destination,UInt32 capacity){if(text==null||destination==null||(UInt32)text.Length>capacity)return 0UL;for(Int32 i=0;i<text.Length;i++){Char c=text[i];destination[i]=(Byte)(c<=255?c:'?');}return (UInt64)text.Length;}
}

/// <summary><inu.api>Coder-facing text output for ordinary ring-3 applications.</inu.api> Output is delivered through the Inu Event syscall boundary; it does not expose kernel console implementation code.</summary>
public static class Output
{
    /// <summary><inu.api>Writes text without appending a line terminator.</inu.api></summary>
    public static Boolean Write(String text)=>UserlandConsole.Write(text);

    /// <summary><inu.api>Writes text followed by a line terminator.</inu.api></summary>
    public static Boolean WriteLine(String text)=>UserlandConsole.WriteLine(text);

    /// <summary><inu.api>Requests that the current text console be cleared.</inu.api></summary>
    public static Boolean Clear()=>UserlandConsole.Clear();
}

public static unsafe class UserlandConsole
{
    public static Boolean Write(String text){if(text==null)return false;Byte* b=stackalloc Byte[256];Int32 offset=0;while(offset<text.Length){UInt32 n=(UInt32)(text.Length-offset);if(n>256U)n=256U;for(UInt32 i=0;i<n;i++){Char c=text[offset+(Int32)i];b[i]=(Byte)(c<=255?c:'?');}Int64 r=UserlandSystem.Call(UserlandOperation.Event,"console.output",b,n,null,0UL);if(r<0L)return false;offset+=(Int32)n;}return true;}
    public static Boolean WriteLine(String text){return Write(text)&&Write("\n");}
    internal static Boolean WriteChar(Char value)
    {
        Byte* one=stackalloc Byte[1];one[0]=(Byte)(value<=255?value:'?');
        return UserlandSystem.Call(UserlandOperation.Event,"console.output",one,1UL,null,0UL)>=0L;
    }
    internal static Boolean WriteUnsigned(UInt64 value)
    {
        Byte* digits=stackalloc Byte[20];UInt32 length=0U;
        do{digits[length++]=(Byte)('0'+(value%10UL));value/=10UL;}while(value!=0UL);
        Byte* output=stackalloc Byte[20];for(UInt32 i=0U;i<length;i++)output[i]=digits[length-1U-i];
        return UserlandSystem.Call(UserlandOperation.Event,"console.output",output,length,null,0UL)>=0L;
    }
    internal static Boolean WriteSigned(Int64 value)
    {
        if(value>=0L)return WriteUnsigned((UInt64)value);
        if(!Write("-"))return false;
        UInt64 magnitude=(UInt64)(-(value+1L))+1UL;
        return WriteUnsigned(magnitude);
    }
    /// <summary>Waits for one decoded character through the kernel's interrupt-driven input service.</summary>
    internal static Int32 ReadChar(){Int64 value=UserlandSystem.Call(UserlandOperation.Get,"console.input",null,0UL,null,0UL);return value>=0L?(Int32)value:-1;}

    internal static String ReadLine()
    {
        StringBuilder line=new StringBuilder();
        for(;;)
        {
            Int32 value=ReadChar();
            if(value<0)throw new InvalidOperationException();
            Char c=(Char)value;
            if(c=='\r'||c=='\n'){Write("\n");return line.ToString();}
            if(c=='\b')
            {
                if(line.Length!=0){line.Length=line.Length-1;Write("\b \b");}
                continue;
            }
            if(c<32||c==127)continue;
            line.Append(c);
            WriteChar(c);
        }
    }

    public static Int32 ReadLineAscii(Byte* buffer,UInt32 capacity)
    {
        if(buffer==null||capacity<2U)return -1;
        UInt32 length=0U;
        Byte* one=stackalloc Byte[1];
        for(;;)
        {
            Int32 value=ReadChar();
            if(value<0)return -1;
            Byte c=(Byte)value;
            if(c=='\r'||c=='\n'){Write("\n");return (Int32)length;}
            if(c=='\b'){if(length!=0U){length--;Write("\b \b");}continue;}
            if(c<32U||c>126U)continue;
            if(length+1U>=capacity)continue;
            buffer[length++]=c;
            one[0]=c;
            UserlandSystem.Call(UserlandOperation.Event,"console.output",one,1UL,null,0UL);
        }
    }
    public static Boolean Clear()=>UserlandSystem.Call(UserlandOperation.Event,"console.clear",null,0UL,null,0UL)>=0L;
}

internal static unsafe class UserlandThreading
{
    internal static UInt64 MonotonicNanoseconds()
    {
        Int64 value = UserlandSystem.Call(UserlandOperation.Get, "time.monotonic", null, 0UL, null, 0UL);
        return value > 0L ? unchecked((UInt64)value) : 0UL;
    }

    internal static Boolean Yield()
    {
        Int64 result = UserlandSystem.Call(UserlandOperation.Event, "scheduler.yield", null, 0UL, null, 0UL);
        UserlandSystem.Pause();
        return result >= 0L;
    }
}

public static unsafe class UserlandProcess
{
    public const UInt64 StandardInput=0UL;
    public const UInt64 StandardOutput=1UL;
    public const UInt64 StandardError=2UL;
    public static Int64 SpawnAscii(Byte* path,UInt32 pathLength,Byte* arguments,UInt32 argumentLength,Byte* environment,UInt32 environmentLength)
    {
        if(path==null||pathLength==0U||pathLength>1024U||argumentLength>2048U||environmentLength>2048U)return UserlandError.InvalidArgument;
        UInt32 total=pathLength+1U+argumentLength+1U+environmentLength;Byte* payload=stackalloc Byte[(Int32)total];UInt32 o=0U;for(UInt32 i=0;i<pathLength;i++)payload[o++]=path[i];payload[o++]=0;for(UInt32 i=0;i<argumentLength;i++)payload[o++]=arguments[i];payload[o++]=0;for(UInt32 i=0;i<environmentLength;i++)payload[o++]=environment[i];
        return UserlandSystem.Call(UserlandOperation.Event,"process.spawn",payload,total,null,0UL);
    }
    public static Int64 Wait(UInt64 processId)=>UserlandSystem.Call(UserlandOperation.Get,"process.wait",null,0UL,null,0UL,processId);
    public static Int32 ReadArgumentsAscii(Byte* output,UInt32 capacity){Int64 r=UserlandSystem.Call(UserlandOperation.Get,"process.arguments",null,0UL,output,capacity);return r<0L?-1:(Int32)r;}
    public static Int32 ReadEnvironmentAscii(Byte* output,UInt32 capacity){Int64 r=UserlandSystem.Call(UserlandOperation.Get,"process.environment",null,0UL,output,capacity);return r<0L?-1:(Int32)r;}
    public static void Exit(Int32 code){UserlandSystem.Call(UserlandOperation.Event,"process.exit",null,0UL,null,0UL,unchecked((UInt64)(Int64)code));for(;;)UserlandSystem.Pause();}
}


public static unsafe class UserlandArguments
{
    /// <summary>Copies the raw command-line argument text supplied by the shell.</summary>
    public static Int32 ReadRaw(Byte* output,UInt32 capacity)=>UserlandProcess.ReadArgumentsAscii(output,capacity);

    /// <summary>Copies the raw environment block supplied by the parent process.</summary>
    public static Int32 ReadEnvironmentRaw(Byte* output,UInt32 capacity)=>UserlandProcess.ReadEnvironmentAscii(output,capacity);
}

public static unsafe class UserlandFile
{
    public static Int64 OpenAscii(Byte* path,UInt32 pathLength,UInt32 access)=>UserlandSystem.Call(UserlandOperation.Get,"file.open",path,pathLength,null,0UL,access);
    public static Int64 Read(UInt64 handle,Byte* output,UInt32 count)=>UserlandSystem.Call(UserlandOperation.Get,"file.read",null,0UL,output,count,handle,count);
    public static Int64 Write(UInt64 handle,Byte* input,UInt32 count)=>UserlandSystem.Call(UserlandOperation.Set,"file.write",input,count,null,0UL,handle,count);
    public static Int64 CreateAscii(Byte* path,UInt32 pathLength,Boolean overwrite)=>UserlandSystem.Call(UserlandOperation.Set,"file.create",path,pathLength,null,0UL,overwrite?1UL:0UL);
    public static Int64 DeleteAscii(Byte* path,UInt32 pathLength)=>UserlandSystem.Call(UserlandOperation.Set,"file.delete",path,pathLength,null,0UL);
    public static Int64 Close(UInt64 handle)=>UserlandSystem.Call(UserlandOperation.Event,"file.close",null,0UL,null,0UL,handle);
}
