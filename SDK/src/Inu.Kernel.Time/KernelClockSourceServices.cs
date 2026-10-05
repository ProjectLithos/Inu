using System;

namespace Inu.Kernel.Time;

/// <summary>Registration and dispatch boundary between the clock core and selectable clock-source implementations.</summary>
public static unsafe class KernelClockSourceServices
{
    private struct Registration
    {
        public KernelClockSource Source;
        public Int32 Priority;
        public Byte Registered;
        public Byte Ready;
        public delegate*<Boolean> Initialize;
        public delegate*<UInt64> GetNanoseconds;
        public delegate*<UInt64*,UInt64*,Boolean> TryReadRaw;
        public delegate*<UInt64> GetFrequencyHz;
        public delegate*<UInt64> GetPeriodFemtoseconds;
    }

    private static Registration _first;
    private static Registration _second;
    private static KernelClockSource _active;

    /// <summary>Registers one monotonic clock implementation without coupling the core to its concrete type.</summary>
    public static Boolean Register(
        KernelClockSource source,
        Int32 priority,
        delegate*<Boolean> initialize,
        delegate*<UInt64> getNanoseconds,
        delegate*<UInt64*,UInt64*,Boolean> tryReadRaw,
        delegate*<UInt64> getFrequencyHz,
        delegate*<UInt64> getPeriodFemtoseconds)
    {
        if (source == KernelClockSource.None || initialize == null || getNanoseconds == null || tryReadRaw == null || getFrequencyHz == null || getPeriodFemtoseconds == null) return false;
        if (IsRegistered(source)) return false;
        Registration value = new Registration
        {
            Source = source,
            Priority = priority,
            Registered = 1,
            Ready = 0,
            Initialize = initialize,
            GetNanoseconds = getNanoseconds,
            TryReadRaw = tryReadRaw,
            GetFrequencyHz = getFrequencyHz,
            GetPeriodFemtoseconds = getPeriodFemtoseconds
        };
        if (_first.Registered == 0) { _first = value; return true; }
        if (_second.Registered == 0) { _second = value; return true; }
        return false;
    }

    /// <summary>Initializes registered sources in dependency-friendly priority order and selects the highest-priority usable source.</summary>
    internal static Boolean InitializeRegisteredSources()
    {
        _active = KernelClockSource.None;
        if (_first.Registered != 0 && _second.Registered != 0)
        {
            if (_first.Priority <= _second.Priority) { Initialize(ref _first); Initialize(ref _second); }
            else { Initialize(ref _second); Initialize(ref _first); }
        }
        else
        {
            if (_first.Registered != 0) Initialize(ref _first);
            if (_second.Registered != 0) Initialize(ref _second);
        }
        if (_first.Ready == 0 && _second.Ready == 0) return false;
        if (_second.Ready == 0 || (_first.Ready != 0 && _first.Priority >= _second.Priority)) _active = _first.Source;
        else _active = _second.Source;
        return true;
    }

    internal static KernelClockSource GetActiveSource() => _active;
    internal static Boolean IsReady(KernelClockSource source) => TryGet(source, out Registration entry) && entry.Ready != 0;
    internal static UInt64 GetFrequencyHz(KernelClockSource source) => TryGet(source, out Registration entry) && entry.Ready != 0 ? entry.GetFrequencyHz() : 0UL;
    internal static UInt64 GetPeriodFemtoseconds(KernelClockSource source) => TryGet(source, out Registration entry) && entry.Ready != 0 ? entry.GetPeriodFemtoseconds() : 0UL;
    internal static UInt64 GetActiveFrequencyHz() => GetFrequencyHz(_active);
    internal static UInt64 GetActiveNanoseconds() => TryGet(_active, out Registration entry) && entry.Ready != 0 ? entry.GetNanoseconds() : 0UL;
    internal static Boolean TryGetSourceNanoseconds(KernelClockSource source, out UInt64 nanoseconds)
    {
        nanoseconds = 0UL;
        if (!TryGet(source, out Registration entry) || entry.Ready == 0) return false;
        nanoseconds = entry.GetNanoseconds();
        return true;
    }
    internal static Boolean TryReadActiveRaw(out UInt64 ticks, out UInt64 frequencyHz)
    {
        ticks = 0UL; frequencyHz = 0UL;
        if (!TryGet(_active, out Registration entry) || entry.Ready == 0) return false;
        UInt64 localTicks = 0UL, localFrequency = 0UL;
        if (!entry.TryReadRaw(&localTicks, &localFrequency)) return false;
        ticks = localTicks; frequencyHz = localFrequency; return true;
    }

    private static void Initialize(ref Registration entry)
    {
        if (entry.Registered == 0) return;
        entry.Ready = entry.Initialize() ? (Byte)1 : (Byte)0;
    }

    private static Boolean IsRegistered(KernelClockSource source) => (_first.Registered != 0 && _first.Source == source) || (_second.Registered != 0 && _second.Source == source);
    private static Boolean TryGet(KernelClockSource source, out Registration entry)
    {
        if (_first.Registered != 0 && _first.Source == source) { entry = _first; return true; }
        if (_second.Registered != 0 && _second.Source == source) { entry = _second; return true; }
        entry = default; return false;
    }
}
