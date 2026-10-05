using System;

namespace Inu.Kernel.Time;

/// <summary>Describes the five independent services in the Inu timekeeping framework.</summary>
public readonly struct KernelTimekeepingCapabilities
{
    public KernelTimekeepingCapabilities(Boolean monotonicClock, Boolean wallClock, Boolean highResolutionTimer, Boolean schedulerTick, Boolean timeoutService)
    { HasMonotonicClock=monotonicClock; HasWallClock=wallClock; HasHighResolutionTimer=highResolutionTimer; HasSchedulerTick=schedulerTick; HasTimeoutService=timeoutService; }
    public Boolean HasMonotonicClock { get; }
    public Boolean HasWallClock { get; }
    public Boolean HasHighResolutionTimer { get; }
    public Boolean HasSchedulerTick { get; }
    public Boolean HasTimeoutService { get; }
}

/// <summary>Monotonic clock API. Values are elapsed nanoseconds and are never derived from wall-clock corrections.</summary>
public static class KernelMonotonicClock
{
    public static Boolean IsAvailable() => KernelTime.IsInitialized;
    public static UInt64 GetNanoseconds() => KernelTime.GetMonotonicNanoseconds();
    public static Boolean TryCreateDeadline(UInt64 delayNanoseconds,out UInt64 deadlineNanoseconds) => KernelTime.TryCreateDeadline(delayNanoseconds,out deadlineNanoseconds);
    public static Boolean HasReached(UInt64 deadlineNanoseconds) => KernelTime.HasReached(deadlineNanoseconds);
}

/// <summary>Wall-clock service anchored from RTC and advanced with the monotonic clock.</summary>
public static class KernelWallClock
{
    private static Boolean _initialized;
    private static Int64 _unixNanosecondsAtAnchor;
    private static UInt64 _monotonicAnchor;

    internal static Boolean Initialize()
    {
        _initialized=false;
        if(!KernelWallClockSourceServices.TryRead(out KernelRtcDateTime rtc)) return false;
        if(!KernelRtcMath.TryToUnixSeconds(rtc,out Int64 seconds)) return false;
        if(seconds > Int64.MaxValue/1000000000L) return false;
        _unixNanosecondsAtAnchor=seconds*1000000000L;
        _monotonicAnchor=KernelTime.GetMonotonicNanoseconds();
        _initialized=true;
        return true;
    }

    public static Boolean IsAvailable() => _initialized;
    public static Boolean TryGetUnixNanoseconds(out Int64 nanoseconds)
    {
        nanoseconds=0L;if(!_initialized)return false;
        UInt64 now=KernelTime.GetMonotonicNanoseconds();UInt64 delta=now>=_monotonicAnchor?now-_monotonicAnchor:0UL;
        if(delta>(UInt64)Int64.MaxValue || _unixNanosecondsAtAnchor>Int64.MaxValue-(Int64)delta)return false;
        nanoseconds=_unixNanosecondsAtAnchor+(Int64)delta;return true;
    }
    /// <summary>Adjusts wall time without changing the monotonic clock.</summary>
    public static Boolean TrySetUnixNanoseconds(Int64 nanoseconds)
    { if(!_initialized)return false;_unixNanosecondsAtAnchor=nanoseconds;_monotonicAnchor=KernelTime.GetMonotonicNanoseconds();return true; }
}

/// <summary>High-resolution raw counter API for profiling and sub-tick measurement.</summary>
public static class KernelHighResolutionTimer
{
    public static Boolean IsAvailable() => KernelTime.IsInitialized && KernelTime.GetClockFrequencyHz()!=0UL;
    public static Boolean TryRead(out UInt64 ticks,out UInt64 frequencyHz) => KernelTime.TryReadHighResolutionCounter(out ticks,out frequencyHz);
    public static UInt64 GetFrequencyHz() => KernelTime.GetClockFrequencyHz();
}

/// <summary>Scheduler tick accounting. The interrupt dispatch layer advances this independently of wall-clock time.</summary>
public static class KernelSchedulerTick
{
    private static UInt64 _tick;
    private static UInt64 _periodNanoseconds;
    public static Boolean Configure(UInt64 periodNanoseconds){if(periodNanoseconds==0UL)return false;_tick=0UL;_periodNanoseconds=periodNanoseconds;return true;}
    public static Boolean Advance(){if(_periodNanoseconds==0UL)return false;_tick++;return true;}
    public static Boolean IsConfigured()=>_periodNanoseconds!=0UL;
    public static UInt64 GetTick()=>_tick;
    public static UInt64 GetPeriodNanoseconds()=>_periodNanoseconds;
    public static UInt64 GetFrequencyHz()=>_periodNanoseconds==0UL?0UL:1000000000UL/_periodNanoseconds;
}

