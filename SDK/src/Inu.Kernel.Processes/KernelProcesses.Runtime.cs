using System;
using System.Runtime;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Kernel.Gui;
using KernelFaultInjection = Inu.Kernel.Contracts.KernelFaultInjection;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Storage;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.Protection;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Security;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;
using Inu.ApplicationFormat;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    private static Boolean _initialized;
    private static Byte _userlandRuntimeConfiguration;
    private static UInt64 _runtimeServicePasses;
    private static UInt32 _runtimeObservedProcesses;


private static Boolean RegisterSelectedProcessComponents()
{
#if INU_COMPONENT_PROCESS_ADDRESS_SPACE_OWNERSHIP
    if(!KernelProcessAddressSpaceServices.IsRegistered&&!KernelProcessAddressSpaceProvider.Register())return false;
#endif
#if INU_COMPONENT_PROCESS_LIFECYCLE
    if(!KernelProcessLifecycleServices.IsRegistered&&!KernelProcessLifecycleProvider.Register())return false;
#endif
#if INU_COMPONENT_PROCESS_FOREGROUND_CONTROL
    if(!KernelProcessForegroundServices.IsRegistered&&!KernelProcessForegroundProvider.Register())return false;
#endif
#if INU_COMPONENT_PROCESS_SIGNALS
    if(!KernelProcessSignalServices.IsRegistered&&!KernelProcessSignalProvider.Register())return false;
#endif
    return true;
}

public static Boolean ConfigureUserlandRuntime(Boolean enabled){if(_initialized)return false;_userlandRuntimeConfiguration=enabled?(Byte)2:(Byte)1;return true;}

public static Boolean Initialize()
    {
        if(_initialized)return true; if(!KernelAddressSpace.IsInitialized()||!KernelProtection.IsInitialized()||!KernelSecurity.IsInitialized()||!KernelSystemCalls.IsInitialized())return false;
        if(!RegisterSelectedProcessComponents())return false;
        _initialized=true;
        if(!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ProcessIdCurrent,&GetCurrentProcessIdSyscall)||!KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.ProcessCommandComplete,&CommandCompleteEventSyscall)||!KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.ProcessExit,&CommandCompleteEventSyscall)||!KernelSystemCalls.RegisterSet(KernelSystemCallMessages.ProcessControl,&SetProcessControlSyscall)||!KernelSystemCalls.RegisterLinux(LinuxGetPidService,&GetCurrentProcessIdSyscall)||!KernelSystemCalls.RegisterLinux(LinuxExitService,&LinuxExitCompatibilitySyscall)||!KernelSystemCalls.RegisterCSharpancellationHandler(&HandleForegroundCancellationAtSyscallBoundary)||!KernelInterruptDispatch.RegisterUserCancellationHandler(&HandleForegroundCancellationFromInterrupt)){_initialized=false;return false;}
        if(_userlandRuntimeConfiguration!=1 && KernelScheduler.IsInitialized() && !KernelScheduler.RegisterRoleWorker(KernelCpuRole.Userland,&ServiceUserlandRuntime,KernelThreadPriority.Normal)){_initialized=false;return false;}
        return true;
    }

public static UInt64 GetUserlandRuntimeWorkerThreadId()=>KernelScheduler.IsInitialized()?KernelScheduler.GetRoleWorkerThreadId(KernelCpuRole.Userland):0UL;

public static UInt64 GetUserlandCommandWorkerThreadId()=>GetUserlandRuntimeWorkerThreadId();

public static UInt64 GetUserlandRuntimeServicePassCount()=>_runtimeServicePasses;

public static UInt32 GetUserlandRuntimeObservedProcessCount()=>_runtimeObservedProcesses;

private static Boolean ServiceUserlandRuntime()
{
    if(!_initialized)return false;
    _runtimeObservedProcesses=KernelProcessRecordStore.CountObserved();
    _runtimeServicePasses++;
    return false;
}

public static Boolean IsInitialized() => _initialized;


public static Boolean TryGetCurrentProcessId(out UInt64 processId) { processId=0UL; return _initialized&&KernelSecurity.TryGetCurrentProcess(out processId)&&processId!=0UL; }

public static KernelProcessCapabilities GetCapabilities()
    {
        UInt32 processors=KernelSmp.IsInitialized()?KernelSmp.GetProcessorCount():1U;if(processors>MaximumExecutionProcessors)processors=MaximumExecutionProcessors;
        return new(MaximumProcesses,KernelProcessRecordStore.ActiveCount,ProcessExecutableMath.MaximumSegments,DefaultStackBytes,true,true,true,processors);
    }

public static UInt64 GetRunningProcessId(){if(!TryGetExecutionProcessor(out UInt32 cpu))return 0UL;fixed(UInt64* running=_execution.RunningProcessIds)return running[cpu];}


}
