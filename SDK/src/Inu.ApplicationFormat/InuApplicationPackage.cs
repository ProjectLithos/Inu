using System;

namespace Inu.ApplicationFormat;

/// <summary>Allocation-free validation and enumeration of the canonical Inu .exe package container.</summary>
public static unsafe class InuApplicationPackage
{
    public static Boolean IsPackage(Byte* package,UInt64 length)=>package!=null && length>=InuApplicationFormat.HeaderBytes && Read32(package,0)==InuApplicationFormat.Magic;

    public static Boolean TryInspect(Byte* p,UInt64 n,out InuApplicationInfo info)
    {
        info=default;if(p==null||n<InuApplicationFormat.HeaderBytes||n>InuApplicationFormat.MaximumPackageBytes||Read32(p,0)!=InuApplicationFormat.Magic||Read16(p,4)!=InuApplicationFormat.Major||Read16(p,6)>InuApplicationFormat.Minor)return false;
        UInt32 header=Read32(p,8);if(header<InuApplicationFormat.HeaderBytes||header>n)return false;
        UInt64 packageBytes=Read64(p,24);if(packageBytes!=n)return false;
        UInt64 nativeOffset=Read64(p,32),nativeLength=Read64(p,40),entryRva=Read64(p,48),depOffset=Read64(p,56),capOffset=Read64(p,72),resOffset=Read64(p,80),strings=Read64(p,96),stringsLength=Read64(p,104),resourceData=Read64(p,112),resourceBytes=Read64(p,120);
        UInt32 depCount=Read32(p,64),capCount=Read32(p,68),resCount=Read32(p,88);
        InuApplicationArchitecture architecture=(InuApplicationArchitecture)Read16(p,12);InuApplicationAbi syscallAbi=(InuApplicationAbi)Read16(p,14);InuApplicationFlags flags=(InuApplicationFlags)Read32(p,20);
        const InuApplicationFlags knownFlags=InuApplicationFlags.Signed|InuApplicationFlags.HasResources|InuApplicationFlags.PositionIndependent;
        if((architecture!=InuApplicationArchitecture.X64&&architecture!=InuApplicationArchitecture.Arm64&&architecture!=InuApplicationArchitecture.RiscV64)||(syscallAbi!=InuApplicationAbi.Inu&&syscallAbi!=InuApplicationAbi.Linux&&syscallAbi!=InuApplicationAbi.WindowsNt)||(flags&~knownFlags)!=0)return false;
        if(depCount>InuApplicationFormat.MaximumDependencies||capCount>InuApplicationFormat.MaximumCapabilities||resCount>InuApplicationFormat.MaximumResources)return false;
        if(!Range(nativeOffset,nativeLength,n)||nativeLength==0UL||!Table(depOffset,depCount,InuApplicationFormat.DependencyRecordBytes,n)||!Table(capOffset,capCount,InuApplicationFormat.CapabilityRecordBytes,n)||!Table(resOffset,resCount,InuApplicationFormat.ResourceRecordBytes,n)||!Range(strings,stringsLength,n)||!Range(resourceData,resourceBytes,n))return false;
        UInt64 depBytes=(UInt64)depCount*InuApplicationFormat.DependencyRecordBytes,capBytes=(UInt64)capCount*InuApplicationFormat.CapabilityRecordBytes,resBytes=(UInt64)resCount*InuApplicationFormat.ResourceRecordBytes;
        if(depOffset<(UInt64)header||capOffset<depOffset+depBytes||resOffset<capOffset+capBytes||strings<resOffset+resBytes||nativeOffset<strings+stringsLength||resourceData<nativeOffset+nativeLength||resourceData+resourceBytes!=n)return false;
        if(((flags&InuApplicationFlags.HasResources)!=0)!=(resCount!=0U)||((resCount==0U)!=(resourceBytes==0UL)))return false;
        InuPackageString id=StringRef(p,n,strings,stringsLength,128),name=StringRef(p,n,strings,stringsLength,136),version=StringRef(p,n,strings,stringsLength,144),publisher=StringRef(p,n,strings,stringsLength,152),minimumSdk=StringRef(p,n,strings,stringsLength,160);
        if(id.Length==0U||name.Length==0U||version.Length==0U)return false;
        info=new InuApplicationInfo(architecture,syscallAbi,Read16(p,16),Read16(p,18),flags,packageBytes,nativeOffset,nativeLength,entryRva,depOffset,depCount,capOffset,capCount,resOffset,resCount,strings,stringsLength,resourceData,resourceBytes,id,name,version,publisher,minimumSdk);
        for(UInt32 i=0;i<depCount;i++)if(!TryGetDependency(p,n,info,i,out InuApplicationDependency _))return false;
        for(UInt32 i=0;i<capCount;i++)if(!TryGetCapability(p,n,info,i,out InuApplicationCapability _))return false;
        for(UInt32 i=0;i<resCount;i++)if(!TryGetResource(p,n,info,i,out InuApplicationResource _))return false;
        return true;
    }

