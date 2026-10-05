using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Acpi;

public static unsafe partial class KernelAcpi
{

    /// <summary>Validates the RSDP and root table supplied by firmware.</summary>
    /// <returns><see langword="true"/> when a checksummed RSDT or XSDT is ready for table discovery.</returns>
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : IAcpiRootPointerContext
    {
        if (_initialized) return true;
        _rsdpAddress = boot.GetAcpiRootPointerAddress();
        if (_rsdpAddress == 0UL) { _status = KernelAcpiStatus.RootPointerUnavailable; return false; }
        Byte* rsdp = (Byte*)_rsdpAddress;
        if (!HasRsdpSignature(rsdp) || !ChecksumIsZero(rsdp, 20U)) { _status = KernelAcpiStatus.InvalidRootPointer; return false; }
        _revision = rsdp[15];
        UInt32 rsdt = Read32(rsdp + 16);
        UInt64 xsdt = 0UL;
        if (_revision >= 2)
        {
            UInt32 length = Read32(rsdp + 20);
            if (length < 36U || length > 4096U || !ChecksumIsZero(rsdp, length)) { _status = KernelAcpiStatus.InvalidRootPointer; return false; }
            xsdt = Read64(rsdp + 24);
        }
        _usesXsdt = xsdt != 0UL;
        _rootAddress = _usesXsdt ? xsdt : rsdt;
        if (_rootAddress == 0UL) { _status = KernelAcpiStatus.RootTableUnavailable; return false; }
        Byte* root = (Byte*)_rootAddress;
        UInt32 expected = _usesXsdt ? XsdtSignature : RsdtSignature;
        if (Read32(root) != expected || !TryValidateTable(root, out UInt32 rootLength)) { _status = KernelAcpiStatus.InvalidRootTable; return false; }
        UInt32 entrySize = _usesXsdt ? 8U : 4U;
        UInt32 payload = rootLength - 36U;
        if (payload % entrySize != 0U) { _status = KernelAcpiStatus.InvalidRootTable; return false; }
        _rootEntryCount = payload / entrySize;
        if (_rootEntryCount > MaximumRootEntries) { _status = KernelAcpiStatus.RootTableTooLarge; return false; }
        _initialized = true;
        _status = KernelAcpiStatus.Success;
        return true;
    }


    /// <summary>Finds the first validated ACPI table with the requested four-byte signature.</summary>
    /// <returns><see langword="true"/> when a checksummed table was found.</returns>
    public static Boolean TryGetTable(UInt32 signature, out UInt64 address, out UInt32 length)
    {
        address = 0UL; length = 0U;
        if (!_initialized) return false;
        Byte* root = (Byte*)_rootAddress;
        UInt32 entrySize = _usesXsdt ? 8U : 4U;
        for (UInt32 index = 0U; index < _rootEntryCount; index++)
        {
            Byte* entry = root + 36U + index * entrySize;
            UInt64 candidateAddress = _usesXsdt ? Read64(entry) : Read32(entry);
            if (candidateAddress == 0UL) continue;
            Byte* candidate = (Byte*)candidateAddress;
            if (Read32(candidate) != signature) continue;
            if (!TryValidateTable(candidate, out UInt32 candidateLength)) continue;
            address = candidateAddress; length = candidateLength; return true;
        }
        return false;
    }

}
