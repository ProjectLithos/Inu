using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Time;

/// <summary>Represents one stable calendar sample read from the PC-compatible RTC/CMOS.</summary>
public readonly struct KernelRtcDateTime
{
    /// <summary>Creates a calendar sample.</summary>
    public KernelRtcDateTime(UInt16 year, Byte month, Byte day, Byte hour, Byte minute, Byte second, Byte dayOfWeek)
    { Year = year; Month = month; Day = day; Hour = hour; Minute = minute; Second = second; DayOfWeek = dayOfWeek; }
    /// <summary>Gets the Gregorian year.</summary>
    public UInt16 Year { get; }
    /// <summary>Gets the month from 1 through 12.</summary>
    public Byte Month { get; }
    /// <summary>Gets the day of month.</summary>
    public Byte Day { get; }
    /// <summary>Gets the hour in 24-hour form.</summary>
    public Byte Hour { get; }
    /// <summary>Gets the minute.</summary>
    public Byte Minute { get; }
    /// <summary>Gets the second.</summary>
    public Byte Second { get; }
    /// <summary>Gets the firmware day-of-week value when supplied, normally 1 through 7.</summary>
    public Byte DayOfWeek { get; }
}

/// <summary>Describes the initialized RTC/CMOS facility.</summary>
public readonly struct KernelRtcCapabilities
{
    /// <summary>Creates an RTC capability snapshot.</summary>
    public KernelRtcCapabilities(Boolean available, Boolean centuryRegister, Boolean binaryMode, Boolean twentyFourHourMode)
    { Available = available; HasCenturyRegister = centuryRegister; BinaryMode = binaryMode; TwentyFourHourMode = twentyFourHourMode; }
    /// <summary>Gets whether stable RTC/CMOS reads are available.</summary>
    public Boolean Available { get; }
    /// <summary>Gets whether ACPI supplies the CMOS century register index.</summary>
    public Boolean HasCenturyRegister { get; }
    /// <summary>Gets whether the RTC stores values in binary rather than packed BCD.</summary>
    public Boolean BinaryMode { get; }
    /// <summary>Gets whether the RTC stores hours in 24-hour format.</summary>
    public Boolean TwentyFourHourMode { get; }
}

/// <summary>Provides stable, format-aware reads from the PC-compatible RTC/CMOS calendar.</summary>
public static unsafe class KernelRtcCmosProvider
{
    private const UInt16 IndexPort = 0x70;
    private const UInt16 DataPort = 0x71;
    private const Byte StatusA = 0x0A;
    private const Byte StatusB = 0x0B;
    private const Byte UpdateInProgress = 0x80;
    private const UInt32 WaitLimit = 1000000U;
    private static Boolean _initialized, _binaryMode, _twentyFourHourMode;
    private static Byte _centuryRegister;

    /// <summary>Registers this optional CMOS calendar provider with the wall-clock source registry.</summary>
    public static Boolean Register() => KernelWallClockSourceServices.Register(&InitializeProvider, &TryReadProvider);

