using System;

namespace Inu.Kernel.E1000;

/// <summary>Provides allocation-free Intel E1000/E1000e identification and ring helpers.</summary>
public static class E1000Math
{
    /// <summary>Identifies supported Intel gigabit PCI device identifiers, excluding I219/I225 which use a later driver family.</summary>
    public static E1000ControllerFamily Identify(UInt16 vendorId,UInt16 deviceId)
    {
        if(vendorId!=0x8086U)return E1000ControllerFamily.Unknown;
        switch(deviceId)
        {
            case 0x1000: case 0x1001: case 0x1004: case 0x1008: case 0x1009: case 0x100C: case 0x100D: case 0x100E: case 0x100F: case 0x1010: case 0x1011: case 0x1012: case 0x1013: case 0x1015: case 0x1016: case 0x1017: case 0x1018: case 0x1019: case 0x101A: case 0x101D: case 0x1026: case 0x1027: case 0x1028: case 0x1075: case 0x1076: case 0x1077: case 0x1078: case 0x1079: case 0x107A: case 0x107B: case 0x107C: case 0x107D: case 0x107E: case 0x107F: case 0x108A: case 0x1099: case 0x10B5:
                return E1000ControllerFamily.E1000;
            case 0x1049: case 0x104A: case 0x104B: case 0x104C: case 0x104D: case 0x105E: case 0x105F: case 0x1060: case 0x10A4: case 0x10A5: case 0x10BC: case 0x10BD: case 0x10C4: case 0x10C5: case 0x10D3: case 0x10EA: case 0x10EB: case 0x10EF: case 0x10F0: case 0x10F5: case 0x10F6: case 0x1501: case 0x150C: case 0x150D: case 0x150E: case 0x150F: case 0x1510: case 0x1511: case 0x1516: case 0x1521: case 0x1522: case 0x1523: case 0x1524: case 0x1525: case 0x1526: case 0x1527: case 0x1528: case 0x1533: case 0x1539:
                return E1000ControllerFamily.E1000e;
            default:return E1000ControllerFamily.Unknown;
        }
    }

    public static Boolean IsSupported(UInt16 vendorId,UInt16 deviceId)=>Identify(vendorId,deviceId)!=E1000ControllerFamily.Unknown;
    public static Boolean IsValidDescriptorCount(UInt32 count)=>count>=8U&&count<=4096U&&(count&(count-1U))==0U&&((count*16U)&127U)==0U;
    public static UInt32 DescriptorBytes(UInt32 count)=>IsValidDescriptorCount(count)?count*16U:0U;
}
