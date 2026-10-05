using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Polling;

/// <summary>Provides an explicit opt-in polling methodology. Normal Inu kernels use timer and interrupt dispatch instead.</summary>
public static unsafe class KernelPolling
{
    private struct Entry { internal Byte Used; internal UInt64 Callback,Cookie; }
    private static Entry* _entries; private static KernelHeapAllocation _allocation; private static UInt32 _capacity,_count; private static Boolean _initialized;
    /// <summary>Initializes the optional polling registry from the kernel heap.</summary>
    public static Boolean Initialize(){if(_initialized)return true;if(!KernelHeap.IsInitialized()||!Allocate(16U,out _allocation,out _entries))return false;_capacity=16U;_initialized=true;return true;}
    /// <summary>Gets whether the optional polling methodology is initialized.</summary>
    public static Boolean IsInitialized()=>_initialized;
    /// <summary>Registers one explicit polling callback and returns its handle.</summary>
    public static Boolean Register(delegate*<UInt64,Boolean> callback,UInt64 cookie,out UInt32 handle){handle=0U;if(!_initialized||callback==null)return false;Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}Entry* e=_entries+slot;e->Used=1;e->Callback=(UInt64)(void*)callback;e->Cookie=cookie;_count++;handle=(UInt32)slot+1U;return true;}
    /// <summary>Removes one explicit polling callback.</summary>
    public static Boolean Unregister(UInt32 handle){Int32 i=(Int32)handle-1;if(!_initialized||i<0||(UInt32)i>=_capacity||(_entries+i)->Used==0)return false;Clear((Byte*)(_entries+i),(UInt64)sizeof(Entry));_count--;return true;}
    /// <summary>Runs one polling pass. This is never called by the generated default kernel.</summary>
    public static Boolean RunOnce(){if(!_initialized)return false;Boolean ok=true;for(UInt32 i=0;i<_capacity;i++){Entry* e=_entries+i;if(e->Used==0)continue;delegate*<UInt64,Boolean> cb=(delegate*<UInt64,Boolean>)(void*)e->Callback;ok=cb(e->Cookie)&ok;}return ok;}
    /// <summary>Gets the number of explicitly registered polling callbacks.</summary>
    public static UInt32 GetCallbackCount()=>_count;
    private static Int32 Free(){for(UInt32 i=0;i<_capacity;i++)if((_entries+i)->Used==0)return (Int32)i;return -1;}
    private static Boolean Grow(){UInt32 next=_capacity>0x7FFFFFFFU?UInt32.MaxValue:_capacity*2U;if(next<=_capacity)return false;if(!Allocate(next,out KernelHeapAllocation a,out Entry* n))return false;for(UInt32 i=0;i<_capacity;i++)n[i]=_entries[i];if(!KernelHeap.TryRelease(_allocation)){KernelHeap.TryRelease(a);return false;}_allocation=a;_entries=n;_capacity=next;return true;}
    private static Boolean Allocate(UInt32 capacity,out KernelHeapAllocation allocation,out Entry* entries){allocation=default;entries=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(Entry),64UL,true,out allocation))return false;entries=(Entry*)(nuint)allocation.Address;return true;}
    private static Boolean Clear(Byte* p,UInt64 n){for(UInt64 i=0;i<n;i++)p[i]=0;return true;}
}
