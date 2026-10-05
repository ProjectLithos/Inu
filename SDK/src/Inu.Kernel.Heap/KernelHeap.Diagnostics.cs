using System;

namespace Inu.Kernel.Heap;

/// <summary>Describes one live heap allocation for diagnostics without exposing allocator internals.</summary>
public readonly struct KernelHeapAllocationInfo
{
    internal KernelHeapAllocationInfo(UInt64 token, UInt64 address, UInt64 byteCount, UInt64 tagHash, UInt64 allocationSequence, Boolean guarded)
    { Token=token; Address=address; ByteCount=byteCount; TagHash=tagHash; AllocationSequence=allocationSequence; Guarded=guarded; }
    public UInt64 Token { get; }
    public UInt64 Address { get; }
    public UInt64 ByteCount { get; }
    public UInt64 TagHash { get; }
    public UInt64 AllocationSequence { get; }
    public Boolean Guarded { get; }
}

/// <summary>Represents one allocation protected by leading and trailing canaries.</summary>
public readonly struct KernelGuardedHeapAllocation
{
    internal KernelGuardedHeapAllocation(KernelHeapAllocation backing, UInt64 address, UInt64 byteCount)
    { Backing=backing; Address=address; ByteCount=byteCount; }
    internal KernelHeapAllocation Backing { get; }
    public UInt64 Token => Backing.Token;
    public UInt64 Address { get; }
    public UInt64 ByteCount { get; }
}

/// <summary>Snapshot of extended heap diagnostics.</summary>
public readonly struct KernelHeapDiagnosticSnapshot
{
    internal KernelHeapDiagnosticSnapshot(KernelHeapStatistics statistics, UInt64 leakCandidates, UInt64 guardFailures, UInt64 doubleFreeFailures, UInt64 guardedAllocations, UInt64 taggedAllocations)
    { Statistics=statistics; LeakCandidates=leakCandidates; GuardFailures=guardFailures; DoubleFreeFailures=doubleFreeFailures; GuardedAllocations=guardedAllocations; TaggedAllocations=taggedAllocations; }
    public KernelHeapStatistics Statistics { get; }
    public UInt64 LeakCandidates { get; }
    public UInt64 GuardFailures { get; }
    public UInt64 DoubleFreeFailures { get; }
    public UInt64 GuardedAllocations { get; }
    public UInt64 TaggedAllocations { get; }
}

public static unsafe partial class KernelHeap
{
    private const Int32 DiagnosticAllocationCapacity = 512;
    private const Int32 ReleasedTokenCapacity = 256;
    private const UInt64 GuardBytes = 8UL;
    private const UInt64 LeadingCanary = 0x4E4F475541524431UL; // NOGUARD1
    private const UInt64 TrailingCanary = 0x4E4F475541524432UL; // NOGUARD2

    private unsafe struct ExtendedDiagnosticState
    {
        internal fixed UInt64 Tokens[DiagnosticAllocationCapacity];
        internal fixed UInt64 Addresses[DiagnosticAllocationCapacity];
        internal fixed UInt64 Lengths[DiagnosticAllocationCapacity];
        internal fixed UInt64 TagHashes[DiagnosticAllocationCapacity];
        internal fixed UInt64 Sequences[DiagnosticAllocationCapacity];
        internal fixed UInt64 LeadingGuardAddresses[DiagnosticAllocationCapacity];
        internal fixed UInt64 TrailingGuardAddresses[DiagnosticAllocationCapacity];
        internal fixed Byte Active[DiagnosticAllocationCapacity];
        internal fixed UInt64 ReleasedTokens[ReleasedTokenCapacity];
    }

#pragma warning disable CS0169
    private static ExtendedDiagnosticState _extendedDiagnostics;
#pragma warning restore CS0169
    private static UInt64 _allocationSequence;
    private static UInt64 _guardFailures;
    private static UInt64 _doubleFreeFailures;
    private static Int32 _releasedTokenCursor;

    /// <summary>Creates a leak checkpoint. Allocations created after this sequence and still live are leak candidates.</summary>
    public static Boolean TryCreateLeakCheckpoint(out UInt64 checkpoint)
    {
        checkpoint = _allocationSequence;
        return _initialized;
    }

    /// <summary>Gets the number of live allocations created after a leak checkpoint.</summary>
    public static UInt64 GetLeakCandidateCount(UInt64 checkpoint)
    {
        UInt64 count=0UL;
        fixed (Byte* active=_extendedDiagnostics.Active)
        fixed (UInt64* seq=_extendedDiagnostics.Sequences)
            for(Int32 i=0;i<DiagnosticAllocationCapacity;i++) if(active[i]!=0 && seq[i]>checkpoint) count++;
        return count;
    }

