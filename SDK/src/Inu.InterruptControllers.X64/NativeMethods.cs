using System.Runtime.InteropServices;
namespace Inu.InterruptControllers.X64;
internal static partial class NativeMethods
{
    [LibraryImport("__Internal", EntryPoint="InuX64ControllerReadPort8")] internal static partial byte ReadPort8(ushort port);
    [LibraryImport("__Internal", EntryPoint="InuX64ControllerWritePort8")] [return: MarshalAs(UnmanagedType.I1)] internal static partial bool WritePort8(ushort port, byte value);
    [LibraryImport("__Internal", EntryPoint="InuX64ReadMsr")] internal static partial ulong ReadMsr(uint index);
    [LibraryImport("__Internal", EntryPoint="InuX64WriteMsr")] [return: MarshalAs(UnmanagedType.I1)] internal static partial bool WriteMsr(uint index, ulong value);
    [LibraryImport("__Internal", EntryPoint="InuX64ReadMmio32")] internal static partial uint ReadMmio32(ulong address);
    [LibraryImport("__Internal", EntryPoint="InuX64WriteMmio32")] [return: MarshalAs(UnmanagedType.I1)] internal static partial bool WriteMmio32(ulong address, uint value);
}
