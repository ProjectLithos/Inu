using Inu.Memory.AddressSpace;
using Inu.Memory.AddressSpace.X64;

namespace Inu.Kernel.Sample;

internal static class AddressSpaceExamples
{
    internal static bool ValidateStandardLayout()
    {
        if (!X64KernelAddressSpace.TryCreateStandard(out KernelAddressSpaceLayout layout)) return false;
        if (!X64KernelAddressSpace.Validate(layout)) return false;
        return layout.KernelHeap.BaseAddress == X64KernelAddressSpace.KernelHeapBase &&
            layout.DirectPhysicalMap.BaseAddress == X64KernelAddressSpace.DirectMapBase;
    }
}
