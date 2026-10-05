# Dispatch and polling

Inu 0.23.0 separates device servicing methodology into three SDK assemblies.

- `Inu.Kernel.InterruptDispatch` owns managed interrupt vector registration and hardware interrupt dispatch.
- `Inu.Kernel.TimerDispatch` owns periodic work driven by the calibrated Local APIC timer.
- `Inu.Kernel.Polling` contains the explicit opt-in polling fallback methodology. It is not initialized by the generated default kernel.

The generated kernel services keyboard input and receive-side network work from timer interrupts and idles with `HLT` between interrupts. E1000/E1000e, RTL8168/RTL8111, and VirtIO network drivers expose service routines rather than public polling loops. Driver interrupt callbacks invoke the same service routines.

Bounded hardware handshakes used during controller reset, queue submission, calibration, or synchronous commands remain finite protocol waits; they are not background polling loops and are guarded by hardware/time limits.
