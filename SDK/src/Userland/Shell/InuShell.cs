using System;
using Inu.Userland.Runtime;

namespace Inu.Userland.Shell;

/// <summary>
/// Ring-3 shell runtime. The shell contains no command registry: it searches executable paths
/// and asks the kernel process runtime to launch whatever compatible executable is present.
/// </summary>
public static unsafe class InuShell
{
    private const UInt32 MaximumLineBytes = 1024U;
    private const UInt32 MaximumPathBytes = 1536U;
    /// <summary>Runs the ordinary ring-3 shell process.</summary>
    public static Int32 Run(String prompt)
    {
        if(prompt==null)return 7;
        Byte* line=stackalloc Byte[(Int32)MaximumLineBytes];
        Byte* path=stackalloc Byte[(Int32)MaximumPathBytes];
        if(UserlandSystem.ProcessId()==0UL)return 1;
        for(;;)
        {
            if(!UserlandConsole.Write(prompt))return 2;
            Int32 length=UserlandConsole.ReadLineAscii(line,MaximumLineBytes);if(length<0)return 3;
            UInt32 start=0U,end=(UInt32)length;while(start<end&&line[start]==' ')start++;while(end>start&&line[end-1U]==' ')end--;
            if(start==end)continue;
            UInt32 split=start;while(split<end&&line[split]!=' ')split++;
            UInt32 commandLength=split-start;UInt32 argumentStart=split;while(argumentStart<end&&line[argumentStart]==' ')argumentStart++;
            UInt32 argumentLength=end-argumentStart;

            Int64 result;
            if(line[start]=='/')
            {
                result=UserlandProcess.SpawnAscii(line+start,commandLength,line+argumentStart,argumentLength,null,0U);
            }
            else
            {
                UInt32 pathLength=BuildCommandPath(path,MaximumPathBytes,line+start,commandLength,true);
                if(pathLength==0U)return 4;
                result=UserlandProcess.SpawnAscii(path,pathLength,line+argumentStart,argumentLength,null,0U);
                if(result==UserlandError.NotFound)
                {
                    pathLength=BuildCommandPath(path,MaximumPathBytes,line+start,commandLength,false);
                    result=pathLength==0U?UserlandError.InvalidArgument:UserlandProcess.SpawnAscii(path,pathLength,line+argumentStart,argumentLength,null,0U);
                }
            }
            // A successful spawn transfers control back to the kernel supervisor and therefore
            // does not return to this shell instance. Only failures are observed here.
            if(result==UserlandError.NotFound){if(!UserlandConsole.WriteLine("Command not found."))return 5;continue;}
            if(result<0L){if(!UserlandConsole.WriteLine("Could not launch executable."))return 6;continue;}
        }
    }

    private static UInt32 BuildCommandPath(Byte* destination,UInt32 capacity,Byte* command,UInt32 commandLength,Boolean appendExe)
    {
        const String root="/System/Commands/";UInt32 required=(UInt32)root.Length+commandLength+(appendExe?4U:0U);if(destination==null||command==null||required>=capacity)return 0U;
        UInt32 o=0U;for(Int32 i=0;i<root.Length;i++)destination[o++]=(Byte)root[i];for(UInt32 i=0U;i<commandLength;i++)destination[o++]=command[i];
        if(appendExe){destination[o++]=(Byte)'.';destination[o++]=(Byte)'E';destination[o++]=(Byte)'X';destination[o++]=(Byte)'E';}
        return o;
    }
}
