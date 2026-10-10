# Kath & Inu 0.1.3

0.1.3 completes the native-integer foundation for the freestanding .NET surface.

- Completes `IntPtr` and `UIntPtr` as pointer-sized value types with canonical zero/min/max behaviour.
- Adds native-sized arithmetic, offset Add/Subtract, comparison/equality, relational operators, and conversion coverage.
- Adds invariant general/decimal/hex formatting and `ISpanFormattable.TryFormat`.
- Adds matching reference-side .NET 10 and in-kernel/runtime conformance coverage.
- Marks the complete native-integer TODO block finished.
