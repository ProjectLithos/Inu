namespace Inu.Primitives;

public readonly record struct Address(ulong Value)
{
    public bool IsZero() => Value == 0;

    public bool IsAligned(Alignment alignment)
    {
        if (alignment.Value == 0 || (alignment.Value & (alignment.Value - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(alignment));

        return (Value & (alignment.Value - 1)) == 0;
    }

    public Address Add(Bytes bytes) => new(checked(Value + bytes.Value));
}

public readonly record struct PhysicalAddress(ulong Value);
public readonly record struct VirtualAddress(ulong Value);
public readonly record struct Bytes(ulong Value);
public readonly record struct Pages(ulong Value)
{
    public Bytes ToBytes(ulong pageSize) => new(checked(Value * pageSize));
}
public readonly record struct Alignment(ulong Value);
public readonly record struct PortNumber(ushort Value);
public readonly record struct ProcessorId(uint Value);
