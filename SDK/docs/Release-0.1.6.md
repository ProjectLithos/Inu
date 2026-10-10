# Kath & Inu 0.1.6

This bug-fix release corrects the `System.Math.Atan` implementation introduced in 0.1.5 so Inu central reference compilation succeeds.

## Fixed

- Removes the C# CS0136 local-name shadowing in the `Atan` reciprocal branch.
- Preserves the completed 0.1.5 `System.Math` behaviour and conformance surface; no Math API contract is removed or reduced.
- Updates the authoritative root `TODO.md` with the regression fix.
