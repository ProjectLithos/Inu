# Kath & Inu 0.1.5

This release completes the next unchecked primitive/runtime TODO block: `System.Math`.

## Included

- Primitive-width `Abs`, `Min`, `Max`, `Clamp`, and `Sign` coverage used by Profile 1.
- NaN propagation, signed-zero handling, overflow/argument exception paths, and banker's rounding.
- `Floor`, `Ceiling`, `Round`, `Truncate`, and `Sqrt`.
- `Pow`, `Exp`, `Log`, and `Log10`, including special-value handling.
- `Sin`, `Cos`, `Tan`, `Atan`, and `Atan2`.
- Matching reference-side and in-kernel/runtime conformance coverage in the existing `System.Math` target.

`TODO.md` at the repository root marks the complete `System.Math` expansion block finished.
