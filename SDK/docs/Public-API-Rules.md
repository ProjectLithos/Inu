# Inu user-facing API rules

Inu has one user-facing SDK contract. It is deliberately smaller than the set of public C# declarations used internally by generated operating-system source.

1. The command API contains only commands accepted by the canonical `inu` executable.
2. A C# declaration is not SDK API merely because it is `public`.
3. A coding API declaration is published only when its XML documentation explicitly contains `<inu.api>`.
4. Generated API documentation must omit unmarked declarations.
5. Kath must consume the generated explicit contract rather than inventing or maintaining a second list.
6. If an advertised API item cannot be compiled/used by an external OS project, that is a conformance failure.

## First explicit userland APIs

`System.Console` is the preferred coder-facing ring-3 text console. Inu supplies this freestanding .NET-compatible type itself; it does not depend on the desktop .NET runtime. `Console.Write`, `Console.WriteLine`, and `Console.Clear` cross the kernel boundary through the existing Event syscall rather than linking to `Inu.Kernel.Console`.

`Inu.Userland.Runtime.Output` remains available as a compatibility surface for source written against Inu 0.0.58, but new ordinary C# userland source should prefer `System.Console`. Kernel console classes are not a userland SDK API.
## Standard .NET-compatible namespaces

Inu may deliberately export familiar `System.*` APIs from its own freestanding CoreLib. These are Inu implementations compiled with NativeAOT; they do not introduce a dependency on the desktop .NET runtime.

The first supported standard namespaces are:

- `System` — including the ring-3 `Console` surface where applicable.
- `System.Collections` — enumeration and basic collection contracts.
- `System.Collections.Generic` — `List<T>`, `Dictionary<TKey,TValue>`, `Queue<T>`, `Stack<T>` and their core interfaces/helpers.
- `System.Text` — `StringBuilder` plus the initial `Encoding`, `ASCIIEncoding`, and `UTF8Encoding` surface.
- `System.IO` — initial ring-3 whole-file operations through the OS-selected filesystem service.

A `System.*` type is still not an Inu SDK promise merely because it happens to exist in CoreLib. It must carry the same explicit `<inu.api>` export marker as every other coder-facing API.


## System.Text baseline

The initial `System.Text` SDK contract is deliberately small and freestanding. `StringBuilder` owns a growable UTF-16 buffer and materializes normal managed `System.String` instances through Inu's NativeAOT string allocator. `Encoding.ASCII` and `Encoding.UTF8` provide whole-string/whole-array conversion without depending on desktop .NET globalization or I/O services.

This first tranche intentionally does not promise culture-aware formatting, encoder/decoder streaming objects, normalization, code pages, or arbitrary legacy encodings. Those remain outside the coder-facing SDK until implemented and conformance-tested.


## System.IO baseline

The first `System.IO` tranche deliberately covers whole-file operations: `File.Exists`, `ReadAllBytes`, `ReadAllText`, `WriteAllBytes`, `WriteAllText`, and `Delete`, with `IOException` and `FileNotFoundException`. The surface is implemented by Inu but crosses the normal Get/Set/Event user/kernel boundary; it does not link userland to kernel VFS implementation classes.

`System.IO` does not choose the filesystem format, path separator, case sensitivity, fixed directories, visibility rules, or permissions policy. The path string is owned by the OS policy and is passed to the selected VFS/filesystem implementation. The initial syscall transport accepts ASCII paths; Unicode path transport is not yet part of the SDK contract.
