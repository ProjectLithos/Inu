# Inu managed NativeAOT runtime

Inu SDK 0.42.0 introduces an explicit two-phase managed runtime for x64 kernels.

```text
UEFI / native entry
        |
        v
NoGcBootstrap
  descriptors / interrupts / ACPI
  physical + virtual memory
  kernel address space
  early allocator
  KernelHeap
        |
        v
Inu.Runtime.NativeAot.Initialize()
        |
        v
ManagedNativeAot
        |
        +--> Inu.Runtime.Conformance
        |
        +--> graphics / drivers / scheduler / userland
```

`NoGcBootstrap` is no longer the permanent execution model. It is the deliberately allocation-free stage required to reach the kernel heap safely. Once `KernelHeap` is online, `Inu.Runtime.NativeAot` reserves the first managed segment and exposes the NativeAOT helper ABI used by ILC-generated code.

## Runtime ownership

The managed runtime is implemented in `SDK/src/Inu.Runtime.NativeAot`. Runtime helpers no longer belong in `Inu.Freestanding.CoreLib` simply because ILC asks for another symbol. CoreLib supplies the language/runtime type surface; `Inu.Runtime.NativeAot` supplies allocation and managed-reference mechanics.

The 0.42.0 x64 runtime exports:

- `RhpNewFast`, `RhpNewFinalizable`, and `RhpNewObject` for fixed-size managed objects;
- `RhpNewArray`, `RhpNewArrayFast`, `RhpNewPtrArrayFast`, and `RhpNewVariableSizeObject` for variable-sized managed objects;
- `RhNewString` for the NativeAOT string allocation ABI;
- `RhpGcAlloc` as the common slow-path allocation ABI;
- `RhpAssignRef`, `RhpCheckedAssignRef`, and `RhpByRefAssignRef` for managed-reference writes.

Object sizing follows NativeAOT MethodTable metadata: the runtime reads the MethodTable base size and component size, aligns the allocation, zeroes it, writes the MethodTable pointer at object offset zero, and writes array/string length at the NativeAOT array-length slot.

## Phase-one managed heap

0.42.0 deliberately starts with a simple collector boundary rather than pretending that a full GC already exists.

The managed heap is:

- backed by `KernelHeap`;
- non-moving;
- non-generational;
- segmented and growable in 4 MiB chunks;
- bounded to 64 MiB in the initial implementation;
- protected by the x64 atomic allocation gate so multiple CPUs cannot advance a managed segment concurrently.

Because the phase-one heap neither moves objects nor performs generational collection, a managed reference barrier is currently a direct pointer store. The barrier ABI is isolated in `Inu.Runtime.NativeAot` so card marking or relocation support can be added later without changing generated callers or CoreLib.

This heap does **not** reclaim objects yet. Exhaustion is a runtime failure. A tracing collector, root enumeration, generations, finalization and compaction are later conformance milestones.

## CoreLib changes

`System.Object` follows the .NET 10 NativeAOT Runtime.Base data contract: it explicitly declares one `Internal.Runtime.MethodTable*` field, which the compiler/runtime treat as the object MethodTable pointer at offset zero. Its finalizer is declared before every other virtual method so Object owns NativeAOT vtable slot 0.
NativeAOT physical vtables are dependency-driven and may omit unused trailing slots in a trimmed image; Inu therefore source-locks the logical Object slot order and validates only the slots physically emitted into each MethodTable.

`System.Buffer.BulkMoveWithWriteBarrier` no longer spins forever. With the non-moving/non-generational 0.42.0 heap it uses the same overlap-safe byte move as the freestanding block-copy helper.

`System.Nullable<T>` now has the normal `hasValue` + `value` field layout together with `HasValue`, `Value`, `GetValueOrDefault`, and conversion operators. `Value` cannot yet throw the normal `InvalidOperationException` for an empty nullable because exception dispatch is not part of the 0.42.0 runtime phase.

## Executable conformance

`Inu.Runtime.Conformance` runs inside the kernel immediately after runtime initialization. Boot is rejected if any current managed-runtime invariant fails. The 0.42.0 gate exercises:

- managed object allocation;
- distinct object identity and `ReferenceEquals`;
- object field initialization;
- reference-field assignment through the NativeAOT write barrier;
- value-array allocation, length, zeroing and indexed reads/writes;
- `Array.Empty<T>()`;
- `Nullable<T>` present/empty layout and value behaviour;
- runtime accounting and phase state;
- x64 object MethodTable pointer at offset `+0` and first vtable slot after the 24-byte MethodTable header;
- primitive compiler sizes (`Boolean`/integers/floats/native integers);
- SZ-array length/data offsets (`+8` / `+16`);
- string length/data offsets (`+8` / `+12`);
- MethodTable header field boundaries through byte 23.

BCL compatibility is now a fixed named target rather than an open-ended claim. The current target is **`Inu.BCL.Core.v1`**, documented in `BCL-Conformance-Target.md`. Every target item is exercised twice: once by the normal .NET 10 reference executable at `SDK/tests/Inu.DotNetConformance.Tests`, and once by the in-kernel `RunBclCoreV1Checks` validation gate. When Inu implementation code changes, the normal SDK build runs the reference-side conformance suite once for the new SDK-code fingerprint. Ordinary generated-OS Debug and Release boots never automatically carry the heavyweight in-kernel gate. `Inu.Runtime.Conformance` is rooted into the NativeAOT image only when explicit runtime conformance is requested. An item is not part of the advertised subset unless both sides contain the corresponding executable check. The primitive/runtime foundation is deliberately first: richer numeric formatting, integer/floating `Math` and `Convert`, primitive comparison/equality, the common managed delegate families, full Core-v1 array-backed `Span<T>`/`ReadOnlySpan<T>`, and `Memory<T>`/`ReadOnlyMemory<T>` are paired before higher-level BCL expansion.

Run the reference executable on the Windows SDK toolchain with:

```bat
SDK\Run-InuDotNetConformance.bat
```

## Deliberately outstanding .NET semantics

0.42.0 does not claim full .NET runtime compatibility. The following remain explicit runtime work rather than placeholder shims:

- reclaiming/tracing garbage collection and root enumeration;
- exception objects, throw/catch/finally and unwind integration;
- identity hashing beyond the legal collision-safe default;
- `System.Type`, complete type metadata and reflection;
- dynamic interface dispatch;
- finalizers;
- general managed string concatenation/formatting;
- boxing/unboxing conformance beyond the paths already exercised by the kernel;
- unmanaged/pointer-backed span and memory forms beyond the array-backed Core-v1 contract;
- broader collections, threading and task/runtime library surface.

Each of these should be added to the executable conformance suite before it is advertised as supported.
