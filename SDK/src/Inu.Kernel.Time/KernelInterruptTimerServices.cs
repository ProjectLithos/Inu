using System;

namespace Inu.Kernel.Time;

/// <summary>Registration and dispatch boundary for an optional programmable kernel interrupt timer.</summary>
public static unsafe class KernelInterruptTimerServices
{
    private static Byte _registered, _ready;
    private static delegate*<Boolean> _initialize;
    private static delegate*<Byte,UInt64,KernelTimerMode,Boolean> _program;
    private static delegate*<Boolean> _cancel;
    private static delegate*<UInt64> _frequency;

    public static Boolean Register(
        delegate*<Boolean> initialize,
        delegate*<Byte,UInt64,KernelTimerMode,Boolean> program,
        delegate*<Boolean> cancel,
        delegate*<UInt64> frequency)
    {
        if (_registered != 0 || initialize == null || program == null || cancel == null || frequency == null) return false;
        _initialize = initialize; _program = program; _cancel = cancel; _frequency = frequency;
        _registered = 1; _ready = 0; return true;
    }

    internal static Boolean InitializeRegisteredTimer()
    {
        _ready = 0;
        if (_registered == 0) return true; // Interrupt timer is optional.
        _ready = _initialize() ? (Byte)1 : (Byte)0;
        return true;
    }

    public static Boolean IsReady() => _ready != 0;
    public static UInt64 GetFrequencyHz() => _ready != 0 ? _frequency() : 0UL;
    public static Boolean TryProgram(Byte vector, UInt64 nanoseconds, KernelTimerMode mode) => _ready != 0 && _program(vector,nanoseconds,mode);
    public static Boolean Cancel() => _ready != 0 && _cancel();
}
