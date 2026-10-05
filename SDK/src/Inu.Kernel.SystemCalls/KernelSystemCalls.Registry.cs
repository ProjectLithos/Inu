using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Protection;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Security;
using Inu.Kernel.Time;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.SystemCalls;

public static unsafe partial class KernelSystemCalls
{
    private static Boolean RegisterNative(KernelSystemCallOperation operation,String message,delegate*<KernelSystemCallFrame*, Int64> handler)
    {
        if(handler==null||message==null||message.Length==0||message.Length>MaximumNativeMessageBytes||_nativeRegistryCount>=(UInt32)NativeRegistrySlots)return false;
        for(Int32 i=0;i<message.Length;i++){Char c=message[i];if(c<32||c>126)return false;}
        fixed(UInt64* handlers=_registry.NativeHandlers)fixed(UInt32* lengths=_registry.NativeMessageLengths)fixed(Byte* operations=_registry.NativeOperations,messages=_registry.NativeMessages)
        {
            for(UInt32 slot=0U;slot<_nativeRegistryCount;slot++)
            {
                if(operations[slot]!=(Byte)operation||lengths[slot]!=(UInt32)message.Length)continue;Boolean same=true;UInt32 baseOffset=slot*(UInt32)MaximumNativeMessageBytes;for(Int32 i=0;i<message.Length;i++)if(messages[baseOffset+(UInt32)i]!=(Byte)message[i]){same=false;break;}if(same)return false;
            }
            UInt32 target=_nativeRegistryCount++;UInt32 offset=target*(UInt32)MaximumNativeMessageBytes;handlers[target]=(UInt64)(void*)handler;lengths[target]=(UInt32)message.Length;operations[target]=(Byte)operation;for(Int32 i=0;i<message.Length;i++)messages[offset+(UInt32)i]=(Byte)message[i];
        }
        return true;
    }

    private static Boolean RegisterNativePrefix(KernelSystemCallOperation operation,String prefix,delegate*<KernelSystemCallFrame*, Int64> handler)
    {
        if(handler==null||prefix==null||prefix.Length==0||prefix.Length>MaximumNativeMessageBytes||_nativePrefixRegistryCount>=(UInt32)NativePrefixRegistrySlots)return false;
        for(Int32 i=0;i<prefix.Length;i++){Char c=prefix[i];if(c<32||c>126)return false;}
        fixed(UInt64* handlers=_registry.NativePrefixHandlers)fixed(UInt32* lengths=_registry.NativePrefixLengths)fixed(Byte* operations=_registry.NativePrefixOperations,prefixes=_registry.NativePrefixes)
        {
            for(UInt32 slot=0U;slot<_nativePrefixRegistryCount;slot++)
            {
                if(operations[slot]!=(Byte)operation||lengths[slot]!=(UInt32)prefix.Length)continue;Boolean same=true;UInt32 baseOffset=slot*(UInt32)MaximumNativeMessageBytes;for(Int32 i=0;i<prefix.Length;i++)if(prefixes[baseOffset+(UInt32)i]!=(Byte)prefix[i]){same=false;break;}if(same)return false;
            }
            UInt32 target=_nativePrefixRegistryCount++;UInt32 offset=target*(UInt32)MaximumNativeMessageBytes;handlers[target]=(UInt64)(void*)handler;lengths[target]=(UInt32)prefix.Length;operations[target]=(Byte)operation;for(Int32 i=0;i<prefix.Length;i++)prefixes[offset+(UInt32)i]=(Byte)prefix[i];
        }
        return true;
    }

    private static Boolean RegisterAbi(KernelSystemCallAbi abi, UInt32 service, delegate*<KernelSystemCallFrame*, Int64> handler)
    {
        if (handler==null || !KernelSystemCallMath.IsRegistrableService(service)) return false;
        fixed (UInt64* linux=_registry.Linux, nt=_registry.Nt)
        {
            UInt64 value=(UInt64)(void*)handler;
            if (abi==KernelSystemCallAbi.Linux) linux[service]=value;
            else if (abi==KernelSystemCallAbi.Nt) nt[service]=value;
            else return false;
        }
        return true;
    }

    private static Int64 DispatchRegisteredNative(KernelSystemCallOperation operation,KernelSystemCallFrame* frame)
    {
        KernelSystemCallMessage message=frame->NativeMessage;if(message.MessageLength==0UL||message.MessageLength>(UInt64)MaximumNativeMessageBytes)return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* requested=stackalloc Byte[MaximumNativeMessageBytes];if(!TryCopyFromUser(message.MessageAddress,(UInt64)(nuint)requested,message.MessageLength))return (Int64)KernelSystemCallError.Fault;
        fixed(UInt64* handlers=_registry.NativeHandlers)fixed(UInt32* lengths=_registry.NativeMessageLengths)fixed(Byte* operations=_registry.NativeOperations,messages=_registry.NativeMessages)
        {
            for(UInt32 slot=0U;slot<_nativeRegistryCount;slot++)
            {
                if(operations[slot]!=(Byte)operation||lengths[slot]!=(UInt32)message.MessageLength)continue;UInt32 offset=slot*(UInt32)MaximumNativeMessageBytes;Boolean same=true;for(UInt32 i=0U;i<(UInt32)message.MessageLength;i++)if(messages[offset+i]!=requested[i]){same=false;break;}if(!same)continue;return Invoke(handlers[slot],frame,(Int64)KernelSystemCallError.NotImplemented);
            }
        }
        fixed(UInt64* handlers=_registry.NativePrefixHandlers)fixed(UInt32* lengths=_registry.NativePrefixLengths)fixed(Byte* operations=_registry.NativePrefixOperations,prefixes=_registry.NativePrefixes)
        {
            for(UInt32 slot=0U;slot<_nativePrefixRegistryCount;slot++)
            {
                UInt32 prefixLength=lengths[slot];if(operations[slot]!=(Byte)operation||prefixLength==0U||prefixLength>(UInt32)message.MessageLength)continue;UInt32 offset=slot*(UInt32)MaximumNativeMessageBytes;Boolean same=true;for(UInt32 i=0U;i<prefixLength;i++)if(prefixes[offset+i]!=requested[i]){same=false;break;}if(!same)continue;return Invoke(handlers[slot],frame,(Int64)KernelSystemCallError.NotImplemented);
            }
        }
        return (Int64)KernelSystemCallError.NotImplemented;
    }

    private static Int64 DispatchRegisteredAbi(KernelSystemCallAbi abi, KernelSystemCallFrame* frame)
    {
        if (!KernelSystemCallMath.IsRegistrableService(frame->ServiceNumber)) return (Int64)KernelSystemCallError.NotImplemented;
        UInt64 address=0UL;
        fixed (UInt64* linux=_registry.Linux, nt=_registry.Nt)
        { address=abi==KernelSystemCallAbi.Linux ? linux[frame->ServiceNumber] : nt[frame->ServiceNumber]; }
        return Invoke(address,frame,(Int64)KernelSystemCallError.NotImplemented);
    }

    private static Int64 Invoke(UInt64 address, KernelSystemCallFrame* frame, Int64 missing)
    {
        if (address==0UL) return missing;
        delegate*<KernelSystemCallFrame*, Int64> handler=(delegate*<KernelSystemCallFrame*, Int64>)(void*)address;
        return handler(frame);
    }

}
