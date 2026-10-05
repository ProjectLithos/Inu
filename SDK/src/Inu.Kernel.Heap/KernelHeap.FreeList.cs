using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Heap;

public static unsafe partial class KernelHeap
{
    private static void Coalesce()
    {
        Boolean changed = true;
        State* state = GetState();
        UInt64* starts = state->Starts;
        UInt64* lengths = state->Lengths;
        Byte* states = state->States;
        {
            while (changed)
            {
                changed = false;
                for (Int32 i = 0; i < MaximumBlocks && !changed; i++)
                {
                    if (states[i] != 1) continue;
                    for (Int32 j = 0; j < MaximumBlocks; j++)
                    {
                        if (i == j || states[j] != 1) continue;
                        if (starts[i] > 0xFFFFFFFFFFFFFFFFUL - lengths[i]) continue;
                        if (starts[i] + lengths[i] != starts[j]) continue;
                        lengths[i] += lengths[j];
                        states[j] = 0;
                        starts[j] = 0UL;
                        lengths[j] = 0UL;
                        changed = true;
                        break;
                    }
                }
            }
        }
    }

    private static UInt64 NextToken()
    {
        UInt64 token = _nextToken++;
        if (token == 0UL) token = _nextToken++;
        return token;
    }

    private static void Reset()
    {
        _committed = 0UL;
        _allocated = 0UL;
        _peak = 0UL;
        _live = 0;
        _nextToken = 1UL;
        ResetExtendedDiagnostics();
        State* state = GetState();
        UInt64* starts = state->Starts;
        UInt64* lengths = state->Lengths;
        UInt64* tokens = state->Tokens;
        Byte* states = state->States;
        for (Int32 i = 0; i < MaximumBlocks; i++)
        {
            starts[i] = 0UL;
            lengths[i] = 0UL;
            tokens[i] = 0UL;
            states[i] = 0;
        }
        SynchronizeDiagnosticHeader();
    }
}
