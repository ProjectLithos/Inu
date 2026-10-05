using Inu.Primitives;

namespace Inu.Boot.Contracts;

/// <summary>
/// Provides a reserved x86 application-processor startup trampoline when that
/// startup mechanism is selected by the OS author.
/// </summary>
public interface IApplicationProcessorTrampoline : IBootContext
{
    PhysicalAddress ApplicationProcessorTrampolineAddress { get; }
}

public readonly struct ApplicationProcessorTrampolineContext : IApplicationProcessorTrampoline
{
    public ApplicationProcessorTrampolineContext(PhysicalAddress applicationProcessorTrampolineAddress)
        => ApplicationProcessorTrampolineAddress = applicationProcessorTrampolineAddress;

    public PhysicalAddress ApplicationProcessorTrampolineAddress { get; }
}
