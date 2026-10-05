using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

if(args.Length!=2){Console.Error.WriteLine("Usage: Inu.ApplicationPacker <Inu.Application.json> <output.exe>");return 2;}
var manifestPath=Path.GetFullPath(args[0]);var outPath=Path.GetFullPath(args[1]);
var model=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? throw new InvalidDataException("Invalid manifest.");
Validate(model);
var baseDir=Path.GetDirectoryName(manifestPath)!;var nativePath=Path.GetFullPath(Path.Combine(baseDir,model.NativeImage));var native=File.ReadAllBytes(nativePath);
var strings=new MemoryStream();var refs=new Dictionary<string,(uint off,uint len)>(StringComparer.Ordinal);
(uint off,uint len) S(string? value){value??="";if(refs.TryGetValue(value,out var r))return r;var b=Encoding.UTF8.GetBytes(value);var rr=((uint)strings.Position,(uint)b.Length);strings.Write(b);refs[value]=rr;return rr;}
var id=S(model.Id);var name=S(model.Name);var version=S(model.Version);var publisher=S(model.Publisher);var minimumSdk=S(model.MinimumSdkVersion);
var deps=(model.Dependencies??[]).Select(d=>(id:S(d.Id),ver:S(d.Version),d.Flags)).ToArray();
var caps=(model.RequiredCapabilities??[]).Select(c=>(name:S(c.Name),c.Rights)).ToArray();
var resources=new List<(uint off,uint len,byte[] data,uint flags)>();foreach(var r in model.Resources??[]){var sr=S(r.Name);var data=File.ReadAllBytes(Path.GetFullPath(Path.Combine(baseDir,r.Path)));resources.Add((sr.off,sr.len,data,r.Flags));}
const int H=192,DR=24,CR=16,RR=32;const long MaximumPackageBytes=67108864L;long depOff=H,capOff=depOff+deps.Length*DR,resOff=capOff+caps.Length*CR,strOff=resOff+resources.Count*RR,strLen=strings.Length,nativeOff=Align(strOff+strLen,16),resDataOff=Align(nativeOff+native.Length,16);long resBytes=resources.Sum(r=>(long)r.data.Length);long packageBytes=checked(resDataOff+resBytes);
if(packageBytes>MaximumPackageBytes)throw new InvalidDataException($"Application package exceeds the {MaximumPackageBytes}-byte format limit.");
var output=new byte[checked((int)packageBytes)];
void U16(int o,ushort v)=>BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(o,2),v);void U32(int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(o,4),v);void U64(int o,ulong v)=>BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(o,8),v);
uint packageFlags=model.Flags;if(resources.Count!=0)packageFlags|=2U;else packageFlags&=~2U;U32(0,0x50414F4E);U16(4,1);U16(6,0);U32(8,H);U16(12,Arch(model.Architecture));U16(14,Abi(model.SyscallAbi));U16(16,model.AbiMajor);U16(18,model.AbiMinor);U32(20,packageFlags);U64(24,(ulong)packageBytes);U64(32,(ulong)nativeOff);U64(40,(ulong)native.Length);U64(48,model.EntryPointRva);U64(56,(ulong)depOff);U32(64,(uint)deps.Length);U32(68,(uint)caps.Length);U64(72,(ulong)capOff);U64(80,(ulong)resOff);U32(88,(uint)resources.Count);U64(96,(ulong)strOff);U64(104,(ulong)strLen);U64(112,(ulong)resDataOff);U64(120,(ulong)resBytes);
void Ref(int o,(uint off,uint len) r){U32(o,r.off);U32(o+4,r.len);}Ref(128,id);Ref(136,name);Ref(144,version);Ref(152,publisher);Ref(160,minimumSdk);
for(int i=0;i<deps.Length;i++){int o=(int)depOff+i*DR;Ref(o,deps[i].id);Ref(o+8,deps[i].ver);U64(o+16,deps[i].Flags);}
for(int i=0;i<caps.Length;i++){int o=(int)capOff+i*CR;Ref(o,caps[i].name);U64(o+8,caps[i].Rights);}
ReadOnlySpan<byte> stringTable=strings.GetBuffer().AsSpan(0,checked((int)strLen));stringTable.CopyTo(output.AsSpan(checked((int)strOff)));
ReadOnlySpan<byte> nativeImage=native;nativeImage.CopyTo(output.AsSpan(checked((int)nativeOff)));long dataCursor=resDataOff;
for(int i=0;i<resources.Count;i++){var r=resources[i];int o=(int)resOff+i*RR;U32(o,r.off);U32(o+4,r.len);U64(o+8,(ulong)dataCursor);U64(o+16,(ulong)r.data.Length);U32(o+24,r.flags);((ReadOnlySpan<byte>)r.data).CopyTo(output.AsSpan(checked((int)dataCursor)));dataCursor+=r.data.Length;}
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);File.WriteAllBytes(outPath,output);Console.WriteLine($"[ OK ] Inu application package: {outPath}");Console.WriteLine($"[INFO] Native image: {nativePath}");Console.WriteLine($"[INFO] Dependencies={deps.Length}, capabilities={caps.Length}, resources={resources.Count}, bytes={output.Length}");return 0;

