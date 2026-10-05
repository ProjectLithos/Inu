# Inu Power Management API

Inu 0.23.0 defines one formal power-management boundary above ACPI, SMP and device drivers.

## System power

`KernelPowerManagement` provides ACPI-backed shutdown, reboot and sleep operations. Shutdown uses the FADT/AML `_S5` package and PM1 control registers. Reboot uses the FADT reset register. Sleep discovers `_S1`, `_S3` and `_S4` packages from the DSDT. S1 can retain the executing context directly. S3/S4 are not attempted until a physical firmware waking vector has been registered in the FACS, preventing Inu from entering a context-losing state without a resume path.

## CPU power states

The public CPU contract names C0 through C3. Inu 0.23.0 implements architectural C1 for the current x64 CPU with `HLT`, returning after an interrupt. C2/C3 remain capability-gated until an ACPI processor power provider (for `_CST`/FFH or equivalent platform data) is present; the API reports them as unsupported rather than emulating them.

## Device suspend and resume

Power transitions are coordinated through `KernelDrivers`. A driver may provide its existing `Suspend` and `Resume` lifecycle callbacks. System sleep/shutdown first suspends all started devices. Partial failure is rolled back: if suspension fails, Inu resumes devices already suspended. Resume walks suspended devices in reverse registry order.

## API summary

- `KernelPowerManagement.Initialize()`
- `KernelPowerManagement.GetCapabilities()`
- `KernelPowerManagement.Shutdown()`
- `KernelPowerManagement.Reboot()`
- `KernelPowerManagement.Sleep(...)`
- `KernelPowerManagement.ConfigureResumeVector(...)`
- `KernelPowerManagement.TryGetCpuPowerState(...)`
- `KernelPowerManagement.TrySetCpuPowerState(...)`
- `KernelPowerManagement.EnterCurrentCpuIdle(...)`
- `KernelPowerManagement.SuspendDevice(...)`
- `KernelPowerManagement.ResumeDevice(...)`

The ACPI layer owns firmware registers and sleep-type discovery. The power coordinator owns transition policy. Device-specific state saving/restoration remains in the driver that owns the device.
