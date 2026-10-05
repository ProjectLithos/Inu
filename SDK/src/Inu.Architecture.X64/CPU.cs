using Inu.Core;

namespace Inu.Architecture.X64;

/// <summary>Provides direct x64 processor operations for compatibility with existing kernel code.</summary>
/// <nova.when>Use for small processor operations when the architecture has already been selected as x64.</nova.when>
/// <nova.depends>Inu x64 native entry points</nova.depends>
public static class CPU
{
    /// <summary>Enables maskable interrupts on the current x64 processor.</summary>
    /// <returns><see langword="true"/> when interrupts were enabled.</returns>
    /// <example><code>bool enabled = CPU.EnableInterrupts();</code></example>
    public static bool EnableInterrupts() => NativeMethods.EnableInterrupts();

    /// <summary>Disables maskable interrupts on the current x64 processor.</summary>
    /// <returns><see langword="true"/> when interrupts were disabled.</returns>
    /// <example><code>bool disabled = CPU.DisableInterrupts();</code></example>
    public static bool DisableInterrupts() => NativeMethods.DisableInterrupts();

    /// <summary>Determines whether maskable interrupts are enabled on the current x64 processor.</summary>
    /// <returns><see langword="true"/> when maskable interrupts are enabled.</returns>
    /// <example><code>bool enabled = CPU.AreInterruptsEnabled();</code></example>
    public static bool AreInterruptsEnabled() => NativeMethods.AreInterruptsEnabled();

    /// <summary>Enters the architecture-defined terminal halt operation.</summary>
    /// <returns><see langword="true"/> only if the native halt operation returns.</returns>
    /// <example><code>bool resumed = CPU.Halt();</code></example>
    [DoesNotReturn]
    public static bool Halt() => NativeMethods.Halt();
}

/// <summary>Provides x64 port-mapped input/output operations.</summary>
/// <nova.when>Use only for hardware that exposes legacy x64 I/O ports.</nova.when>
/// <nova.depends>Inu x64 native port-I/O entry points</nova.depends>
public static class Port
{
    /// <summary>Writes one byte to an x64 I/O port.</summary>
    /// <param name="port">The destination I/O port.</param>
    /// <param name="value">The byte to write.</param>
    /// <returns><see langword="true"/> when the byte was written.</returns>
    /// <example><code>bool written = Port.Write8(0x3F8, value);</code></example>
    public static bool Write8(ushort port, byte value) => NativeMethods.WritePort8(port, value);

    /// <summary>Attempts to read one byte from an x64 I/O port.</summary>
    /// <param name="port">The source I/O port.</param>
    /// <param name="value">Receives the byte read from the port.</param>
    /// <returns><see langword="true"/> when the byte was read.</returns>
    /// <example><code>bool read = Port.TryRead8(0x3FD, out byte status);</code></example>
    public static bool TryRead8(ushort port, out byte value) => NativeMethods.ReadPort8(port, out value);
}