    /// <summary>Enumerates one live allocation created after a leak checkpoint.</summary>
    public static Boolean TryGetLeakCandidate(UInt64 checkpoint, UInt64 candidateIndex, out KernelHeapAllocationInfo info)
    {
        info=default; UInt64 seen=0UL;
        fixed (Byte* active=_extendedDiagnostics.Active)
        fixed (UInt64* seq=_extendedDiagnostics.Sequences)
        fixed (UInt64* tokens=_extendedDiagnostics.Tokens)
        fixed (UInt64* addresses=_extendedDiagnostics.Addresses)
        fixed (UInt64* lengths=_extendedDiagnostics.Lengths)
        fixed (UInt64* tags=_extendedDiagnostics.TagHashes)
        fixed (UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses)
        {
            for(Int32 i=0;i<DiagnosticAllocationCapacity;i++)
            {
                if(active[i]==0 || seq[i]<=checkpoint) continue;
                if(seen++!=candidateIndex) continue;
                info=new KernelHeapAllocationInfo(tokens[i],addresses[i],lengths[i],tags[i],seq[i],lead[i]!=0UL);
                return true;
            }
        }
        return false;
    }

    /// <summary>Assigns a stable FNV-1a allocation tag hash to a live heap allocation.</summary>
    public static Boolean TrySetAllocationTag(KernelHeapAllocation allocation, String tag)
    {
        if(tag==null) return false;
        Int32 slot=FindExtendedSlotByToken(allocation.Token);
        if(slot<0) return false;
        UInt64 hash=HashTag(tag);
        fixed(UInt64* tags=_extendedDiagnostics.TagHashes) tags[slot]=hash;
        return true;
    }

    /// <summary>Gets the tag hash assigned to a live allocation.</summary>
    public static Boolean TryGetAllocationTagHash(UInt64 token, out UInt64 tagHash)
    {
        tagHash=0UL; Int32 slot=FindExtendedSlotByToken(token); if(slot<0) return false;
        fixed(UInt64* tags=_extendedDiagnostics.TagHashes) tagHash=tags[slot];
        return true;
    }

    /// <summary>Allocates bytes with dedicated leading/trailing 64-bit canaries.</summary>
    public static Boolean TryAllocateGuarded(UInt64 byteCount, UInt64 alignment, Boolean zeroFill, String tag, out KernelGuardedHeapAllocation allocation)
    {
        allocation=default;
        if(byteCount==0UL || alignment==0UL || (alignment&(alignment-1UL))!=0UL || alignment>PageSize) { _status=KernelHeapStatus.InvalidParameter; return false; }
        UInt64 extra=GuardBytes*2UL + alignment-1UL;
        if(byteCount>UInt64.MaxValue-extra) { _status=KernelHeapStatus.InvalidParameter; return false; }
        if(!TryAllocate(byteCount+extra,8UL,false,out KernelHeapAllocation backing)) return false;
        UInt64 candidate=backing.Address+GuardBytes;
        UInt64 user=(candidate+(alignment-1UL))&~(alignment-1UL);
        UInt64 leading=user-GuardBytes, trailing=user+byteCount;
        *(UInt64*)(nuint)leading=LeadingCanary ^ backing.Token;
        *(UInt64*)(nuint)trailing=TrailingCanary ^ backing.Token;
        if(zeroFill){ Byte* p=(Byte*)(nuint)user; for(UInt64 i=0;i<byteCount;i++)p[i]=0; }
        Int32 slot=FindExtendedSlotByToken(backing.Token);
        if(slot>=0)
        {
            fixed(UInt64* addresses=_extendedDiagnostics.Addresses) addresses[slot]=user;
            fixed(UInt64* lengths=_extendedDiagnostics.Lengths) lengths[slot]=byteCount;
            fixed(UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses) lead[slot]=leading;
            fixed(UInt64* trail=_extendedDiagnostics.TrailingGuardAddresses) trail[slot]=trailing;
            if(tag!=null){ fixed(UInt64* tags=_extendedDiagnostics.TagHashes) tags[slot]=HashTag(tag); }
        }
        allocation=new KernelGuardedHeapAllocation(backing,user,byteCount);
        return true;
    }

    /// <summary>Validates canaries and releases a guarded allocation exactly once.</summary>
    public static Boolean TryReleaseGuarded(KernelGuardedHeapAllocation allocation)
    {
        Int32 slot=FindExtendedSlotByToken(allocation.Token);
        if(slot<0)
        {
            if(WasReleasedToken(allocation.Token)){ _doubleFreeFailures++; _status=KernelHeapStatus.DoubleFreeDetected; }
            else _status=KernelHeapStatus.AllocationNotFound;
            return false;
        }
        if(!ValidateGuardSlot(slot)) { _status=KernelHeapStatus.GuardCorruptionDetected; return false; }
        return TryRelease(allocation.Backing);
    }

    /// <summary>Validates every currently active guarded allocation.</summary>
    public static Boolean TryValidateGuards(out UInt64 failures)
    {
        failures=0UL;
        fixed(Byte* active=_extendedDiagnostics.Active)
        fixed(UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses)
        {
            for(Int32 i=0;i<DiagnosticAllocationCapacity;i++)
            {
                if(active[i]==0 || lead[i]==0UL) continue;
                if(!ValidateGuardSlot(i)) failures++;
            }
        }
        return failures==0UL;
    }

