using Inu.Primitives;

namespace Inu.Architecture;

/// <summary>Identifies a selector loaded into an x64 segment register.</summary>
public readonly record struct SegmentSelector(ushort Value)
{
    /// <summary>Gets the descriptor-table index encoded by this selector.</summary>
    public ushort Index => (ushort)(Value >> 3);

    /// <summary>Gets the requested privilege level encoded by this selector.</summary>
    public DescriptorPrivilegeLevel PrivilegeLevel => (DescriptorPrivilegeLevel)(Value & 0x3);

    /// <summary>Creates a selector for a GDT entry.</summary>
    /// <returns>The encoded selector value.</returns>
    public static SegmentSelector Create(ushort index, DescriptorPrivilegeLevel privilegeLevel) =>
        new((ushort)((index << 3) | ((ushort)privilegeLevel & 0x3)));
}

/// <summary>Defines x64 descriptor privilege levels.</summary>
public enum DescriptorPrivilegeLevel : byte
{
    /// <summary>Kernel privilege.</summary>
    Kernel = 0,
    /// <summary>Privilege level one.</summary>
    Level1 = 1,
    /// <summary>Privilege level two.</summary>
    Level2 = 2,
    /// <summary>User privilege.</summary>
    User = 3
}

/// <summary>Defines the I/O bitmap policy applied to an x64 TSS.</summary>
public enum IoPermissionBitmapPolicy : byte
{
    /// <summary>No bitmap is installed; all port access is controlled by IOPL.</summary>
    Disabled = 0,
    /// <summary>A caller-provided deny-by-default bitmap follows the TSS.</summary>
    DenyByDefault = 1
}

/// <summary>Describes storage and selectors for one processor's x64 GDT.</summary>
public readonly record struct GlobalDescriptorTableConfiguration(
    ProcessorId ProcessorId,
    Address TableAddress,
    uint TableCapacity,
    SegmentSelector KernelCodeSelector,
    SegmentSelector KernelDataSelector,
    SegmentSelector UserCodeSelector,
    SegmentSelector UserDataSelector,
    SegmentSelector TaskStateSelector);

/// <summary>Describes one processor's x64 TSS and emergency stacks.</summary>
public readonly record struct TaskStateSegmentConfiguration(
    ProcessorId ProcessorId,
    Address TaskStateSegmentAddress,
    uint TaskStateSegmentCapacity,
    Address RingZeroStackTop,
    Address DoubleFaultStackTop,
    Address NmiStackTop,
    Address MachineCheckStackTop,
    IoPermissionBitmapPolicy IoPermissionBitmapPolicy,
    Address IoPermissionBitmapAddress,
    uint IoPermissionBitmapLength);

/// <summary>Controls and installs one processor's global descriptor table.</summary>
public interface IGlobalDescriptorTable
{
    /// <summary>Gets the processor that owns this table.</summary>
    ProcessorId GetProcessorId();

    /// <summary>Builds all required descriptors into caller-owned memory.</summary>
    /// <returns><see langword="true"/> when the table is valid.</returns>
    bool Configure(GlobalDescriptorTableConfiguration configuration, ITaskStateSegment taskStateSegment);

    /// <summary>Loads the configured table and reloads segment registers.</summary>
    /// <returns><see langword="true"/> when LGDT and segment reload complete.</returns>
    bool Install();

    /// <summary>Gets a configured selector by descriptor index.</summary>
    /// <returns>The selector, or zero when the index is unavailable.</returns>
    SegmentSelector GetSelector(ushort descriptorIndex);
}

/// <summary>Controls one processor's x64 Task State Segment.</summary>
public interface ITaskStateSegment
{
    /// <summary>Gets the processor that owns this TSS.</summary>
    ProcessorId GetProcessorId();

    /// <summary>Initialises RSP0, IST stacks and I/O bitmap policy.</summary>
    /// <returns><see langword="true"/> when the TSS is valid.</returns>
    bool Configure(TaskStateSegmentConfiguration configuration);

    /// <summary>Loads this TSS into the current processor's task register.</summary>
    /// <returns><see langword="true"/> when LTR completes.</returns>
    bool Install(SegmentSelector taskStateSelector);

    /// <summary>Updates the ring-zero stack used for user-to-kernel transitions.</summary>
    /// <returns><see langword="true"/> when RSP0 was updated.</returns>
    bool SetRingZeroStack(Address stackTop);

    /// <summary>Updates one Interrupt Stack Table entry.</summary>
    /// <returns><see langword="true"/> when the entry was updated.</returns>
    bool SetInterruptStack(byte entry, Address stackTop);

    /// <summary>Gets the TSS address used by the descriptor.</summary>
    Address GetAddress();

    /// <summary>Gets the architectural TSS limit.</summary>
    uint GetLimit();
}
