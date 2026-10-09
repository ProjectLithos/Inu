using System;
using Inu.Kernel.Heap;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;

namespace Inu.Kernel.Storage;

/// <summary>
/// Maintains a best-effort in-kernel index of mounted filesystem paths. Mounts are scanned on the
/// storage worker after boot; successful VFS mutations update the index immediately. The VFS remains
/// authoritative and all callers must fall back to it while a directory has not yet been indexed.
/// </summary>
public static unsafe class KernelPathIndex
{
    private const UInt32 MaximumEntries=2048U;
    private const UInt32 MaximumPathBytes=1536U;
    private struct Entry
    {
        internal Byte Used,Type,Scanned,Queued;
        internal UInt32 Namespace,Length;
        internal UInt64 Hash;
        internal KernelHeapAllocation Path;
    }

    private static Entry[] _entries=new Entry[(Int32)MaximumEntries];
    private static UInt32[] _queue=new UInt32[(Int32)MaximumEntries];
    private static UInt32 _queueRead,_queueWrite,_queueCount,_count;
    private static Boolean _initialized;

    internal static Boolean Initialize(){_initialized=true;return true;}
    public static Boolean IsInitialized()=>_initialized;
    public static UInt32 Count=>_count;
    public static UInt32 PendingScans=>_queueCount;

    internal static void NotifyMount(KernelMountNamespaceHandle ns,String path)
    {
        if(!_initialized||ns.Value==0U||path==null||path.Length==0||path.Length>MaximumPathBytes)return;
        Byte* ascii=stackalloc Byte[path.Length];for(Int32 i=0;i<path.Length;i++){Char c=path[i];if(c>0x7F)return;ascii[i]=(Byte)c;}
        Int32 slot=AddOrUpdate(ns,ascii,(UInt32)path.Length,KernelFileType.Directory,false);if(slot>=0)Queue((UInt32)slot);
    }

    internal static void NotifyCreated(KernelMountNamespaceHandle ns,Byte* path,UInt32 length,KernelFileType type)
    {
        if(!_initialized||path==null||length==0U)return;AddOrUpdate(ns,path,length,type,type==KernelFileType.Directory);
    }

    internal static void NotifyDeleted(KernelMountNamespaceHandle ns,Byte* path,UInt32 length,Boolean subtree)
    {
        if(!_initialized||path==null||length==0U)return;
        for(UInt32 i=0U;i<MaximumEntries;i++)
        {
            Entry e=_entries[(Int32)i];if(e.Used==0U||e.Namespace!=ns.Value||e.Path.Address==0UL)continue;Byte* candidate=(Byte*)(nuint)e.Path.Address;
            if(!PathEqualsOrChild(candidate,e.Length,path,length,subtree))continue;KernelHeap.TryRelease(e.Path);_entries[(Int32)i]=default;if(_count!=0U)_count--;
        }
    }

    internal static void NotifyRenamed(KernelMountNamespaceHandle ns,Byte* source,UInt32 sourceLength,Byte* destination,UInt32 destinationLength)
    {
        if(!_initialized||source==null||destination==null||sourceLength==0U||destinationLength==0U)return;
        for(UInt32 i=0U;i<MaximumEntries;i++)
        {
            Entry e=_entries[(Int32)i];if(e.Used==0U||e.Namespace!=ns.Value||e.Path.Address==0UL)continue;Byte* oldPath=(Byte*)(nuint)e.Path.Address;
            if(!PathEqualsOrChild(oldPath,e.Length,source,sourceLength,true))continue;
            UInt32 suffix=e.Length-sourceLength,newLength=destinationLength+suffix;if(newLength>MaximumPathBytes)continue;
            if(!KernelHeap.TryAllocate(newLength,8UL,false,out KernelHeapAllocation replacement))continue;Byte* target=(Byte*)(nuint)replacement.Address;
            for(UInt32 c=0U;c<destinationLength;c++)target[c]=destination[c];for(UInt32 c=0U;c<suffix;c++)target[destinationLength+c]=oldPath[sourceLength+c];
            KernelHeap.TryRelease(e.Path);e.Path=replacement;e.Length=newLength;e.Hash=Hash(target,newLength);_entries[(Int32)i]=e;
        }
    }

