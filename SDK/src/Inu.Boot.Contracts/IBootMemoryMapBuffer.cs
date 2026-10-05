using Inu.Primitives;
namespace Inu.Boot.Contracts;
public interface IBootMemoryMapBuffer : IBootContext
{
    PhysicalAddress MemoryMapAddress { get; }
    ulong MemoryMapLength { get; }
}
public readonly struct BootMemoryMapBufferContext : IBootMemoryMapBuffer
{
    public BootMemoryMapBufferContext(PhysicalAddress address, ulong length){ MemoryMapAddress=address; MemoryMapLength=length; }
    public PhysicalAddress MemoryMapAddress { get; }
    public ulong MemoryMapLength { get; }
}
