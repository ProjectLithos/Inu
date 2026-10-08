# Inu BCL conformance target

Inu does not claim support for the whole .NET Base Class Library. The fixed compatibility target for this release is the named subset **`Inu.BCL.Core.v1`**.

A BCL item is part of this target only when the same item is exercised in both of these executable gates:

1. **Reference side:** `SDK/tests/Inu.DotNetConformance.Tests`, executed against the normal .NET 10 BCL by `SDK/Run-InuDotNetConformance.bat`.
2. **Kernel side:** `Inu.Runtime.Conformance.ManagedRuntimeConformance.RunBclCoreV1Checks`, executed inside the booted Inu kernel. A failed item contributes to the hard managed-runtime failure count and rejects boot.

The target is intentionally type-level rather than a claim that every API on a listed type is implemented. The APIs exercised by the paired gates are the supported contract. Extending the target requires extending both gates in the same change.

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
| 21 | primitive numeric formatting / `System.IFormattable` | signed/unsigned integer decimal formatting and `G`/`D` general decimal contract |
| 22 | `System.Math` | integer `Abs`, `Min`, `Max`, `Sign`, and `Clamp` |
| 23 | `System.Convert` | Boolean/integer conversions and primitive string conversion |
| 24 | `System.IComparable` / `System.IComparable<T>` | boxed and strongly typed integer ordering |
| 25 | `System.Delegate` / `System.Action` / `System.Func` | managed delegate construction and invocation with and without arguments/results |
| 26 | `System.Span<T>` / `System.ReadOnlySpan<T>` | array-backed length, indexing, mutation, slicing, read-only view, and `ToArray` |

## Change rule

The target is fixed by name and count. `ManagedRuntimeConformance.BclTargetName` is `Inu.BCL.Core.v1` and `BclTargetItemCount` is `26`. The reference executable independently carries the same name and count and fails if it does not execute exactly 20 target items.

When adding a new item:

- add one named reference-side check;
- add one corresponding in-kernel `Record` in `RunBclCoreV1Checks`;
- increment the item count in both places;
- add the item to this table;
- do not advertise the item as supported until both gates pass.

A future incompatible expansion should receive a new target name such as `Inu.BCL.Core.v2` rather than silently changing the meaning of `v1`.
