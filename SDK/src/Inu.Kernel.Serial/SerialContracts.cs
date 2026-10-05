using System;

namespace Inu.Kernel.Serial;

/// <summary>Identifies a serial/debug transport managed by Inu.</summary>
public enum KernelSerialTransport : Byte
{
    /// <summary>No serial transport.</summary>
    None = 0,
    /// <summary>Legacy PC-compatible 8250/16450/16550 UART, normally COM1.</summary>
    Uart16550 = 1,
    /// <summary>PCI communications-class UART using a 16550-compatible programming interface.</summary>
    PciUart = 2,
    /// <summary>VirtIO console device.</summary>
    VirtioConsole = 3
}

/// <summary>Reports the serial facilities discovered and attached to kernel debug output.</summary>
public readonly struct KernelSerialCapabilities
{
    /// <summary>Creates one serial capability snapshot.</summary>
    public KernelSerialCapabilities(Boolean initialized, Boolean uart16550, UInt32 pciUartsDiscovered, UInt32 pciUartsOnline, UInt32 virtioConsoles, Boolean secondaryMirroring, UInt64 transmitFailures)
    { Initialized=initialized; Uart16550=uart16550; PciUartsDiscovered=pciUartsDiscovered; PciUartsOnline=pciUartsOnline; VirtioConsoles=virtioConsoles; SecondaryMirroring=secondaryMirroring; TransmitFailures=transmitFailures; }
    /// <summary>Whether the post-PCI serial facility has been initialized.</summary>
    public Boolean Initialized { get; }
    /// <summary>Whether the legacy 16550-compatible COM1 debug UART is active.</summary>
    public Boolean Uart16550 { get; }
    /// <summary>Number of PCI communications-class serial functions discovered.</summary>
    public UInt32 PciUartsDiscovered { get; }
    /// <summary>Number of discovered PCI UARTs with a supported 16550-compatible I/O or MMIO BAR.</summary>
    public UInt32 PciUartsOnline { get; }
    /// <summary>Number of started VirtIO console devices.</summary>
    public UInt32 VirtioConsoles { get; }
    /// <summary>Whether at least one post-boot secondary serial transport is mirrored by KernelConsole.</summary>
    public Boolean SecondaryMirroring { get; }
    /// <summary>Best-effort secondary transmit failures observed since initialization.</summary>
    public UInt64 TransmitFailures { get; }
}
