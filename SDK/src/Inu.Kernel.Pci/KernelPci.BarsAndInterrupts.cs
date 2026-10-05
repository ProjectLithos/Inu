using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Pci;

public static unsafe partial class KernelPci
{

    /// <summary>Discovers one implemented BAR, including its standard sizing transaction.</summary>
    public static Boolean TryGetBar(PciLocation location,Byte barIndex,out PciBarInfo bar)
    {
        bar=default;if(barIndex>=6U||!TryRead8(location,0x0E,out Byte header))return false;Byte type=(Byte)(header&0x7F);Byte maximum=type==0? (Byte)6 : type==1 ? (Byte)2 : (Byte)0;if(barIndex>=maximum)return false;
        UInt16 command;if(!TryRead16(location,0x04,out command))return false;if(!TryWrite16(location,0x04,(UInt16)(command&~3U)))return false;
        UInt16 offset=(UInt16)(0x10U+(UInt16)barIndex*4U);Boolean result=false;
        if(TryRead32(location,offset,out UInt32 low)&&low!=0U)
        {
            if((low&1U)!=0U)
            {
                if(TryWrite32(location,offset,0xFFFFFFFFU)&&TryRead32(location,offset,out UInt32 mask)&&TryWrite32(location,offset,low))
                {UInt32 masked=mask&0xFFFFFFFCU;UInt64 length=masked==0U?0UL:(UInt64)(~masked+1U);if(length!=0UL){bar=new PciBarInfo(barIndex,PciBarType.Io,low&0xFFFFFFFCU,length,false);result=true;}}
            }
            else
            {
                UInt32 memoryType=(low>>1)&3U;Boolean prefetch=(low&8U)!=0U;
                if(memoryType==2U&&barIndex+1U<maximum&&TryRead32(location,(UInt16)(offset+4U),out UInt32 high))
                {
                    if(TryWrite32(location,offset,0xFFFFFFFFU)&&TryWrite32(location,(UInt16)(offset+4U),0xFFFFFFFFU)&&TryRead32(location,offset,out UInt32 maskLow)&&TryRead32(location,(UInt16)(offset+4U),out UInt32 maskHigh)&&TryWrite32(location,offset,low)&&TryWrite32(location,(UInt16)(offset+4U),high))
                    {UInt64 address=((UInt64)high<<32)|(low&0xFFFFFFF0U);UInt64 mask=((UInt64)maskHigh<<32)|(maskLow&0xFFFFFFF0U);UInt64 length=mask==0UL?0UL:(~mask)+1UL;if(length!=0UL){bar=new PciBarInfo(barIndex,PciBarType.Memory64,address,length,prefetch);result=true;}}
                }
                else if(memoryType==0U)
                {
                    if(TryWrite32(location,offset,0xFFFFFFFFU)&&TryRead32(location,offset,out UInt32 mask)&&TryWrite32(location,offset,low))
                    {UInt32 masked=mask&0xFFFFFFF0U;UInt64 length=masked==0U?0UL:(UInt64)(~masked+1U);if(length!=0UL){bar=new PciBarInfo(barIndex,PciBarType.Memory32,low&0xFFFFFFF0U,length,prefetch);result=true;}}
                }
            }
        }
        TryWrite16(location,0x04,command);return result;
    }

    /// <summary>Gets a conventional PCI capability by zero-based list index.</summary>
    public static Boolean TryGetCapability(PciLocation location,UInt32 requestedIndex,out PciCapabilityInfo capability)
    {
        capability=default;if(!TryRead16(location,0x06,out UInt16 status)||(status&PciStatusCapabilities)==0||!TryRead8(location,0x34,out Byte pointer))return false;
        UInt64 visited=0UL;UInt32 index=0U;
        while(pointer>=0x40U&&pointer<=0xFCU&&(pointer&3U)==0U)
        {UInt32 slot=(UInt32)(pointer-0x40U)>>2;if(slot<64U){UInt64 bit=1UL<<(Int32)slot;if((visited&bit)!=0)return false;visited|=bit;}if(!TryRead8(location,pointer,out Byte id)||!TryRead8(location,(UInt16)(pointer+1U),out Byte next))return false;if(index++==requestedIndex){capability=new PciCapabilityInfo(id,pointer,next);return true;}pointer=(Byte)(next&0xFCU);}
        return false;
    }

    /// <summary>Finds the first conventional PCI capability with the requested identifier.</summary>
    public static Boolean TryFindCapability(PciLocation location,Byte capabilityId,out PciCapabilityInfo capability)
    {capability=default;for(UInt32 i=0;i<48U;i++){if(!TryGetCapability(location,i,out PciCapabilityInfo current))return false;if(current.Id==capabilityId){capability=current;return true;}}return false;}

    /// <summary>Gets one PCIe extended capability by zero-based list index.</summary>
    public static Boolean TryGetExtendedCapability(PciLocation location,UInt32 requestedIndex,out PciExtendedCapabilityInfo capability)
    {
        capability=default;UInt16 offset=0x100;UInt32 index=0;
        for(UInt32 guard=0;guard<1024U&&offset>=0x100U&&offset<=0xFFCU;guard++)
        {if(!TryRead32(location,offset,out UInt32 header)||header==0U||header==0xFFFFFFFFU)return false;UInt16 id=(UInt16)(header&0xFFFFU);Byte version=(Byte)((header>>16)&0xFU);UInt16 next=(UInt16)((header>>20)&0xFFFU);if(index++==requestedIndex){capability=new PciExtendedCapabilityInfo(id,version,offset,next);return true;}if(next==0U||next==offset)return false;offset=next;}
        return false;
    }

