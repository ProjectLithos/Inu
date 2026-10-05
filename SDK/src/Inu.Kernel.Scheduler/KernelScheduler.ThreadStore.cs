using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Scheduler;

public static unsafe partial class KernelScheduler
{
    private struct ThreadRecord
    {
        internal UInt64 Id, StackBase, StackTop, EntryPoint, Argument, AffinityMask, ContextAddress, LastMigrationNanoseconds;
        internal KernelHeapAllocation StackAllocation, ContextAllocation;
        internal UInt32 State, Priority, ProcessorIndex, NextReady;
        internal Byte ExecutionClass, OneShot, RetireReady;
    }
    private static ThreadRecord* _threads;
    private static UInt32 _activeThreads;
    private static UInt64 _nextThreadId = 1UL;
}
