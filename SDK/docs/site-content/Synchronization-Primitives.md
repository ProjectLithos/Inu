# Inu Synchronization Primitives

Inu 0.16.0 exposes freestanding synchronization primitives through `Inu.Kernel.Synchronization`. The API is designed for kernel code: state is held directly in value/unmanaged objects, no hot-path managed allocation is required, and the x64 backend uses real locked instructions and hardware memory fences.

## Atomic operations and ordering

`KernelAtomic` provides 64-bit load/store, compare-exchange, exchange, fetch-add, increment/decrement, a full memory barrier, and the processor spin-wait hint. `KernelMemoryOrder` documents the requested ordering intent. The current x64 backend is at least as strong as the requested ordering for the exposed operations.

## Spin lock

`KernelSpinLock` is non-recursive and intended only for short, non-blocking critical sections. `TryEnter` never waits. `Enter(spinLimit)` can be bounded; `spinLimit == 0` means unbounded spinning. `Exit` detects an unlocked state.

## Mutex

`KernelMutex` is an adaptive non-recursive mutex. Ownership is associated with the current scheduler thread when available, otherwise with the current CPU. `Unlock` rejects a non-owner. `Lock(timeoutNanoseconds)` spins initially and periodically yields through the CPU-local scheduler when the scheduler is online.

## Semaphore

`KernelSemaphore` is a bounded counting semaphore. Initialization requires `initialCount <= maximumCount`. Acquisition uses compare-exchange so a count can never become negative, and release rejects overflow beyond the configured maximum.

## Event

`KernelEvent` supports manual-reset and auto-reset semantics. A manual-reset event remains signalled until `Reset`; an auto-reset event atomically consumes one signal when one waiter succeeds.

## Reader/writer lock

`KernelReaderWriterLock` permits concurrent readers or one writer. Waiting writers are tracked and new readers stop entering while a writer is queued, avoiding indefinite writer starvation. `KernelReaderWriterLockInfo` exposes reader/writer state for diagnostics.

## Barrier

`KernelBarrier` is reusable and generation counted. The last participant resets the arrival count and advances the generation. A timed-out participant attempts to withdraw its arrival without corrupting a concurrently completed generation.

## Lock-free primitive

`KernelLockFreeStack64` is an intrusive lock-free LIFO stack over caller-owned unmanaged `KernelLockFreeStackNode64` records. Its head packs a 32-bit index and a 32-bit generation tag so ordinary pop/push reuse does not suffer the simple ABA error of an untagged head. A node must not be pushed again while it is already present in the stack.

## Timeouts

`KernelSynchronizationTimeout.Infinite` requests an indefinite wait. Finite nanosecond timeouts use the initialized monotonic kernel clock. Before the clock is online, non-zero finite waits fail rather than pretending that an uncalibrated spin count is a duration.

## Architecture boundary

The public synchronization API does not expose x64 opcodes. The current x64 backend provides compare-exchange, exchange, fetch-add, atomic load/store, `mfence`, and `pause` through the canonical architecture boundary. The same synchronization surface can therefore be retained when an ARM64 backend is added.
