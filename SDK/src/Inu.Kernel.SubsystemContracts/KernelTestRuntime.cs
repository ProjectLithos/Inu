using System;

namespace Inu.Kernel.Contracts;

/// <summary>
/// Allocation-free test execution support for the freestanding Inu kernel.
/// A host runner may discover tests from manifests, but the kernel executes each
/// test through this stable function-pointer ABI so testing does not depend on GC,
/// reflection, exceptions, or managed interface dispatch.
/// </summary>
public static unsafe class KernelTestRuntime
{
    private static delegate* managed<KernelTestExecution*,Boolean> _begin;
    private static delegate* managed<KernelTestReport*,Boolean> _complete;
    private static delegate* managed<UInt64> _clockMilliseconds;
    private static UInt64 _runCount,_passed,_failed,_skipped,_timedOut;
    private static UInt64 _assertions,_assertionFailures,_faultsInjected;
    private static Boolean _running;
    private static KernelTestExecution _current;
    private static KernelTestReport _last;

    public static Boolean ConfigureFreestanding(
        delegate* managed<KernelTestExecution*,Boolean> begin,
        delegate* managed<KernelTestReport*,Boolean> complete,
        delegate* managed<UInt64> clockMilliseconds)
    {
        _begin=begin;_complete=complete;_clockMilliseconds=clockMilliseconds;return true;
    }

    public static Boolean IsRunning()=>_running;
    public static KernelTestStatistics GetStatistics()=>new(_runCount,_passed,_failed,_skipped,_timedOut,_assertions,_assertionFailures,_faultsInjected);
    public static Boolean TryGetLastReport(out KernelTestReport report){report=_last;return _runCount!=0;}
    public static Boolean ResetStatistics(){if(_running)return false;_runCount=_passed=_failed=_skipped=_timedOut=_assertions=_assertionFailures=_faultsInjected=0;_last=default;return true;}

    public static Boolean Run(UInt64 testId,KernelTestKind kind,UInt64 timeoutMilliseconds,delegate* managed<KernelTestResult> test)
    {
        if(_running||test==null||testId==0)return false;
        _running=true;
        UInt64 start=_clockMilliseconds!=null?_clockMilliseconds():0;
        KernelTestExecution current=new KernelTestExecution(testId,kind,timeoutMilliseconds,start);
        _current=current;
        if(_begin!=null)_begin(&current);
        UInt64 assertionsBefore=_assertions,failuresBefore=_assertionFailures,faultsBefore=_faultsInjected;
        KernelTestResult result=test();
        UInt64 end=_clockMilliseconds!=null?_clockMilliseconds():start;
        UInt64 duration=end>=start?end-start:0;
        if(timeoutMilliseconds!=0&&duration>timeoutMilliseconds)result=KernelTestResult.Timeout;
        KernelTestReport last=new KernelTestReport(testId,kind,result,duration,_assertions-assertionsBefore,_assertionFailures-failuresBefore,_faultsInjected-faultsBefore);
        _last=last;
        _runCount++;
        switch(result){case KernelTestResult.Passed:_passed++;break;case KernelTestResult.Failed:_failed++;break;case KernelTestResult.Skipped:_skipped++;break;case KernelTestResult.Timeout:_timedOut++;break;default:_failed++;break;}
        if(_complete!=null)_complete(&last);
        _running=false;
        return result==KernelTestResult.Passed||result==KernelTestResult.Skipped;
    }

    public static Boolean Assert(Boolean condition){_assertions++;if(!condition)_assertionFailures++;return condition;}
    public static Boolean AssertEqual(UInt64 expected,UInt64 actual){return Assert(expected==actual);}
    public static Boolean AssertNotEqual(UInt64 unexpected,UInt64 actual){return Assert(unexpected!=actual);}
    public static Boolean AssertZero(UInt64 actual){return Assert(actual==0);}
    public static Boolean AssertNonZero(UInt64 actual){return Assert(actual!=0);}
    public static Boolean RecordInjectedFault(){_faultsInjected++;return true;}
}

