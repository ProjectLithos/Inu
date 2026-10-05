# Inu Timekeeping Framework

Inu OS SDK 0.42.0 separates timekeeping into five independent kernel services.

## Monotonic clock
`KernelMonotonicClock` is the authoritative source for elapsed time and deadlines. It uses the calibrated invariant TSC when available and HPET otherwise. Wall-clock correction never changes monotonic deadlines.

## Wall clock
`KernelWallClock` samples the RTC/CMOS during initialization, converts the Gregorian value to Unix nanoseconds without a managed DateTime dependency, then advances that anchor with monotonic time. `TrySetUnixNanoseconds` corrects wall time without altering monotonic time.

## High-resolution timer
`KernelHighResolutionTimer` exposes the raw active counter and calibrated frequency for profiling and precision measurements. It is separate from scheduler policy and wall time.

## Scheduler tick
`KernelSchedulerTick` is advanced by `KernelTimerDispatch` for each Local APIC scheduling/service interrupt. Tick count and period are observable independently from the monotonic clock.

## Timeout service
`KernelTimeoutService` is an allocation-free 64-slot monotonic deadline registry. Callers schedule absolute or relative deadlines, cancel by generation-tagged ID, dequeue expired timeouts, and query the earliest pending deadline. Timeouts never depend on wall-clock time.

## Async work
The timeout service is synchronous notification infrastructure. Future asynchronous I/O can use it for deadlines without changing the timekeeping ABI.
