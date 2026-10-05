using System;

namespace Inu.Kernel.Serial;

/// <summary>Pure serial/UART identification and baud-rate helpers shared by runtime code and tests.</summary>
public static class SerialMath
{
    /// <summary>Gets whether a PCI class code identifies a communications/serial controller.</summary>
    public static Boolean IsPciSerialController(UInt32 classCode) => ((classCode >> 16) & 0xFFU) == 0x07U && ((classCode >> 8) & 0xFFU) == 0x00U;

    /// <summary>Gets whether the PCI serial programming interface is compatible with the 8250-to-16950 register model.</summary>
    public static Boolean Is16550CompatibleProgrammingInterface(UInt32 classCode)
    {
        if (!IsPciSerialController(classCode)) return false;
        UInt32 programmingInterface = classCode & 0xFFU;
        return programmingInterface <= 0x06U;
    }

    /// <summary>Calculates the integer UART divisor for the standard 1.8432 MHz input clock.</summary>
    public static Boolean TryCalculateDivisor(UInt32 baudRate, out UInt16 divisor)
    {
        divisor = 0;
        if (baudRate == 0U || baudRate > 115200U || (115200U % baudRate) != 0U) return false;
        UInt32 value = 115200U / baudRate;
        if (value == 0U || value > UInt16.MaxValue) return false;
        divisor = (UInt16)value;
        return true;
    }
}
