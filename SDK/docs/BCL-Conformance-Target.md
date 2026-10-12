# Inu BCL conformance target

Inu does not claim support for the whole .NET Base Class Library. The fixed compatibility target for this release is the named subset **`Inu.BCL.Core.v1`**.

A BCL item is part of this target only when the same item is exercised in both of these executable gates:

1. **Reference side:** `SDK/tests/Inu.DotNetConformance.Tests`, executed against the normal .NET 10 BCL by `SDK/Run-InuDotNetConformance.bat`.
2. **Kernel side:** `Inu.Runtime.Conformance.ManagedRuntimeConformance.RunBclCoreV1Checks`, available for explicit SDK runtime validation. It is **not** injected into an ordinary generated-OS boot merely because the SDK changed. When Inu implementation code changes, the normal build runs the reference-side suite once for the new SDK fingerprint; the heavier in-kernel gate is opt-in via the SDK runtime-conformance validation path.

The target is intentionally type-level rather than a claim that every API on a listed type is implemented. The APIs exercised by the paired gates are the supported contract. Extending the target requires extending both gates in the same change. The host/reference gate remains available independently of OS boot, while the in-kernel gate is SDK-change validation code rather than a normal or Debug startup prerequisite. Inu records the validated SDK-code fingerprint only after the runtime gate completes successfully.

## Inu.BCL.Core.v1

