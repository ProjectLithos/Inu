# Kernel coder API

`Kernel/<OSName>/Kernel.cs` is the OS author's primary privileged policy source.
Kath must never populate it by copying `using` directives from Inu bootstrap/HAL implementation files.

The generated `using` list is instead derived from the OS configuration and contains only stable coder-facing namespaces whose facilities are present in the kernel.

The baseline kernel-facing namespaces are:

```csharp
using System;
using Inu.Kernel.Console;
using Inu.Kernel.Memory;
using Inu.Kernel.Processes;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Interrupts;
using Inu.Kernel.Time;
using Inu.Kernel.Power;
```

Additional imports are emitted only when applicable:

```csharp
using Inu.Kernel.Scheduler;   // scheduler selected
using Inu.Kernel.Smp;         // SMP selected
using Inu.Kernel.Drivers;     // selected kernel model owns drivers
using Inu.Kernel.Hardware;    // unified device-tree policy when drivers are kernel-resident
using Inu.Kernel.Storage;     // storage/filesystem is kernel-resident
using Inu.Kernel.Graphics;    // graphics support selected
using Inu.Kernel.Input;       // kernel input mechanism present
using Inu.Kernel.Networking;  // networking is kernel-resident
using Inu.Kernel.Audio;       // audio is kernel-resident
```

These namespaces expose lifecycle/policy facades such as `Console`, `Memory`, `Processes`, `SystemCalls`, `Interrupts`, `Time`, `Scheduler`, `Smp`, `Drivers`, `Devices`, `FileSystem`, `Graphics`, `Input`, `Networking`, `Audio`, and `Power`. The lower-level `Kernel*` implementation classes are not automatically SDK promises merely because they are public.

Example:

```csharp
using System;
using Inu.Kernel.Console;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Power;

namespace KathInu.MyOs.Kernel;

public static class Kernel
{
    public static Boolean Start()
    {
        Console.WriteLine("My kernel policy is running.");

        // Runtime policy belongs here. For example, the OS author may pause,
        // resume, stop or unload selected facilities through their lifecycle
        // facades where the current architecture supports doing so.

        return true;
    }
}
```

Kath selection controls which source components are present. `Kernel.cs` controls the OS author's runtime policy over those selected mechanisms. Inu bootstrap/HAL implementation namespaces remain implementation detail unless separately promoted to the explicit SDK API.


## Core kernel-policy facades

The core kernel imports are present because every generated kernel needs to be able to express policy over these mechanisms without importing Inu bootstrap internals:

```csharp
Memory.GetStatistics();
Processes.GetActiveCount();
SystemCalls.IsInitialized();
Interrupts.Run();
```

When kernel-resident drivers are selected, `Inu.Kernel.Hardware.Devices` exposes unified device-tree policy such as counts, matching/start, pause and resume. It deliberately does not make PCI/USB/VirtIO implementation classes part of the default coder API.

`SystemCalls` preserves Inu's native ABI rule: custom native services are registered only as Get, Set or Event messages. No fourth native syscall class is introduced.


## Runtime policy controls

The OS author can now express common runtime choices directly in coder-owned `Kernel.cs` instead of editing Inu bootstrap implementation files.

```csharp
// Scheduler policy
Scheduler.SetQuantumMilliseconds(5);

// CPU-role policy
Smp.SetRole(KernelCpuRole.Kernel, 0);
Smp.SetRole(KernelCpuRole.Userland, KernelCpuSet.All());

// Console buffering policy: 0=automatic, 1=single, 2=double, 3=triple
Console.SetBufferCount(0);

// Graphical-session startup policy. These remain ordinary isolated ring-3 processes.
Processes.ConfigureGraphicalSession("/BIN/INU-DESKTOP.EXE", "/BIN/INU-LOGIN.EXE");
Processes.StartGraphicalSession();
```

These calls change runtime policy over mechanisms that Kath selected into the OS. They do not cause Kath to regenerate or replace coder-owned source.

Filesystem **path syntax policy is deliberately not exposed by this release**. The current VFS still canonicalises around `/`; advertising a configurable joiner/case policy before the VFS obeys it would make the SDK API untruthful. That policy must be implemented in the VFS first and only then promoted to this coder-facing surface.