    public static Boolean TryGetNativeImage(Byte* p,UInt64 n,InuApplicationInfo info,out Byte* image,out UInt64 length)
    {image=null;length=0UL;if(p==null||!Range(info.NativeImageOffset,info.NativeImageLength,n))return false;image=p+info.NativeImageOffset;length=info.NativeImageLength;return true;}

    public static Boolean TryGetDependency(Byte* p,UInt64 n,InuApplicationInfo info,UInt32 index,out InuApplicationDependency dependency)
    {dependency=default;if(index>=info.DependencyCount)return false;UInt64 o=info.DependencyTableOffset+(UInt64)index*InuApplicationFormat.DependencyRecordBytes;if(!Range(o,InuApplicationFormat.DependencyRecordBytes,n))return false;InuPackageString id=RecordString(p,n,info,o),version=RecordString(p,n,info,o+8UL);if(id.Length==0U)return false;dependency=new(id,version,Read64(p,o+16UL));return true;}
    public static Boolean TryGetCapability(Byte* p,UInt64 n,InuApplicationInfo info,UInt32 index,out InuApplicationCapability capability)
    {capability=default;if(index>=info.CapabilityCount)return false;UInt64 o=info.CapabilityTableOffset+(UInt64)index*InuApplicationFormat.CapabilityRecordBytes;if(!Range(o,InuApplicationFormat.CapabilityRecordBytes,n))return false;InuPackageString name=RecordString(p,n,info,o);if(name.Length==0U)return false;capability=new(name,Read64(p,o+8UL));return true;}
    public static Boolean TryGetResource(Byte* p,UInt64 n,InuApplicationInfo info,UInt32 index,out InuApplicationResource resource)
    {resource=default;if(index>=info.ResourceCount)return false;UInt64 o=info.ResourceTableOffset+(UInt64)index*InuApplicationFormat.ResourceRecordBytes;if(!Range(o,InuApplicationFormat.ResourceRecordBytes,n))return false;InuPackageString name=RecordString(p,n,info,o);UInt64 dataOffset=Read64(p,o+8UL),dataLength=Read64(p,o+16UL);InuApplicationResourceFlags flags=(InuApplicationResourceFlags)Read32(p,o+24UL);const InuApplicationResourceFlags known=InuApplicationResourceFlags.ReadOnly|InuApplicationResourceFlags.Localized|InuApplicationResourceFlags.Executable;if(name.Length==0U||!Range(dataOffset,dataLength,n)||dataOffset<info.ResourceDataOffset||(flags&~known)!=0)return false;resource=new(name,dataOffset,dataLength,flags);return true;}
    public static Boolean TryGetStringBytes(Byte* p,UInt64 n,InuApplicationInfo info,InuPackageString value,out Byte* bytes,out UInt32 length)
    {bytes=null;length=0U;if(value.Length==0U)return true;UInt64 absolute=info.StringTableOffset+value.Offset;if(!Range(absolute,value.Length,n)||value.Offset>info.StringTableLength||value.Length>info.StringTableLength-value.Offset)return false;bytes=p+absolute;length=value.Length;return Hash(bytes,length)==value.Hash;}

    public static UInt64 Hash(Byte* value,UInt32 length){UInt64 h=14695981039346656037UL;for(UInt32 i=0;i<length;i++){h^=value[i];h*=1099511628211UL;}return h;}
    private static InuPackageString RecordString(Byte* p,UInt64 n,InuApplicationInfo info,UInt64 o){UInt32 off=Read32(p,o),len=Read32(p,o+4UL);if((UInt64)off>info.StringTableLength||(UInt64)len>info.StringTableLength-(UInt64)off)return default;Byte* s=p+info.StringTableOffset+off;return new(off,len,Hash(s,len));}
    private static InuPackageString StringRef(Byte* p,UInt64 n,UInt64 stringBase,UInt64 stringBytes,UInt64 o){UInt32 off=Read32(p,o),len=Read32(p,o+4UL);if((UInt64)off>stringBytes||(UInt64)len>stringBytes-(UInt64)off||!Range(stringBase+off,len,n))return default;return new(off,len,Hash(p+stringBase+off,len));}
    private static Boolean Table(UInt64 o,UInt32 count,UInt32 record,UInt64 n)=>count==0U?o<=n:Range(o,(UInt64)count*record,n);
    private static Boolean Range(UInt64 o,UInt64 l,UInt64 n)=>o<=n&&l<=n-o;
    private static UInt16 Read16(Byte* p,UInt64 o)=>(UInt16)(p[o]|((UInt16)p[o+1]<<8));
    private static UInt32 Read32(Byte* p,UInt64 o)=>(UInt32)(p[o]|((UInt32)p[o+1]<<8)|((UInt32)p[o+2]<<16)|((UInt32)p[o+3]<<24));
    private static UInt64 Read64(Byte* p,UInt64 o)=>(UInt64)Read32(p,o)|((UInt64)Read32(p,o+4UL)<<32);
}
