# Inu source-component decomposition

Inu is a source-component SDK. Kath composes an operating system from source components; it does not force OS policy into a monolithic runtime component. The source graph and the source tree are two views of the same architecture.

## Decomposition rule

Large implementation units are split by responsibility while preserving their existing public subsystem contract. A split source unit is not automatically a separately selectable OS feature: selection becomes independent only when the unit has a meaningful contract, dependency set, and lifecycle of its own. This prevents file splitting from inventing artificial policy boundaries.

The first decomposition pass is deliberately behaviour preserving. Public type names, ABI, subsystem contracts, and runtime behaviour remain unchanged. Partial implementation classes are used so existing consumers continue to compile against the same surface while Kath gains smaller source units to analyse and eventually compose.

## First decomposed components

### Driver framework

`Inu.Kernel.Drivers.KernelDrivers` is divided into:

- registry and device discovery;
- device lifecycle and binding;
- inspection and device-tree/event queries;
- capability grants;
- interrupt brokering and lifecycle events;
- backing storage/allocation helpers.

### Virtual filesystem

`Inu.Kernel.Storage.KernelVfs` is divided into:

- provider/namespace registry;
- mount management;
- file I/O;
- directory I/O;
- permissions;
- routing, validation, growth, and allocation helpers.

This decomposition does not impose path spelling, case sensitivity, separator choice, visibility, writability, or filesystem layout policy. Those remain OS-author choices.

### Scheduler

`Inu.Kernel.Scheduler.KernelScheduler` is divided into:

- scheduler state and lifecycle;
- thread creation/control;
- dispatch and run-queue decisions;
- application-processor and role-worker execution;
- accounting, interrupts, and GC stop-the-world coordination;
- low-level record lookup/bootstrap helpers.

The stable `Scheduler` lifecycle facade is now a separate source file.

## Second decomposed components

### Command execution host

`Inu.Kernel.CommandLine.CommandExecutionHost` is divided by responsibility into initialization and registry state, shell and builtin dispatch, userland command dispatch, userland package/session/filesystem/syscall bridges, CPU dispatch and monitor rendering, subsystem lifecycle/status/display diagnostics, console settings, session state, and line-input editing. The public command-host contract is unchanged.

### FAT provider

`Inu.Filesystem.FatFs.FatFs` is divided into boot-sector parsing, file I/O, directory/name handling, mutation operations, allocation, and lookup helpers. This split does not impose filesystem naming or path policy on the generated OS.

### Framebuffer console

`Inu.Kernel.Console.FramebufferConsole` is divided into lifecycle, live-view support, text editing, history, rendering, and presentation responsibilities while retaining the same console surface.

### Process manager

`Inu.Kernel.Processes.KernelProcesses` is divided into runtime state, process creation, control, syscall handling, execution, diagnostics, memory, and backing storage helpers. Process isolation and the Get/Set/Event ABI are unchanged.

### Bootstrap kernel

The bootstrap kernel's already-separable helpers for console input, networking, graphics, and boot tracing are now separate source units. The central boot orchestration method remains intact in this pass: it will only be staged into smaller boot phases when those phases can be compiled and runtime-tested together, rather than being mechanically cut into artificial components.

## Third decomposed components

### Kernel console front end

`Inu.Kernel.Console.KernelConsole` is now divided into presentation locking, output configuration and TrueType activation, formatted/raw output, framebuffer control and capture, live-view operations, interactive input/editing, and low-level transport/ANSI handling. The existing console API remains the contract.

### SMP

`Inu.Kernel.Smp.KernelSmp` is now divided into topology/role/per-CPU state, firmware-discovered processor population, AP startup, and IPI sequencing. Processor-role policy remains selected by the generated OS rather than by the decomposition.

### VirtIO core and VirtIO GPU

The generic VirtIO driver is divided into transport setup, block operations, network/queue service, and PCI/DMA/storage integration. The VirtIO GPU driver is divided into driver lifecycle, graphics modes, command protocol, transport/queue handling, and backing-storage helpers. These are implementation slices under the existing driver contracts rather than newly imposed device policy.

### GUI

`Inu.Kernel.Gui.KernelGui` is divided into input routing, compositor state, GUI syscall handlers, rendering, and event/surface storage helpers. GUI applications remain ordinary isolated ring-3 processes, and the userland interface still uses the existing Get/Set/Event syscall model.

### System calls

