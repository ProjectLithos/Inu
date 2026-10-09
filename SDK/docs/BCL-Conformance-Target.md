# Inu BCL conformance target

Inu does not claim support for the whole .NET Base Class Library. The fixed compatibility target for this release is the named subset **`Inu.BCL.Core.v1`**.

A BCL item is part of this target only when the same item is exercised in both of these executable gates:

1. **Reference side:** `SDK/tests/Inu.DotNetConformance.Tests`, executed against the normal .NET 10 BCL by `SDK/Run-InuDotNetConformance.bat`.
2. **Kernel side:** `Inu.Runtime.Conformance.ManagedRuntimeConformance.RunBclCoreV1Checks`, executed inside the booted Inu kernel only when Inu detects SDK code that has changed since the last successful runtime validation (or when validation is explicitly requested). A failed item rejects that validation run; ordinary Debug and Release OS boots of an unchanged SDK do not execute the conformance suite.

The target is intentionally type-level rather than a claim that every API on a listed type is implemented. The APIs exercised by the paired gates are the supported contract. Extending the target requires extending both gates in the same change. The host/reference gate remains available independently of OS boot, while the in-kernel gate is SDK-change validation code rather than a normal or Debug startup prerequisite. Inu records the validated SDK-code fingerprint only after the runtime gate completes successfully.

## Inu.BCL.Core.v1

| # | BCL item | Contract exercised by both gates |
|---:|---|---|
| 1 | `System.Object` | construction, identity, `ReferenceEquals`, default `Equals`, default `ToString` |
| 2 | `System.Boolean` | canonical `ToString` values |
| 3 | `System.Char` | min/max values and supported whitespace classification |
| 4 | `System.Int32` | value equality, hash code, min/max constants |
| 5 | `System.IntPtr` | pointer-size contract, construction, add/subtract, integer round-trip |
| 6 | `System.UIntPtr` | pointer-size contract, construction, add/subtract, integer round-trip |
| 7 | `System.Array` | SZ-array length/long length, element access, `Array.Empty<T>` |
| 8 | `System.String` | empty, length/indexing, equality, ordinal comparison, search, prefix/suffix, substring, concat, null/whitespace helpers |
| 9 | `System.Nullable<T>` | present/empty state, `Value`, `GetValueOrDefault` |
| 10 | `System.Type` | value/primitive/array identity, element/base type, assignability and subclass checks |
| 11 | `System.Collections.Generic.KeyValuePair<TKey,TValue>` | construction and key/value access |
| 12 | `System.Collections.Generic.EqualityComparer<T>` | default integer/string equality and stable equal-value hashing |
| 13 | `System.Collections.Generic.List<T>` | add/insert/index/search/copy/remove/count |
| 14 | `System.Collections.Generic.Dictionary<TKey,TValue>` | add/indexer/count/lookup/`TryAdd`/remove |
| 15 | `System.Collections.Generic.Queue<T>` | enqueue/peek/dequeue/count and FIFO ordering |
| 16 | `System.Collections.Generic.Stack<T>` | push/peek/pop/count and LIFO ordering |
| 17 | `System.Text.StringBuilder` | append, line append, index/length, clear, capacity, materialisation |
| 18 | `System.Text.Encoding` | ASCII/UTF-8 factories and byte-count contract |
| 19 | `System.Text.ASCIIEncoding` | string-to-byte and byte-to-string round-trip |
| 20 | `System.Text.UTF8Encoding` | multi-byte UTF-8 encode/decode round-trip |
| 21 | primitive numeric formatting / `System.IFormattable` | signed/unsigned `G`, precision `D`, hexadecimal `X`, and bounded invariant floating `G` formatting |
| 22 | `System.Math` | integer/floating `Abs`, `Min`, `Max`, `Sign`, `Clamp`, plus `Floor`, `Ceiling`, `Truncate`, banker's `Round`, and `Sqrt` |
| 23 | `System.Convert` | Boolean, all integer widths, integer/floating conversions, banker's rounding on floating-to-integer conversion, and primitive invariant string conversion |
| 24 | `System.IComparable` / `System.IComparable<T>` / `System.IEquatable<T>` | boxed and strongly typed ordering/equality across Boolean, Char, signed/unsigned integer and floating primitive families |
| 25 | delegate family | managed delegate construction/invocation for `Action`/`Func` through four arguments plus `Predicate<T>`, `Comparison<T>`, and `Converter<TInput,TOutput>` |
| 26 | `System.Span<T>` / `System.ReadOnlySpan<T>` | null/empty semantics, array windows, indexing, mutation, slicing, fill/clear, overlap-safe copy/try-copy, read-only view, and `ToArray` |
| 27 | `System.Memory<T>` / `System.ReadOnlyMemory<T>` | storable array-backed windows, slicing, `Span`, empty/null semantics, mutation through `Memory<T>.Span`, and `ToArray` |
| 28 | `System.Collections.Generic.Comparer<T>` + equality consistency | default generic ordering and equality across multiple primitive families |

## Primitive/runtime foundation status

For Core v1, the primitive/runtime foundation is considered complete when the two executable gates pass all items 21-28. This deliberately means **the named Inu subset**, not every culture-sensitive or reflection-heavy desktop .NET overload. The completed foundation provides:

- deterministic invariant primitive formatting required by kernel/runtime diagnostics;
- integer and finite floating-point arithmetic/conversion paths used by Inu components;
- consistent `IComparable<T>`/`IEquatable<T>` and default generic comparer behaviour for the covered primitive families;
- the common managed delegate families needed by callbacks and generic helpers;
- temporary byref windows through `Span<T>`/`ReadOnlySpan<T>` and storable array-backed windows through `Memory<T>`/`ReadOnlyMemory<T>`;
- paired boundary behaviour for empty/null windows, slicing, copy sizing and overlapping span copies.

Culture-aware numeric formatting/parsing, arbitrary custom format strings, decimal arithmetic, SIMD/vector numerics and the full desktop `System.Math` surface are outside `Inu.BCL.Core.v1` unless deliberately added to a later named target.

## Change rule

The target is fixed by name and count. `ManagedRuntimeConformance.BclTargetName` is `Inu.BCL.Core.v1` and `BclTargetItemCount` is `28`. The reference executable independently carries the same name and count and fails if it does not execute exactly 28 target items.

When adding a new item:

- add one named reference-side check;
- add one corresponding in-kernel `Record` in `RunBclCoreV1Checks`;
- increment the item count in both places;
- add the item to this table;
- do not advertise the item as supported until both gates pass.

A future incompatible expansion should receive a new target name such as `Inu.BCL.Core.v2` rather than silently changing the meaning of `v1`.
