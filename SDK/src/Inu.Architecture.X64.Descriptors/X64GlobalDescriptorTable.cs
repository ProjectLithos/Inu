using Inu.Architecture;
using Inu.Primitives;

namespace Inu.Architecture.X64.Descriptors;

/// <summary>Builds and installs one processor-local x64 GDT.</summary>
public sealed unsafe class X64GlobalDescriptorTable : IGlobalDescriptorTable
{
    private const uint RequiredBytes = 7 * 8;
    private GlobalDescriptorTableConfiguration configuration;
    private ITaskStateSegment? taskStateSegment;
    private bool configured;

    /// <inheritdoc />
    public ProcessorId GetProcessorId() => configuration.ProcessorId;

    /// <inheritdoc />
    public bool Configure(GlobalDescriptorTableConfiguration value, ITaskStateSegment taskState)
    {
        if (value.TableAddress.Value == 0 || value.TableCapacity < RequiredBytes || taskState.GetAddress().Value == 0 || taskState.GetLimit() == 0)
            return false;
        if (value.KernelCodeSelector.Index != 1 || value.KernelDataSelector.Index != 2 ||
            value.UserDataSelector.Index != 3 || value.UserCodeSelector.Index != 4 || value.TaskStateSelector.Index != 5)
            return false;

        configuration = value;
        taskStateSegment = taskState;
        ulong* table = (ulong*)value.TableAddress.Value;
        table[0] = 0;
        table[1] = 0x00AF9A000000FFFFUL; // kernel 64-bit code
        table[2] = 0x00CF92000000FFFFUL; // kernel data
        table[3] = 0x00CFF2000000FFFFUL; // user data
        table[4] = 0x00AFFA000000FFFFUL; // user 64-bit code
        WriteTaskStateDescriptor(table, 5, taskState.GetAddress().Value, taskState.GetLimit());
        configured = true;
        return true;
    }

    /// <inheritdoc />
    public bool Install()
    {
        if (!configured || taskStateSegment is null) return false;
        bool loaded = NativeMethods.LoadGlobalDescriptorTable(configuration.TableAddress.Value, (ushort)(RequiredBytes - 1),
            configuration.KernelCodeSelector.Value, configuration.KernelDataSelector.Value);
        return loaded && taskStateSegment.Install(configuration.TaskStateSelector);
    }

    /// <inheritdoc />
    public SegmentSelector GetSelector(ushort descriptorIndex) => descriptorIndex switch
    {
        1 => configuration.KernelCodeSelector,
        2 => configuration.KernelDataSelector,
        3 => configuration.UserDataSelector,
        4 => configuration.UserCodeSelector,
        5 => configuration.TaskStateSelector,
        _ => new SegmentSelector(0)
    };

    private static void WriteTaskStateDescriptor(ulong* table, int index, ulong address, uint limit)
    {
        ulong low = (limit & 0xFFFFUL) |
                    ((address & 0xFFFFFFUL) << 16) |
                    (0x89UL << 40) |
                    (((ulong)limit & 0xF0000UL) << 32) |
                    (((address >> 24) & 0xFFUL) << 56);
        table[index] = low;
        table[index + 1] = address >> 32;
    }
}
