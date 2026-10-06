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
        Byte* commandRoot=stackalloc Byte[(Int32)MaximumPathBytes];
        if(UserlandSystem.ProcessId()==0UL)return 1;
        Byte pathSeparator=GetPathSeparator();if(pathSeparator==0U)return 8;
        UInt32 commandRootLength=GetCommandsPath(commandRoot,MaximumPathBytes);if(commandRootLength==0U)return 9;
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
            if(line[start]==pathSeparator)
            {
                result=UserlandProcess.SpawnAscii(line+start,commandLength,line+argumentStart,argumentLength,null,0U);
            }
            else
            {
                UInt32 pathLength=BuildCommandPath(path,MaximumPathBytes,commandRoot,commandRootLength,pathSeparator,line+start,commandLength,true);
                if(pathLength==0U)return 4;
                result=UserlandProcess.SpawnAscii(path,pathLength,line+argumentStart,argumentLength,null,0U);
                if(result==UserlandError.NotFound)
                {
                    pathLength=BuildCommandPath(path,MaximumPathBytes,commandRoot,commandRootLength,pathSeparator,line+start,commandLength,false);
                    result=pathLength==0U?UserlandError.InvalidArgument:UserlandProcess.SpawnAscii(path,pathLength,line+argumentStart,argumentLength,null,0U);
                }
            }
            // A successful spawn transfers control back to the kernel supervisor and therefore
            // does not return to this shell instance. Only failures are observed here.
            if(result==UserlandError.NotFound){if(!UserlandConsole.WriteLine("Command not found."))return 5;continue;}
            if(result<0L){if(!UserlandConsole.WriteLine("Could not launch executable."))return 6;continue;}
        }
    }

    private static Byte GetPathSeparator()
    {
        Byte* current=stackalloc Byte[(Int32)MaximumPathBytes];
        Int64 length=UserlandSystem.Call(UserlandOperation.Get,"process.current-directory",null,0UL,current,MaximumPathBytes);
        return length>0L?current[0]:(Byte)0;
    }

    private static UInt32 GetCommandsPath(Byte* destination,UInt32 capacity)
    {
        // FileSystemLogicalPath.Commands has the stable ABI id 7.
        Int64 length=UserlandSystem.Call(UserlandOperation.Get,"filesystem.logical-path",null,0UL,destination,capacity,7UL);
        return length>0L?(UInt32)length:0U;
    }

    private static UInt32 BuildCommandPath(Byte* destination,UInt32 capacity,Byte* root,UInt32 rootLength,Byte separator,Byte* command,UInt32 commandLength,Boolean appendExe)
    {
        UInt32 required=rootLength+1U+commandLength+(appendExe?4U:0U);if(destination==null||root==null||command==null||rootLength==0U||separator==0U||required>=capacity)return 0U;
        UInt32 o=0U;for(UInt32 i=0U;i<rootLength;i++)destination[o++]=root[i];destination[o++]=separator;for(UInt32 i=0U;i<commandLength;i++)destination[o++]=command[i];
        if(appendExe){destination[o++]=(Byte)'.';destination[o++]=(Byte)'E';destination[o++]=(Byte)'X';destination[o++]=(Byte)'E';}
        return o;
    }
}