| # | BCL item | Contract exercised by both gates |
|---:|---|---|
| 1 | `System.Object` | construction, identity, `ReferenceEquals`, default `Equals`, stable identity `GetHashCode`, runtime type identity, and default `ToString` for base, derived, array and closed-generic runtime types |
| 2 | `System.Boolean` | full .NET 10 Boolean contract: constants, hashing/text, formatting, parsing, typed/boxed comparison/equality, `IConvertible`, `IParsable<bool>`, `ISpanParsable<bool>`, conversion/exception semantics and static-interface dispatch |
| 3 | `System.Char` | comparison/equality/hash semantics; selected classification; parsing; conversion; and span/provider formatting |
| 4 | `System.SByte` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 5 | `System.Int16` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 6 | `System.Int32` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 7 | `System.Int64` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 8 | `System.Byte` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 9 | `System.UInt16` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 10 | `System.UInt32` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 11 | `System.UInt64` | min/max constants, string/span parsing, formatting, checked/unchecked conversion, comparison/equality and `IConvertible` |
| 12 | `System.Single` | IEEE special values, signed zero detection, comparison/equality, string/span parsing, `G`/`F`/`E` formatting, span formatting and primitive conversions |
| 13 | `System.Double` | IEEE special values, signed zero detection, comparison/equality, string/span parsing, `G`/`F`/`E` formatting, span formatting and primitive conversions |
| 14 | `System.IntPtr` | pointer-size contract, min/max/zero, arithmetic, comparison/equality, conversion and invariant formatting |
| 15 | `System.UIntPtr` | pointer-size contract, min/max/zero, arithmetic, comparison/equality, conversion and invariant formatting |
| 16 | `System.Array` | one-dimensional SZ-array length/rank, compiler bounds checks, runtime covariance/store checks, cloning, clearing, overlap-safe copying, `Array.Empty<T>`, and direct enumeration |
| 17 | `System.Collections.IEnumerable` | non-generic array enumeration, value-type boxing, reset and current-state behaviour |
| 18 | `System.Collections.IEnumerator` | `Current`, `MoveNext`, `Reset`, and invalid-current state semantics |
| 19 | `System.Collections.Generic.IEnumerable<T>` | SZ-array generic interface dispatch, including safe reference covariance |
| 20 | `System.Collections.Generic.IEnumerator<T>` | strongly typed `Current`, movement/reset semantics and `IDisposable` contract |
| 21 | `System.String` | Inu-required constructors; empty; length/indexing; equality/hash; ordinal compare; prefix/suffix; char/string contains/index/last-index; substring/remove/insert/replace; trim; ASCII/invariant upper/lower subset; split/join/concat; null/whitespace helpers; character enumeration |
| 22 | `System.Nullable<T>` | present/empty state, `Value`, `GetValueOrDefault` |
| 23 | `System.Type` | value/primitive/array identity, `Name` from NativeAOT metadata, element/base type, assignability and subclass checks |
| 24 | `System.Collections.Generic.KeyValuePair<TKey,TValue>` | construction and key/value access |
| 25 | `System.Collections.Generic.EqualityComparer<T>` | default integer/string equality and stable equal-value hashing |
| 26 | `System.Collections.Generic.List<T>` | add/add-range (including self-add), insert, indexer get/set and bounds, contains/index lookup, remove/remove-at, clear, count/capacity, generic/non-generic enumeration state/version semantics, and independent `ToArray` |
| 27 | `System.Collections.Generic.Dictionary<TKey,TValue>` | add/indexer/count/lookup/`TryAdd`/remove |
| 28 | `System.Collections.Generic.Queue<T>` | enqueue/peek/dequeue/count and FIFO ordering |
| 29 | `System.Collections.Generic.Stack<T>` | push/peek/pop/count and LIFO ordering |
| 30 | `System.Text.StringBuilder` | append/object append, line append, insert, remove, character/string replace, index/length, clear, capacity growth/set, materialisation, and argument-error paths |
| 31 | `System.Text.Encoding` | Profile 1 ASCII/UTF-8 factories by property/name/code page plus byte/character counts and allocating/range/span/buffer conversion contracts |
| 32 | `System.Text.ASCIIEncoding` | 7-bit replacement semantics plus byte/character counts and allocating/range/span/buffer encode/decode |
| 33 | `System.Text.UTF8Encoding` | UTF-8 BMP/supplementary-scalar byte/character counts and allocating/range/span/buffer encode/decode with replacement fallback |
| 34 | `primitive numeric formatting / System.IFormattable` | signed/unsigned `G`/`D`/`X` plus invariant floating `G`/`F`/`E` formatting |
| 35 | `string interpolation runtime support` | compiler-known `DefaultInterpolatedStringHandler`, literal/formatted segments, strings/chars/spans, alignment, basic format strings, numeric formats, `Text`, `Clear`, `ToString` and `ToStringAndClear` |
| 36 | `System.String` composite formatting | indexed placeholders such as `{0}`, escaped braces, positive/negative alignment, null arguments, basic item format strings, malformed-format exceptions, and the Profile 1 primitive numeric format subset |
| 37 | `System.Math` | primitive `Abs`, `Min`, `Max`, `Clamp`, `Sign`; `Floor`, `Ceiling`, `Round`, `Truncate`; `Sqrt`, `Pow`, `Exp`, `Log`, `Log10`; and `Sin`, `Cos`, `Tan`, `Atan`, `Atan2`, including NaN/infinity and documented exception paths covered by the paired gate |
| 38 | `System.MathF` | single-precision counterpart to the covered `System.Math` surface |
| 39 | `System.Convert` | Boolean, integer and floating primitive conversions, rounding on floating-to-integer conversion, and primitive invariant string conversion |
| 40 | `System.IComparable / IComparable<T> / IEquatable<T>` | boxed and strongly typed ordering/equality across covered primitive families |
| 41 | `delegate family` | managed delegate construction/invocation for `Action`/`Func` through four arguments plus `Predicate<T>`, `Comparison<T>`, and `Converter<TInput,TOutput>` |
| 42 | `System.Span<T> / System.ReadOnlySpan<T>` | full .NET 10 type-owned `Span<T>` / `ReadOnlySpan<T>` surface: managed/native byref windows, array/ref/pointer construction, indexing, slicing, fill/clear, overlap-safe copy/try-copy, enumeration, pinning, `ToArray`, `ToString`, window identity, `ArraySegment<T>` conversion, and read-only covariance |
| 43 | `System.Memory<T> / System.ReadOnlyMemory<T>` | complete .NET 10 type-owned array-backed surface: constructors/range/covariance rules, `Empty`/`IsEmpty`/`Length`/`Span`, slicing, copy/try-copy, equality/hash, non-moving-heap `Pin`/`MemoryHandle`, `ToArray`, `ToString`, and array/`ArraySegment<T>` conversions |
| 44 | `System.Collections.Generic.Comparer<T> + equality consistency` | default generic ordering and equality across multiple primitive families |


