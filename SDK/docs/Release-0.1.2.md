# Kath & Inu 0.1.2

0.1.2 completes the unsigned-integer foundation for the freestanding .NET surface.

- Completes `byte`, `ushort`, `uint`, and `ulong` parsing and formatting, including string/span parsing and `ISpanFormattable`.
- Adds full `IConvertible` participation and unsigned `Decimal` conversion through the full `UInt64` range.
- Covers checked and unchecked conversions, comparison, equality, hashing, and canonical min/max constants.
- Adds matching reference-side .NET 10 and in-kernel/runtime conformance coverage.
- Marks the complete unsigned-integer TODO block finished.
