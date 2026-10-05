# Inu / Kath 0.0.200

## USB bus compile repair for the GUI input integration

0.0.200 repairs the managed-build regression discovered immediately after the 0.0.199 GUI work.

`KernelUsbBus.AddDevice` receives the selected USB configuration as its `config` parameter. The 0.0.199 source correctly stored that parameter in the device record, but one `KernelDevicePropertyKey.Flags` publication accidentally referenced a non-existent local named `configuration`. That caused C# error `CS0103` before NativeAOT ILC could run.

The property publication now uses `config`, matching the method parameter and the already-correct `r->Configuration=config` assignment. A Kath source verifier has also been added to reject the stale out-of-scope identifier if this path regresses again.

No GUI ABI or syscall design changes are made in this repair: GUI applications remain ordinary isolated ring-3 processes and continue to use only **Get / Set / Event**.
