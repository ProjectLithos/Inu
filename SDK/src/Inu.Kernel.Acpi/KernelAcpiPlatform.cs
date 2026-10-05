using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Describes one ACPI Generic Address Structure.</summary>
public readonly struct AcpiGenericAddress
{
    internal AcpiGenericAddress(Byte addressSpace, Byte bitWidth, Byte bitOffset, Byte accessSize, UInt64 address)
    { AddressSpace = addressSpace; BitWidth = bitWidth; BitOffset = bitOffset; AccessSize = accessSize; Address = address; }
    /// <summary>Gets the ACPI address-space identifier.</summary>
    public Byte AddressSpace { get; }
    /// <summary>Gets the register width in bits.</summary>
    public Byte BitWidth { get; }
    /// <summary>Gets the register bit offset.</summary>
    public Byte BitOffset { get; }
    /// <summary>Gets the ACPI access-size encoding.</summary>
    public Byte AccessSize { get; }
    /// <summary>Gets the physical memory address or I/O port number.</summary>
    public UInt64 Address { get; }
    /// <summary>Gets whether the structure names a usable register.</summary>
    public Boolean IsPresent() => Address != 0UL && (AddressSpace == 0U || AddressSpace == 1U);
}
