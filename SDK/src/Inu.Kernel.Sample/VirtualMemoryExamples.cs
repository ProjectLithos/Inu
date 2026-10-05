using Inu.Memory.Virtual;
using Inu.Memory.Virtual.X64;
using Inu.Primitives;

namespace Inu.Kernel.Sample;

internal static class VirtualMemoryExamples
{
    internal static bool ValidateContracts()
    {
        VirtualMemoryProtection protection = VirtualMemoryProtection.Read | VirtualMemoryProtection.Write;
        if (!VirtualMappingRequest.TryCreate(0xFFFF800000200000UL, new PhysicalAddress(0x200000UL), VirtualPageSize.Page2MiB, protection, out VirtualMappingRequest request)) return false;
        if (!X64VirtualAddress.TryGetIndices(request.VirtualAddress, out _, out _, out _, out _)) return false;
        if (!X64PageTableCodec.TryEncodeLeaf(request.PhysicalAddress, request.PageSize, request.Protection, out ulong entry)) return false;
        return X64PageTableCodec.IsPresent(entry) && X64PageTableCodec.IsLargePage(entry);
    }
}