`Inu.Kernel.SystemCalls.KernelSystemCalls` is divided into native/compatibility registration, user-memory copy validation, dispatch and diagnostics, handler registries, and native-message validation/built-ins. The ABI remains exactly the existing three general-purpose native operations: Get, Set, and Event; this split adds no syscall numbers.

## Next decomposition stage

The next pass should continue through remaining combined driver/HAL and runtime units, then begin promoting stable source slices into truly selectable Inu components where their inputs, outputs, dependencies, lifecycle, and ownership are explicit.

Language-specific source remains in language-specific files. Kath must compile each file with the toolchain appropriate to that file and must not translate architecture-specific assembly merely to satisfy a selected high-level language.

## Fourth decomposed components

### PCI

`Inu.Kernel.Pci.KernelPci` is divided into lifecycle/discovery state, configuration-space access, BAR/capability/interrupt handling, MMIO mapping, bus/function enumeration, and device-record storage helpers. The PCI contract and driver-framework integration are unchanged.

### Virtual memory

`Inu.Kernel.VirtualMemory.KernelVirtualMemory` is divided into lifecycle/status, public mapping operations, page-table traversal/construction, entry encoding/protection, and manager state helpers. Existing direct-map and diagnostics slices remain separate. Address-space policy and the existing page-size/protection contract are unchanged.

### Physical memory

`Inu.Kernel.Memory.KernelPhysicalMemory` is divided into lifecycle/bootstrap-facing state, allocation/release/statistics, free-extent maintenance, and allocation-record/state helpers. The existing bootstrap slice remains separate and no new allocation policy is imposed.

### Kernel heap

`Inu.Kernel.Heap.KernelHeap` is divided into lifecycle/status, allocation/release/statistics, page-backed growth and existing-block allocation, free-list/coalescing state, and diagnostic-metadata maintenance. The existing diagnostics implementation remains separate.

### TrueType

`Inu.Kernel.TrueType.KernelTrueType` is divided into its public font API, font/table parsing and cmap/metrics helpers, glyph outline construction, and raster/blending/scaling helpers. Font rendering behaviour and public contracts remain unchanged.

## Following decomposition stage

The next pass should continue with compact driver implementations whose responsibilities are currently densely packed into single source files, especially NVMe and xHCI, then split the remaining ACPI platform orchestration and other combined HAL units. Those should be reformatted only where necessary to expose genuine source-component seams; formatting alone is not a component boundary.


## Fifth decomposed components

### NVMe

`Inu.Kernel.Nvme.KernelNvme` is divided into the stable public facade/state, driver lifecycle and controller startup, namespace discovery, block I/O, admin/I/O queue protocol, and backing allocation/storage helpers. Namespace registration and block-device behaviour are unchanged.

### xHCI

`Inu.Usb.Xhci.KernelXhci` is divided into the public facade/state, controller lifecycle and memory setup, USB device preparation and transfers, command/event/transfer-ring handling, and record/allocation/MMIO helpers. USB bus contracts and endpoint behaviour are unchanged.

### ACPI

The ACPI implementation is divided into core discovery, MADT processing, platform-table access, validation/primitive reads, MADT capability reporting, MCFG, HPET, FADT, embedded-controller, power-management, and generic-register access source units. These remain implementation slices under the existing ACPI contracts and do not make firmware policy mandatory for the post-boot kernel.

The next pass should continue through remaining combined HAL/device drivers and then begin identifying which of the now-small source slices have sufficiently independent contracts and lifecycle to become true Kath-selectable Inu components.

## Sixth decomposed components

### Ethernet drivers

The Intel E1000/E1000e and Realtek RTL8168/RTL8111 implementations are now divided into stable facade/state, driver lifecycle, hardware/ring servicing, network I/O, and backing allocation/MMIO source units. Their generic driver and networking contracts are unchanged.

### Networking core

`KernelNetworking` is divided into interface registration/configuration, route selection, IPv4/IPv6 neighbour state, frame ingress/egress, and registry storage/growth. These remain one networking subsystem contract while exposing smaller implementation responsibilities for later Kath composition.

### Storage core

`KernelStorage` is divided into block-device registry/I/O, partition and volume discovery, and registry storage/growth. Filesystem placement and path policy remain outside this subsystem and remain end-user OS policy.

### USB bus

