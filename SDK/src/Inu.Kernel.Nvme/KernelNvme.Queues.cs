using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Memory;
using Inu.Kernel.Heap;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Nvme;
/// <summary>PCI NVMe 1.x controller driver with admin/I/O queues, namespace discovery and synchronous block I/O.</summary>
public static unsafe partial class KernelNvme
{
 static Boolean AdminIdentify(Controller* c,UInt32 nsid,Byte cns,UInt64 prp)=>SubmitAdmin(c,6,nsid,prp,0,cns,0);
 static Boolean AdminCreateCq(Controller* c,UInt16 qid,UInt16 entries,UInt64 prp)=>SubmitAdmin(c,5,0,prp,0,((UInt32)(entries-1U)<<16)|qid,c->InterruptHandle!=0UL?3U:1U);
 static Boolean AdminCreateSq(Controller* c,UInt16 qid,UInt16 entries,UInt64 prp,UInt16 cqid)=>SubmitAdmin(c,1,0,prp,0,((UInt32)(entries-1U)<<16)|qid,((UInt32)cqid<<16)|1U);
 static Boolean SubmitAdmin(Controller* c,Byte opcode,UInt32 nsid,UInt64 prp1,UInt64 prp2,UInt32 cdw10,UInt32 cdw11){Byte* sq=Direct(c->Asq);Byte* e=sq+(UInt32)c->AdminTail*64U;for(UInt32 i=0;i<64;i++)e[i]=0;UInt16 cid=++c->AdminCid;e[0]=opcode;*(UInt16*)(e+2)=cid;*(UInt32*)(e+4)=nsid;*(UInt64*)(e+24)=prp1;*(UInt64*)(e+32)=prp2;*(UInt32*)(e+40)=cdw10;*(UInt32*)(e+44)=cdw11;c->AdminTail=(UInt16)((c->AdminTail+1U)%c->AdminEntries);W32(c->Mmio,NvmeMath.DoorbellOffset(0,false,c->DbStride),c->AdminTail);return WaitCompletion(c,0,cid,true);}
 static Boolean SubmitIo(Controller* c,UInt32 nsid,Byte opcode,UInt64 lba,UInt64 prp1,UInt16 blocks){Byte* sq=Direct(c->IoSq);Byte* e=sq+(UInt32)c->IoTail*64U;for(UInt32 i=0;i<64;i++)e[i]=0;UInt16 cid=++c->IoCid;e[0]=opcode;*(UInt16*)(e+2)=cid;*(UInt32*)(e+4)=nsid;*(UInt64*)(e+24)=prp1;if(opcode!=0){*(UInt64*)(e+40)=lba;*(UInt32*)(e+48)=(UInt32)(blocks-1U);}c->IoTail=(UInt16)((c->IoTail+1U)%c->IoEntries);W32(c->Mmio,NvmeMath.DoorbellOffset(1,false,c->DbStride),c->IoTail);return WaitCompletion(c,1,cid,false);}
 static Boolean WaitCompletion(Controller* c,UInt16 qid,UInt16 cid,Boolean admin){Byte* cq=Direct(admin?c->Acq:c->IoCq);UInt16 entries=admin?c->AdminEntries:c->IoEntries;UInt16 head=admin?c->AdminHead:c->IoHead;Byte phase=admin?c->AdminPhase:c->IoPhase;UInt32 spins=SpinLimit;while(spins-->0){Byte* e=cq+(UInt32)head*16U;UInt16 status=*(UInt16*)(e+14);if((status&1U)==phase){if(*(UInt16*)(e+12)!=cid)return false;Boolean ok=(status&0xFFFEU)==0;head++;if(head==entries){head=0;phase=(Byte)(phase==0?1:0);}if(admin){c->AdminHead=head;c->AdminPhase=phase;}else{c->IoHead=head;c->IoPhase=phase;}W32(c->Mmio,NvmeMath.DoorbellOffset(qid,true,c->DbStride),head);return ok;}}return false;}
 static Boolean WaitReady(Controller* c,Boolean ready){UInt32 spins=SpinLimit;while(spins-->0){Boolean r=(R32(c->Mmio,0x1C)&1U)!=0;if(r==ready)return true;}return false;}
}
