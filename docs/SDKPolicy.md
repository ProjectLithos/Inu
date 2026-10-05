# Inu SDK Policy

## Authoritative implementation language

Inu SDK components and generated kernels are written in **C#** and compiled to freestanding native machine code with **.NET NativeAOT**. Kath copies the selected C# SDK source into the generated operating-system source tree, where it becomes OS-owned source.

Inu does not maintain a parallel C implementation of the kernel or SDK. Architecture-specific assembly remains assembly where required by the hardware/ABI. Other implementation languages may be considered in the future, but they are not part of the current SDK.

## Source ownership

Selected Inu component source is copied into the generated OS. The copied source belongs to the OS author and must not be silently regenerated over user edits.

## Build model

Kath invokes `Build-Inu.bat` / `Build-Inu.ps1`. The managed compiler and NativeAOT/ILC compile the selected C# source to freestanding C# / NativeAOTode. Boot architecture is a separate OS-author choice and must not make the post-boot kernel firmware-dependent unless the OS author explicitly chooses a firmware-hosted design.
