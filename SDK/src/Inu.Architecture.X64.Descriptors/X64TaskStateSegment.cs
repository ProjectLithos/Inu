using Inu.Architecture;
using Inu.Primitives;

namespace Inu.Architecture.X64.Descriptors;

/// <summary>Initialises and owns one caller-allocated x64 Task State Segment.</summary>
public sealed unsafe class X64TaskStateSegment : ITaskStateSegment
{
    private const uint ArchitecturalSize = 104;
    private TaskStateSegmentConfiguration configuration;
    private bool configured;

    /// <inheritdoc />
    public ProcessorId GetProcessorId() => configuration.ProcessorId;

    /// <inheritdoc />
    public bool Configure(TaskStateSegmentConfiguration value)
    {
        if (value.TaskStateSegmentAddress.Value == 0 || value.TaskStateSegmentCapacity < ArchitecturalSize ||
            value.RingZeroStackTop.Value == 0 || value.DoubleFaultStackTop.Value == 0 || value.NmiStackTop.Value == 0)
        {
            return false;
        }
        if (value.IoPermissionBitmapPolicy == IoPermissionBitmapPolicy.DenyByDefault &&
            (value.IoPermissionBitmapAddress.Value == 0 || value.IoPermissionBitmapLength == 0))
        {
            return false;
        }

        configuration = value;
        byte* tss = (byte*)value.TaskStateSegmentAddress.Value;
        for (uint index = 0; index < ArchitecturalSize; index++) tss[index] = 0;
        Write64(tss, 4, value.RingZeroStackTop.Value);
        Write64(tss, 36, value.DoubleFaultStackTop.Value); // IST1
        Write64(tss, 44, value.NmiStackTop.Value);         // IST2
        if (value.MachineCheckStackTop.Value != 0) Write64(tss, 52, value.MachineCheckStackTop.Value); // IST3

        ushort bitmapOffset = (ushort)ArchitecturalSize;
        if (value.IoPermissionBitmapPolicy == IoPermissionBitmapPolicy.DenyByDefault)
        {
            ulong bitmapDistance = value.IoPermissionBitmapAddress.Value - value.TaskStateSegmentAddress.Value;
            if (value.IoPermissionBitmapAddress.Value < value.TaskStateSegmentAddress.Value || bitmapDistance > ushort.MaxValue)
                return false;
            bitmapOffset = (ushort)bitmapDistance;
            byte* bitmap = (byte*)value.IoPermissionBitmapAddress.Value;
            for (uint index = 0; index < value.IoPermissionBitmapLength; index++) bitmap[index] = 0xFF;
        }
        Write16(tss, 102, bitmapOffset);
        configured = true;
        return true;
    }

    /// <inheritdoc />
    public bool Install(SegmentSelector taskStateSelector) => configured && NativeMethods.LoadTaskRegister(taskStateSelector.Value);

    /// <inheritdoc />
    public bool SetRingZeroStack(Address stackTop)
    {
        if (!configured || stackTop.Value == 0) return false;
        Write64((byte*)configuration.TaskStateSegmentAddress.Value, 4, stackTop.Value);
        configuration = configuration with { RingZeroStackTop = stackTop };
        return true;
    }

    /// <inheritdoc />
    public bool SetInterruptStack(byte entry, Address stackTop)
    {
        if (!configured || entry is < 1 or > 7 || stackTop.Value == 0) return false;
        Write64((byte*)configuration.TaskStateSegmentAddress.Value, (uint)(28 + entry * 8), stackTop.Value);
        return true;
    }

    /// <inheritdoc />
    public Address GetAddress() => configured ? configuration.TaskStateSegmentAddress : new Address(0);

    /// <inheritdoc />
    public uint GetLimit()
    {
        if (!configured) return 0;
        if (configuration.IoPermissionBitmapPolicy != IoPermissionBitmapPolicy.DenyByDefault) return ArchitecturalSize - 1;
        ulong length = configuration.IoPermissionBitmapAddress.Value - configuration.TaskStateSegmentAddress.Value + configuration.IoPermissionBitmapLength;
        return length > uint.MaxValue ? 0 : (uint)length;
    }

    private static void Write16(byte* target, uint offset, ushort value) => *(ushort*)(target + offset) = value;
    private static void Write64(byte* target, uint offset, ulong value) => *(ulong*)(target + offset) = value;
}
