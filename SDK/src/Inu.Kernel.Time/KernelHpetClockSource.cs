using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Time;

/// <summary>Selectable ACPI HPET monotonic clock implementation.</summary>
public static unsafe class KernelHpetClockSource
{
    private const UInt64 HpetConfigOffset = 0x10UL;
    private const UInt64 HpetCounterOffset = 0xF0UL;
    private static UInt64 _baseAddress, _periodFemtoseconds, _start;
    private static Boolean _counter64;

    /// <summary>Registers the HPET implementation with the kernel clock core.</summary>
    public static Boolean Register() => KernelClockSourceServices.Register(KernelClockSource.Hpet, 100, &Initialize, &GetNanoseconds, &TryReadRaw, &GetFrequencyHz, &GetPeriodFemtoseconds);

    private static Boolean Initialize()
    {
        if (!KernelAcpi.IsInitialized() || !KernelAcpi.TryGetHpet(out AcpiHpetInfo hpet) || hpet.AddressSpace != (Byte)0U) return false;
        _baseAddress = hpet.BaseAddress;
        UInt64 capabilities = Native.ReadMmio64(_baseAddress);
        _periodFemtoseconds = capabilities >> 32;
        _counter64 = (capabilities & (1UL << 13)) != 0UL;
        if (_periodFemtoseconds == 0UL || _periodFemtoseconds > 100000000UL) return false;
        UInt64 config = Native.ReadMmio64(_baseAddress + HpetConfigOffset);
        if (!Native.WriteMmio64(_baseAddress + HpetConfigOffset, config | 1UL)) return false;
        _start = ReadCounter();
        return true;
    }

    private static UInt64 GetNanoseconds() => KernelTimeMath.HpetTicksToNanoseconds(Delta(_start, ReadCounter()), _periodFemtoseconds);
    private static UInt64 GetFrequencyHz() => _periodFemtoseconds == 0UL ? 0UL : 1000000000000000UL / _periodFemtoseconds;
    private static UInt64 GetPeriodFemtoseconds() => _periodFemtoseconds;
    private static Boolean TryReadRaw(UInt64* ticks, UInt64* frequencyHz)
    {
        if (ticks == null || frequencyHz == null || _periodFemtoseconds == 0UL) return false;
        *ticks = Delta(_start, ReadCounter()); *frequencyHz = GetFrequencyHz(); return *frequencyHz != 0UL;
    }
    private static UInt64 ReadCounter() { UInt64 value = Native.ReadMmio64(_baseAddress + HpetCounterOffset); return _counter64 ? value : value & 0xFFFFFFFFUL; }
    private static UInt64 Delta(UInt64 start, UInt64 end) => _counter64 ? end - start : (UInt32)end - (UInt32)start;
}
