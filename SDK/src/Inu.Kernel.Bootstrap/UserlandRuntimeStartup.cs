using System;
using System.Text;
using Inu.Kernel.Console;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Processes;
using Inu.Kernel.Storage;
#if INU_KERNELAREA_DRIVERS
using Inu.Kernel.Drivers;
#endif
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>
/// Connects ordinary ring-3 userland to the kernel without embedding shell commands in the kernel.
/// The shell and commands are normal PE/ELF executables. The kernel exposes only generic process,
/// console and file services through Get/Set/Event.
/// </summary>
public static unsafe class UserlandRuntimeStartup
{
    private const UInt32 InputCapacity=4096U;
    private const UInt32 PathCapacity=1536U;
    private const UInt32 ArgumentCapacity=2048U;
    private const UInt32 EnvironmentCapacity=2048U;
    private const UInt32 IoChunkCapacity=4096U;

    private unsafe struct RuntimeState
    {
        internal fixed Byte Input[(Int32)InputCapacity];
        internal fixed Byte PendingArguments[(Int32)ArgumentCapacity];
        internal fixed Byte PendingEnvironment[(Int32)EnvironmentCapacity];
        internal fixed Byte CurrentArguments[(Int32)ArgumentCapacity];
        internal fixed Byte CurrentEnvironment[(Int32)EnvironmentCapacity];
    }

#pragma warning disable CS0169
    private static RuntimeState _state;
#pragma warning restore CS0169
    private static UInt32 _inputRead,_inputWrite;
    private static UInt32 _pendingArgumentLength,_pendingEnvironmentLength;
    private static UInt32 _currentArgumentLength,_currentEnvironmentLength;
    private static UInt64 _pendingProcessId;
    private static Boolean _initialized;
    private static UInt64 _shellProcessId;
    private static Boolean _shellOutputWritten,_interactiveReadyPublished;

    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot:ISystemAssetBundleContext
    {
        if(_initialized)return true;
        if(!SystemAssetCatalog.Initialize(boot))return false;
        if(!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ConsoleInput,&ConsoleInputGet)||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.ConsoleOutput,&ConsoleOutputEvent)||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.ConsoleClear,&ConsoleClearEvent)||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.ProcessSpawn,&ProcessSpawnEvent)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ProcessWait,&ProcessWaitGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ProcessArguments,&ProcessArgumentsGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ProcessEnvironment,&ProcessEnvironmentGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ProcessCurrentDirectory,&ProcessCurrentDirectoryGet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.ProcessCurrentDirectory,&ProcessCurrentDirectorySet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.FileOpen,&FileOpenGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.FileRead,&FileReadGet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.FileWrite,&FileWriteSet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.FileCreate,&FileCreateSet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.FileDelete,&FileDeleteSet)||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.FileClose,&FileCloseEvent)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.DirectoryOpen,&DirectoryOpenGet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.DirectoryCreate,&DirectoryCreateSet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.DirectoryDelete,&DirectoryDeleteSet)||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.DirectoryClose,&DirectoryCloseEvent)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.DirectoryRead,&DirectoryReadGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.FileSystemLogicalPath,&FileSystemLogicalPathGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.FileSystemPathSeparator,&FileSystemPathSeparatorGet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.FileSystemPathSeparator,&FileSystemPathSeparatorSet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.FileSystemCommandsPathCount,&FileSystemCommandsPathCountGet)||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.FileSystemCommandsPath,&FileSystemCommandsPathGet)||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.FileSystemCommandsPath,&FileSystemCommandsPathSet))return false;
        if(!KernelSystemCalls.RegisterGet("system.device.inspect",&DeviceInspectGet))return false;
        _initialized=true;return true;
    }

    /// <summary>Queues one decoded character for the foreground ring-3 terminal process.</summary>
    public static Boolean QueueCharacter(Char character)
    {
        if(!_initialized||character==0)return true;
        Byte value=(Byte)(character<=255?character:'?');
        UInt32 next=(_inputWrite+1U)%InputCapacity;
        if(next==_inputRead)return false;
        fixed(Byte* input=_state.Input)input[_inputWrite]=value;
        _inputWrite=next;return true;
    }

    /// <summary>Cancels the current foreground ring-3 program. The shell is relaunched by the supervisor.</summary>
    public static Boolean HandleControlC()=>KernelProcesses.RequestForegroundCommandCancellation();

    /// <summary>
    /// Runs the shell as an ordinary ring-3 process. Spawn requests are created by the generic
    /// process.spawn syscall; the supervisor runs the requested child and then starts a fresh shell.
    /// No command names or command registry exist in this code.
    /// </summary>
    public static Boolean RunShellSession()
    {
        if(!_initialized)return false;
        Byte* shellPath=stackalloc Byte[23];
        UInt32 shellLength=CopyLiteral("/SYSTEM/SHELL/SHELL.EXE",shellPath,23U);
        if(shellLength==0U)return false;

        // Load the interactive shell once and keep its address space resident. A command spawn
        // still leaves ring 3 so the child can run in its own isolated process, but returning to
        // the prompt no longer reparses and remaps SHELL.EXE after every command.
        if(!TryCreateExecutable(shellPath,shellLength,KernelProcessOwnership.Foreground,out KernelProcessInfo shell))
        {
            KernelConsole.WriteHostControl("SHELL_CREATE_FAIL");
            return false;
        }
        _shellProcessId=shell.Id;

        for(;;)
        {
            SetCurrentContext(null,0U,null,0U);
            _shellOutputWritten=false;
            if(!KernelProcesses.TryStart(shell.Id,0UL))
            {
                KernelConsole.WriteHostControl("SHELL_START_FAIL");
                KernelProcesses.TryTerminate(shell.Id,-1L);
                return false;
            }

            UInt64 childId=_pendingProcessId;
            if(childId==0UL)
            {
                // A microkernel shell may return cleanly to its supervisor without having queued
                // a child. Keep the already-loaded shell resident and resume it instead of treating
                // that hand-off as a fatal kernel return. Faulted/non-completed shells still fail.
                if(!KernelProcesses.TryGetProcess(shell.Id,out KernelProcessInfo shellDone) ||
                   shellDone.State!=KernelProcessState.Completed ||
                   !KernelProcessRecordStore.ReactivateCompleted(shell.Id))
                {
                    KernelProcesses.TryTerminate(shell.Id,-1L);
                    return false;
                }
                continue;
            }

            _pendingProcessId=0UL;
            PromotePendingContext();
            if(!KernelProcesses.TryStart(childId,0UL))
            {
                KernelProcesses.TryTerminate(childId,-1L);
                ClearCurrentContext();
                if(!KernelProcessRecordStore.ReactivateCompleted(shell.Id)){KernelProcesses.TryTerminate(shell.Id,-1L);return false;}
                continue;
            }
            if(KernelProcesses.TryGetProcess(childId,out KernelProcessInfo childDone))
                KernelProcesses.TryTerminate(childId,childDone.ExitCode);
            ClearCurrentContext();
            if(!KernelProcessRecordStore.ReactivateCompleted(shell.Id)){KernelProcesses.TryTerminate(shell.Id,-1L);return false;}
        }
    }

    // Get(system.device.inspect), Value0=index, output=8 UInt64 values (64 bytes).
    // Returns 64, NotFound at the end, or NotImplemented when drivers are unselected.
    private static Int64 DeviceInspectGet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.Value0>0xFFFFFFFFUL||frame->NativeMessage.OutputCapacity<64UL)return (Int64)KernelSystemCallError.InvalidArgument;
