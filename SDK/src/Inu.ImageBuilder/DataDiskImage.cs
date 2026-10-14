using System.Buffers.Binary;
using System.Text;

internal static class DataDiskImage
{
    private const int SectorSize = 512;
    private const uint TotalSectors = 524288; // 256 MiB
    private const uint Partition1Start = 2048;
    private const uint PartitionSectors = 245760; // 120 MiB each
    private const uint Partition1End = Partition1Start + PartitionSectors - 1;
    private const uint Partition2Start = 249856;
    private const uint Partition2End = Partition2Start + PartitionSectors - 1;
    private const uint ReservedSectors = 32;
    private const uint FatCount = 2;
    private const uint RootCluster = 2;
    private static readonly Guid BasicDataPartitionType = new("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7");

    internal static bool TryEnsure(string path, string controller, out bool created, out string error)
    {
        created = false; error = string.Empty;
        try
        {
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length != (long)TotalSectors * SectorSize)
                {
                    error = $"Existing {controller} data disk has an unexpected size and was not overwritten: {path}";
                    return false;
                }
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using FileStream image = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            image.SetLength((long)TotalSectors * SectorSize);
            WriteProtectiveMbr(image);
            WriteGpt(image, controller);
            WriteFat32(image, Partition1Start, PartitionSectors, "INU DATA1", true);
            WriteFat32(image, Partition2Start, PartitionSectors, "INU DATA2", false);
            image.Flush(true);
            created = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void WriteProtectiveMbr(Stream image)
    {
        byte[] sector = new byte[SectorSize]; int entry = 446;
        sector[entry + 4] = 0xEE;
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(entry + 8, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(entry + 12, 4), TotalSectors - 1);
        sector[510] = 0x55; sector[511] = 0xAA;
        WriteSector(image, 0, sector);
    }

    private static void WriteGpt(Stream image, string controller)
    {
        const uint entryCount = 128, entrySize = 128, primaryEntriesLba = 2;
        uint backupHeaderLba = TotalSectors - 1, backupEntriesLba = TotalSectors - 33;
        byte[] entries = new byte[checked((int)(entryCount * entrySize))];
        WritePartitionEntry(entries.AsSpan(0, (int)entrySize), 1, Partition1Start, Partition1End, $"Inu {controller} Data 1");
        WritePartitionEntry(entries.AsSpan((int)entrySize, (int)entrySize), 2, Partition2Start, Partition2End, $"Inu {controller} Data 2");
        uint entriesCrc = Crc32.Compute(entries);
        WriteAtLba(image, primaryEntriesLba, entries);
        WriteAtLba(image, backupEntriesLba, entries);
        Guid diskId = DeterministicGuid(controller + ":disk");
        WriteSector(image, 1, CreateGptHeader(1, backupHeaderLba, primaryEntriesLba, entriesCrc, diskId));
        WriteSector(image, backupHeaderLba, CreateGptHeader(backupHeaderLba, 1, backupEntriesLba, entriesCrc, diskId));
    }

    private static void WritePartitionEntry(Span<byte> entry, int index, uint first, uint last, string name)
    {
        BasicDataPartitionType.ToByteArray().CopyTo(entry);
        DeterministicGuid(name + ":" + index).ToByteArray().CopyTo(entry.Slice(16));
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(32, 8), first);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(40, 8), last);
        byte[] label = Encoding.Unicode.GetBytes(name.Length > 36 ? name[..36] : name);
        label.AsSpan().CopyTo(entry.Slice(56, Math.Min(label.Length, 72)));
    }

    private static byte[] CreateGptHeader(uint current, uint backup, uint entriesLba, uint entriesCrc, Guid diskId)
    {
        byte[] sector = new byte[SectorSize];
        Encoding.ASCII.GetBytes("EFI PART").CopyTo(sector, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(8, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(12, 4), 92);
        BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(24, 8), current);
        BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(32, 8), backup);
        BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(40, 8), 34);
        BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(48, 8), TotalSectors - 34);
        diskId.ToByteArray().CopyTo(sector, 56);
        BinaryPrimitives.WriteUInt64LittleEndian(sector.AsSpan(72, 8), entriesLba);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(80, 4), 128);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(84, 4), 128);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(88, 4), entriesCrc);
        BinaryPrimitives.WriteUInt32LittleEndian(sector.AsSpan(16, 4), Crc32.Compute(sector.AsSpan(0, 92)));
        return sector;
    }

    private static void WriteFat32(Stream image, uint startLba, uint totalSectors, string volumeLabel, bool seedDirectories)
    {
        uint fatSectors = CalculateFatSectors(totalSectors);
        uint dataStart = ReservedSectors + FatCount * fatSectors;
        byte[] boot = new byte[SectorSize];
        boot[0] = 0xEB; boot[1] = 0x58; boot[2] = 0x90;
        Encoding.ASCII.GetBytes("INUDATA ").CopyTo(boot, 3);
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(11, 2), (ushort)SectorSize);
        boot[13] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(14, 2), (ushort)ReservedSectors);
        boot[16] = (byte)FatCount; boot[21] = 0xF8;
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(24, 2), 63);
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(26, 2), 255);
        BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(28, 4), startLba);
        BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(32, 4), totalSectors);
        BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(36, 4), fatSectors);
        BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(44, 4), RootCluster);
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(48, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(boot.AsSpan(50, 2), 6);
        boot[64] = 0x80; boot[66] = 0x29;
        BinaryPrimitives.WriteUInt32LittleEndian(boot.AsSpan(67, 4), startLba ^ totalSectors ^ 0x494E5500U);
        string label = volumeLabel.ToUpperInvariant().PadRight(11).Substring(0, 11);
        Encoding.ASCII.GetBytes(label).CopyTo(boot, 71);
        Encoding.ASCII.GetBytes("FAT32   ").CopyTo(boot, 82);
        boot[510] = 0x55; boot[511] = 0xAA;
        byte[] fsInfo = new byte[SectorSize];
        BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(0, 4), 0x41615252);
        BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(484, 4), 0x61417272);
        BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(488, 4), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(492, 4), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(fsInfo.AsSpan(508, 4), 0xAA550000);
        WriteSector(image, startLba, boot); WriteSector(image, startLba + 1, fsInfo);
        WriteSector(image, startLba + 6, boot); WriteSector(image, startLba + 7, fsInfo);

        byte[] fat = new byte[checked((int)(fatSectors * SectorSize))];
        SetFat(fat, 0, 0x0FFFFFF8); SetFat(fat, 1, 0x0FFFFFFF); SetFat(fat, 2, 0x0FFFFFFF);
        if (seedDirectories) { SetFat(fat, 3, 0x0FFFFFFF); SetFat(fat, 4, 0x0FFFFFFF); SetFat(fat, 5, 0x0FFFFFFF); }
        WriteAtLba(image, startLba + ReservedSectors, fat);
        WriteAtLba(image, startLba + ReservedSectors + fatSectors, fat);

        byte[] root = new byte[SectorSize];
        if (seedDirectories)
        {
            WriteDirectoryEntry(root.AsSpan(0, 32), "USERS      ", 3);
            WriteDirectoryEntry(root.AsSpan(32, 32), "WORK       ", 4);
            WriteDirectoryEntry(root.AsSpan(64, 32), "TEMP       ", 5);
        }
        WriteSector(image, startLba + dataStart, root);
        if (seedDirectories)
        {
            WriteEmptyDirectory(image, startLba + dataStart + 1, 3);
            WriteEmptyDirectory(image, startLba + dataStart + 2, 4);
            WriteEmptyDirectory(image, startLba + dataStart + 3, 5);
        }
    }

    private static uint CalculateFatSectors(uint totalSectors)
    {
        uint fat = 1;
        for (int i = 0; i < 16; i++)
        {
            uint clusters = (totalSectors - ReservedSectors - FatCount * fat);
            uint needed = (uint)(((ulong)(clusters + 2) * 4 + SectorSize - 1) / SectorSize);
            if (needed == fat) return fat;
            fat = needed;
        }
        return fat;
    }

    private static void WriteDirectoryEntry(Span<byte> entry, string shortName, uint cluster)
    {
        Encoding.ASCII.GetBytes(shortName).CopyTo(entry);
        entry[11] = 0x10;
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(20, 2), (ushort)(cluster >> 16));
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(26, 2), (ushort)cluster);
    }

    private static void WriteEmptyDirectory(Stream image, uint lba, uint self)
    {
        byte[] sector = new byte[SectorSize];
        WriteDirectoryEntry(sector.AsSpan(0, 32), ".          ", self);
        WriteDirectoryEntry(sector.AsSpan(32, 32), "..         ", RootCluster);
        WriteSector(image, lba, sector);
    }

    private static void SetFat(Span<byte> fat, uint cluster, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(fat.Slice((int)(cluster * 4), 4), value);
    private static void WriteSector(Stream image, uint lba, ReadOnlySpan<byte> data) { if (data.Length != SectorSize) throw new InvalidOperationException("Sector write must be 512 bytes."); WriteAtLba(image, lba, data); }
    private static void WriteAtLba(Stream image, uint lba, ReadOnlySpan<byte> data) { image.Position = (long)lba * SectorSize; image.Write(data); }
    private static Guid DeterministicGuid(string value)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value));
        byte[] guid = bytes[..16]; guid[7] = (byte)((guid[7] & 0x0F) | 0x40); guid[8] = (byte)((guid[8] & 0x3F) | 0x80); return new Guid(guid);
    }
}