    internal static void NotifyUnmount(KernelMountNamespaceHandle ns,Byte* path,UInt32 length)=>NotifyDeleted(ns,path,length,true);

    /// <summary>Returns true only when the directory is known and its initial background scan has completed.</summary>
    public static Boolean IsDirectoryReadyAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 length)
    {
        Int32 slot=Find(ns,path,length);return slot>=0&&_entries[slot].Type==(Byte)KernelFileType.Directory&&_entries[slot].Scanned!=0U;
    }

    /// <summary>Enumerates one indexed direct child and advances an in-memory cursor.</summary>
    public static Boolean TryGetNextDirectoryEntryAscii(KernelMountNamespaceHandle ns,Byte* directory,UInt32 directoryLength,ref UInt32 cursor,Byte* name,UInt32 capacity,out UInt32 nameLength,out KernelFileType type)
    {
        nameLength=0U;type=KernelFileType.Unknown;if(!_initialized||directory==null||directoryLength==0U||name==null||capacity==0U)return false;
        for(UInt32 i=cursor;i<MaximumEntries;i++)
        {
            Entry e=_entries[(Int32)i];if(e.Used==0U||e.Namespace!=ns.Value||e.Path.Address==0UL)continue;Byte* candidate=(Byte*)(nuint)e.Path.Address;
            if(e.Length<=directoryLength||!Prefix(candidate,e.Length,directory,directoryLength))continue;
            UInt32 start=directoryLength;if(directoryLength>1U){if(candidate[start]!=(Byte)'/')continue;start++;}else if(candidate[start]==(Byte)'/')start++;
            if(start>=e.Length)continue;UInt32 childLength=0U;while(start+childLength<e.Length&&candidate[start+childLength]!=(Byte)'/')childLength++;
            if(start+childLength!=e.Length)continue; // only direct children; their own entries exist separately
            if(childLength==0U||childLength>capacity){cursor=i+1U;return false;}for(UInt32 c=0U;c<childLength;c++)name[c]=candidate[start+c];
            nameLength=childLength;type=(KernelFileType)e.Type;cursor=i+1U;return true;
        }
        cursor=MaximumEntries;return false;
    }

    /// <summary>Processes one queued directory scan. Called by the storage role worker.</summary>
    internal static Boolean ServiceBackgroundStep()
    {
        if(!_initialized||_queueCount==0U)return false;UInt32 slot=_queue[(Int32)_queueRead];_queueRead=(_queueRead+1U)%MaximumEntries;_queueCount--;
        if(slot>=MaximumEntries)return false;Entry root=_entries[(Int32)slot];if(root.Used==0U||root.Type!=(Byte)KernelFileType.Directory||root.Path.Address==0UL)return false;root.Queued=0U;_entries[(Int32)slot]=root;
        Byte* path=(Byte*)(nuint)root.Path.Address;
        if(!KernelVfs.OpenDirectoryAscii(new KernelMountNamespaceHandle(root.Namespace),path,root.Length,out KernelDirectoryHandle handle)){root.Scanned=1U;_entries[(Int32)slot]=root;return true;}
        Char* childName=stackalloc Char[512];Byte* child=stackalloc Byte[(Int32)MaximumPathBytes];
        while(KernelVfs.ReadDirectory(handle,childName,512U,out UInt32 nameLength,out KernelFileType type,out _,out _))
        {
            if(nameLength==0U||nameLength>511U)continue;UInt32 childLength=root.Length;for(UInt32 c=0U;c<root.Length;c++)child[c]=path[c];
            if(childLength>1U&&child[childLength-1U]!=(Byte)'/')child[childLength++]=(Byte)'/';
            for(UInt32 c=0U;c<nameLength;c++){Char value=childName[c];if(value>0x7FU){childLength=0U;break;}if(childLength>=MaximumPathBytes){childLength=0U;break;}child[childLength++]=(Byte)value;}if(childLength==0U)continue;
            Int32 childSlot=AddOrUpdate(new KernelMountNamespaceHandle(root.Namespace),child,childLength,type,false);if(childSlot>=0&&type==KernelFileType.Directory)Queue((UInt32)childSlot);
        }
        KernelVfs.CloseDirectory(handle);root=_entries[(Int32)slot];if(root.Used!=0U){root.Scanned=1U;root.Queued=0U;_entries[(Int32)slot]=root;}if(_queueCount!=0U)KernelScheduler.NotifyRoleWork(KernelCpuRole.Storage);return true;
    }

    private static Int32 AddOrUpdate(KernelMountNamespaceHandle ns,Byte* path,UInt32 length,KernelFileType type,Boolean scanned)
    {
        if(!_initialized||ns.Value==0U||path==null||length==0U||length>MaximumPathBytes)return -1;Int32 existing=Find(ns,path,length);if(existing>=0){Entry current=_entries[existing];current.Type=(Byte)type;if(scanned)current.Scanned=1U;_entries[existing]=current;return existing;}
        Int32 slot=-1;for(Int32 i=0;i<(Int32)MaximumEntries;i++)if(_entries[i].Used==0U){slot=i;break;}if(slot<0)return -1;
        if(!KernelHeap.TryAllocate(length,8UL,false,out KernelHeapAllocation allocation))return -1;Byte* saved=(Byte*)(nuint)allocation.Address;for(UInt32 i=0U;i<length;i++)saved[i]=path[i];
        _entries[slot]=new Entry{Used=1U,Type=(Byte)type,Scanned=(Byte)(scanned?1:0),Namespace=ns.Value,Length=length,Hash=Hash(path,length),Path=allocation};_count++;return slot;
    }
    private static Int32 Find(KernelMountNamespaceHandle ns,Byte* path,UInt32 length)
    {UInt64 hash=Hash(path,length);for(Int32 i=0;i<(Int32)MaximumEntries;i++){Entry e=_entries[i];if(e.Used==0U||e.Namespace!=ns.Value||e.Length!=length||e.Hash!=hash||e.Path.Address==0UL)continue;if(Equals((Byte*)(nuint)e.Path.Address,path,length))return i;}return -1;}
    private static void Queue(UInt32 slot)
    {if(slot>=MaximumEntries)return;Entry e=_entries[(Int32)slot];if(e.Used==0U||e.Type!=(Byte)KernelFileType.Directory||e.Scanned!=0U||e.Queued!=0U||_queueCount>=MaximumEntries)return;e.Queued=1U;_entries[(Int32)slot]=e;_queue[(Int32)_queueWrite]=slot;_queueWrite=(_queueWrite+1U)%MaximumEntries;_queueCount++;KernelScheduler.NotifyRoleWork(KernelCpuRole.Storage);}
    private static UInt64 Hash(Byte* value,UInt32 length){UInt64 h=14695981039346656037UL;for(UInt32 i=0U;i<length;i++){Byte b=value[i];if(!FileSystemPathPolicyRuntime.CaseSensitive&&b>='A'&&b<='Z')b=(Byte)(b+32);h^=b;h*=1099511628211UL;}return h;}
    private static Boolean Equals(Byte* a,Byte* b,UInt32 length){for(UInt32 i=0U;i<length;i++){Byte x=a[i],y=b[i];if(!FileSystemPathPolicyRuntime.CaseSensitive){if(x>='A'&&x<='Z')x=(Byte)(x+32);if(y>='A'&&y<='Z')y=(Byte)(y+32);}if(x!=y)return false;}return true;}
    private static Boolean Prefix(Byte* value,UInt32 valueLength,Byte* prefix,UInt32 prefixLength){if(prefixLength>valueLength)return false;return Equals(value,prefix,prefixLength);}
    private static Boolean PathEqualsOrChild(Byte* value,UInt32 valueLength,Byte* path,UInt32 pathLength,Boolean subtree){if(valueLength<pathLength||!Prefix(value,valueLength,path,pathLength))return false;if(valueLength==pathLength)return true;if(!subtree)return false;if(pathLength==1U&&path[0]==(Byte)'/')return true;return pathLength>0U&&value[pathLength]==(Byte)'/';}
}