#if INU_KERNELAREA_DRIVERS
        if(!KernelDrivers.GetCapabilities().Initialized)return (Int64)KernelSystemCallError.NotImplemented;
        if(!KernelDrivers.TryGetDeviceNodeByIndex((UInt32)frame->NativeMessage.Value0,out KernelDeviceNode node))return (Int64)KernelSystemCallError.NotFound;
        UInt64* record=stackalloc UInt64[8];
        record[0]=(UInt64)(Byte)node.Identifier.Bus;record[1]=node.Identifier.VendorId;record[2]=node.Identifier.DeviceId;record[3]=node.Identifier.ClassCode;
        record[4]=(UInt64)(Byte)node.State;record[5]=node.Driver.Value;record[6]=(UInt64)(UInt32)node.Failure;record[7]=node.Handle.Value;
        return KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)record,64UL)?64L:(Int64)KernelSystemCallError.Fault;
#else
        return (Int64)KernelSystemCallError.NotImplemented;
#endif
    }

    private static Int64 ConsoleInputGet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        // A live shell has presented output and reached its input service. Empty input
        // is normal here; acceptance must not wait for the user to press a key.
        if(!_interactiveReadyPublished&&_shellOutputWritten&&
           KernelProcesses.TryGetCurrentProcessId(out UInt64 processId)&&processId==_shellProcessId)
        {
            if(!KernelConsole.WriteHostControl("INTERACTIVE_READY"))return (Int64)KernelSystemCallError.Fault;
            _interactiveReadyPublished=true;
        }
        // SYSCALL enters with IF clear. Check the queue before STI;HLT, then
        // mask interrupts again before inspecting it. IRQ handlers queue input;
        // unrelated interrupts only wake this wait and never complete the read.
        for(;;)
        {
            if(KernelProcesses.IsForegroundCommandCancellationRequested())
                return KernelProcesses.RequestCurrentProcessExit(KernelProcesses.ForegroundCancellationExitCode,true)?
                    0L:(Int64)KernelSystemCallError.Fault;
            if(_inputRead!=_inputWrite)break;
            Boolean woke=Native.WaitForInterrupt();
            Boolean masked=Native.DisableInterrupts();
            if(!woke||!masked)return (Int64)KernelSystemCallError.Fault;
        }
        Byte value;fixed(Byte* input=_state.Input)value=input[_inputRead];
        _inputRead=(_inputRead+1U)%InputCapacity;return value;
    }

    private static Int64 ConsoleOutputEvent(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.DataLength>KernelSystemCallMessage.MaximumPayloadBytes)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 remaining=frame->NativeMessage.DataLength,address=frame->NativeMessage.DataAddress;
        Byte* buffer=stackalloc Byte[(Int32)IoChunkCapacity];
        while(remaining!=0UL)
        {
            UInt32 count=(UInt32)(remaining>IoChunkCapacity?IoChunkCapacity:remaining);
            if(!KernelSystemCalls.TryCopyFromUser(address,(UInt64)(nuint)buffer,count))return (Int64)KernelSystemCallError.Fault;
            if(!KernelConsole.WriteAscii(buffer,count))return (Int64)KernelSystemCallError.Fault;
            address+=count;remaining-=count;
        }
        if(frame->NativeMessage.DataLength!=0UL&&
           KernelProcesses.TryGetCurrentProcessId(out UInt64 processId)&&processId==_shellProcessId)
            _shellOutputWritten=true;
        return 0L;
    }

    private static Int64 ConsoleClearEvent(KernelSystemCallFrame* frame)=>KernelConsole.ClearScreen()?0L:(Int64)KernelSystemCallError.Fault;

    private static Int64 ProcessSpawnEvent(KernelSystemCallFrame* frame)
    {
        if(frame==null||_pendingProcessId!=0UL)return (Int64)KernelSystemCallError.Busy;
        UInt64 bytes=frame->NativeMessage.DataLength;
        if(bytes<3UL||bytes>(UInt64)(PathCapacity+ArgumentCapacity+EnvironmentCapacity+2U))return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* payload=stackalloc Byte[(Int32)bytes];
        if(!KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,(UInt64)(nuint)payload,bytes))return (Int64)KernelSystemCallError.Fault;

        UInt32 total=(UInt32)bytes,pathLength=0U;
        while(pathLength<total&&payload[pathLength]!=0)pathLength++;
        if(pathLength==0U||pathLength>=total||pathLength>PathCapacity)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt32 argumentStart=pathLength+1U,argumentLength=0U;
        while(argumentStart+argumentLength<total&&payload[argumentStart+argumentLength]!=0)argumentLength++;
        if(argumentStart+argumentLength>=total||argumentLength>ArgumentCapacity)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt32 environmentStart=argumentStart+argumentLength+1U;
        UInt32 environmentLength=total-environmentStart;
        if(environmentLength>EnvironmentCapacity)return (Int64)KernelSystemCallError.InvalidArgument;

        if(!TryCreateExecutable(payload,pathLength,KernelProcessOwnership.Foreground,out KernelProcessInfo process))
            return (Int64)KernelSystemCallError.NotFound;
        if(KernelProcesses.TryGetCurrentProcessId(out UInt64 parentProcessId)&&parentProcessId!=0UL&&
           !KernelProcessRecordStore.TryCopyCurrentDirectory(parentProcessId,process.Id))
        {
            KernelProcesses.TryTerminate(process.Id,-1L);
            return (Int64)KernelSystemCallError.Fault;
        }

        fixed(Byte* args=_state.PendingArguments,env=_state.PendingEnvironment)
        {
            CopyBytes(payload+argumentStart,args,argumentLength);
            CopyBytes(payload+environmentStart,env,environmentLength);
        }
        _pendingArgumentLength=argumentLength;_pendingEnvironmentLength=environmentLength;_pendingProcessId=process.Id;

        // Return from the currently running shell to the kernel supervisor. The child is started
        // only after the shell's address space has been restored to the kernel.
        if(!KernelProcesses.RequestCurrentProcessExit(0L,false))
        {
            _pendingProcessId=0UL;KernelProcesses.TryTerminate(process.Id,-1L);
            return (Int64)KernelSystemCallError.Fault;
        }
        return unchecked((Int64)process.Id);
    }

    private static Int64 ProcessWaitGet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.Value0==0UL)return (Int64)KernelSystemCallError.InvalidArgument;
        if(!KernelProcesses.TryGetProcess(frame->NativeMessage.Value0,out KernelProcessInfo process))return (Int64)KernelSystemCallError.NotFound;
        return process.State==KernelProcessState.Ready||process.State==KernelProcessState.Running||process.State==KernelProcessState.Loading?
            (Int64)KernelSystemCallError.Busy:process.ExitCode;
    }

    private static Int64 ProcessArgumentsGet(KernelSystemCallFrame* frame)=>CopyContextToUser(frame,true);
    private static Int64 ProcessEnvironmentGet(KernelSystemCallFrame* frame)=>CopyContextToUser(frame,false);



    private static Int64 FileSystemPathSeparatorGet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        return (Int64)(UInt16)FileSystem.GetPathSeparator();
    }

    private static Int64 FileSystemPathSeparatorSet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.Value0==0UL||frame->NativeMessage.Value0>0x7FUL)return (Int64)KernelSystemCallError.InvalidArgument;
        return FileSystem.SetPathSeparator((Char)frame->NativeMessage.Value0)?0L:(Int64)KernelSystemCallError.InvalidArgument;
    }

    private static Int64 FileSystemCommandsPathCountGet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        return (Int64)FileSystemLogicalPaths.CommandCount;
    }

    private static Int64 FileSystemCommandsPathGet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.OutputCapacity==0UL)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 requested=frame->NativeMessage.Value0;if(requested>UInt32.MaxValue)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];
        if(!FileSystemLogicalPaths.TryGetCommandExternalAscii((UInt32)requested,path,PathCapacity,out UInt32 length))return (Int64)KernelSystemCallError.NotFound;
        if(frame->NativeMessage.OutputCapacity<length)return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)path,length)?(Int64)length:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 FileSystemCommandsPathSet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.DataLength==0UL||frame->NativeMessage.DataLength>(UInt64)(PathCapacity*FileSystemLogicalPaths.MaximumCommandPaths))return (Int64)KernelSystemCallError.InvalidArgument;
        UInt32 bytes=(UInt32)frame->NativeMessage.DataLength;Byte* payload=stackalloc Byte[(Int32)bytes];
        if(!KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,(UInt64)(nuint)payload,bytes))return (Int64)KernelSystemCallError.Fault;
        String[] paths=new String[(Int32)FileSystemLogicalPaths.MaximumCommandPaths];UInt32 count=0U,start=0U;
        for(UInt32 i=0U;i<=bytes;i++)
        {
            if(i!=bytes&&payload[i]!=0U)continue;
            UInt32 length=i-start;if(length==0U||count>=FileSystemLogicalPaths.MaximumCommandPaths)return (Int64)KernelSystemCallError.InvalidArgument;
            StringBuilder path=new StringBuilder((Int32)length);
            for(UInt32 j=0U;j<length;j++){Byte b=payload[start+j];if(b==0U||b>0x7FU)return (Int64)KernelSystemCallError.InvalidArgument;path.Append((Char)b);}
            paths[(Int32)count++]=path.ToString();start=i+1U;
        }
        String[] exact=new String[(Int32)count];for(UInt32 i=0U;i<count;i++)exact[(Int32)i]=paths[(Int32)i];
        return FileSystem.SetCommandsPaths(exact)?0L:(Int64)KernelSystemCallError.InvalidArgument;
    }
    private static Int64 FileSystemLogicalPathGet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.OutputCapacity==0UL||frame->NativeMessage.Value0==0UL||frame->NativeMessage.Value0>10UL)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];
        if(!FileSystemLogicalPaths.TryGetExternalAscii((FileSystemLogicalPath)frame->NativeMessage.Value0,path,PathCapacity,out UInt32 length))return (Int64)KernelSystemCallError.NotFound;
        if(frame->NativeMessage.OutputCapacity<length)return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)path,length)?(Int64)length:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 ProcessCurrentDirectoryGet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.OutputCapacity==0UL)return (Int64)KernelSystemCallError.InvalidArgument;
        if(!KernelProcesses.TryGetCurrentProcessId(out UInt64 processId)||processId==0UL)return (Int64)KernelSystemCallError.NotPermitted;
        Byte* canonical=stackalloc Byte[(Int32)PathCapacity];
        if(!KernelProcessRecordStore.TryGetCurrentDirectoryAscii(processId,canonical,PathCapacity,out UInt32 canonicalLength))return (Int64)KernelSystemCallError.NotFound;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];
        if(!FileSystemPathPolicyRuntime.TryExternalizeCanonicalAscii(canonical,canonicalLength,path,PathCapacity,out UInt32 length))return (Int64)KernelSystemCallError.Fault;
        if(frame->NativeMessage.OutputCapacity<length)return (Int64)KernelSystemCallError.InvalidArgument;
        if(length!=0U&&!KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)path,length))return (Int64)KernelSystemCallError.Fault;
        return length;
    }

    private static Int64 ProcessCurrentDirectorySet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.DataLength==0UL||frame->NativeMessage.DataLength>PathCapacity)return (Int64)KernelSystemCallError.InvalidArgument;
        if(!KernelProcesses.TryGetCurrentProcessId(out UInt64 processId)||processId==0UL)return (Int64)KernelSystemCallError.NotPermitted;
        Byte* raw=stackalloc Byte[(Int32)PathCapacity];
        if(!KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,(UInt64)(nuint)raw,frame->NativeMessage.DataLength))return (Int64)KernelSystemCallError.Fault;
        Byte* resolved=stackalloc Byte[(Int32)PathCapacity];
        if(!TryResolveProcessPath(processId,raw,(UInt32)frame->NativeMessage.DataLength,resolved,PathCapacity,out UInt32 resolvedLength))return (Int64)KernelSystemCallError.InvalidArgument;
        if(!KernelVfs.OpenDirectoryAscii(KernelVfs.DefaultNamespace,resolved,resolvedLength,out KernelDirectoryHandle handle))return (Int64)KernelSystemCallError.NotFound;
        Boolean closed=KernelVfs.CloseDirectory(handle);
        if(!closed)return (Int64)KernelSystemCallError.Fault;
        return KernelProcessRecordStore.TrySetCurrentDirectoryAscii(processId,resolved,resolvedLength)?0L:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 CopyContextToUser(KernelSystemCallFrame* frame,Boolean arguments)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt32 length=arguments?_currentArgumentLength:_currentEnvironmentLength;
        if(frame->NativeMessage.OutputCapacity<length)return (Int64)KernelSystemCallError.InvalidArgument;
        fixed(Byte* args=_state.CurrentArguments,env=_state.CurrentEnvironment)
        {
            Byte* source=arguments?args:env;
            if(length!=0U&&!KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)source,length))
                return (Int64)KernelSystemCallError.Fault;
        }
        return length;
    }

    private static Boolean TryResolveProcessPath(UInt64 processId,Byte* path,UInt32 pathLength,Byte* output,UInt32 capacity,out UInt32 resolvedLength)
    {
        resolvedLength=0U;if(processId==0UL||path==null||pathLength==0U||output==null||capacity==0U)return false;
        Byte* normalized=stackalloc Byte[(Int32)PathCapacity];
        if(!FileSystemPathPolicyRuntime.TryNormalizeUserAscii(path,pathLength,normalized,PathCapacity,out UInt32 normalizedLength,out Boolean absolute))return false;
        if(absolute)
        {
            if(normalizedLength>capacity)return false;
            for(UInt32 i=0U;i<normalizedLength;i++)output[i]=normalized[i];
            resolvedLength=normalizedLength;return true;
        }
        Byte* current=stackalloc Byte[(Int32)PathCapacity];
        if(!KernelProcessRecordStore.TryGetCurrentDirectoryAscii(processId,current,PathCapacity,out UInt32 currentLength)||currentLength==0U)return false;
        UInt32 separator=(currentLength==1U&&current[0]=='/')?0U:1U;
        if(currentLength+separator+normalizedLength>capacity)return false;
        for(UInt32 i=0U;i<currentLength;i++)output[i]=current[i];
        UInt32 offset=currentLength;if(separator!=0U)output[offset++]=(Byte)'/';
        for(UInt32 i=0U;i<normalizedLength;i++)output[offset+i]=normalized[i];
        resolvedLength=offset+normalizedLength;return true;
    }

    private static Boolean TryCopyResolvedPath(KernelSystemCallFrame* frame,Byte* output,out UInt32 length)
    {
        length=0U;if(frame==null||frame->NativeMessage.DataLength==0UL||frame->NativeMessage.DataLength>PathCapacity||output==null)return false;
        if(!KernelProcesses.TryGetCurrentProcessId(out UInt64 processId)||processId==0UL)return false;
        Byte* raw=stackalloc Byte[(Int32)PathCapacity];
        if(!KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,(UInt64)(nuint)raw,frame->NativeMessage.DataLength))return false;
        return TryResolveProcessPath(processId,raw,(UInt32)frame->NativeMessage.DataLength,output,PathCapacity,out length);
    }

    private static Int64 FileOpenGet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];UInt32 pathLength=0U;
        if(!TryCopyResolvedPath(frame,path,out pathLength))return (Int64)KernelSystemCallError.InvalidArgument;
        KernelFileAccess access=(KernelFileAccess)(Byte)frame->NativeMessage.Value0;
        if(!KernelVfs.OpenAscii(KernelVfs.DefaultNamespace,path,pathLength,access,out KernelFileHandle handle))
            return (Int64)KernelSystemCallError.NotFound;
        if(!KernelVfs.TryGetFileInfo(handle,out KernelVfsFileInfo info)||info.Type!=KernelFileType.File)
        {
            KernelVfs.Close(handle);
            return (Int64)KernelSystemCallError.NotFound;
        }
        return handle.Value;
    }

    private static Int64 FileReadGet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 handle=frame->NativeMessage.Value0;
        UInt32 requested=(UInt32)frame->NativeMessage.Value1;
        if(requested>IoChunkCapacity||frame->NativeMessage.OutputCapacity<requested)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* buffer=stackalloc Byte[(Int32)IoChunkCapacity];

        if(handle==0UL)
        {
            UInt32 read=0U;
            while(read<requested&&_inputRead!=_inputWrite)
            {
                fixed(Byte* input=_state.Input)buffer[read]=input[_inputRead];
                _inputRead=(_inputRead+1U)%InputCapacity;
                read++;
            }
            if(read==0U)return (Int64)KernelSystemCallError.NotFound;
            if(!KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)buffer,read))return (Int64)KernelSystemCallError.Fault;
            return read;
        }

        if(handle<=2UL)return (Int64)KernelSystemCallError.NotPermitted;
        if(!KernelVfs.Read(new KernelFileHandle((UInt32)handle),buffer,requested,out UInt32 fileRead))return (Int64)KernelSystemCallError.Fault;
        if(fileRead!=0U&&!KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)buffer,fileRead))return (Int64)KernelSystemCallError.Fault;
        return fileRead;
    }

    private static Int64 FileWriteSet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 handle=frame->NativeMessage.Value0;
        UInt32 requested=(UInt32)frame->NativeMessage.Value1;
        if(requested>IoChunkCapacity||frame->NativeMessage.DataLength<requested)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* buffer=stackalloc Byte[(Int32)IoChunkCapacity];
        if(requested!=0U&&!KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,(UInt64)(nuint)buffer,requested))return (Int64)KernelSystemCallError.Fault;

        if(handle==1UL||handle==2UL)return KernelConsole.WriteAscii(buffer,requested)?requested:(Int64)KernelSystemCallError.Fault;
        if(handle==0UL)return (Int64)KernelSystemCallError.NotPermitted;
        return KernelVfs.Write(new KernelFileHandle((UInt32)handle),buffer,requested,out UInt32 written)?
            written:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 FileCreateSet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];UInt32 pathLength=0U;
        if(!TryCopyResolvedPath(frame,path,out pathLength))return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelVfs.CreateFileAscii(KernelVfs.DefaultNamespace,path,pathLength,frame->NativeMessage.Value0!=0UL)?
            0L:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 FileDeleteSet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];UInt32 pathLength=0U;
        if(!TryCopyResolvedPath(frame,path,out pathLength))return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelVfs.DeleteFileAscii(KernelVfs.DefaultNamespace,path,pathLength)?
            0L:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 FileCloseEvent(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 handle=frame->NativeMessage.Value0;
        if(handle<=2UL)return 0L;
        return KernelVfs.Close(new KernelFileHandle((UInt32)handle))?0L:(Int64)KernelSystemCallError.NotFound;
    }

    private static Int64 DirectoryOpenGet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];UInt32 pathLength=0U;
        if(!TryCopyResolvedPath(frame,path,out pathLength))return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelVfs.OpenDirectoryAscii(KernelVfs.DefaultNamespace,path,pathLength,out KernelDirectoryHandle handle)?
            handle.Value:(Int64)KernelSystemCallError.NotFound;
    }

    private static Int64 DirectoryCreateSet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];UInt32 pathLength=0U;
        if(!TryCopyResolvedPath(frame,path,out pathLength))return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelVfs.CreateDirectoryAscii(KernelVfs.DefaultNamespace,path,pathLength)?
            0L:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 DirectoryDeleteSet(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* path=stackalloc Byte[(Int32)PathCapacity];UInt32 pathLength=0U;
        if(!TryCopyResolvedPath(frame,path,out pathLength))return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelVfs.RemoveDirectoryAscii(KernelVfs.DefaultNamespace,path,pathLength)?
            0L:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 DirectoryReadGet(KernelSystemCallFrame* frame)
    {
        if(frame==null||frame->NativeMessage.Value0==0UL||frame->NativeMessage.OutputCapacity==0UL)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 capacity=frame->NativeMessage.OutputCapacity;if(capacity>512UL)capacity=512UL;Char* name=stackalloc Char[(Int32)capacity];
        if(!KernelVfs.ReadDirectory(new KernelDirectoryHandle((UInt32)frame->NativeMessage.Value0),name,(UInt32)capacity,out UInt32 length,out _,out _,out _))return 0L;
        Byte* ascii=stackalloc Byte[(Int32)capacity];for(UInt32 i=0U;i<length;i++){Char c=name[i];if(c>0x7F)return (Int64)KernelSystemCallError.Fault;ascii[i]=(Byte)c;}
        return KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)ascii,length)?(Int64)length:(Int64)KernelSystemCallError.Fault;
    }

    private static Int64 DirectoryCloseEvent(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 handle=frame->NativeMessage.Value0;
        if(handle<=2UL)return (Int64)KernelSystemCallError.InvalidArgument;
        return KernelVfs.CloseDirectory(new KernelDirectoryHandle((UInt32)handle))?0L:(Int64)KernelSystemCallError.NotFound;
    }

    private static Boolean TryCreateExecutable(Byte* path,UInt32 pathLength,KernelProcessOwnership ownership,out KernelProcessInfo process)
    {
        process=default;if(path==null||pathLength==0U||pathLength>PathCapacity)return false;

        Byte externalSeparator=(Byte)FileSystem.GetPathSeparator();
        Boolean absolute=path[0]==externalSeparator;

        // A bare executable name is a command request. Resolve it against the configured
        // CommandsPath(s) inside the kernel, where canonical filesystem paths and the boot
        // asset catalogue already live. This avoids round-tripping a logical path through
        // userland merely to convert it back into canonical form here.
        if(!absolute)
            return TryCreateCommandExecutable(path,pathLength,ownership,out process);

        // Userland absolute paths use the OS-selected external separator. SystemAssetCatalog
        // and the VFS use Inu's canonical '/' form internally.
        Byte* canonical=stackalloc Byte[(Int32)PathCapacity];
        if(!FileSystemPathPolicyRuntime.TryNormalizeUserAscii(path,pathLength,canonical,PathCapacity,out UInt32 canonicalLength,out Boolean normalizedAbsolute)||
           !normalizedAbsolute||canonicalLength<=1U)return false;
        if(TryCreateCanonicalExecutable(canonical,canonicalLength,ownership,out process))return true;

        // If the caller supplied a command candidate assembled in userland, retry its final
        // component against the kernel-authoritative CommandsPath(s). This keeps older generated
        // shells working while path policy remains entirely owned by the OS.
        UInt32 componentStart=canonicalLength;
        while(componentStart>1U&&canonical[componentStart-1U]!=(Byte)'/')componentStart--;
        if(componentStart<canonicalLength)
            return TryCreateCommandExecutable(canonical+componentStart,canonicalLength-componentStart,ownership,out process);
        return false;
    }

    private static Boolean TryCreateCommandExecutable(Byte* command,UInt32 commandLength,KernelProcessOwnership ownership,out KernelProcessInfo process)
    {
        process=default;
        if(command==null||commandLength==0U||commandLength>255U)return false;

        Byte externalSeparator=(Byte)FileSystem.GetPathSeparator();
        for(UInt32 i=0U;i<commandLength;i++)
        {
            Byte b=command[i];
            if(b==0U||b>0x7FU||b==(Byte)'/'||b==externalSeparator)return false;
        }

        Byte* candidate=stackalloc Byte[(Int32)PathCapacity];
        UInt32 count=FileSystemLogicalPaths.CommandCount;
        for(UInt32 rootIndex=0U;rootIndex<count;rootIndex++)
        {
            String root=FileSystemLogicalPaths.CanonicalCommand(rootIndex);
            if(root==null||root.Length==0||root[0]!='/'||(UInt32)root.Length+1U+commandLength+4U>PathCapacity)continue;

            UInt32 length=0U;
            for(Int32 i=0;i<root.Length;i++)
            {
                Char c=root[i];if(c>0x7F){length=0U;break;}
                candidate[length++]=(Byte)c;
            }
            if(length==0U)continue;
            if(candidate[length-1U]!=(Byte)'/')candidate[length++]=(Byte)'/';
            for(UInt32 i=0U;i<commandLength;i++)candidate[length++]=command[i];

            UInt32 plainLength=length;
            candidate[length++]=(Byte)'.';candidate[length++]=(Byte)'E';candidate[length++]=(Byte)'X';candidate[length++]=(Byte)'E';
            if(TryCreateCanonicalExecutable(candidate,length,ownership,out process))return true;
            if(TryCreateCanonicalExecutable(candidate,plainLength,ownership,out process))return true;
        }
        return false;
    }

    private static Boolean TryCreateCanonicalExecutable(Byte* canonical,UInt32 canonicalLength,KernelProcessOwnership ownership,out KernelProcessInfo process)
    {
        process=default;
        if(canonical==null||canonicalLength<=1U||canonicalLength>PathCapacity||canonical[0]!=(Byte)'/'||
           !FileSystemPathPolicyRuntime.ValidateCanonicalAscii(canonical,canonicalLength))return false;

        Byte* assetPath=canonical+1;
        UInt32 assetLength=canonicalLength-1U;
        if(SystemAssetCatalog.TryFindAscii(assetPath,assetLength,out Byte* image,out UInt32 imageLength))
            return KernelProcesses.TryCreateFromImage((UInt64)(nuint)image,imageLength,ownership,out process);

        return KernelProcesses.TryCreateFromFileAscii(KernelVfs.DefaultNamespace,canonical,canonicalLength,ownership,out process);
    }

    private static void PromotePendingContext()
    {
        fixed(Byte* pa=_state.PendingArguments,pe=_state.PendingEnvironment,ca=_state.CurrentArguments,ce=_state.CurrentEnvironment)
        {
            CopyBytes(pa,ca,_pendingArgumentLength);CopyBytes(pe,ce,_pendingEnvironmentLength);
        }
        _currentArgumentLength=_pendingArgumentLength;_currentEnvironmentLength=_pendingEnvironmentLength;
        _pendingArgumentLength=0U;_pendingEnvironmentLength=0U;
    }

    private static void SetCurrentContext(Byte* arguments,UInt32 argumentLength,Byte* environment,UInt32 environmentLength)
    {
        ClearCurrentContext();
        fixed(Byte* ca=_state.CurrentArguments,ce=_state.CurrentEnvironment)
        {
            if(arguments!=null)CopyBytes(arguments,ca,argumentLength);
            if(environment!=null)CopyBytes(environment,ce,environmentLength);
        }
        _currentArgumentLength=argumentLength;_currentEnvironmentLength=environmentLength;
    }

    private static void ClearCurrentContext()
    {
        _currentArgumentLength=0U;_currentEnvironmentLength=0U;
    }

    private static void CopyBytes(Byte* source,Byte* destination,UInt32 count){for(UInt32 i=0U;i<count;i++)destination[i]=source[i];}
    private static UInt32 CopyLiteral(String text,Byte* destination,UInt32 capacity)
    {
        if(text==null||destination==null||(UInt32)text.Length>capacity)return 0U;
        for(Int32 i=0;i<text.Length;i++)destination[i]=(Byte)text[i];return (UInt32)text.Length;
    }
}
