# Inu Architecture

Inu separates managed kernel policy from architecture, boot, memory, console, runtime, compiler, linker, image, and launch concerns.

The current x64 UEFI path is:

```text
UEFI firmware
 -> native x64 EFI entry
 -> Graphics Output Protocol discovery
 -> final UEFI memory-map capture
 -> ExitBootServices
 -> Inu-owned no-CoreLib NativeAOT bootstrap
 -> managed serial/framebuffer console
 -> managed KMain
 -> repeating CLI/HLT loop
```

The native entry performs only the firmware ABI work that must occur before managed execution. Framebuffer validation, clearing, pixel-format conversion, bitmap-font rendering, cursor movement, and serial/framebuffer mirroring are managed C# responsibilities.

The reusable SDK memory architecture is layered separately from the minimal bootstrap path:

```text
Inu.Boot.Contracts
 -> Inu.Memory.Contracts
 -> Inu.Boot.Memory
 -> Inu.Memory.Physical.Contracts
 -> Inu.Memory.Physical
 -> kernel address-space design (next)
 -> virtual memory management
 -> early allocator / kernel heap
```

`Inu.Boot.Memory` turns the final firmware map plus explicit Inu reservations into a normalised ownership map. `Inu.Memory.Physical` consumes only ranges marked immediately allocatable and provides bitmap, buddy, and extent frame allocators through `IPhysicalMemoryManager`. Their metadata storage is caller-owned so physical allocation does not depend on the future kernel heap.

The ordinary SDK surface exposes boot data through `Inu.Boot.Contracts`, reusable memory ownership through `Inu.Memory.Contracts`, physical allocation through the two `Inu.Memory.Physical*` assemblies, and framebuffer output through `Inu.Console.Framebuffer`. Architecture-specific CPU and port operations remain in `Inu.Architecture.X64`.

## Source-component decomposition

Large runtime implementation units are decomposed by responsibility while their existing public subsystem contracts remain stable. The decomposition rules and current first-pass split are defined in `Component-Decomposition.md`. A smaller source file is not automatically a separately selectable OS feature; Kath should expose it independently only when its contract, dependencies, lifecycle, and ownership form a meaningful component boundary.


## Language-neutral component definition layer

Canonical component metadata lives under `components/definitions`. One definition represents one OS component independently of implementation language; its implementation map points to the authoritative C# source files, plus architecture-specific assembly where required. Kath may only expose definitions marked `selectable`; definitions marked `candidate` document a real architectural seam that still shares state or lifecycle with its parent and therefore must not yet be offered as an independent OS choice.
