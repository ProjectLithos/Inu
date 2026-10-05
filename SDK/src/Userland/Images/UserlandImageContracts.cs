using System;
namespace Inu.Userland.Images;
/// <summary>Describes an image resource exposed to Inu userland.</summary>
public readonly struct UserlandImageInfo
{
    public UserlandImageInfo(UInt32 width,UInt32 height,UInt32 bitsPerPixel){Width=width;Height=height;BitsPerPixel=bitsPerPixel;}
    public UInt32 Width { get; } public UInt32 Height { get; } public UInt32 BitsPerPixel { get; }
}