`KernelUsbBus` is divided into host registration, enumeration and descriptor exposure, transfer dispatch, device/interface creation/removal, and backing registry helpers. Host-controller implementations continue to bind through the same USB bus callbacks.

### PS/2

`KernelPs2` is divided into driver lifecycle, service/configuration and syscall-facing state, keyboard decoding/state, and mouse/i8042 I/O helpers. Input ownership and GUI routing contracts are unchanged.

### AHCI

`KernelAhci` is divided into controller lifecycle, disk discovery/IDENTIFY, block I/O/ATA command construction, port start/stop, and backing allocation/MMIO helpers. Storage registration behaviour is unchanged.

### Candidate true component boundaries

This pass identifies several slices that are now plausible **real** Inu components rather than merely partial-class files: network route management, neighbour cache management, USB enumeration, block partition discovery, and keyboard decoding. They are not made independently selectable yet because their contracts/dependencies must first be represented explicitly in the language-neutral component-definition layer.

## Seventh decomposition stage — explicit component boundaries

### Security

`KernelSecurity` is divided into process/address-space ownership, user-memory and W^X protection, syscall-personality policy, capability/handle authority, and low-level page-table inspection/protection. The public security contract is unchanged.

### Interrupt dispatch

`KernelInterruptDispatch` is divided into deferred interrupt work, user-fault capture, lifecycle/vector ownership, and the exported managed dispatch path. Interrupt-vector and scheduler integration remain unchanged.

### Timekeeping

`KernelTime` is divided into clock-source/capability reporting, monotonic/deadline operations, delay operations, and local-APIC timer programming/calibration. No timer or clock-source policy is imposed by the split.

### Graphics registry

`KernelGraphics` is divided into display registry/query operations, mode/presentation activation, and backing registry helpers. Driver ownership and display-selection policy remain unchanged.

### Language-neutral component definitions

`components/definitions` is the beginning of Inu's canonical language-neutral component-definition layer. A definition describes the component once and lists language implementations as source mappings; it does **not** create a separate implementation definition for every language.

The first definitions cover route management, neighbour caching, USB enumeration, partition discovery, and keyboard decoding. They are deliberately marked `candidate`, not `selectable`, because their current C# slices still share private parent state. Kath must not present a candidate as an independent choice until its state ownership, required/provided contracts, lifecycle, dependencies, and at least one usable language implementation are explicit.

This makes the next refactoring step concrete: extract those shared-state dependencies behind small contracts, then change the relevant definition from `candidate` to `selectable`.


## Eighth decomposition stage — owned component state

### Network interface registry

The interface registry is now represented explicitly as the required `Kernel.Networking.InterfaceRegistry` component contract. It owns interface identity and validation used by route and neighbour services.

### Route manager

`KernelRouteManager` now owns route records, allocation, dynamic growth, counters, route selection, and interface-removal cleanup. `KernelNetworking.AddRoute` and `KernelNetworking.TryResolveRoute` remain compatibility facades, so callers do not change. The manager depends only on the interface-registry contract and heap service rather than on private route storage inside the parent class.

### Neighbour cache

`KernelNeighborCache` now owns IPv4/IPv6 neighbour records, allocation, dynamic growth, counters, lookup/update operations, and interface-removal cleanup. The existing `KernelNetworking` neighbour APIs remain compatibility facades.

These two definitions intentionally remain `candidate` in 0.2.12. Their state ownership is now explicit, but `KernelNetworking.Initialize` still wires them directly. The next promotion step is dependency-resolved startup so Kath can include, omit, or substitute an implementation without editing the parent source.


## 0.2.13 — dependency-resolved networking components

`KernelNetworking` no longer names `KernelRouteManager` or `KernelNeighborCache`. Optional networking services register with `KernelNetworkExtensionServices`, and the compatibility APIs dispatch through that contract. Interface validation is exposed through `KernelNetworkInterfaceRegistryContract`, so optional components no longer reach into private parent storage.

Route management and neighbour caching are now marked `selectable`: generated startup includes them only when their component symbols are selected. The default Inu kernel configuration selects both, preserving the previous behaviour. Omitting either component leaves the parent networking core compilable; its corresponding compatibility operations simply report unavailable/false and capability counts remain zero.


## 0.2.14 selectable component promotion

USB enumeration, partition discovery, and keyboard decoding now cross the selectable-component boundary. Each owns its implementation state/policy and reaches its parent subsystem only through an explicit contract/dispatch registry. Parent compatibility APIs remain stable and report unavailable when the optional component is not registered.

