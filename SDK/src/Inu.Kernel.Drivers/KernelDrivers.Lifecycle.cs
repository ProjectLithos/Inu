using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

public static unsafe partial class KernelDrivers
{
    public static Boolean TryBindDevice(KernelDeviceHandle device,out KernelDriverHandle driver)
    {
        driver=default;if(!TryDevice(device,out DeviceRecord* d)||d->BoundDriver!=0U)return false;KernelDeviceIdentifier identifier=Identifier(d);
        for(Int32 i=0;i<(Int32)_driverCapacity;i++)
        {
            DriverRecord* r=Driver(i);if(r->Used==0||!KernelDriverMath.Matches(Rule(r),identifier))continue;KernelDriverHandle candidate=new((UInt32)i+1U);KernelDriverDeviceContext context=new(device,candidate,identifier);
            KernelDeviceState previous=(KernelDeviceState)d->State;d->State=(Byte)KernelDeviceState.Matched;EmitDeviceStateEvent(KernelDeviceStateEventKind.StateChanged,device,d,candidate,previous,KernelDeviceState.Matched,KernelDriverFailureCode.None);d->State=(Byte)KernelDeviceState.Probing;
            if(r->Discover!=0UL){delegate*<KernelDriverDeviceContext*,Boolean> discover=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Discover;if(!discover(&context)){d->State=(Byte)previous;continue;}}
            delegate*<KernelDriverDeviceContext*,Boolean> probe=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Probe;if(!probe(&context)){d->State=(Byte)previous;continue;}
            d->State=(Byte)KernelDeviceState.Probed;EmitLifecycle(device,candidate,KernelDriverLifecycleStage.Probe,KernelDeviceState.Probing,KernelDeviceState.Probed,KernelDriverFailureCode.None);d->BoundDriver=candidate.Value;d->State=(Byte)KernelDeviceState.Binding;
            if(r->Bind!=0UL){delegate*<KernelDriverDeviceContext*,Boolean> bind=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Bind;if(!bind(&context)){d->BoundDriver=0U;MarkFailed(d,device,candidate,KernelDriverFailureCode.BindFailed,KernelDriverLifecycleStage.Bind);continue;}}
            d->State=(Byte)KernelDeviceState.Bound;r->State=(Byte)KernelDriverState.Active;
            // Capability declarations are maximum permission ceilings. They are not
            // an all-or-nothing requirement that every possible grant exist at bind.
            if(r->DeclaredCapabilities!=0UL)GrantDeclaredCapabilities(&context,d,r);
            _boundCount++;_deviceTreeGeneration++;EmitLifecycle(device,candidate,KernelDriverLifecycleStage.Bind,KernelDeviceState.Binding,KernelDeviceState.Bound,KernelDriverFailureCode.None);EmitDeviceStateEvent(KernelDeviceStateEventKind.DriverBound,device,d,candidate,KernelDeviceState.Binding,KernelDeviceState.Bound,KernelDriverFailureCode.None);driver=candidate;return true;
        }
        return false;
    }

    /// <summary>
    /// Reconciles the authoritative device tree with all currently registered
    /// drivers. Matching devices are bound and started; unsupported devices stay
    /// discovered rather than being treated as failures.
    /// </summary>
    /// <summary>Matches, binds and starts one fully-described device. Dynamic buses call this after resources/properties are complete.</summary>
    public static Boolean MatchAndStartDevice(KernelDeviceHandle device)
    { if(!TryDevice(device,out DeviceRecord* d))return false;if(d->BoundDriver==0U&&!TryBindDevice(device,out _))return false;return StartDevice(device); }

    public static Boolean BindAndStartMatchingDevices()
    {
        if(!_initialized)return false;
        Boolean ok=true;
        for(UInt32 i=0;i<_deviceCapacity;i++)
        {
            DeviceRecord* d=Device((Int32)i);
            if(d->Used==0)continue;
            KernelDeviceHandle device=new(i+1U);

            if(d->BoundDriver!=0U)
            {
                if(d->State==(Byte)KernelDeviceState.Bound||d->State==(Byte)KernelDeviceState.Stopped)
                    ok=StartDevice(device)&ok;
                continue;
            }

            if(d->State==(Byte)KernelDeviceState.Failed)
            {
                d->State=(Byte)KernelDeviceState.Discovered;
                d->FailureCode=0U;
            }

            if(TryBindDevice(device,out _))
                ok=StartDevice(device)&ok;
        }
        return ok;
    }

