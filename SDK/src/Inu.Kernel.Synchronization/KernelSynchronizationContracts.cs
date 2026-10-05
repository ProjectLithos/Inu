using System;

namespace Inu.Kernel.Synchronization;

/// <summary>Timeout constants shared by blocking-style synchronization operations.</summary>
public static class KernelSynchronizationTimeout
{
    /// <summary>Wait indefinitely until the operation succeeds.</summary>
    public const UInt64 Infinite = UInt64.MaxValue;
}

/// <summary>Memory-ordering strength requested from an atomic operation.</summary>
public enum KernelMemoryOrder : Byte
{
    Relaxed = 0,
    Acquire = 1,
    Release = 2,
    AcquireRelease = 3,
    SequentiallyConsistent = 4
}

/// <summary>Snapshot of a reader/writer lock.</summary>
public readonly struct KernelReaderWriterLockInfo
{
    public KernelReaderWriterLockInfo(UInt32 readers, Boolean writerHeld, UInt32 waitingWriters)
    { ReaderCount=readers; WriterHeld=writerHeld; WaitingWriterCount=waitingWriters; }
    public UInt32 ReaderCount { get; }
    public Boolean WriterHeld { get; }
    public UInt32 WaitingWriterCount { get; }
}

/// <summary>One intrusive node used by the tagged lock-free index stack.</summary>
public struct KernelLockFreeStackNode64
{
    public UInt64 Value;
    public UInt32 NextIndexPlusOne;
    public UInt32 Reserved;
}
