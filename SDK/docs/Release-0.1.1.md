# Inu / Kath 0.1.1

0.1.1 is a corrective release for the signed-integer conformance milestone introduced in 0.1.0.

- Fixes the in-kernel signed-integer conformance source so negative values cast through `SByte`, `Int16`, `Int32`, and `Int64` use unambiguous parenthesised negative expressions accepted by C#.
- Keeps the completed signed-integer API/runtime surface from 0.1.0 unchanged; this release corrects only the conformance source syntax and release bookkeeping.
- Retains host-side and in-kernel coverage for parsing, formatting, checked/unchecked conversion, comparison, equality, and canonical min/max constants.
- Keeps the authoritative `TODO.md` at the root of both release ZIPs.
