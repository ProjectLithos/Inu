# Inu command-line SDK

Inu is designed to build operating systems without requiring Kath. The SDK ships a single `inu` command that uses the same project creator, build pipeline, test framework, package format, target configuration and pinned toolchain as the IDE.

Run `Install-InuCli.bat` once to place a small command shim in `%LOCALAPPDATA%\Inu\bin` and add that directory to the current user's PATH. Open a new terminal afterwards. You can also invoke `SDK\inu.cmd` directly without installing the shim.

## Commands

```text
inu new MyOS
inu build
inu run
inu debug
inu test
inu pack Inu.Package.json payload MyApp-1.0.0.zip
inu doctor
```

`inu new <name>` creates a project in `<current-directory>\<name>`. Use `--output <directory>` to choose an explicit location. Project creation delegates to `Inu.ProjectCreator`; the CLI does not maintain a second template system.

`inu build` builds the current directory by default. Use `--project <directory>`, `--configuration Debug|Release`, and `--boot-timeout <seconds>` when required. It delegates to `Build-Inu.ps1`.

`inu run` performs the same build and launches the project's configured target. It uses the same QEMU/physical-target configuration consumed by the IDE.

`inu debug` is a Debug configuration build followed by target launch. On QEMU this enables the same debug image/symbol generation and debugger transport contracts used by Kath; it does not require the IDE itself to produce the debug-capable kernel image.

`inu test` delegates to the Inu test framework and accepts the test runner's manifest/kind/tag/list/fail-fast/report options.

`inu pack` invokes `Inu.PackagePacker` and produces the standard Inu ZIP package containing `Inu.Package.json` plus validated payload hashes.

`inu doctor` verifies the SDK manifest, required build/test/package components, compatibility baseline, source manifest and pinned .NET/LLVM toolchain presence, and reports optional QEMU/NASM discoverability.

`inu version` prints the CLI release and SDK/API/ABI versions. `inu help` prints command help.

## One source of truth

The CLI deliberately contains no independent compiler, linker, project generator, test runner or package implementation. It orchestrates the same SDK components used by the IDE. A project that builds through `inu build` therefore exercises the normal Inu SDK build path rather than a CLI-specific approximation.
