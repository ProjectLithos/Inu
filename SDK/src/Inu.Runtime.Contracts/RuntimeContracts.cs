using Inu.Primitives;

namespace Inu.Runtime.Contracts;

public interface IRuntimeAllocator
{
    bool TryAllocate(Bytes size, Alignment alignment, out Address address);
    bool Release(Address address, Bytes size);
}

public interface IRuntimePanicHandler
{
    bool Panic(string message);
}

public readonly record struct RuntimeConfiguration(
    IRuntimeAllocator Allocator,
    IRuntimePanicHandler PanicHandler);
