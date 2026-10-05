using System.Buffers.Binary;
using System.Text;

/// <summary>Creates the Inu GPT/FAT32 system disk with VFAT long-file-name support.</summary>
internal static class EfiDiskImage
{
    private const int SectorSize = 512;
    private const uint TotalSectors = 131072;
    private const uint PartitionStartLba = 2048;
    private const uint ReservedSectors = 32;
    private const uint FatCount = 2;
    private const uint SectorsPerCluster = 1;
    private const uint RootCluster = 2;
    private const uint EndOfChain = 0x0FFFFFFF;
    private static readonly Guid EfiSystemPartitionType = new("C12A7328-F81F-11D2-BA4B-00A0C93EC93B");
    private static readonly Guid DiskIdentifier = new("4E6F7661-4F72-796E-8000-000000000027");
    private static readonly Guid PartitionIdentifier = new("4E6F7661-4F72-796E-8100-000000000027");

    internal static bool TryCreate(string stagingRoot, string imagePath, out bool preservedUserState, out string error)
    {
        error=string.Empty;preservedUserState=false;
        if(!Directory.Exists(stagingRoot)){error=$"FAT32 staging root not found: {stagingRoot}";return false;}
        string temporaryImage=imagePath+".new";
        try
        {
            // /USERS is mutable OS state. Rebuilding the system files must not erase registered
            // accounts, Home folders or personal settings from a previously run image.
            if(File.Exists(imagePath)&&!TryExtractUsersTree(imagePath,stagingRoot,out preservedUserState,out error))return false;
            // 0.0.168 and earlier booted a disposable image copy under Runs/<timestamp>.
            // If the base image has no /USERS yet, import the newest usable run copy once so
            // upgrading to persistent-image semantics does not force the user to register again.
            if(!preservedUserState)TryExtractUsersTreeFromLatestRun(imagePath,stagingRoot,out preservedUserState);
            FatNode root=BuildTree(stagingRoot,null,true);
            uint lastUsableLba=TotalSectors-34,partitionSectors=lastUsableLba-PartitionStartLba+1;
            FatLayout layout=CalculateFatLayout(partitionSectors);
            uint next=RootCluster;AllocateClusters(root,ref next);
            if(next>layout.ClusterCount+2U){error=$"Staged system files are too large for the {TotalSectors*SectorSize/1024/1024} MiB FAT32 image.";return false;}
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(imagePath))!);
            if(File.Exists(temporaryImage))File.Delete(temporaryImage);
            using(FileStream image=new(temporaryImage,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {
                image.SetLength((long)TotalSectors*SectorSize);
                WriteProtectiveMbr(image);WriteGuidPartitionTable(image,lastUsableLba);WriteFat32Volume(image,partitionSectors,layout,root);image.Flush(true);
            }
            File.Move(temporaryImage,imagePath,true);return true;
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or ArgumentException)
        {try{if(File.Exists(temporaryImage))File.Delete(temporaryImage);}catch{}error=ex.Message;return false;}
    }