    /// <summary>Discovers the standard MSI capability when present.</summary>
    public static Boolean TryGetMsiCapability(PciLocation location,out PciMsiCapability msi)
    {msi=default;if(!TryFindCapability(location,0x05,out PciCapabilityInfo cap)||!TryRead16(location,(UInt16)(cap.Offset+2U),out UInt16 control))return false;msi=new PciMsiCapability(cap.Offset,(control&(1U<<7))!=0,(control&(1U<<8))!=0,(Byte)((control>>1)&7U));return true;}

    /// <summary>Discovers the standard MSI-X capability when present.</summary>
    public static Boolean TryGetMsixCapability(PciLocation location,out PciMsixCapability msix)
    {msix=default;if(!TryFindCapability(location,0x11,out PciCapabilityInfo cap)||!TryRead16(location,(UInt16)(cap.Offset+2U),out UInt16 control)||!TryRead32(location,(UInt16)(cap.Offset+4U),out UInt32 table)||!TryRead32(location,(UInt16)(cap.Offset+8U),out UInt32 pending))return false;msix=new PciMsixCapability(cap.Offset,(UInt16)((control&0x7FFU)+1U),(Byte)(table&7U),table&0xFFFFFFF8U,(Byte)(pending&7U),pending&0xFFFFFFF8U);return true;}

    /// <summary>Programs one standard PCI MSI message. Interrupt policy remains owned by the kernel interrupt broker.</summary>
    public static Boolean TryProgramMsi(PciLocation location,UInt64 messageAddress,UInt16 messageData)
    {if(!TryGetMsiCapability(location,out PciMsiCapability msi)||!TryRead16(location,(UInt16)(msi.Offset+2U),out UInt16 control))return false;if(!TryWrite32(location,(UInt16)(msi.Offset+4U),(UInt32)messageAddress))return false;UInt16 dataOffset;if(msi.Address64){if(!TryWrite32(location,(UInt16)(msi.Offset+8U),(UInt32)(messageAddress>>32)))return false;dataOffset=(UInt16)(msi.Offset+12U);}else dataOffset=(UInt16)(msi.Offset+8U);if(!TryWrite16(location,dataOffset,messageData))return false;control=(UInt16)((control&~0x0070U)|1U);if(!TryWrite16(location,(UInt16)(msi.Offset+2U),control))return false;return SetIntxDisabled(location,true);}

    /// <summary>Disables standard PCI MSI delivery for one function.</summary>
    public static Boolean TryDisableMsi(PciLocation location)
    {if(!TryGetMsiCapability(location,out PciMsiCapability msi)||!TryRead16(location,(UInt16)(msi.Offset+2U),out UInt16 control))return false;return TryWrite16(location,(UInt16)(msi.Offset+2U),(UInt16)(control&~1U));}

    /// <summary>Programs and enables one PCI MSI-X table entry. Interrupt policy remains owned by the kernel interrupt broker.</summary>
    public static Boolean TryProgramMsix(PciLocation location,UInt16 tableEntry,UInt64 messageAddress,UInt32 messageData)
    {if(!TryGetMsixCapability(location,out PciMsixCapability msix)||tableEntry>=msix.TableSize||!TryMapBar(location,msix.TableBar,out PciBarInfo bar,out UInt64 tableBase))return false;UInt64 offset=(UInt64)msix.TableOffset+(UInt64)tableEntry*16UL;if(bar.Length<16UL||offset>bar.Length-16UL)return false;UInt32* entry=(UInt32*)(nuint)(tableBase+offset);entry[3]=1U;entry[0]=(UInt32)messageAddress;entry[1]=(UInt32)(messageAddress>>32);entry[2]=messageData;entry[3]=0U;if(!TryRead16(location,(UInt16)(msix.Offset+2U),out UInt16 control))return false;control=(UInt16)((control|0x8000U)&~0x4000U);if(!TryWrite16(location,(UInt16)(msix.Offset+2U),control))return false;return SetIntxDisabled(location,true);}

    /// <summary>Disables PCI MSI-X delivery for one function.</summary>
    public static Boolean TryDisableMsix(PciLocation location)
    {if(!TryGetMsixCapability(location,out PciMsixCapability msix)||!TryRead16(location,(UInt16)(msix.Offset+2U),out UInt16 control))return false;return TryWrite16(location,(UInt16)(msix.Offset+2U),(UInt16)(control&~0x8000U));}

    /// <summary>Enables or disables legacy PCI INTx assertion through the PCI command register.</summary>
    public static Boolean SetIntxDisabled(PciLocation location,Boolean disabled)
    {if(!TryRead16(location,4,out UInt16 command))return false;UInt16 next=disabled?(UInt16)(command|0x0400U):(UInt16)(command&~0x0400U);return TryWrite16(location,4,next);}
}