/// <summary>Allocation-free, deterministic fault-injection engine used by kernel and driver tests.</summary>
public static unsafe class KernelFaultInjection
{
    private const Int32 MaximumRules=8;
    private struct KernelFaultSlot { public Boolean Active; public UInt64 RuleId; public KernelFaultKind Kind; public UInt64 SubsystemHash; public UInt64 TriggerAfter; public UInt32 RepeatCount; public UInt32 FiredCount; public UInt64 SeenCount; public UInt64 Parameter; }
    private static KernelFaultSlot _rule0,_rule1,_rule2,_rule3,_rule4,_rule5,_rule6,_rule7;
    private static UInt64 _nextId=1;

    private static ref KernelFaultSlot Slot(Int32 index)
    {
        switch(index){case 0:return ref _rule0;case 1:return ref _rule1;case 2:return ref _rule2;case 3:return ref _rule3;case 4:return ref _rule4;case 5:return ref _rule5;case 6:return ref _rule6;default:return ref _rule7;}
    }

    public static UInt64 HashSubsystem(String subsystem)
    {
        if(subsystem==null)return 0;
        UInt64 hash=1469598103934665603UL;
        for(Int32 i=0;i<subsystem.Length;i++){hash^=subsystem[i];hash*=1099511628211UL;}
        return hash;
    }

    public static Boolean TryArm(KernelFaultNativeRule rule,out UInt64 ruleId)
    {
        ruleId=0;
        for(Int32 i=0;i<MaximumRules;i++)
        {
            ref KernelFaultSlot slot=ref Slot(i);if(slot.Active)continue;
            UInt64 generation=_nextId++&0xFFFFFFFFUL;if(generation==0UL)generation=_nextId++&0xFFFFFFFFUL;
            ruleId=((UInt64)(UInt32)i<<32)|generation;
            slot.Active=true;slot.RuleId=ruleId;slot.Kind=rule.Kind;slot.SubsystemHash=rule.SubsystemHash;slot.TriggerAfter=rule.TriggerAfter;slot.RepeatCount=rule.RepeatCount;slot.FiredCount=0;slot.SeenCount=0;slot.Parameter=rule.Parameter;
            return true;
        }
        return false;
    }

    public static Boolean TryDisarm(UInt64 ruleId)
    {
        Int32 index=(Int32)(ruleId>>32);if(index<0||index>=MaximumRules||ruleId==0UL)return false;ref KernelFaultSlot slot=ref Slot(index);if(!slot.Active||slot.RuleId!=ruleId)return false;slot=default;return true;
    }

    public static Boolean ShouldInject(KernelFaultKind kind,UInt64 subsystemHash,out UInt64 parameter)
    {
        parameter=0;
        for(Int32 i=0;i<MaximumRules;i++)
        {
            ref KernelFaultSlot slot=ref Slot(i);if(!slot.Active||slot.Kind!=kind)continue;if(slot.SubsystemHash!=0&&slot.SubsystemHash!=subsystemHash)continue;
            UInt64 seen=++slot.SeenCount;if(seen<=slot.TriggerAfter)continue;if(slot.RepeatCount!=0&&slot.FiredCount>=slot.RepeatCount)continue;
            slot.FiredCount++;parameter=slot.Parameter;KernelTestRuntime.RecordInjectedFault();return true;
        }
        return false;
    }

    /// <summary>Arms a deterministic fault using a subsystem name without requiring a hosted injector object.</summary>
    public static Boolean TryArm(KernelFaultKind kind,String subsystem,UInt64 triggerAfter,UInt32 repeatCount,UInt64 parameter,out UInt64 ruleId)
        =>TryArm(new KernelFaultNativeRule(kind,HashSubsystem(subsystem),triggerAfter,repeatCount,parameter),out ruleId);

    /// <summary>Checks one named subsystem fault point.</summary>
    public static Boolean ShouldInject(KernelFaultKind kind,String subsystem,out UInt64 parameter)
        =>ShouldInject(kind,HashSubsystem(subsystem),out parameter);

    /// <summary>Applies deterministic corruption to a DMA address when a BadDma rule fires.</summary>
    public static Boolean TryCorruptDmaAddress(String subsystem,UInt64 address,out UInt64 injectedAddress)
    {
        injectedAddress=address;if(!ShouldInject(KernelFaultKind.BadDma,subsystem,out UInt64 parameter))return false;
        UInt64 mask=parameter==0UL?1UL:parameter;injectedAddress=address^mask;return true;
    }

