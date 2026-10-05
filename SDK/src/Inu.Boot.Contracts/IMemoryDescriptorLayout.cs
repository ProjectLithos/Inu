namespace Inu.Boot.Contracts;
public interface IMemoryDescriptorLayout : IBootContext
{
    ulong MemoryDescriptorSize { get; }
    uint MemoryDescriptorVersion { get; }
}
public readonly struct MemoryDescriptorLayoutContext : IMemoryDescriptorLayout
{
    public MemoryDescriptorLayoutContext(ulong size, uint version){ MemoryDescriptorSize=size; MemoryDescriptorVersion=version; }
    public ulong MemoryDescriptorSize { get; }
    public uint MemoryDescriptorVersion { get; }
}
