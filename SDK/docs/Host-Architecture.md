# Inu host architecture

Inu separates the **hosted development toolchain** from the **freestanding operating-system SDK**.

## Hosted side

`Inu\Bin\inu.exe` is the single host entry point.  Core host services are C# services registered through `Microsoft.Extensions.Hosting` and dependency injection:

- Roslyn C# diagnostics (`Microsoft.CodeAnalysis.CSharp`)
- SDK API catalog/model
- SDK source/API validation
- disk image discovery/format identification
- host module execution

Kath remains a Theia/Electron application. TypeScript/React is the presentation layer; it calls the typed Inu host services rather than becoming a second implementation of compiler or SDK domain logic.

## MSBuild and NuGet

`Inu.Sdk.props` and `Inu.Sdk.targets` define the standard MSBuild contract for Inu projects.  The `Inu.Sdk` package project packs those files as `buildTransitive` assets so the same properties and targets can be consumed through NuGet when the SDK is distributed that way.

Important properties include `InuSdkRoot`, `InuTargetArchitecture`, `InuKernelModel`, `InuBootTarget`, `InuConfiguration`, and `InuHostExecutable`.

NativeAOT, the freestanding system module, native assembly/linking, image construction, and QEMU remain Inu-specific build stages. MSBuild orchestrates them; it does not pretend an operating system is an ordinary desktop class library.

## Freestanding side

The hosted services and their dependencies never become part of Boot, Kernel, or Userland binaries. `Microsoft.Extensions.Hosting`, Roslyn, and NuGet are host-development dependencies only.
