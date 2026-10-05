using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Graphics;

/// <summary>Owns generic framebuffer targets independently of the firmware or graphics driver that created them.</summary>
public static unsafe partial class KernelGraphics
{
    private static KernelGraphicsDisplayInfo Info(UInt32 index,DisplayRecord* r){KernelGraphicsMode mode=new(r->Width,r->Height,r->Pitch,(KernelGraphicsPixelFormat)r->PixelFormat);KernelGraphicsFramebuffer fb=new(r->Physical,r->Virtual,r->Bytes,mode);return new(new KernelGraphicsDisplayHandle(index+1U),new KernelDeviceHandle(r->Device),(KernelGraphicsTargetKind)r->Kind,fb,r->CanSetMode!=0,r->Primary!=0);}
    private static Boolean TryRecord(KernelGraphicsDisplayHandle h,out DisplayRecord* r){r=null;if(!_initialized||h.Value==0U||h.Value>_capacity)return false;DisplayRecord* p=_records+(h.Value-1U);if(p->Used==0)return false;r=p;return true;}
    private static Int32 Free(){for(Int32 i=0;i<(Int32)_capacity;i++)if((_records+i)->Used==0)return i;return -1;}
    private static Boolean Allocate(UInt32 capacity,out KernelHeapAllocation allocation,out DisplayRecord* pointer){allocation=default;pointer=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(DisplayRecord),64U,true,out allocation))return false;pointer=(DisplayRecord*)(nuint)allocation.Address;return true;}
    private static Boolean Grow(){UInt32 next=_capacity>=0x40000000U?UInt32.MaxValue:_capacity*2U;if(next<=_capacity||next>Int32.MaxValue||!Allocate(next,out KernelHeapAllocation fresh,out DisplayRecord* p))return false;Copy((Byte*)_records,(Byte*)p,(UInt64)_capacity*(UInt64)sizeof(DisplayRecord));KernelHeapAllocation old=_allocation;_allocation=fresh;_records=p;_capacity=next;return KernelHeap.TryRelease(old);}
    private static void Clear(Byte* target,Int32 bytes){for(Int32 i=0;i<bytes;i++)target[i]=0;}
    private static Boolean Copy(Byte* source,Byte* destination,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)destination[i]=source[i];return true;}
}