    public static Boolean StartDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context))return false;if(d->State==(Byte)KernelDeviceState.Started)return true;if(d->State==(Byte)KernelDeviceState.Suspended)return ResumeDevice(device);if(d->State!=(Byte)KernelDeviceState.Bound&&d->State!=(Byte)KernelDeviceState.Stopped)return false;
        KernelDeviceState previous=(KernelDeviceState)d->State;d->State=(Byte)KernelDeviceState.Starting;if(r->DeclaredCapabilities!=0UL)GrantDeclaredCapabilities(&context,d,r);
        Boolean failed=KernelFaultInjection.ShouldFailDriverInitialization("driver");if(!failed){delegate*<KernelDriverDeviceContext*,Boolean> start=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Start;failed=!start(&context);}if(failed){delegate*<KernelDriverDeviceContext*,Boolean> rollback=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Stop;Boolean rolledBack=rollback(&context);ReleaseAllInterrupts(d);RevokeAllCapabilities(d);d->State=(Byte)(rolledBack?KernelDeviceState.Bound:KernelDeviceState.Failed);d->FailureCode=(UInt32)KernelDriverFailureCode.StartFailed;if(!rolledBack){r->State=(Byte)KernelDriverState.Failed;}EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Start,KernelDeviceState.Starting,(KernelDeviceState)d->State,KernelDriverFailureCode.StartFailed);return false;}
        d->State=(Byte)KernelDeviceState.Started;d->FailureCode=0U;_startedCount++;_deviceTreeGeneration++;EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Start,previous,KernelDeviceState.Started,KernelDriverFailureCode.None);return true;
    }
    public static Boolean StopDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context))return false;if(d->State!=(Byte)KernelDeviceState.Started&&d->State!=(Byte)KernelDeviceState.Suspended){ReleaseAllInterrupts(d);RevokeAllCapabilities(d);return true;}
        KernelDeviceState previous=(KernelDeviceState)d->State;d->State=(Byte)KernelDeviceState.Stopping;delegate*<KernelDriverDeviceContext*,Boolean> stop=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Stop;if(!stop(&context)){MarkFailed(d,device,context.Driver,KernelDriverFailureCode.StopFailed,KernelDriverLifecycleStage.Stop);return false;}d->State=(Byte)KernelDeviceState.Stopped;if(previous==KernelDeviceState.Started&&_startedCount>0U)_startedCount--;ReleaseAllInterrupts(d);RevokeAllCapabilities(d);_deviceTreeGeneration++;EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Stop,previous,KernelDeviceState.Stopped,KernelDriverFailureCode.None);return true;
    }
    public static Boolean RestartDevice(KernelDeviceHandle device)
    { if(!TryDevice(device,out DeviceRecord* d)||d->BoundDriver==0U)return false;if(d->State==(Byte)KernelDeviceState.Started||d->State==(Byte)KernelDeviceState.Suspended){if(!StopDevice(device))return false;}return StartDevice(device); }

    /// <summary>Stops and detaches the current driver while preserving the authoritative device node for later rebinding.</summary>
    public static Boolean UnbindDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context))return false;KernelDriverHandle oldDriver=context.Driver;KernelDeviceState previous=(KernelDeviceState)d->State;if((d->State==(Byte)KernelDeviceState.Started||d->State==(Byte)KernelDeviceState.Suspended)&&!StopDevice(device))return false;KernelDeviceState afterStop=(KernelDeviceState)d->State;d->State=(Byte)KernelDeviceState.Removing;delegate*<KernelDriverDeviceContext*,Boolean> remove=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Remove;if(!remove(&context)){d->State=(Byte)afterStop;return false;}ReleaseAllInterrupts(d);RevokeAllCapabilities(d);d->BoundDriver=0U;d->FailureCode=0U;d->State=(Byte)KernelDeviceState.Discovered;if(_boundCount>0U)_boundCount--;_deviceTreeGeneration++;EmitDeviceStateEvent(KernelDeviceStateEventKind.DriverUnbound,device,d,oldDriver,previous,KernelDeviceState.Discovered,KernelDriverFailureCode.None);return true;
    }
    public static Boolean RebindDevice(KernelDeviceHandle device)
    { if(!TryDevice(device,out DeviceRecord* d))return false;if(d->BoundDriver!=0U&&!UnbindDevice(device))return false;return MatchAndStartDevice(device); }
    public static Boolean ResetDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context)||r->Reset==0UL)return false;KernelDeviceState previous=(KernelDeviceState)d->State;d->State=(Byte)KernelDeviceState.Resetting;delegate*<KernelDriverDeviceContext*,Boolean> reset=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Reset;if(!reset(&context)){MarkFailed(d,device,context.Driver,KernelDriverFailureCode.ResetFailed,KernelDriverLifecycleStage.Reset);return false;}d->State=(Byte)(previous==KernelDeviceState.Started?KernelDeviceState.Started:KernelDeviceState.Bound);d->FailureCode=0U;EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Reset,previous,(KernelDeviceState)d->State,KernelDriverFailureCode.None);return true;
    }
    public static Boolean SuspendDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context)||d->State!=(Byte)KernelDeviceState.Started)return false;d->State=(Byte)KernelDeviceState.Suspending;if(r->Suspend!=0UL){delegate*<KernelDriverDeviceContext*,Boolean> suspend=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Suspend;if(!suspend(&context)){MarkFailed(d,device,context.Driver,KernelDriverFailureCode.SuspendFailed,KernelDriverLifecycleStage.Suspend);return false;}}d->State=(Byte)KernelDeviceState.Suspended;r->State=(Byte)KernelDriverState.Suspended;if(_startedCount>0U)_startedCount--;EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Suspend,KernelDeviceState.Started,KernelDeviceState.Suspended,KernelDriverFailureCode.None);return true;
    }
    public static Boolean ResumeDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context)||d->State!=(Byte)KernelDeviceState.Suspended)return false;d->State=(Byte)KernelDeviceState.Resuming;if(r->DeclaredCapabilities!=0UL&&!AllDeclaredCapabilitiesGranted(d,r->DeclaredCapabilities)&&!GrantDeclaredCapabilities(&context,d,r)){MarkFailed(d,device,context.Driver,KernelDriverFailureCode.CapabilityFailure,KernelDriverLifecycleStage.Resume);return false;}if(r->Resume!=0UL){delegate*<KernelDriverDeviceContext*,Boolean> resume=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Resume;if(!resume(&context)){MarkFailed(d,device,context.Driver,KernelDriverFailureCode.ResumeFailed,KernelDriverLifecycleStage.Resume);return false;}}d->State=(Byte)KernelDeviceState.Started;r->State=(Byte)KernelDriverState.Active;_startedCount++;EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Resume,KernelDeviceState.Suspended,KernelDeviceState.Started,KernelDriverFailureCode.None);return true;
    }
    public static Boolean FailDevice(KernelDeviceHandle device,KernelDriverFailureCode failure)
    { if(failure==KernelDriverFailureCode.None||!TryBound(device,out DeviceRecord* d,out _,out KernelDriverDeviceContext context))return false;MarkFailed(d,device,context.Driver,failure,KernelDriverLifecycleStage.Fail);return true; }
    public static Boolean RecoverDevice(KernelDeviceHandle device)
    {
        if(!TryBound(device,out DeviceRecord* d,out DriverRecord* r,out KernelDriverDeviceContext context)||d->State!=(Byte)KernelDeviceState.Failed)return false;KernelDriverFailureCode failure=(KernelDriverFailureCode)d->FailureCode;d->State=(Byte)KernelDeviceState.Recovering;r->State=(Byte)KernelDriverState.Recovering;if(r->Recover!=0UL){delegate*<KernelDriverDeviceContext*,Boolean> recover=(delegate*<KernelDriverDeviceContext*,Boolean>)(void*)r->Recover;if(!recover(&context)){d->State=(Byte)KernelDeviceState.Failed;r->State=(Byte)KernelDriverState.Failed;return false;}}d->State=(Byte)KernelDeviceState.Bound;d->FailureCode=0U;r->State=(Byte)KernelDriverState.Active;EmitLifecycle(device,context.Driver,KernelDriverLifecycleStage.Recover,KernelDeviceState.Failed,KernelDeviceState.Bound,failure);return true;
    }
    public static Boolean RemoveDevice(KernelDeviceHandle device)
    {
        if(!TryDevice(device,out DeviceRecord* d))return false;
        while(d->FirstChild!=0U){KernelDeviceHandle child=new(d->FirstChild);if(!RemoveDevice(child))return false;if(!TryDevice(device,out d))return false;}
        KernelDeviceState previous=(KernelDeviceState)d->State;KernelDeviceIdentifier identifier=Identifier(d);KernelDeviceHandle parent=new(d->Parent);KernelDriverHandle driver=new(d->BoundDriver);
        if(d->BoundDriver!=0U&&!UnbindDevice(device))return false;if(!TryDevice(device,out d))return false;
        d->State=(Byte)KernelDeviceState.Removed;EmitLifecycle(device,driver,KernelDriverLifecycleStage.Remove,previous,KernelDeviceState.Removed,KernelDriverFailureCode.None);EmitDeviceStateEvent(KernelDeviceStateEventKind.Removed,device,d,driver,previous,KernelDeviceState.Removed,KernelDriverFailureCode.None,identifier,parent);
        UnlinkFromParent(device,d);ReleaseAllInterrupts(d);RevokeAllCapabilities(d);Clear((Byte*)d,sizeof(DeviceRecord));_deviceCount--;_deviceTreeGeneration++;return true;
    }
    /// <summary>Suspends every currently started device through its bound driver's lifecycle callback. Partial failure is rolled back.</summary>
    public static Boolean SuspendStartedDevices()
    {
        if(!_initialized)return true;for(UInt32 n=_deviceCapacity;n>0U;n--){UInt32 i=n-1U;DeviceRecord* d=Device((Int32)i);if(d->Used==0||d->State!=(Byte)KernelDeviceState.Started)continue;if(!SuspendDevice(new KernelDeviceHandle(i+1U))){ResumeSuspendedDevices();return false;}}return true;
    }
    /// <summary>Resumes suspended devices in forward registry order so providers/parents are restored before later-discovered consumers.</summary>
    public static Boolean ResumeSuspendedDevices()
    {
        if(!_initialized)return true;Boolean success=true;for(UInt32 i=0U;i<_deviceCapacity;i++){DeviceRecord* d=Device((Int32)i);if(d->Used==0||d->State!=(Byte)KernelDeviceState.Suspended)continue;if(!ResumeDevice(new KernelDeviceHandle(i+1U)))success=false;}return success;
    }

}