    /// <summary>Applies deterministic byte corruption to a packet buffer when a CorruptPacket rule fires.</summary>
    public static Boolean TryCorruptPacket(String subsystem,Byte* packet,UInt32 length,out UInt32 changedOffset,out Byte original)
    {
        changedOffset=0;original=0;if(packet==null||length==0||!ShouldInject(KernelFaultKind.CorruptPacket,subsystem,out UInt64 parameter))return false;
        changedOffset=(UInt32)(parameter%(UInt64)length);original=packet[changedOffset];packet[changedOffset]=(Byte)(original^0xFF);return true;
    }


    /// <summary>Returns a deterministic interrupt delay in nanoseconds when a DelayedInterrupt rule fires.</summary>
    public static Boolean TryGetInterruptDelay(String subsystem,out UInt64 delayNanoseconds)
    { delayNanoseconds=0UL;return ShouldInject(KernelFaultKind.DelayedInterrupt,subsystem,out delayNanoseconds); }

    /// <summary>Returns true when an injected packet-loss rule consumes the current packet.</summary>
    public static Boolean ShouldDropPacket(String subsystem)
    { return ShouldInject(KernelFaultKind.NetworkPacketLoss,subsystem,out _); }

    /// <summary>Returns true when driver start should fail before entering the driver callback.</summary>
    public static Boolean ShouldFailDriverInitialization(String subsystem)
    { return ShouldInject(KernelFaultKind.DriverInitializationFailure,subsystem,out _); }

    /// <summary>Returns true when an already-running driver should fail its current runtime operation.</summary>
    public static Boolean ShouldFailDriverRuntime(String subsystem)
    { return ShouldInject(KernelFaultKind.DriverRuntimeFailure,subsystem,out _); }

    /// <summary>Returns true when a controlled user-process crash should be injected.</summary>
    public static Boolean ShouldCrashUserProcess(String subsystem,out UInt64 parameter)
    { return ShouldInject(KernelFaultKind.UserProcessCrash,subsystem,out parameter); }

        public static Boolean Reset(){_rule0=_rule1=_rule2=_rule3=_rule4=_rule5=_rule6=_rule7=default;_nextId=1;return true;}
}

/// <summary>
/// Hardware-simulation bridge. Tests can replace physical MMIO/PIO/IRQ behaviour with a deterministic model.
/// </summary>
public static unsafe class KernelHardwareSimulation
{
    private static delegate* managed<UInt64,UInt32,UInt64*,Boolean> _read;
    private static delegate* managed<UInt64,UInt32,UInt64,Boolean> _write;
    private static delegate* managed<UInt32,Boolean> _interrupt;
    private static delegate* managed<UInt64,Boolean> _advanceTime;

    public static Boolean ConfigureFreestanding(
        delegate* managed<UInt64,UInt32,UInt64*,Boolean> read,
        delegate* managed<UInt64,UInt32,UInt64,Boolean> write,
        delegate* managed<UInt32,Boolean> interrupt,
        delegate* managed<UInt64,Boolean> advanceTime)
    { _read=read;_write=write;_interrupt=interrupt;_advanceTime=advanceTime;return read!=null&&write!=null; }
    public static Boolean IsConfigured()=>_read!=null&&_write!=null;
    public static Boolean TryRead(UInt64 address,UInt32 width,out UInt64 value){UInt64 temporary=0;Boolean ok=_read!=null&&_read(address,width,&temporary);value=temporary;return ok;}
    public static Boolean TryWrite(UInt64 address,UInt32 width,UInt64 value)=>_write!=null&&_write(address,width,value);
    public static Boolean TryRaiseInterrupt(UInt32 vector)=>_interrupt!=null&&_interrupt(vector);
    public static Boolean TryAdvanceTime(UInt64 nanoseconds)=>_advanceTime!=null&&_advanceTime(nanoseconds);
    public static Boolean Reset(){_read=null;_write=null;_interrupt=null;_advanceTime=null;return true;}
}
