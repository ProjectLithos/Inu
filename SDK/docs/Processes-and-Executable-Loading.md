# Processes and executable loading

Inu 0.0.173 hardens the freestanding x64 process/userland execution path for ordinary applications and concurrent ring-3 execution.

`Inu.Kernel.Processes` accepts an executable already present in memory and validates either a System V x86-64 ELF64 `ET_EXEC` image or a Microsoft x86-64 PE32+ image. Named-file loading is available through the mounted VFS, while in-memory images and canonical Inu application packages remain supported directly.

Each process receives a private lower-half four-level page-table hierarchy. The higher-half kernel entries are shared from the active kernel root, preserving kernel services and the per-CPU syscall stack while preventing one process from inheriting another process's user mappings. Loadable image pages are backed by PMM allocations, zero-filled before file bytes are copied, and mapped with permissions derived from the executable. A 1 MiB non-executable user stack is created near the top of the canonical user half.

Before any process pages are allocated, the loader validates the complete mapped section layout. Distinct ELF/PE sections may not overlap the same user page and writable+executable (W+X) sections are rejected. Unsupported architecture, ABI/personality, malformed package, package policy and entry-point failures are classified with stable `InuApplicationLoadError` values.

The public API uses ordinary .NET-style value types and `Boolean`/value returns: `KernelProcesses.Initialize`, `TryCreateFromImage`, `TryCreateFromFile`, `TryGetProcess`, `TryTerminate`, and `TryStart`. Creation accepts explicit `Foreground` or `Background` ownership. `ProcessExecutableMath` provides deterministic, allocation-free ELF64/PE32+ inspection and segment decoding for custom loaders and tests.

Process lifecycle reservation uses `Loading` and `Terminating` states so a slot cannot be double-claimed or its address-space resources double-released. Execution, pending completion and pending forced-kill state are tracked per CPU. `TryStart` can therefore run different ready processes on different CPUs instead of serialising the whole kernel behind one global running PID. The active syscall process identity is also per CPU.

`TryStart` switches CR3 to the process root and uses the native x64 `IRETQ` ring-3 transition. Ordinary calls return with `SYSRET`; controlled completion and forced termination return through the saved kernel continuation, restore the kernel CR3, revoke process security/capability state and release process-owned pages. Faulted processes are contained and their owned mappings are released before their diagnostic record is retained.

The fixed-image loader deliberately accepts only self-contained x64 ELF64 ET_EXEC or PE32+ images. PIE/ET_DYN, relocation-dependent images, dynamic ELF linking and PE import resolution are rejected rather than executed incorrectly. Canonical Inu packages select one syscall ABI and may carry validated resources/metadata; signed packages remain reserved until signature verification is introduced.
