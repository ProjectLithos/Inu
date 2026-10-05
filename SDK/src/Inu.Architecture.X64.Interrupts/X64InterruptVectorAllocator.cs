using Inu.Interrupts;

namespace Inu.Architecture.X64.Interrupts;

/// <summary>Allocates x64 device vectors only from Inu's formal dynamic-device vector band.</summary>
public sealed class X64InterruptVectorAllocator : IInterruptVectorAllocator
{
    private readonly bool[] allocated = new bool[256];

    /// <inheritdoc />
    public byte Allocate()
    {
        for (int vector = InterruptVectorLayout.FirstDynamicDeviceVector; vector <= InterruptVectorLayout.LastDynamicDeviceVector; vector++)
        {
            if (allocated[vector]) continue;
            allocated[vector] = true;
            return (byte)vector;
        }
        return 0;
    }

    /// <inheritdoc />
    public bool Release(byte vector)
    {
        if (!InterruptVectorLayout.IsDynamicDeviceVector(vector) || !allocated[vector]) return false;
        allocated[vector] = false;
        return true;
    }

    /// <inheritdoc />
    public bool IsAllocated(byte vector) => allocated[vector];
}