## 0.2.15 selectable USB protocol components

USB HID is no longer one component that owns transport plus keyboard and mouse policy. `UsbHid` owns only USB HID interface matching, interrupt transfers, and HID device records. Keyboard report interpretation/event delivery is owned by `UsbHidKeyboard`; mouse report interpretation/pointer state/event delivery is owned by `UsbHidMouse`. The transport reaches these optional components only through `UsbHidKeyboardServices` and `UsbHidMouseServices`.

USB mass-storage is now represented explicitly as a selectable component as well. Its existing bulk-only transport implementation already consumes only USB-bus, driver-registry, heap, and block-storage contracts; boot/HAL startup now follows a component selection symbol rather than assuming it is always present.

Kath-generated configuration and Visual Studio configuration now emit the default component symbols from the selected work areas. This closes the gap between the language-neutral component catalog and generated source configuration while retaining the rule that the OS author can later deselect or substitute an independently selectable component.


## 0.2.17 — provider boundaries

This pass promotes provider-shaped subsystems rather than treating every source slice as a user choice.

- **FAT12/FAT16/FAT32** is now represented by one language-neutral selectable filesystem-provider definition. `KernelVfs` remains the registry/facade and does not own FAT policy.
- **Firmware/boot framebuffer** and **simple framebuffer** are represented as selectable graphics providers above the required display registry. The default UEFI configuration selects the firmware-framebuffer provider to preserve current behaviour.
- **Timekeeping** now uses `KernelClockSourceServices` as the parent-owned registration/dispatch contract. HPET and invariant TSC own their hardware-specific state and are `selectable`; the invariant-TSC provider declares HPET calibration-reference dependency explicitly.

A selectable provider must remain omittable without changing the parent registry source. A `candidate` continues to mean that the source seam is visible but the ownership/startup contract is not yet sufficient for Kath to present it as an independent choice.

## 0.2.17 clock-source promotion

HPET and invariant TSC are now selectable providers behind `KernelClockSourceServices`. The clock core owns dispatch and timer consumers; providers own hardware-specific state and may be omitted from generated source according to resolved dependencies. Invariant TSC currently requires HPET as its calibration reference.


## 0.2.18 — wall-clock and interrupt-timer providers

RTC/CMOS calendar access and the x64 Local APIC programmable timer are no longer hardware state owned by `KernelTime`. `KernelWallClockSourceServices` and `KernelInterruptTimerServices` are source-neutral registries. The concrete RTC provider exposes registration rather than the removed legacy `KernelRtcCmos.Initialize()` entry point. Both providers are independently selectable source components; omitting either leaves the clock core valid.


## 0.2.19 — interrupt broker responsibility split

The x64 interrupt broker is no longer a single dense implementation body. Route lifecycle/dispatch, PCI MSI/MSI-X fallback policy, I/O APIC discovery/GSI routing, CPU-affinity resolution, and route storage are separate source responsibilities. This is deliberately a decomposition step rather than a claim that each delivery mechanism is independently selectable.

`Kernel.Interrupts.BrokerCore` is recorded as internal infrastructure. `Kernel.Interrupts.IoApicRouter`, `Kernel.Interrupts.PciMessageSignaledRouter`, and `Kernel.Interrupts.AffinityResolver` are recorded as `candidate` seams. They must not become Kath choices until the broker reaches them only through explicit provider contracts and can compile when each concrete implementation is absent.

## 0.2.20 — selectable interrupt-delivery providers

The interrupt broker no longer names concrete I/O APIC, PCI MSI/MSI-X, or SMP-affinity implementations. `KernelIoApicRouterServices`, `KernelPciInterruptRouterServices`, and `KernelInterruptAffinityServices` are parent-owned contracts used by the broker core. Concrete providers register during selected-component startup and own their hardware-specific state or policy.

`Kernel.Interrupts.IoApicRouter`, `Kernel.Interrupts.PciMessageSignaledRouter`, and `Kernel.Interrupts.AffinityResolver` are therefore promoted from `candidate` to `selectable`. The standard Drivers configuration selects all three to preserve existing behaviour, but Kath may omit or substitute them without editing `KernelInterruptBroker` source. Legacy GSI routing requires an installed GSI provider; PCI interrupt requests require the PCI routing provider; affinity falls back to the requested processor/current APIC when its policy provider is absent.

