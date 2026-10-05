using System;
using System.Runtime;
using System.Runtime.InteropServices;
using Inu.Kernel.Acpi;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Heap;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;

namespace Inu.Kernel.InterruptDispatch;

/// <summary>Owns freestanding managed interrupt dispatch, formal vector allocation and per-CPU interrupt runtime state.</summary>
public static unsafe partial class KernelInterruptDispatch
{
    /// <summary>Gets whether managed interrupt dispatch is installed.</summary>
    public static Boolean IsInitialized()=>_initialized;
    /// <summary>Gets the subsystem lifecycle state: 0 stopped, 1 ready, 2 running, 3 paused.</summary>
    public static Byte GetLifecycleState()=>_lifecycle;
    /// <summary>Starts interrupt delivery from the ready or stopped state.</summary>
    public static Boolean Start(){if(!_initialized||(_lifecycle!=StateReady&&_lifecycle!=StateStopped))return false;_lifecycle=StateRunning;SetCurrentProcessorLifecycle(StateRunning);return Native.EnableInterrupts();}
    /// <summary>Runs interrupt delivery. Starts it when ready/stopped and resumes it when paused.</summary>
    public static Boolean Run(){if(!_initialized)return false;if(_lifecycle==StatePaused)return Resume();if(_lifecycle==StateRunning)return true;return Start();}
    /// <summary>Pauses normal maskable interrupt delivery on the current processor while preserving registrations and vector ownership.</summary>
    public static Boolean Pause(){if(!_initialized||_lifecycle!=StateRunning)return false;if(!Native.DisableInterrupts())return false;_lifecycle=StatePaused;SetCurrentProcessorLifecycle(StatePaused);return true;}
    /// <summary>Resumes a paused interrupt runtime without rebuilding routes or registrations.</summary>
    public static Boolean Resume(){if(!_initialized||_lifecycle!=StatePaused)return false;_lifecycle=StateRunning;SetCurrentProcessorLifecycle(StateRunning);return Native.EnableInterrupts();}
    /// <summary>Stops normal maskable interrupt delivery while retaining the initialized runtime for a later Start/Run.</summary>
    public static Boolean Stop(){if(!_initialized||_lifecycle==StateStopped)return false;if(!Native.DisableInterrupts())return false;_lifecycle=StateStopped;SetCurrentProcessorLifecycle(StateStopped);return true;}
    /// <summary>Gets a snapshot of one processor's interrupt runtime.</summary>
    public static Boolean TryGetProcessorState(UInt32 processorId,out ProcessorInterruptState state){state=default;if(!_initialized||processorId>=MaximumProcessors)return false;ProcessorRuntime* p=_processors+processorId;state=new ProcessorInterruptState(p->ProcessorId,p->ApicId,p->Lifecycle,p->CurrentVector,p->Nesting,p->TotalInterrupts,p->HandledInterrupts,p->UnhandledInterrupts,p->ExceptionCount,p->LastVector);return true;}
    /// <summary>Sets the dispatch lifecycle for a known processor. Hardware IF changes occur when that processor executes Start/Pause/Resume/Stop.</summary>
    public static Boolean SetProcessorLifecycle(UInt32 processorId,Byte lifecycle){if(!_initialized||processorId>=MaximumProcessors||lifecycle>StatePaused)return false;(_processors+processorId)->Lifecycle=lifecycle;return true;}
    /// <summary>Allocates one device vector from the formal 0x40-0xDF dynamic-device band.</summary>
    public static Byte AllocateVector(){if(!_initialized)return 0;for(Int32 i=FirstDynamicVector;i<=LastDynamicVector;i++)if(_allocated[i]==0){_allocated[i]=1;return (Byte)i;}return 0;}
    /// <summary>Releases an unused dynamically allocated device vector.</summary>
    public static Boolean ReleaseVector(Byte vector){if(vector<FirstDynamicVector||vector>LastDynamicVector||_allocated[vector]==0||_callbacks[vector]!=0UL)return false;_allocated[vector]=0;return true;}
    private static Boolean HandleSchedulerRescheduleIpi(Byte vector,UInt64 cookie)=>vector==SchedulerRescheduleIpiVector;
    /// <summary>Registers one interrupt callback. The callback receives vector and caller cookie.</summary>
    public static Boolean Register(Byte vector,delegate*<Byte,UInt64,Boolean> callback,UInt64 cookie){if(!_initialized||vector<32||callback==null||_callbacks[vector]!=0UL)return false;_callbacks[vector]=(UInt64)(void*)callback;_cookies[vector]=cookie;return true;}
    /// <summary>Removes one registered callback.</summary>
    public static Boolean Unregister(Byte vector){if(!_initialized||_callbacks[vector]==0UL)return false;_callbacks[vector]=0UL;_cookies[vector]=0UL;return true;}
    /// <summary>Compatibility alias for Start/Run.</summary>
    public static Boolean Enable()=>Run();
    /// <summary>Idles until the next hardware interrupt and then returns.</summary>
    public static Boolean Wait()=>_initialized&&_lifecycle==StateRunning&&Native.WaitForInterrupt();
    private static void SetCurrentProcessorLifecycle(Byte lifecycle){UInt32 id=Native.GetCurrentApicId();for(UInt32 i=0;i<MaximumProcessors;i++){ProcessorRuntime* p=_processors+i;if(p->ApicId==id||p->TotalInterrupts==0UL){p->ProcessorId=i;p->ApicId=id;p->Lifecycle=lifecycle;break;}}}
}