static long Align(long v,long a)=>(v+(a-1))&~(a-1);
static ushort Arch(string v)=>v.ToLowerInvariant() switch{"x86_64" or "x64"=>1,"arm64"=>2,"riscv64"=>3,_=>throw new InvalidDataException("Unsupported architecture.")};
static ushort Abi(string v)=>v.ToLowerInvariant() switch{"inu"=>1,"linux"=>2,"windows-nt" or "nt"=>3,_=>throw new InvalidDataException("Unsupported syscall ABI.")};
static void Validate(Manifest m)
{
    if(string.IsNullOrWhiteSpace(m.Id)||string.IsNullOrWhiteSpace(m.Name)||string.IsNullOrWhiteSpace(m.Version)||string.IsNullOrWhiteSpace(m.NativeImage))throw new InvalidDataException("id, name, version and nativeImage are required.");
    _=Arch(m.Architecture);string abi=m.SyscallAbi.Trim().ToLowerInvariant();UInt16 expectedMajor=abi=="inu"?(UInt16)2:abi is "linux" or "windows-nt" or "nt"?(UInt16)1:throw new InvalidDataException("Unsupported syscall ABI.");if(m.AbiMajor==0)m.AbiMajor=expectedMajor;if(m.AbiMajor!=expectedMajor||m.AbiMinor!=0)throw new InvalidDataException($"{m.SyscallAbi} syscall ABI requires {expectedMajor}.0 in this SDK.");
    if((m.Flags&~7U)!=0U)throw new InvalidDataException("Application package contains unknown flags.");if((m.Flags&1U)!=0U)throw new InvalidDataException("Signed Inu packages are reserved until signature verification is enabled.");if((m.Flags&4U)!=0U)throw new InvalidDataException("Position-independent application packages are not yet emitted by the fixed-image x64 loader.");
    if((m.Dependencies?.Length??0)>256)throw new InvalidDataException("Application package has more than 256 dependencies.");if((m.RequiredCapabilities?.Length??0)>256)throw new InvalidDataException("Application package has more than 256 required capabilities.");if((m.Resources?.Length??0)>4096)throw new InvalidDataException("Application package has more than 4096 resources.");
    foreach(var d in m.Dependencies??[])if(string.IsNullOrWhiteSpace(d.Id))throw new InvalidDataException("Every dependency requires an id.");
    foreach(var c in m.RequiredCapabilities??[])if(string.IsNullOrWhiteSpace(c.Name))throw new InvalidDataException("Every required capability requires a name.");
    foreach(var r in m.Resources??[])if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Path)||(r.Flags&~7U)!=0U)throw new InvalidDataException("Every resource requires a name/path and only ReadOnly, Localized and Executable flags are valid.");
}
sealed class Manifest{public string Id{get;set;}="";public string Name{get;set;}="";public string Version{get;set;}="";public string Publisher{get;set;}="";public string MinimumSdkVersion{get;set;}="";public string Architecture{get;set;}="x86_64";public string SyscallAbi{get;set;}="inu";public ushort AbiMajor{get;set;}=0;public ushort AbiMinor{get;set;}=0;public ulong EntryPointRva{get;set;}=0;public uint Flags{get;set;}=0;public string NativeImage{get;set;}="";public Dependency[]? Dependencies{get;set;}public Capability[]? RequiredCapabilities{get;set;}public Resource[]? Resources{get;set;}}
sealed class Dependency{public string Id{get;set;}="";public string Version{get;set;}="";public ulong Flags{get;set;}=0;}
sealed class Capability{public string Name{get;set;}="";public ulong Rights{get;set;}=0;}
sealed class Resource{public string Name{get;set;}="";public string Path{get;set;}="";public uint Flags{get;set;}=1;}
