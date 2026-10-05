using System.Runtime.InteropServices;

namespace Inu.Architecture.X64.Interrupts;

internal static partial class NativeMethods
{
    [LibraryImport("__Internal", EntryPoint = "InuX64LoadInterruptDescriptorTable")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool LoadInterruptDescriptorTable(ulong address, ushort limit);

    [LibraryImport("__Internal", EntryPoint = "InuX64SetInterruptDispatcher")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool SetInterruptDispatcher(ulong dispatcher);

    [LibraryImport("__Internal", EntryPoint = "InuX64SetInterruptStackSwitch")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool SetInterruptStackSwitch(byte vector, [MarshalAs(UnmanagedType.I1)] bool enabled);

    [LibraryImport("__Internal", EntryPoint = "InuX64GetInterruptStub")]
    internal static partial ulong GetInterruptStub(byte vector);

    [LibraryImport("__Internal", EntryPoint = "InuX64StopProcessor")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool StopProcessor();
}
