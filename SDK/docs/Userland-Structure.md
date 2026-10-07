# Userland and system command structure

Inu separates universal applications from operating-system commands.

- `/bin` is reserved for applications that are universal to all users, such as `Inu-Shell` once it is packaged as a standalone application.
- `/System/Commands` contains ordinary command programs such as `dir`, `stress`, and `selftest`.
- `/System/Help` contains the manuals referenced by command packages.
- `/System/Fonts` contains system console fonts.

The canonical SDK source mirrors this layout:

- `src/System/Commands` contains canonical command implementations and grammar.
- `src/Userland/Settings` contains user-configurable settings contracts.
- `src/Userland/Fonts` contains font catalogs and font-management surface.
- `src/Userland/Images` contains image-resource contracts.
- `src/Userland/Drivers` contains userland-visible driver contracts; privileged device drivers remain in HAL/kernel assemblies.

`stress` and `selftest` are ordinary command implementations directly under `src/System/Commands`; the command line host supplies generic command dispatch, output presentation and process lifecycle services. Their ring-3 process image source is colocated with those commands rather than in the core process-manager source file.

The older `Inu.Userland.Font`, `Inu.Userland.Buffering`, and `Inu.Userland.Keyboard` assemblies are retained as compatibility facades that forward to the canonical `System/Commands` command library.

## 0.0.81 coder-visible shell and commands

Shell/application authors use high-level SDK APIs (`System.Console`, `System.IO`, `FileSystemPaths`, `Process`, `CommandLine`, `SystemInformation`). Native Get/Set/Event envelopes are implementation details and are not required in coder-owned shell or command source.

`Userland/<OSName>/Shell.cs` owns `Configure()` and `Run()`. `Configure()` is guarded by the compiled shell entry and runs once for the lifetime of the resident shell image; command completion does not rerun it. `Run()` owns the visible prompt/input/dispatch loop.

The complete shipped starter command source is copied into `Userland/<OSName>/Commands`. These files are coder-owned after generation. SDK implementation under `Boot/Provided`, `Kernel/Provided`, and `Userland/Provided` remains on disk for compilation but is hidden from Kath's normal workspace explorer.