## 0.2.22 — selectable PCI configuration transports

PCI enumeration no longer owns the concrete configuration-space mechanism. `KernelPciConfigurationServices` is the parent-owned dispatch boundary used by `KernelPci.TryRead*` and `KernelPci.TryWrite*`. The x64 CF8/CFC mechanism and PCIe ECAM mechanism are separate providers with their own source ownership and startup selection.

`Kernel.Pci.LegacyConfiguration` owns configuration mechanism #1 port access. `Kernel.Pci.EcamConfiguration` owns MCFG region matching and transient ECAM function mappings. The internal `Kernel.Pci.ConfigurationRegistry` owns only registration and dispatch. A generated OS may select either transport or both; the default Drivers configuration selects both to preserve the existing x64 discovery behaviour.

This is intentionally a transport split, not yet a claim that ACPI MCFG discovery itself is replaceable. The PCI core still consumes the existing ACPI table contract to obtain ECAM bus ranges when the ECAM provider is selected. A later pass can separate ACPI table providers from the ACPI registry in the same way.


## 0.2.22 — ACPI table-consumer providers

The ACPI root is now treated as a registry rather than as the owner of every ACPI table interpretation. `Kernel.Acpi.TableRegistry` owns only RSDP/RSDT/XSDT validation and generic checksummed table lookup. MADT, MCFG and HPET interpretation register through `KernelAcpiMadtServices`, `KernelAcpiMcfgServices` and `KernelAcpiHpetServices`.

`Kernel.Acpi.MadtTopology`, `Kernel.Acpi.McfgDiscovery`, and `Kernel.Acpi.HpetTable` are selectable. Their parser state and table-specific interpretation disappear when their component symbol is omitted, while `KernelAcpi` remains valid and its existing facade methods return unavailable/zero. The standard x64 configuration selects all three where currently required.

FADT, fixed-feature power and the embedded controller are documented as candidates rather than selectable components in this release. They still share concrete generic-address/FADT dependencies and will be promoted only after those dependencies are represented by neutral contracts.


## 0.2.23 — selectable ACPI FADT, power and EC providers

The remaining ACPI platform coupling has been removed. `KernelAcpiFadtProvider` owns FADT parsing and registers through `KernelAcpiFadtServices`; `KernelAcpiPowerProvider` consumes only the FADT/register contracts and registers through `KernelAcpiPowerServices`; `KernelAcpiEcProvider` consumes only table lookup and generic-register services and registers through `KernelAcpiEcServices`.

`Kernel.Acpi.FadtPlatform`, `Kernel.Acpi.Power`, and `Kernel.Acpi.EmbeddedController` are therefore `selectable`. `Kernel.Acpi.RegisterAccess` is internal dependency infrastructure. The old concrete `KernelAcpiFadt`, `KernelAcpiPower`, and `KernelAcpiEc` APIs are intentionally removed instead of retained as compatibility shims.


## 0.2.24 — selectable networking services and protocol codecs

The kernel socket implementation is no longer a mandatory child of `KernelNetworking`. `KernelSocketServices` is the neutral registration/dispatch boundary. The concrete `KernelSockets` source owns socket records, allocation, TCP state observation and UDP receive queues, while the networking core, API and packet stack know only the service contract. This allows Kath to omit or substitute the socket service without editing the networking core.

The former combined `KernelDhcpDns` source has also been split into independent DHCP and DNS codecs. Both register through `KernelNetworkProtocolServices`; the networking API no longer names their concrete implementations. DHCP and DNS can therefore be selected independently.

## 0.2.25 — selectable Ethernet/network-layer protocols

`KernelNetworkStack` is now an Ethernet dispatch facade rather than the owner of ARP, IPv4 and IPv6/NDP packet implementations. `KernelNetworkPacketServices` provides the source-neutral registration boundary.

`Kernel.Networking.ArpProtocol`, `Kernel.Networking.Ipv4Protocol`, and `Kernel.Networking.Ipv6NdpProtocol` are independently selectable source components. ARP owns ARP request/reply handling, IPv4 owns IPv4 validation plus the current ICMPv4/UDP/TCP packet framing path, and IPv6/NDP owns IPv6 validation and neighbour-discovery interpretation. Omitting one protocol leaves the Ethernet/interface core valid and causes only that protocol's dispatch operations to report unavailable.

