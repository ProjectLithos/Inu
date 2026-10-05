# Inu user-facing API rules

Inu has one user-facing SDK contract. It is deliberately smaller than the set of public C# declarations used internally by generated operating-system source.

1. The command API contains only commands accepted by the canonical `inu` executable.
2. A C# declaration is not SDK API merely because it is `public`.
3. A coding API declaration is published only when its XML documentation explicitly contains `<inu.api>`.
4. Generated API documentation must omit unmarked declarations.
5. Kath must consume the generated explicit contract rather than inventing or maintaining a second list.
6. If an advertised API item cannot be compiled/used by an external OS project, that is a conformance failure.

## First explicit userland API

`Inu.Userland.Runtime.Output` is an intentionally exported ring-3 API. Coder-owned userland source may call `Output.Write`, `Output.WriteLine`, and `Output.Clear`; those operations cross the kernel boundary through the existing Event syscall rather than linking to `Inu.Kernel.Console`. Kernel console classes are not a userland SDK API.
