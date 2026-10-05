using System.Runtime.InteropServices;

namespace Inu.Architecture.X64;

internal static partial class NativeMethods
{
    [LibraryImport("__Internal", EntryPoint = "InuX64DisableInterrupts")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool DisableInterrupts();

    [LibraryImport("__Internal", EntryPoint = "InuX64EnableInterrupts")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool EnableInterrupts();

    [LibraryImport("__Internal", EntryPoint = "InuX64AreInterruptsEnabled")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool AreInterruptsEnabled();

    [LibraryImport("__Internal", EntryPoint = "InuX64Halt")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool Halt();

    [LibraryImport("__Internal", EntryPoint = "InuX64WritePort8")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool WritePort8(ushort port, byte value);

    [LibraryImport("__Internal", EntryPoint = "InuX64ReadPort8")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool ReadPort8(ushort port, out byte value);
}
