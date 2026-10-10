# Kath & Inu 0.1.4

This release completes the listed floating-point foundation work for `float`/`System.Single` and `double`/`System.Double`.

## Included

- NaN, positive infinity, negative infinity and signed-zero handling.
- Typed/boxed comparison and equality semantics.
- String and span parsing, including decimal fractions, exponents and special values.
- Invariant `G`, `F` and `E` formatting plus `ISpanFormattable.TryFormat`.
- Primitive conversion participation through `IConvertible` and the existing checked floating-to-integer helpers.
- Paired host/reference and in-kernel conformance checks.
- BCL target accounting reconciled to all 37 paired checks.

`TODO.md` at the repository root records the completed block.
