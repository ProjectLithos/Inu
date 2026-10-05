using System;

namespace Inu.ApplicationFormat;

/// <summary>Canonical Inu application/package architecture identifiers.</summary>
public enum InuApplicationArchitecture : UInt16 { X64=1, Arm64=2, RiscV64=3 }
/// <summary>Syscall personality requested by an application package.</summary>
public enum InuApplicationAbi : UInt16 { Inu=1, Linux=2, WindowsNt=3 }
[Flags] public enum InuApplicationFlags : UInt32 { None=0, Signed=1, HasResources=2, PositionIndependent=4 }
[Flags] public enum InuApplicationResourceFlags : UInt32 { None=0, ReadOnly=1, Localized=2, Executable=4 }

/// <summary>Stable visible filename conventions. OS policy may override associations without changing the package ABI.</summary>
public static class InuApplicationExtensions
{
    public const String Application=".exe";
    public const String NativeExecutable=".nexe";
    public const String DynamicLibrary=".dll";
    public const String StaticOrImportLibrary=".lib";
}

/// <summary>On-disk Inu packaged-application format constants.</summary>
public static class InuApplicationFormat
{
    // ASCII NOAP in little-endian memory.
    public const UInt32 Magic=0x50414F4EU;
    public const UInt16 Major=1;
    public const UInt16 Minor=0;
    public const UInt32 HeaderBytes=192U;
    public const UInt32 DependencyRecordBytes=24U;
    public const UInt32 CapabilityRecordBytes=16U;
    public const UInt32 ResourceRecordBytes=32U;
    public const UInt32 MaximumDependencies=256U;
    public const UInt32 MaximumCapabilities=256U;
    public const UInt32 MaximumResources=4096U;
    public const UInt64 MaximumPackageBytes=67108864UL;
}

/// <summary>A slice into the immutable UTF-8 package string table.</summary>
public readonly struct InuPackageString
{
    public InuPackageString(UInt32 offset,UInt32 length,UInt64 hash){Offset=offset;Length=length;Hash=hash;}
    public UInt32 Offset{get;} public UInt32 Length{get;} public UInt64 Hash{get;}
}

/// <summary>Validated top-level metadata for a Inu .exe application package.</summary>
public readonly struct InuApplicationInfo
{
    public InuApplicationInfo(InuApplicationArchitecture architecture,InuApplicationAbi syscallAbi,UInt16 abiMajor,UInt16 abiMinor,InuApplicationFlags flags,UInt64 packageBytes,UInt64 nativeOffset,UInt64 nativeLength,UInt64 entryPointRva,UInt64 dependencyTableOffset,UInt32 dependencyCount,UInt64 capabilityTableOffset,UInt32 capabilityCount,UInt64 resourceTableOffset,UInt32 resourceCount,UInt64 stringTableOffset,UInt64 stringTableLength,UInt64 resourceDataOffset,UInt64 resourceDataLength,InuPackageString id,InuPackageString name,InuPackageString version,InuPackageString publisher,InuPackageString minimumSdkVersion)
    {Architecture=architecture;SyscallAbi=syscallAbi;AbiMajor=abiMajor;AbiMinor=abiMinor;Flags=flags;PackageBytes=packageBytes;NativeImageOffset=nativeOffset;NativeImageLength=nativeLength;EntryPointRva=entryPointRva;DependencyTableOffset=dependencyTableOffset;DependencyCount=dependencyCount;CapabilityTableOffset=capabilityTableOffset;CapabilityCount=capabilityCount;ResourceTableOffset=resourceTableOffset;ResourceCount=resourceCount;StringTableOffset=stringTableOffset;StringTableLength=stringTableLength;ResourceDataOffset=resourceDataOffset;ResourceDataLength=resourceDataLength;Id=id;Name=name;Version=version;Publisher=publisher;MinimumSdkVersion=minimumSdkVersion;}
    public InuApplicationArchitecture Architecture{get;} public InuApplicationAbi SyscallAbi{get;} public UInt16 AbiMajor{get;} public UInt16 AbiMinor{get;} public InuApplicationFlags Flags{get;}
    public UInt64 PackageBytes{get;} public UInt64 NativeImageOffset{get;} public UInt64 NativeImageLength{get;} public UInt64 EntryPointRva{get;}
    public UInt64 DependencyTableOffset{get;} public UInt32 DependencyCount{get;} public UInt64 CapabilityTableOffset{get;} public UInt32 CapabilityCount{get;} public UInt64 ResourceTableOffset{get;} public UInt32 ResourceCount{get;}
    public UInt64 StringTableOffset{get;} public UInt64 StringTableLength{get;} public UInt64 ResourceDataOffset{get;} public UInt64 ResourceDataLength{get;}
    public InuPackageString Id{get;} public InuPackageString Name{get;} public InuPackageString Version{get;} public InuPackageString Publisher{get;} public InuPackageString MinimumSdkVersion{get;}
}

public readonly struct InuApplicationDependency
{
    public InuApplicationDependency(InuPackageString id,InuPackageString versionConstraint,UInt64 flags){Id=id;VersionConstraint=versionConstraint;Flags=flags;}
    public InuPackageString Id{get;} public InuPackageString VersionConstraint{get;} public UInt64 Flags{get;}
}
public readonly struct InuApplicationCapability
{
    public InuApplicationCapability(InuPackageString name,UInt64 requestedRights){Name=name;RequestedRights=requestedRights;}
    public InuPackageString Name{get;} public UInt64 RequestedRights{get;}
}
public readonly struct InuApplicationResource
{
    public InuApplicationResource(InuPackageString name,UInt64 dataOffset,UInt64 dataLength,InuApplicationResourceFlags flags){Name=name;DataOffset=dataOffset;DataLength=dataLength;Flags=flags;}
    public InuPackageString Name{get;} public UInt64 DataOffset{get;} public UInt64 DataLength{get;} public InuApplicationResourceFlags Flags{get;}
}