### Profile 1 encoding boundary

Profile 1 deliberately includes only ASCII and UTF-8 conversion support: `Encoding.ASCII`, `Encoding.UTF8`, `Encoding.GetEncoding` for ASCII/20127 and UTF-8/65001, byte/character counting, and allocating or caller-buffer/span encode/decode paths. `System.String` remains UTF-16 internally, but UTF-16 *encoding APIs* (`UnicodeEncoding`, `Encoding.Unicode`, and `Encoding.BigEndianUnicode`) are not part of Profile 1. Streaming `Encoder`/`Decoder`, configurable fallback objects, BOM/preamble policy APIs, UTF-16/UTF-32 and additional code pages are later-profile work.

## Primitive/runtime foundation status

For TODO-listed compatibility targets, completion means the **full applicable .NET 10 contract for that listed item**, not a reduced Inu subset. The summary rows in this document are progress groupings only; they do not narrow the completion definition in `TODO.md`. A listed target remains incomplete while any applicable .NET 10 public member, explicit interface member, runtime/compiler-required member, documented behaviour, exception rule or boundary case is missing. The foundation work provides:

- deterministic invariant primitive formatting required by kernel/runtime diagnostics;
- integer and finite floating-point arithmetic/conversion paths used by Inu components;
- consistent `IComparable<T>`/`IEquatable<T>` and default generic comparer behaviour for the covered primitive families;
- the common managed delegate families needed by callbacks and generic helpers;
- temporary byref windows through `Span<T>`/`ReadOnlySpan<T>` and the complete Profile-1 .NET 10 type-owned array-backed `Memory<T>`/`ReadOnlyMemory<T>` surface;
- paired boundary behaviour for empty/null windows, slicing, copy sizing and overlapping span copies.

Where one of these areas is explicitly listed in `TODO.md` (for example `System.Math`, primitive formatting/parsing, or `System.Convert`), the full applicable .NET 10 surface for that listed target is required before its checkbox may be completed. Areas not listed remain outside Profile 1 until they are deliberately added.

## Change rule

The target is fixed by name and count. `ManagedRuntimeConformance.BclTargetName` is `Inu.BCL.Core.v1` and `BclTargetItemCount` is `44`. The reference executable independently carries the same name and count and fails if it does not execute exactly 44 target items.

When adding a new item:

- add one named reference-side check;
- add one corresponding in-kernel `Record` in `RunBclCoreV1Checks`;
- increment the item count in both places;
- add the item to this table;
- do not advertise the item as supported until both gates pass.

A future incompatible expansion should receive a new target name such as `Inu.BCL.Core.v2` rather than silently changing the meaning of `v1`.

### NativeAOT JIT helper contract

The freestanding `System.Math` surface also supplies the CoreLib helper entry points that RyuJIT/NativeAOT imports for checked floating-point-to-integer conversion (`ConvertToInt32Checked`, `ConvertToUInt32Checked`, `ConvertToInt64Checked`, and `ConvertToUInt64Checked`). These are runtime/compiler ABI helpers rather than public BCL APIs, and their behaviour is covered through the public `System.Convert` conformance cases.

### Compiler-known constant span helpers

Profile 1 includes the Roslyn/.NET 10 CoreLib companion contracts required for constant array and `ReadOnlySpan<T>` lowering: `System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(Array, RuntimeFieldHandle)` and `RuntimeHelpers.CreateSpan<T>(RuntimeFieldHandle)`. Both are NativeAOT intrinsics and are covered by reference-side and in-kernel constant-span regression cases.

### Shared conformance definitions

Starting with 0.1.39, the completed `System.Array`, `System.Collections.IEnumerable`, `System.Collections.IEnumerator`, `System.Collections.Generic.IEnumerable<T>`, and `System.Collections.Generic.IEnumerator<T>` contract logic lives in one shared source file compiled into both conformance environments. The host runner remains the Microsoft .NET reference oracle; the in-kernel runner remains the Inu freestanding implementation check. Sharing the expectation body prevents duplicated constants and edge-case logic from drifting between the two suites.
