using System;

namespace Inu.Kernel.Processes;

/// <summary>Creates and manages isolated user processes. Implementation responsibilities are split across focused process-management source units.</summary>
public static unsafe partial class KernelProcesses
{
    internal const UInt32 MaximumProcesses=8U, MaximumTablesPerProcess=64U, MaximumImageAllocations=17U;
    private const UInt32 MaximumExecutionProcessors=256U;
    public const Int64 ForegroundCancellationExitCode=-130L;
    private const UInt64 DefaultStackBytes=1048576UL, UserStackTop=0x00007FFFFFF00000UL;
}