    private static void TryExtractUsersTreeFromLatestRun(string imagePath,string stagingRoot,out bool preserved)
    {
        preserved=false;string? outputDirectory=Path.GetDirectoryName(Path.GetFullPath(imagePath));if(string.IsNullOrWhiteSpace(outputDirectory))return;string runsRoot=Path.Combine(outputDirectory,"Runs");if(!Directory.Exists(runsRoot))return;
        string fileName=Path.GetFileName(imagePath);foreach(string candidate in Directory.GetFiles(runsRoot,fileName,SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            if(string.Equals(Path.GetFullPath(candidate),Path.GetFullPath(imagePath),StringComparison.OrdinalIgnoreCase))continue;
            if(TryExtractUsersTree(candidate,stagingRoot,out bool found,out _)&&found){preserved=true;return;}
        }
    }

    private static bool TryExtractUsersTree(string imagePath,string stagingRoot,out bool preserved,out string error)
    {
        preserved=false;error=string.Empty;
        try
        {
            using FileStream image=new(imagePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            if(image.Length<(long)(PartitionStartLba+1U)*SectorSize){error="Existing Inu image is too small to preserve /USERS state.";return false;}
            byte[] boot=new byte[SectorSize];ReadExactlyAtLba(image,PartitionStartLba,boot);
            ushort bytesPerSector=BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(11,2));byte sectorsPerCluster=boot[13];ushort reserved=BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(14,2));byte fats=boot[16];uint fatSectors=BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(36,4));uint rootCluster=BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(44,4));
            if(bytesPerSector!=SectorSize||sectorsPerCluster==0||reserved==0||fats==0||fatSectors==0||rootCluster<2U){error="Existing Inu image has an unsupported FAT32 layout; refusing to erase /USERS state.";return false;}
            uint dataStart=(uint)reserved+(uint)fats*fatSectors;byte[] fat=new byte[checked((int)(fatSectors*(uint)SectorSize))];ReadExactlyAtLba(image,PartitionStartLba+(uint)reserved,fat);
            foreach(FatDirectoryEntry entry in ReadDirectory(image,fat,dataStart,sectorsPerCluster,rootCluster))
            {
                if(!entry.IsDirectory||!string.Equals(entry.Name,"USERS",StringComparison.OrdinalIgnoreCase))continue;
                string usersRoot=Path.Combine(stagingRoot,"USERS");if(Directory.Exists(usersRoot))Directory.Delete(usersRoot,true);Directory.CreateDirectory(usersRoot);
                ExtractDirectory(image,fat,dataStart,sectorsPerCluster,entry.FirstCluster,usersRoot,0);preserved=true;return true;
            }
            return true;
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or ArgumentException)
        {error="Existing /USERS state could not be preserved: "+ex.Message;return false;}
    }

    private static void ExtractDirectory(Stream image,byte[] fat,uint dataStart,byte sectorsPerCluster,uint cluster,string destination,int depth)
    {
        if(depth>16)throw new InvalidOperationException("/USERS directory nesting exceeds the preservation limit.");
        foreach(FatDirectoryEntry entry in ReadDirectory(image,fat,dataStart,sectorsPerCluster,cluster))
        {
            ValidateFatName(entry.Name);string target=Path.Combine(destination,entry.Name);
            if(entry.IsDirectory){Directory.CreateDirectory(target);ExtractDirectory(image,fat,dataStart,sectorsPerCluster,entry.FirstCluster,target,depth+1);continue;}
            using FileStream output=new(target,FileMode.Create,FileAccess.Write,FileShare.None);WriteFileFromImage(image,fat,dataStart,sectorsPerCluster,entry.FirstCluster,entry.Length,output);
        }
    }

    private static List<FatDirectoryEntry> ReadDirectory(Stream image,byte[] fat,uint dataStart,byte sectorsPerCluster,uint firstCluster)
    {
        byte[] data=ReadClusterChain(image,fat,dataStart,sectorsPerCluster,firstCluster);List<FatDirectoryEntry> entries=new();List<(int Ordinal,string Text,byte Checksum,bool Last)> lfn=new();
        for(int offset=0;offset+32<=data.Length;offset+=32)
        {
            byte marker=data[offset];if(marker==0x00)break;if(marker==0xE5){lfn.Clear();continue;}byte attributes=data[offset+11];
            if(attributes==0x0F){byte ord=data[offset];lfn.Add((ord&0x1F,ReadLongNameFragment(data.AsSpan(offset,32)),data[offset+13],(ord&0x40)!=0));continue;}
            if((attributes&0x08)!=0){lfn.Clear();continue;}
            string shortName=ReadShortName(data.AsSpan(offset,11));string name=TryAssembleLongName(lfn,data.AsSpan(offset,11))??shortName;lfn.Clear();if(name=="."||name==".."||name.Length==0)continue;
            uint high=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset+20,2));uint low=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset+26,2));uint cluster=(high<<16)|low;uint length=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset+28,4));bool directory=(attributes&0x10)!=0;
            if(directory&&cluster<2U)throw new InvalidOperationException($"Directory {name} has an invalid FAT32 cluster.");entries.Add(new FatDirectoryEntry(name,directory,cluster,length));
        }
        return entries;
    }
    private static string ReadLongNameFragment(ReadOnlySpan<byte> entry)
    {
        int[] offsets={1,3,5,7,9,14,16,18,20,22,24,28,30};StringBuilder b=new(13);foreach(int o in offsets){ushort c=BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(o,2));if(c==0x0000||c==0xFFFF)break;b.Append((char)c);}return b.ToString();
    }
    private static string? TryAssembleLongName(List<(int Ordinal,string Text,byte Checksum,bool Last)> parts,ReadOnlySpan<byte> shortEntry)
    {
        if(parts.Count==0)return null;byte checksum=0;for(int i=0;i<11;i++)checksum=(byte)(((checksum&1)!=0?0x80:0)+(checksum>>1)+shortEntry[i]);
        if(parts.Any(p=>p.Checksum!=checksum)||parts.Count(p=>p.Last)!=1)return null;parts.Sort((a,b)=>a.Ordinal.CompareTo(b.Ordinal));for(int i=0;i<parts.Count;i++)if(parts[i].Ordinal!=i+1)return null;StringBuilder b=new();foreach(var p in parts)b.Append(p.Text);return b.ToString();
    }

    private static string ReadShortName(ReadOnlySpan<byte> entry)
    {
        string basis=Encoding.ASCII.GetString(entry[..8]).TrimEnd(' ');string ext=Encoding.ASCII.GetString(entry.Slice(8,3)).TrimEnd(' ');return ext.Length==0?basis:basis+"."+ext;
    }

    private static byte[] ReadClusterChain(Stream image,byte[] fat,uint dataStart,byte sectorsPerCluster,uint firstCluster)
    {
        if(firstCluster<2U)return Array.Empty<byte>();int clusterBytes=checked(sectorsPerCluster*SectorSize);using MemoryStream result=new();HashSet<uint> visited=new();uint cluster=firstCluster;
        while(cluster>=2U&&cluster<0x0FFFFFF8U)
        {
            if(!visited.Add(cluster))throw new InvalidOperationException("FAT32 cluster chain contains a loop.");uint fatOffset=checked(cluster*4U);if(fatOffset+4U>(uint)fat.Length)throw new InvalidOperationException("FAT32 cluster chain is outside the FAT.");
            byte[] buffer=new byte[clusterBytes];uint relative=dataStart+(cluster-2U)*(uint)sectorsPerCluster;ReadExactlyAtLba(image,PartitionStartLba+relative,buffer);result.Write(buffer);cluster=BinaryPrimitives.ReadUInt32LittleEndian(fat.AsSpan(checked((int)fatOffset),4))&0x0FFFFFFFU;
            if(cluster==0U||cluster==1U||cluster==0x0FFFFFF7U)throw new InvalidOperationException("FAT32 cluster chain is corrupt.");
        }
        return result.ToArray();
    }

    private static void WriteFileFromImage(Stream image,byte[] fat,uint dataStart,byte sectorsPerCluster,uint firstCluster,uint length,Stream output)
    {
        if(length==0U)return;byte[] data=ReadClusterChain(image,fat,dataStart,sectorsPerCluster,firstCluster);if((ulong)data.Length<length)throw new InvalidOperationException("FAT32 file chain is shorter than its directory length.");output.Write(data,0,checked((int)length));
    }

    private static void ReadExactlyAtLba(Stream image,uint lba,Span<byte> buffer)
    {
        image.Position=(long)lba*SectorSize;int total=0;while(total<buffer.Length){int read=image.Read(buffer[total..]);if(read<=0)throw new EndOfStreamException("Unexpected end of Inu FAT32 image.");total+=read;}
    }

    private static FatNode BuildTree(string path,FatNode? parent,bool root)
    {
        FatNode node=new(root?string.Empty:Path.GetFileName(path),path,true,parent);
        foreach(string dir in Directory.GetDirectories(path).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase))
        { ValidateFatName(Path.GetFileName(dir));node.Children.Add(BuildTree(dir,node,false)); }
        foreach(string file in Directory.GetFiles(path).OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase))
        { ValidateFatName(Path.GetFileName(file));node.Children.Add(new FatNode(Path.GetFileName(file),file,false,node){Length=new FileInfo(file).Length}); }
        return node;
    }

    private static void AllocateClusters(FatNode node,ref uint next)
    {
        uint bytesPerCluster=SectorsPerCluster*SectorSize;
        if(node.IsDirectory)
        {
            uint entries=node.Parent is null?0U:2U;
            HashSet<string> aliases=new(StringComparer.OrdinalIgnoreCase);
            foreach(FatNode child in node.Children)
            {
                string alias=CreateShortAlias(child.Name,aliases);aliases.Add(alias);uint sequence=1U+(NeedsLongName(child.Name,alias)?LongEntryCount(child.Name):0U);uint slotsPerSector=SectorSize/32U,remaining=slotsPerSector-(entries%slotsPerSector);if(remaining<sequence)entries+=remaining;entries+=sequence;
            }
            node.ClusterCount=Math.Max(1U,(entries*32U+bytesPerCluster-1U)/bytesPerCluster);
        }
        else node.ClusterCount=node.Length==0?0U:checked((uint)((node.Length+bytesPerCluster-1)/bytesPerCluster));
        if(node.ClusterCount!=0U){node.FirstCluster=next;next=checked(next+node.ClusterCount);}
        foreach(FatNode child in node.Children)AllocateClusters(child,ref next);
    }

    private static FatLayout CalculateFatLayout(uint partitionSectors)
    {
        for(uint fatSectors=1;fatSectors<partitionSectors/2;fatSectors++)
        {
            uint dataSectors=checked(partitionSectors-ReservedSectors-FatCount*fatSectors),clusterCount=dataSectors/SectorsPerCluster;
            if(checked(fatSectors*SectorSize/4)>=clusterCount+2)return new FatLayout(fatSectors,clusterCount,ReservedSectors+FatCount*fatSectors);
        }
        throw new InvalidOperationException("FAT32 geometry could not be calculated.");
    }

    private static void WriteFat32Volume(Stream image,uint partitionSectors,FatLayout layout,FatNode root)
    { WriteFatBootSectors(image,partitionSectors,layout);WriteFatTables(image,layout,root);WriteNode(image,layout,root); }

    private static void WriteFatTables(Stream image,FatLayout layout,FatNode root)
    {
        byte[] fat=new byte[checked((int)(layout.FatSectors*SectorSize))];SetFatEntry(fat,0,0x0FFFFFF8);SetFatEntry(fat,1,EndOfChain);WriteNodeFat(fat,root);
        WriteAtLba(image,PartitionStartLba+ReservedSectors,fat);WriteAtLba(image,PartitionStartLba+ReservedSectors+layout.FatSectors,fat);
    }
    private static void WriteNodeFat(Span<byte> fat,FatNode node)
    {
        for(uint i=0;i<node.ClusterCount;i++){uint cluster=node.FirstCluster+i;SetFatEntry(fat,cluster,i+1U==node.ClusterCount?EndOfChain:cluster+1U);}
        foreach(FatNode child in node.Children)WriteNodeFat(fat,child);
    }

    private static void WriteNode(Stream image,FatLayout layout,FatNode node)
    {
        if(node.IsDirectory)WriteDirectory(image,layout,node);else WriteFile(image,layout,node);
        foreach(FatNode child in node.Children)WriteNode(image,layout,child);
    }
    private static void WriteDirectory(Stream image,FatLayout layout,FatNode node)
    {
        byte[] data=new byte[checked((int)(node.ClusterCount*SectorsPerCluster*SectorSize))];int index=0;
        if(node.Parent is not null)
        {
            WriteDirectoryEntry(data,index++,".          ",0x10,node.FirstCluster,0);
            WriteDirectoryEntry(data,index++,"..         ",0x10,node.Parent.FirstCluster,0);
        }
        HashSet<string> aliases=new(StringComparer.OrdinalIgnoreCase);
        foreach(FatNode child in node.Children)
        {
            string alias=CreateShortAlias(child.Name,aliases);aliases.Add(alias);int sequence=1+(NeedsLongName(child.Name,alias)?checked((int)LongEntryCount(child.Name)):0);int slotsPerSector=SectorSize/32,remaining=slotsPerSector-(index%slotsPerSector);if(remaining<sequence){for(int pad=0;pad<remaining;pad++)data[(index++*32)]=0xE5;}
            if(NeedsLongName(child.Name,alias))index=WriteLongNameEntries(data,index,child.Name,alias);
            WriteDirectoryEntry(data,index++,alias,child.IsDirectory?(byte)0x10:(byte)0x20,child.FirstCluster,child.IsDirectory?0U:checked((uint)child.Length));
        }
        WriteClusterChain(image,layout,node,data);
    }
    private static void WriteFile(Stream image,FatLayout layout,FatNode node)
    {
        if(node.ClusterCount==0U)return;byte[] data=new byte[checked((int)(node.ClusterCount*SectorsPerCluster*SectorSize))];using FileStream input=File.OpenRead(node.FullPath);
        int offset=0,read;while((read=input.Read(data,offset,data.Length-offset))>0){offset+=read;if(offset==data.Length)break;}WriteClusterChain(image,layout,node,data);
    }
    private static void WriteClusterChain(Stream image,FatLayout layout,FatNode node,ReadOnlySpan<byte> data)
    {
        int clusterBytes=checked((int)(SectorsPerCluster*SectorSize));for(uint i=0;i<node.ClusterCount;i++)
        { uint relativeLba=layout.DataStartSector+(node.FirstCluster+i-2U)*SectorsPerCluster;image.Position=(long)(PartitionStartLba+relativeLba)*SectorSize;image.Write(data.Slice(checked((int)i*clusterBytes),clusterBytes)); }
    }

    private static void ValidateFatName(string name)
    {
        if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("FAT32 staging contains an empty name.");
        if(name.Length>255)throw new InvalidOperationException($"FAT32 long filename exceeds 255 characters: {name}");
        foreach(char c in name)if(c<' '||c=='"'||c=='*'||c=='/'||c==':'||c=='<'||c=='>'||c=='?'||c=='\\'||c=='|')throw new InvalidOperationException($"FAT32 filename contains unsupported character '{c}': {name}");
        if(name.EndsWith(' ')||name.EndsWith('.'))throw new InvalidOperationException($"FAT32 filename cannot end with a space or dot: {name}");
    }
    private static uint LongEntryCount(string name)=>checked((uint)((name.Length+12)/13));
    private static bool IsShortNameCompatible(string name,out string shortName)
    {
        shortName=string.Empty;string upper=name.ToUpperInvariant();int dot=upper.LastIndexOf('.');string basis=dot>0?upper[..dot]:upper,ext=dot>0?upper[(dot+1)..]:string.Empty;
        if(basis.Length is <1 or >8||ext.Length>3||upper!=name)return false;
        foreach(char c in basis+ext)if(!(c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_'||c=='-'||c=='$'||c=='~'))return false;
        shortName=basis.PadRight(8,' ')+ext.PadRight(3,' ');return true;
    }
    private static string CreateShortAlias(string name,HashSet<string> used)
    {
        if(IsShortNameCompatible(name,out string exact)&&!used.Contains(exact))return exact;
        string upper=name.ToUpperInvariant();int dot=upper.LastIndexOf('.');string basis=dot>0?upper[..dot]:upper,ext=dot>0?upper[(dot+1)..]:string.Empty;
        basis=SanitizeShortPart(basis);ext=SanitizeShortPart(ext);if(basis.Length==0)basis="FILE";if(ext.Length>3)ext=ext[..3];
        for(int n=1;n<=999999;n++)
        {
            string tail="~"+n.ToString();int take=Math.Max(1,8-tail.Length);string b=(basis.Length>take?basis[..take]:basis)+tail;if(b.Length>8)b=b[..8];string alias=b.PadRight(8,' ')+ext.PadRight(3,' ');if(!used.Contains(alias))return alias;
        }
        throw new InvalidOperationException($"Could not allocate unique FAT32 short alias for {name}");
    }
    private static string SanitizeShortPart(string value)
    {
        StringBuilder b=new();foreach(char c in value)if(c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_'||c=='-'||c=='$')b.Append(c);return b.ToString();
    }
    private static bool NeedsLongName(string name,string alias)
    {
        if(!IsShortNameCompatible(name,out string exact))return true;return !string.Equals(exact,alias,StringComparison.Ordinal);
    }
    private static byte ShortNameChecksum(string alias)
    {
        byte sum=0;for(int i=0;i<11;i++)sum=(byte)(((sum&1)!=0?0x80:0)+(sum>>1)+(byte)alias[i]);return sum;
    }
    private static int WriteLongNameEntries(Span<byte> directory,int index,string name,string alias)
    {
        int count=checked((int)LongEntryCount(name));byte checksum=ShortNameChecksum(alias);
        for(int ordinal=count;ordinal>=1;ordinal--)
        {
            Span<byte> entry=directory.Slice(index++*32,32);entry.Fill(0xFF);entry[0]=(byte)ordinal;if(ordinal==count)entry[0]|=0x40;entry[11]=0x0F;entry[12]=0;entry[13]=checksum;entry[26]=entry[27]=0;
            int source=(ordinal-1)*13;WriteLfnChars(entry,name,source);
        }
        return index;
    }
    private static void WriteLfnChars(Span<byte> entry,string name,int source)
    {
        int[] offsets={1,3,5,7,9,14,16,18,20,22,24,28,30};
        for(int i=0;i<13;i++)
        {
            int pos=source+i;ushort value=pos<name.Length?name[pos]:(ushort)(pos==name.Length?0x0000:0xFFFF);BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(offsets[i],2),value);
        }
    }
    private static void WriteDirectoryEntry(Span<byte> directory,int index,string shortName,byte attributes,uint firstCluster,uint size)
    {
        if(shortName.Length!=11)throw new InvalidOperationException($"FAT short name must contain 11 characters: {shortName}");Span<byte> entry=directory.Slice(index*32,32);Encoding.ASCII.GetBytes(shortName).AsSpan().CopyTo(entry);entry[11]=attributes;
        const ushort date=((2026-1980)<<9)|(1<<5)|1;BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(16,2),date);BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(18,2),date);BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(20,2),(ushort)(firstCluster>>16));BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(24,2),date);BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(26,2),(ushort)firstCluster);BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(28,4),size);
    }

    private static void WriteProtectiveMbr(Stream image)
    {
        byte[] sector=new byte[SectorSize];int entry=446;sector[entry]=0;sector[entry+1]=0;sector[entry+2]=2;sector[entry+3]=0;sector[entry+4]=0xEE;sector[entry+5]=sector[entry+6]=sector[entry+7]=0xFF;BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(entry+8,4),1);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(entry+12,4),TotalSectors-1);sector[510]=0x55;sector[511]=0xAA;WriteSector(image,0,sector);
    }
    private static void WriteGuidPartitionTable(Stream image,uint lastUsableLba)
    {
        const uint count=128,size=128,primary=2;uint backupHeader=TotalSectors-1,backupEntries=TotalSectors-33;byte[] entries=new byte[checked((int)(count*size))];EfiSystemPartitionType.ToByteArray().CopyTo(entries,0);PartitionIdentifier.ToByteArray().CopyTo(entries,16);BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(32,8),PartitionStartLba);BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(40,8),lastUsableLba);Encoding.Unicode.GetBytes("Inu System").CopyTo(entries,56);uint crc=Crc32.Compute(entries);WriteAtLba(image,primary,entries);WriteAtLba(image,backupEntries,entries);WriteSector(image,1,CreateGptHeader(1,backupHeader,primary,lastUsableLba,crc));WriteSector(image,backupHeader,CreateGptHeader(backupHeader,1,backupEntries,lastUsableLba,crc));
    }
    private static byte[] CreateGptHeader(uint current,uint backup,uint entriesLba,uint lastUsable,uint entriesCrc)
    {
        byte[] sector=new byte[SectorSize];Encoding.ASCII.GetBytes("EFI PART").CopyTo(sector,0);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(8,4),0x00010000);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(12,4),92);BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(24,8),current);BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(32,8),backup);BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(40,8),34);BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(48,8),lastUsable);DiskIdentifier.ToByteArray().CopyTo(sector,56);BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(72,8),entriesLba);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(80,4),128);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(84,4),128);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(88,4),entriesCrc);BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(16,4),Crc32.Compute(sector.AsSpan(0,92)));return sector;
    }
    private static void WriteFatBootSectors(Stream image,uint partitionSectors,FatLayout layout)
    {
        byte[] boot=new byte[SectorSize];boot[0]=0xEB;boot[1]=0x58;boot[2]=0x90;Encoding.ASCII.GetBytes("INU").CopyTo(boot,3);BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(11,2),SectorSize);boot[13]=(byte)SectorsPerCluster;BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(14,2),(ushort)ReservedSectors);boot[16]=(byte)FatCount;boot[21]=0xF8;BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(24,2),63);BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(26,2),255);BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(28,4),PartitionStartLba);BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(32,4),partitionSectors);BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(36,4),layout.FatSectors);BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(44,4),RootCluster);BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(48,2),1);BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(50,2),6);boot[64]=0x80;boot[66]=0x29;BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(67,4),0x4E4F5627);Encoding.ASCII.GetBytes("INU   ").CopyTo(boot,71);Encoding.ASCII.GetBytes("FAT32   ").CopyTo(boot,82);boot[510]=0x55;boot[511]=0xAA;
        byte[] fsInfo=new byte[SectorSize];BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(0,4),0x41615252);BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(484,4),0x61417272);BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(488,4),0xFFFFFFFF);BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(492,4),0xFFFFFFFF);BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(508,4),0xAA550000);WriteVolumeSector(image,0,boot);WriteVolumeSector(image,1,fsInfo);WriteVolumeSector(image,6,boot);WriteVolumeSector(image,7,fsInfo);
    }
    private static void SetFatEntry(Span<byte> fat,uint cluster,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(fat.Slice(checked((int)(cluster*4)),4),value);
    private static void WriteVolumeSector(Stream image,uint relativeLba,ReadOnlySpan<byte> sector)=>WriteSector(image,PartitionStartLba+relativeLba,sector);
    private static void WriteSector(Stream image,uint lba,ReadOnlySpan<byte> sector){if(sector.Length!=SectorSize)throw new InvalidOperationException("Sector writes must contain exactly 512 bytes.");WriteAtLba(image,lba,sector);}
    private static void WriteAtLba(Stream image,uint lba,ReadOnlySpan<byte> data){image.Position=(long)lba*SectorSize;image.Write(data);}

    private readonly record struct FatDirectoryEntry(string Name,bool IsDirectory,uint FirstCluster,uint Length);

    private sealed class FatNode
    {
        internal FatNode(string name,string fullPath,bool directory,FatNode? parent){Name=name;FullPath=fullPath;IsDirectory=directory;Parent=parent;}
        internal string Name { get; } internal string FullPath { get; } internal bool IsDirectory { get; } internal FatNode? Parent { get; }
        internal List<FatNode> Children { get; }=new();internal long Length;internal uint FirstCluster,ClusterCount;
    }
    private readonly record struct FatLayout(uint FatSectors,uint ClusterCount,uint DataStartSector);
}

internal static class Crc32
{
    internal static uint Compute(ReadOnlySpan<byte> data){uint crc=0xFFFFFFFF;foreach(byte value in data){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^(0xEDB88320&(uint)-(int)(crc&1));}return ~crc;}
}
