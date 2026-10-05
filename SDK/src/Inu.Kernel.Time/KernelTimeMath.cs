using System;

namespace Inu.Kernel.Time;

/// <summary>Provides overflow-aware integer timing conversions shared by clocks, calibration, and timer programming.</summary>
public static class KernelTimeMath
{
    /// <summary>Converts HPET counter ticks to nanoseconds using the ACPI-advertised femtosecond period.</summary>
    public static UInt64 HpetTicksToNanoseconds(UInt64 ticks, UInt64 periodFemtoseconds)
    {
        if (periodFemtoseconds == 0UL) return 0UL;
        UInt64 whole = ticks / 1000000UL;
        UInt64 remainder = ticks % 1000000UL;
        if (whole != 0UL && periodFemtoseconds > 0xFFFFFFFFFFFFFFFFUL / whole) return 0xFFFFFFFFFFFFFFFFUL;
        UInt64 high = whole * periodFemtoseconds;
        UInt64 low = (remainder * periodFemtoseconds) / 1000000UL;
        return 0xFFFFFFFFFFFFFFFFUL - high < low ? 0xFFFFFFFFFFFFFFFFUL : high + low;
    }

    /// <summary>Converts elapsed ticks and elapsed nanoseconds into an integer frequency in hertz.</summary>
    public static UInt64 CalibrateFrequency(UInt64 elapsedTicks, UInt64 elapsedNanoseconds)
    {
        if (elapsedTicks == 0UL || elapsedNanoseconds == 0UL) return 0UL;
        UInt64 whole = elapsedTicks / elapsedNanoseconds;
        UInt64 remainder = elapsedTicks % elapsedNanoseconds;
        if (whole > 0xFFFFFFFFFFFFFFFFUL / 1000000000UL) return 0xFFFFFFFFFFFFFFFFUL;
        UInt64 high = whole * 1000000000UL;
        UInt64 low = (remainder * 1000000000UL) / elapsedNanoseconds;
        return 0xFFFFFFFFFFFFFFFFUL - high < low ? 0xFFFFFFFFFFFFFFFFUL : high + low;
    }

    /// <summary>Converts nanoseconds to timer ticks, rounding upward so a programmed timer never expires early.</summary>
    public static UInt64 NanosecondsToTicksCeiling(UInt64 nanoseconds, UInt64 frequencyHz)
    {
        if (nanoseconds == 0UL || frequencyHz == 0UL) return 0UL;
        UInt64 whole = nanoseconds / 1000000000UL;
        UInt64 remainder = nanoseconds % 1000000000UL;
        if (whole != 0UL && frequencyHz > 0xFFFFFFFFFFFFFFFFUL / whole) return 0xFFFFFFFFFFFFFFFFUL;
        UInt64 high = whole * frequencyHz;
        UInt64 product = remainder * frequencyHz;
        UInt64 low = product / 1000000000UL;
        if ((product % 1000000000UL) != 0UL) low++;
        return 0xFFFFFFFFFFFFFFFFUL - high < low ? 0xFFFFFFFFFFFFFFFFUL : high + low;
    }

    /// <summary>Converts source ticks to nanoseconds using a calibrated source frequency.</summary>
    public static UInt64 TicksToNanoseconds(UInt64 ticks, UInt64 frequencyHz)
    {
        if (frequencyHz == 0UL) return 0UL;
        UInt64 whole = ticks / frequencyHz;
        UInt64 remainder = ticks % frequencyHz;
        if (whole > 0xFFFFFFFFFFFFFFFFUL / 1000000000UL) return 0xFFFFFFFFFFFFFFFFUL;
        UInt64 high = whole * 1000000000UL;
        UInt64 low = (remainder * 1000000000UL) / frequencyHz;
        return 0xFFFFFFFFFFFFFFFFUL - high < low ? 0xFFFFFFFFFFFFFFFFUL : high + low;
    }
}
