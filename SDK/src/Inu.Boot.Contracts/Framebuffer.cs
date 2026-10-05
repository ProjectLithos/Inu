using Inu.Primitives;

namespace Inu.Boot.Contracts;

public enum FramebufferPixelFormat
{
    RedGreenBlueReserved8BitPerColor = 0,
    BlueGreenRedReserved8BitPerColor = 1,
    BitMask = 2,
    BltOnly = 3
}

public readonly struct PixelBitMask
{
    public PixelBitMask(uint red, uint green, uint blue, uint reserved)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Reserved = reserved;
    }

    public uint Red { get; }
    public uint Green { get; }
    public uint Blue { get; }
    public uint Reserved { get; }

    public bool IsDirectColor()
    {
        if (Red == 0 || Green == 0 || Blue == 0) return false;
        if ((Red & Green) != 0 || (Red & Blue) != 0 || (Green & Blue) != 0) return false;
        if (Reserved != 0 && ((Reserved & Red) != 0 || (Reserved & Green) != 0 || (Reserved & Blue) != 0)) return false;
        return IsContiguous(Red) && IsContiguous(Green) && IsContiguous(Blue);
    }

    private static bool IsContiguous(uint mask)
    {
        while ((mask & 1U) == 0U) mask >>= 1;
        while ((mask & 1U) != 0U) mask >>= 1;
        return mask == 0U;
    }
}

public readonly struct Framebuffer
{
    public Framebuffer(
        PhysicalAddress address,
        ulong sizeInBytes,
        uint width,
        uint height,
        uint pixelsPerScanLine,
        FramebufferPixelFormat pixelFormat,
        PixelBitMask pixelMask)
    {
        Address = address;
        SizeInBytes = sizeInBytes;
        Width = width;
        Height = height;
        PixelsPerScanLine = pixelsPerScanLine;
        PixelFormat = pixelFormat;
        PixelMask = pixelMask;
    }

    public PhysicalAddress Address { get; }
    public ulong SizeInBytes { get; }
    public uint Width { get; }
    public uint Height { get; }
    public uint PixelsPerScanLine { get; }
    public FramebufferPixelFormat PixelFormat { get; }
    public PixelBitMask PixelMask { get; }

    public bool IsAvailable()
    {
        if (Address.Value == 0 || SizeInBytes == 0 || Width == 0 || Height == 0) return false;
        if (PixelsPerScanLine < Width || PixelFormat > FramebufferPixelFormat.BitMask) return false;
        ulong bytesPerScanLine = checked((ulong)PixelsPerScanLine * 4UL);
        if ((ulong)Height > SizeInBytes / bytesPerScanLine) return false;
        return PixelFormat != FramebufferPixelFormat.BitMask || PixelMask.IsDirectColor();
    }
}
