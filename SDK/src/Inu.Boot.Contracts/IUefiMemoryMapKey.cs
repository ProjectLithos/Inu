namespace Inu.Boot.Contracts;
public interface IUefiMemoryMapKey : IBootContext { ulong MemoryMapKey { get; } }
public readonly struct UefiMemoryMapKeyContext : IUefiMemoryMapKey
{
    public UefiMemoryMapKeyContext(ulong key) => MemoryMapKey=key;
    public ulong MemoryMapKey { get; }
}
