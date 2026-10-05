# Console subsystem Run API

Kath 0.31.0 introduces the first beginner-facing unified subsystem lifecycle API. The Console subsystem lets kernel code select a console policy without manually choosing and initializing each low-level transport.

## Basic use

```csharp
using Inu.Kernel.Console;

Console.Run(ConsoleType.Auto);
Console.WriteLine("Inu is running.");
```

The Inu bootstrap registers the current `IBootContext` before normal kernel/user code executes. This means ordinary code uses `Console.Run(ConsoleType)`; the `Console.Run(ConsoleType, IBootFramebufferContext)` overload exists for bootstrap and specialist code.

## Console modes

`ConsoleType.Auto` selects `FramebufferText` when a valid GOP framebuffer is available and otherwise selects `Serial`.

`ConsoleType.TextAscii` uses Inu's framebuffer text console and serial debug mirror with ASCII character output.

`ConsoleType.TextAnsi` enables ANSI-control parsing for framebuffer text while preserving the original ANSI byte stream on the serial/debug transport. The initial framebuffer subset handles clear-screen and caret visibility sequences and safely consumes unsupported SGR/cursor sequences rather than rendering escape bytes as visible text.

`ConsoleType.FramebufferText` uses the existing GOP framebuffer text renderer while retaining the serial debug mirror.

`ConsoleType.Graphics` stops text rendering into the framebuffer so `Inu.Kernel.Graphics` or another graphics owner can use the display, while the serial/debug console remains active.

`ConsoleType.Serial` uses only the primary serial/debug console.

## Lifecycle

Every unified subsystem follows a predictable lifecycle vocabulary. Console 0.31.0 implements:

```csharp
Console.Run(ConsoleType.TextAnsi);
Console.Stop();
Console.Resume();
Console.Unload();
```

`Stop()` pauses normal output but retains the framebuffer history, selected mode, boot context and transport configuration. `Resume()` reactivates a stopped console. `Unload()` removes the logical console binding and resets the low-level output state. The saved boot context remains available so a later `Run(...)` can initialize the subsystem again.

Inspect lifecycle state with:

```csharp
ConsoleState state = Console.State;
ConsoleType active = Console.Mode;
ConsoleType requested = Console.RequestedMode;
bool running = Console.IsRunning;
```

## Beginner and professional layers

The high-level Console class selects policy. It does not replace the existing implementation. `Console.Run(...)` configures the same `KernelConsole`, framebuffer, serial, font, buffering and input machinery already used by Inu.

Use `Console` when you want a simple lifecycle-oriented API. Use `KernelConsole` when kernel code needs exact framebuffer buffers, font installation, serial diagnostic lines, caret ticks, direct raw writes, shell input services or other low-level control.

This is the model intended for the rest of the SDK:

```text
Subsystem.Run(mode)
Subsystem.Stop()
Subsystem.Resume()
Subsystem.Unload()

// Advanced/professional layer remains available underneath.
KernelSubsystem.*
```
