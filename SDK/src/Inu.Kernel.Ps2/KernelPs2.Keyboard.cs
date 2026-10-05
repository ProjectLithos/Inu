using System;

namespace Inu.Kernel.Ps2;

/// <summary>PS/2 keyboard transport integration. Decode state is owned by the optional KernelKeyboardDecoder component.</summary>
public static unsafe partial class KernelPs2
{
    private static Boolean DecodeKeyboard(Byte code)=>KernelKeyboardDecoderServices.Decode(code);
}
