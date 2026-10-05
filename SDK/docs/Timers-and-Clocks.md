# Timers and clocks

Inu 0.23.0 provides the x64 timing stack in `Inu.Kernel.Time` while keeping CPUID, port I/O, RDTSC, APIC MMIO, and HPET MMIO out of generated application/kernel code.

## HPET

`KernelHpetClockSource` owns ACPI HPET discovery, MMIO enablement, counter width/period state, and monotonic conversion. It registers through `KernelClockSourceServices`; `KernelTime` consumes only the clock-source contract. `KernelHpet` remains the high-level availability, frequency, and counter-period facade.

HPET remains the fallback monotonic source if an invariant TSC cannot be selected.

## TSC and invariant-TSC clock source

Inu separately detects ordinary TSC support and invariant-TSC support through CPUID. `KernelTsc` exposes TSC availability, invariant status, the HPET-calibrated frequency, and a serialized raw counter read for facilities that explicitly require it.

An invariant TSC is not promoted to the monotonic clock merely because CPUID advertises it. `KernelInvariantTscClockSource` calibrates through the registered HPET calibration-reference contract over a bounded interval and becomes eligible only when the resulting frequency is valid. The clock core then selects the highest-priority initialized provider; otherwise HPET remains active.

`KernelTime.GetMonotonicNanoseconds()` is source-independent, so higher-level code does not need to know whether HPET or invariant TSC supplied the reading.

## Local APIC timer

When MADT supplies a Local APIC base, Inu calibrates the Local APIC timer against HPET using divide-by-16. `KernelLocalApicTimer` then exposes calibrated frequency, one-shot interrupts, periodic interrupts, and cancellation.

Vectors below 32 are rejected because they are reserved for processor exceptions. This processor-local timer remains separate from the opaque driver interrupt broker: device drivers still do not care whether their interrupts arrive via I/O APIC, MSI, or MSI-X.

## RTC / CMOS

`KernelRtcCmosProvider` provides the optional PC-compatible real-time calendar source through `KernelWallClockSourceServices`. It waits for the CMOS update-in-progress window to clear, reads two complete samples, and accepts the time only when both samples match. This prevents mixed calendar fields across an RTC rollover.

The reader supports both packed BCD and binary CMOS formats, both 12-hour and 24-hour modes, and converts the result to a validated `KernelRtcDateTime`. When ACPI FADT supplies the RTC century-register index, Inu uses it to form the full Gregorian year. Firmware without a century register uses the conventional 1970/2000 pivot for the two-digit year.

RTC/CMOS is a wall-calendar facility. It is not used as the kernel monotonic clock because RTC resolution and update behaviour are unsuitable for scheduling and interval measurement.

## Capability model

`KernelTime.GetCapabilities()` reports:

- HPET availability;
- TSC availability;
- invariant-TSC support;
- RTC/CMOS availability;
- selected monotonic source and its frequency;
- calibrated Local APIC timer availability and frequency.

Named façade classes (`KernelHpet`, `KernelTsc`, `KernelWallClock`, and `KernelLocalApicTimer`) let SDK users consume one facility without exposing low-level x64 programming in `Kernel.cs`.

## Integer timing methodology

Inu keeps timing conversion arithmetic integer-only and overflow-aware:

- HPET ticks to nanoseconds use the ACPI femtosecond period with quotient/remainder splitting;
- calibration uses elapsed ticks and elapsed nanoseconds to derive integer hertz;
- nanoseconds to Local APIC timer ticks rounds upward so hardware does not expire before the requested interval;
- calibrated source ticks convert to nanoseconds without floating point;
- RTC packed BCD, 12-hour PM, century, leap-year, and date-range conversions are isolated in `KernelRtcMath` and exercised by standalone tests.
