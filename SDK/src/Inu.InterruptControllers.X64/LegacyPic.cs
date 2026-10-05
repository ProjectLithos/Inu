namespace Inu.InterruptControllers.X64;
/// <summary>Controls the pair of legacy 8259 PICs during APIC takeover.</summary>
public sealed class LegacyPic : ILegacyPic
{
    private const ushort MasterCommand=0x20, MasterData=0x21, SlaveCommand=0xA0, SlaveData=0xA1;
    /// <inheritdoc />
    public bool MaskAll() => NativeMethods.WritePort8(MasterData,0xFF) && NativeMethods.WritePort8(SlaveData,0xFF);
    /// <inheritdoc />
    public bool Disable() => MaskAll();
    /// <inheritdoc />
    public bool EndOfInterrupt(byte irq)
    {
        if (irq >= 16) return false;
        bool slave = irq >= 8 ? NativeMethods.WritePort8(SlaveCommand,0x20) : true;
        return slave && NativeMethods.WritePort8(MasterCommand,0x20);
    }
}
