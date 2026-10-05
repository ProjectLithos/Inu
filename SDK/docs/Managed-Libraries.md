# Inu managed library architecture

Inu separates the NativeAOT runtime ABI from the managed library surface. `Inu.Freestanding.CoreLib` owns the fundamental `System.*` types and members required by C#, Roslyn and ILC. Allocation, reference barriers and later GC/type-system mechanics belong to `Inu.Runtime.NativeAot` rather than being accumulated as unrelated CoreLib stubs.

Higher-level functionality belongs in selectable assemblies. `Inu.String` provides freestanding value formatting, and peers such as `Inu.Math`, `Inu.Hashing`, `Inu.Cryptography`, `Inu.Collections`, `Inu.IO`, and `Inu.Threading` should be implemented as real facilities rather than placeholder assemblies.

## Runtime transition in 0.42.0

Early boot remains allocation-free while Inu establishes physical memory, virtual memory, the kernel address space, the early allocator and `KernelHeap`. `Inu.Runtime.NativeAot.Initialize()` then ends `NoGcBootstrap` and enters the managed NativeAOT phase.

The first managed phase provides object/array/string allocation entry points and managed-reference write barriers over a non-moving, non-generational heap. See `Managed-Runtime.md` for the ABI, executable conformance gate and current limitations.

## Boolean and string output

The 0.42.0 runtime has a real `RhNewString` allocation path, but general `String.Concat`, formatting and exception-backed string APIs are not yet implemented in the freestanding CoreLib. Runtime code should continue to use value-aware console overloads when it would otherwise require unsupported formatting infrastructure:

```csharp
Boolean ecReady = KernelAcpiEcServices.Initialize();
KernelConsole.WriteLine("ecReady Status = ", ecReady);
```

`Boolean.ToString()` returns the normal `True` or `False` literals without allocation.

General runtime `string + value` support should be enabled only together with the appropriate CoreLib implementation and conformance tests. It must not be implemented with mutable string literals, hidden global scratch strings, or other non-.NET semantics.