    /// <summary>Gets extended heap diagnostic counters and current leak-candidate count.</summary>
    public static KernelHeapDiagnosticSnapshot GetDiagnosticSnapshot(UInt64 leakCheckpoint)
    {
        UInt64 guarded=0UL, tagged=0UL;
        fixed(Byte* active=_extendedDiagnostics.Active)
        fixed(UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses)
        fixed(UInt64* tags=_extendedDiagnostics.TagHashes)
            for(Int32 i=0;i<DiagnosticAllocationCapacity;i++) if(active[i]!=0){ if(lead[i]!=0UL)guarded++; if(tags[i]!=0UL)tagged++; }
        return new KernelHeapDiagnosticSnapshot(GetStatistics(),GetLeakCandidateCount(leakCheckpoint),_guardFailures,_doubleFreeFailures,guarded,tagged);
    }

    internal static void OnAllocationCreated(UInt64 token, UInt64 address, UInt64 length)
    {
        Int32 slot=FindFreeExtendedSlot(); if(slot<0)return;
        _allocationSequence++;
        fixed(UInt64* tokens=_extendedDiagnostics.Tokens)tokens[slot]=token;
        fixed(UInt64* addresses=_extendedDiagnostics.Addresses)addresses[slot]=address;
        fixed(UInt64* lengths=_extendedDiagnostics.Lengths)lengths[slot]=length;
        fixed(UInt64* tags=_extendedDiagnostics.TagHashes)tags[slot]=0UL;
        fixed(UInt64* seq=_extendedDiagnostics.Sequences)seq[slot]=_allocationSequence;
        fixed(UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses)lead[slot]=0UL;
        fixed(UInt64* trail=_extendedDiagnostics.TrailingGuardAddresses)trail[slot]=0UL;
        fixed(Byte* active=_extendedDiagnostics.Active)active[slot]=1;
    }

    internal static void OnAllocationReleased(UInt64 token)
    {
        Int32 slot=FindExtendedSlotByToken(token);
        if(slot>=0)
        {
            fixed(Byte* active=_extendedDiagnostics.Active)active[slot]=0;
            fixed(UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses)lead[slot]=0UL;
            fixed(UInt64* trail=_extendedDiagnostics.TrailingGuardAddresses)trail[slot]=0UL;
        }
        fixed(UInt64* released=_extendedDiagnostics.ReleasedTokens)
        {
            released[_releasedTokenCursor]=token;
            _releasedTokenCursor++; if(_releasedTokenCursor>=ReleasedTokenCapacity)_releasedTokenCursor=0;
        }
    }

    internal static Boolean WasReleasedToken(UInt64 token)
    {
        if(token==0UL)return false;
        fixed(UInt64* released=_extendedDiagnostics.ReleasedTokens)
            for(Int32 i=0;i<ReleasedTokenCapacity;i++)if(released[i]==token)return true;
        return false;
    }

    internal static void ResetExtendedDiagnostics()
    {
        _allocationSequence=0UL; _guardFailures=0UL; _doubleFreeFailures=0UL; _releasedTokenCursor=0;
        fixed(Byte* active=_extendedDiagnostics.Active)for(Int32 i=0;i<DiagnosticAllocationCapacity;i++)active[i]=0;
        fixed(UInt64* released=_extendedDiagnostics.ReleasedTokens)for(Int32 i=0;i<ReleasedTokenCapacity;i++)released[i]=0UL;
    }

    private static Int32 FindFreeExtendedSlot(){ fixed(Byte* active=_extendedDiagnostics.Active)for(Int32 i=0;i<DiagnosticAllocationCapacity;i++)if(active[i]==0)return i; return -1; }
    private static Int32 FindExtendedSlotByToken(UInt64 token){ fixed(Byte* active=_extendedDiagnostics.Active)fixed(UInt64* tokens=_extendedDiagnostics.Tokens)for(Int32 i=0;i<DiagnosticAllocationCapacity;i++)if(active[i]!=0&&tokens[i]==token)return i; return -1; }
    private static Boolean ValidateGuardSlot(Int32 slot)
    {
        UInt64 token,leading,trailing;
        fixed(UInt64* tokens=_extendedDiagnostics.Tokens)token=tokens[slot];
        fixed(UInt64* lead=_extendedDiagnostics.LeadingGuardAddresses)leading=lead[slot];
        fixed(UInt64* trail=_extendedDiagnostics.TrailingGuardAddresses)trailing=trail[slot];
        if(leading==0UL||trailing==0UL)return true;
        Boolean ok=*(UInt64*)(nuint)leading==(LeadingCanary^token) && *(UInt64*)(nuint)trailing==(TrailingCanary^token);
        if(!ok)_guardFailures++;
        return ok;
    }
    private static UInt64 HashTag(String tag){ UInt64 h=14695981039346656037UL; for(Int32 i=0;i<tag.Length;i++){ h^=(UInt64)tag[i]; h*=1099511628211UL; } return h==0UL?1UL:h; }
}
