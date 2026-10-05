# Inu / Kath 0.2.18

RTC/CMOS and Local APIC timer componentisation.

- Removes the legacy public `KernelRtcCmos.Initialize()` API; the concrete RTC provider exposes registration only.
- Adds `KernelWallClockSourceServices` as the source-neutral optional wall-clock provider boundary.
- Moves RTC/CMOS lifecycle behind `KernelRtcCmosProvider` registration and removes concrete RTC references from `KernelTime` and `KernelWallClock`.
- Adds `KernelInterruptTimerServices` as the source-neutral programmable interrupt-timer boundary.
- Moves Local APIC MMIO/calibration/programming state out of `KernelTime` into `KernelLocalApicTimerProvider`.
- Adds selectable component definitions for RTC/CMOS wall-clock and Local APIC interrupt timer.
- Adds default HAL selection symbols for both providers while preserving OS-author composition control.