/// <summary>Allocation-free monotonic timeout registry. Expiration is consumed explicitly by kernel services.</summary>
public static unsafe class KernelTimeoutService
{
    private const Int32 Capacity=64;
    private unsafe struct TimeoutTable { public fixed UInt64 Deadline[Capacity]; public fixed UInt64 Cookie[Capacity]; public fixed UInt32 Generation[Capacity]; public fixed Byte State[Capacity]; }
    #pragma warning disable CS0414 // Fixed-buffer storage is consumed through fixed pointers below; Roslyn does not count that as a field read.
    private static TimeoutTable _table;
    #pragma warning restore CS0414
    private static UInt32 _nextGeneration=1U;
    private static Boolean _initialized;

    public static Boolean Initialize(){_table=default;_nextGeneration=1U;_initialized=true;return true;}
    public static Boolean IsInitialized()=>_initialized;
    public static UInt32 GetCapacity()=>Capacity;
    /// <summary>Gets the number of currently armed timeout slots.</summary>
    public static UInt32 GetActiveCount(){if(!_initialized)return 0U;UInt32 count=0U;fixed(Byte* states=_table.State){for(Int32 i=0;i<Capacity;i++)if(states[i]!=0)count++;}return count;}
    public static Boolean TrySchedule(UInt64 deadlineNanoseconds,UInt64 cookie,out UInt64 timeoutId)
    {
        timeoutId=0UL;if(!_initialized||deadlineNanoseconds==0UL)return false;
        fixed(UInt64* deadlines=_table.Deadline) fixed(UInt64* cookies=_table.Cookie) fixed(UInt32* generations=_table.Generation) fixed(Byte* states=_table.State)
        {
            for(Int32 i=0;i<Capacity;i++)if(states[i]==0){UInt32 generation=_nextGeneration++;if(generation==0U)generation=_nextGeneration++;deadlines[i]=deadlineNanoseconds;cookies[i]=cookie;generations[i]=generation;states[i]=1;timeoutId=((UInt64)generation<<32)|(UInt32)(i+1);return true;}
        }
        return false;
    }
    public static Boolean TryScheduleAfter(UInt64 delayNanoseconds,UInt64 cookie,out UInt64 timeoutId)
    {timeoutId=0UL;return KernelMonotonicClock.TryCreateDeadline(delayNanoseconds,out UInt64 deadline)&&TrySchedule(deadline,cookie,out timeoutId);}
    public static Boolean Cancel(UInt64 timeoutId)
    {
        if(!_initialized)return false;UInt32 slot=(UInt32)timeoutId;UInt32 generation=(UInt32)(timeoutId>>32);if(slot==0U||slot>Capacity||generation==0U)return false;Int32 i=(Int32)slot-1;
        fixed(UInt32* generations=_table.Generation) fixed(Byte* states=_table.State){if(states[i]==0||generations[i]!=generation)return false;states[i]=0;return true;}
    }
    public static Boolean TryDequeueExpired(out UInt64 timeoutId,out UInt64 cookie)
    {
        timeoutId=0UL;cookie=0UL;if(!_initialized)return false;UInt64 now=KernelMonotonicClock.GetNanoseconds();
        fixed(UInt64* deadlines=_table.Deadline) fixed(UInt64* cookies=_table.Cookie) fixed(UInt32* generations=_table.Generation) fixed(Byte* states=_table.State)
        {
            Int32 chosen=-1;UInt64 earliest=UInt64.MaxValue;for(Int32 i=0;i<Capacity;i++)if(states[i]!=0&&deadlines[i]<=now&&deadlines[i]<earliest){chosen=i;earliest=deadlines[i];}
            if(chosen<0)return false;timeoutId=((UInt64)generations[chosen]<<32)|(UInt32)(chosen+1);cookie=cookies[chosen];states[chosen]=0;return true;
        }
    }
    public static Boolean TryGetNextDeadline(out UInt64 deadlineNanoseconds)
    {
        deadlineNanoseconds=0UL;if(!_initialized)return false;UInt64 earliest=UInt64.MaxValue;
        fixed(UInt64* deadlines=_table.Deadline) fixed(Byte* states=_table.State){for(Int32 i=0;i<Capacity;i++)if(states[i]!=0&&deadlines[i]<earliest)earliest=deadlines[i];}
        if(earliest==UInt64.MaxValue)return false;deadlineNanoseconds=earliest;return true;
    }
}

/// <summary>Single discovery facade for the separated timekeeping services.</summary>
public static class KernelTimekeeping
{
    public static KernelTimekeepingCapabilities GetCapabilities()=>new(KernelMonotonicClock.IsAvailable(),KernelWallClock.IsAvailable(),KernelHighResolutionTimer.IsAvailable(),KernelSchedulerTick.IsConfigured(),KernelTimeoutService.IsInitialized());
}