    private static Boolean InitializeProvider()
    {
        _initialized = false;
        _centuryRegister = 0;
        if (KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo fadt)) _centuryRegister = fadt.CenturyRegister;
        if (!WaitForStableWindow() || !TryReadRegister(StatusB, out Byte status)) return false;
        _binaryMode = (status & 0x04) != 0;
        _twentyFourHourMode = (status & 0x02) != 0;
        _initialized = true;
        KernelRtcDateTime sample = default;
        if (!TryReadProvider(&sample)) { _initialized = false; return false; }
        return true;
    }

    /// <summary>Gets whether RTC/CMOS initialization succeeded.</summary>
    private static Boolean IsInitialized() => _initialized;

    /// <summary>Gets an RTC/CMOS capability snapshot.</summary>
    private static KernelRtcCapabilities GetCapabilities() => new(_initialized, _centuryRegister != 0, _binaryMode, _twentyFourHourMode);

    /// <summary>Reads a stable Gregorian calendar sample from RTC/CMOS.</summary>
    private static Boolean TryReadProvider(KernelRtcDateTime* output)
    {
        if (output == null || !_initialized) return false;
        for (Byte attempt = 0; attempt < 8; attempt++)
        {
            if (!WaitForStableWindow() || !TryReadRaw(out RtcRaw first)) return false;
            if (!WaitForStableWindow() || !TryReadRaw(out RtcRaw second)) return false;
            if (!first.Equals(second)) continue;
            if (!TryReadRegister(StatusB, out Byte status)) return false;
            Boolean binary = (status & 0x04) != 0;
            Boolean hour24 = (status & 0x02) != 0;
            Byte secondValue = binary ? second.Second : KernelRtcMath.BcdToBinary(second.Second);
            Byte minuteValue = binary ? second.Minute : KernelRtcMath.BcdToBinary(second.Minute);
            Byte hourValue = KernelRtcMath.DecodeHour(second.Hour, binary, hour24);
            Byte dayValue = binary ? second.Day : KernelRtcMath.BcdToBinary(second.Day);
            Byte monthValue = binary ? second.Month : KernelRtcMath.BcdToBinary(second.Month);
            UInt16 yearValue = KernelRtcMath.DecodeYear(second.Year, second.Century, _centuryRegister != 0, binary);
            Byte weekdayValue = binary ? second.DayOfWeek : KernelRtcMath.BcdToBinary(second.DayOfWeek);
            if (!KernelRtcMath.IsValid(yearValue, monthValue, dayValue, hourValue, minuteValue, secondValue)) return false;
            *output = new KernelRtcDateTime(yearValue, monthValue, dayValue, hourValue, minuteValue, secondValue, weekdayValue);
            return true;
        }
        return false;
    }

    private static Boolean TryReadRaw(out RtcRaw value)
    {
        value = default;
        if (!TryReadRegister(0x00, out Byte second) || !TryReadRegister(0x02, out Byte minute) || !TryReadRegister(0x04, out Byte hour) ||
            !TryReadRegister(0x06, out Byte dayOfWeek) || !TryReadRegister(0x07, out Byte day) || !TryReadRegister(0x08, out Byte month) || !TryReadRegister(0x09, out Byte year)) return false;
        Byte century = 0;
        if (_centuryRegister != 0 && !TryReadRegister(_centuryRegister, out century)) return false;
        value = new RtcRaw(second, minute, hour, dayOfWeek, day, month, year, century);
        return true;
    }

    private static Boolean WaitForStableWindow()
    {
        for (UInt32 i = 0U; i < WaitLimit; i++)
        {
            if (!TryReadRegister(StatusA, out Byte status)) return false;
            if ((status & UpdateInProgress) == 0) return true;
            Native.Pause();
        }
        return false;
    }

    private static Boolean TryReadRegister(Byte register, out Byte value)
    {
        value = 0;
        return Native.WritePort8(IndexPort, (Byte)(register & 0x7F)) && Native.ReadPort8(DataPort, out value);
    }

    private readonly struct RtcRaw
    {
        internal RtcRaw(Byte second, Byte minute, Byte hour, Byte dayOfWeek, Byte day, Byte month, Byte year, Byte century)
        { Second = second; Minute = minute; Hour = hour; DayOfWeek = dayOfWeek; Day = day; Month = month; Year = year; Century = century; }
        internal Byte Second { get; }
        internal Byte Minute { get; }
        internal Byte Hour { get; }
        internal Byte DayOfWeek { get; }
        internal Byte Day { get; }
        internal Byte Month { get; }
        internal Byte Year { get; }
        internal Byte Century { get; }
        internal Boolean Equals(RtcRaw other) => Second == other.Second && Minute == other.Minute && Hour == other.Hour && DayOfWeek == other.DayOfWeek && Day == other.Day && Month == other.Month && Year == other.Year && Century == other.Century;
    }
}
