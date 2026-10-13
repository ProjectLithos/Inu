
> **Test OS profile (0.1.58):** Kath can generate an explicitly marked Test OS that contains the complete validation harness. A Test OS built/run without Debug boots normally and does not execute the in-kernel managed/BCL/GC/ABI conformance gate. Selecting Debug (or explicitly requesting runtime conformance) activates the in-kernel validation. Host-side .NET reference conformance remains an SDK-build fingerprint check when SDK implementation code changes.
- 0.1.40 completes Profile 1 `List<T>` mutation, capacity/indexing, self-`AddRange`, enumeration state/version and `ToArray` behaviour, with one shared conformance definition compiled by both reference and freestanding runners.
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
- Profile 1 `System.String` now includes Inu-required constructors, ordinal comparison/search/manipulation, trim, ASCII/invariant casing, split/join/concat, null helpers, and character enumeration;
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

BCL compatibility is now a fixed named target rather than an open-ended claim. The current target is **`Inu.BCL.Core.v1`**, documented in `BCL-Conformance-Target.md`. Every target item is exercised twice: once by the normal .NET 10 reference executable at `SDK/tests/Inu.DotNetConformance.Tests`, and once by the in-kernel `RunBclCoreV1Checks` validation gate. When Inu implementation code changes, the normal SDK build runs the reference-side conformance suite once for the new SDK-code fingerprint. Ordinary generated-OS Debug and Release boots never automatically carry the heavyweight in-kernel gate. `Inu.Runtime.Conformance` is rooted into the NativeAOT image only when explicit runtime conformance is requested. An item is not part of the advertised subset unless both sides contain the corresponding executable check. The primitive/runtime foundation is deliberately first: richer numeric formatting, integer/floating `Math` and `Convert`, primitive comparison/equality, the common managed delegate families, full Core-v1 `Span<T>`/`ReadOnlySpan<T>` plus the .NET 10 type-owned array-backed `Memory<T>`/`ReadOnlyMemory<T>` surface are paired before higher-level BCL expansion. Profile 1 now also includes the practical `System.Linq.Enumerable` subset: deferred `Select`/`Where`, element/quantifier/count operators, comparer-aware `Contains`, materialisation to arrays/lists, and stable deferred `OrderBy`.

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
- `System.String` constructor support includes the NativeAOT compiler companion contract: each supported public `String(...)` constructor has a matching private static `Ctor(...)` implementation for ILC string allocation lowering.


## StringBuilder Profile 1

`System.Text.StringBuilder` supports append/line append, insertion, removal, character and string replacement, clear, length/capacity management, indexing and materialisation through `ToString()`. Reference-side and in-kernel conformance cover mutation, growth and invalid argument paths.
## Encoding Profile 1

Profile 1 completes the requested `System.Text` encoding substrate for ASCII and UTF-8. The freestanding `Encoding`, `ASCIIEncoding`, and `UTF8Encoding` implementations provide property/name/code-page factories for ASCII (20127) and UTF-8 (65001), byte and UTF-16-character counts, allocating conversions, array-range conversions, caller-provided array buffers, and span-based encode/decode. ASCII uses `?` replacement outside 7-bit ASCII; UTF-8 uses replacement-character fallback for malformed scalar sequences.

UTF-16 is **not** a Profile 1 encoding API. Inu strings are still stored as UTF-16, but `UnicodeEncoding`, `Encoding.Unicode`, and `Encoding.BigEndianUnicode` are deferred together with BOM/endian policy, streaming `Encoder`/`Decoder`, configurable fallback objects, UTF-32 and broader code-page support.


## Profile 1 SZ-array and enumeration completion (0.1.38)

Profile 1 one-dimensional managed arrays now include compiler-enforced bounds checks, NativeAOT covariance/store checks, `Clone`, `Array.Clear`, overlap-safe `Array.Copy`, `Array.Empty<T>`, direct `foreach`, and both generic and non-generic interface enumeration. NativeAOT generic SZ-array interface dispatch is supplied through the compiler/runtime-known `SZArrayHelper` companion and a typed `SZGenericArrayEnumerator<T>`. Non-generic enumeration boxes value-type elements and enforces `IEnumerator.Current` state rules before the first element and after the end. `IEnumerable`, `IEnumerator`, `IEnumerable<T>`, and `IEnumerator<T>` are explicit Profile 1 conformance targets.

### Shared conformance definitions

Starting with 0.1.39, the completed `System.Array`, `System.Collections.IEnumerable`, `System.Collections.IEnumerator`, `System.Collections.Generic.IEnumerable<T>`, and `System.Collections.Generic.IEnumerator<T>` contract logic lives in one shared source file compiled into both conformance environments. The host runner remains the Microsoft .NET reference oracle; the in-kernel runner remains the Inu freestanding implementation check. Sharing the expectation body prevents duplicated constants and edge-case logic from drifting between the two suites.

Starting with 0.1.44, `TupleConformance.cs` applies the same shared-definition rule to `System.Tuple`, `System.ValueTuple`, and C# language tuples. The shared source deliberately contains named tuple signatures, deconstruction, equality and a nine-element tuple so Roslyn must consume Inu's `TupleElementNamesAttribute`, `ITuple`, and `ValueTuple<T1,...,T7,TRest>.Rest` compiler/runtime contracts rather than only calling tuple APIs manually.