The socket service now declares its actual IPv4 dependency in the canonical component graph. The current IPv4 component still contains ICMPv4, UDP framing and TCP observation; these are deliberately retained together for this release rather than claiming finer selectability before their transport contracts are extracted.

## 0.2.26 — selectable IPv4 control/transport protocols

The former `KernelIpv4Protocol` bundle has been reduced to the IPv4 network layer: header validation, routing-facing transmission and IPv4 framing. ICMPv4, UDP-over-IPv4 and the current TCP observation path now register independently through `KernelIpv4TransportServices`.

`KernelIpv4Services` is the source-neutral outbound IPv4 contract. This prevents the new control/transport components from reaching into private IPv4 implementation state while still allowing them to construct their own protocol payloads. The existing `KernelNetworkStack`/`KernelNetworkApi` high-level surface remains stable and dispatches through the registries.



## 0.2.27 — selectable PCI storage and network drivers

NVMe, AHCI/SATA, Intel E1000/E1000e, and Realtek RTL8168/RTL8111 are now canonical selectable components. Their concrete startup and service calls are guarded by component selection symbols, while their normal integration continues through the existing driver, storage, networking, PCI, interrupt, and memory contracts. The standard Storage and Networking profiles still select them by default.


## 0.2.28 — process-management responsibility split

`KernelProcesses` has been decomposed around process-management ownership rather than file size. PID/process-record storage now owns process-table locking, PID allocation, lookup, snapshots and active-count transitions. Process lifecycle is separated into creation and termination. Image population is separate from address-space/allocation ownership. Foreground-command ownership/cancellation and process signalling/kill handling are also separate source responsibilities.

These five seams are deliberately recorded as `candidate`, not `selectable`: they still share the private `ProcessRecord` representation. The next promotion step is to replace that shared representation with neutral process-record, lifecycle, address-space, foreground and signalling contracts so Kath can include/substitute policy without compiling against private state. Thread management and scheduler policy are intentionally unchanged in this release.


## 0.2.29 — opaque process-record ownership

The private process table representation is now owned exclusively by `KernelProcessRecordStore`. Process lifecycle, execution, diagnostics, address-space ownership, foreground control and signalling no longer receive `ProcessRecord*` or access table fields directly; they use an opaque `KernelProcessRecordHandle` and record-store operations.

This removes the representation-sharing blocker identified in 0.2.28 without falsely declaring every process responsibility selectable. The record store is internal required infrastructure. Lifecycle, address-space ownership, foreground control and signalling remain candidates until their invocation paths are registered behind explicit services and can be omitted/substituted independently.

## 0.2.30 — selectable process services

Process lifecycle, address-space ownership, foreground control and signalling now register through neutral process-service contracts. The public `KernelProcesses` facade remains stable and does not directly name those providers. The opaque record store remains mandatory infrastructure.


## 0.2.31 — thread/scheduler boundary
Thread record ownership, lifecycle, state control, and priority selection are now separate source responsibilities. Candidate status remains until direct scheduler-state access is replaced by explicit contracts.

## 0.2.32 — selectable scheduler policy

The default priority-first policy no longer reads scheduler-owned thread or CPU arrays. `KernelRunnableThreadServices` exposes an opaque, read-only runnable-thread view plus scheduler-owned operations for processor load and controlled reassignment. `KernelSchedulingPolicyServices` owns policy registration and dispatch.

`Kernel.Scheduler.PriorityPolicy` is now `selectable`. The standard Scheduler configuration selects it by default, while a custom policy can register against the same contract without taking ownership of `ThreadRecord`, `_threads`, `_cpus`, context-switch state, or dispatch machinery.

This release also repairs the malformed `HasDispatchableWork` tail left by the 0.2.31 source extraction and moves that decision fully into the policy contract.



## 0.2.33 — CPU runnable-set and placement policy boundary

Per-CPU runnable-set/load ownership is now isolated behind `KernelCpuRunQueueServices`. It remains internal scheduler infrastructure because the current implementation represents runnable queues through thread ownership (`ProcessorIndex`) plus CPU-local scheduler state rather than a standalone queue data structure.

`KernelThreadPlacementServices` is a neutral registry for CPU-placement policy. `KernelLoadAwarePlacementPolicy` is the default selectable implementation and owns affinity-aware initial placement, wake-time rebalancing, migration residency hysteresis and work stealing. `KernelPrioritySchedulingPolicy` no longer migrates threads or reads CPU scheduler arrays directly.
