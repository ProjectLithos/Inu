using System.Buffers.Binary;
using System.Text.Json;

internal static class DataDiskImage
{
    private const int SectorSize = 512;
    private const uint TotalSectors = 524288; // 256 MiB
    private const uint LegacyInuGptMarker = 0x47505431U;

    internal static bool WasLegacyAutoProvisionedByPreviousManifest(string manifestPath, string controller)
    {
        if (!File.Exists(manifestPath)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("dataDisks", out JsonElement dataDisks) || dataDisks.ValueKind != JsonValueKind.Array) return false;
            foreach (JsonElement disk in dataDisks.EnumerateArray())
            {
                if (!disk.TryGetProperty("controller", out JsonElement controllerElement) || !string.Equals(controllerElement.GetString(), controller, StringComparison.OrdinalIgnoreCase)) continue;
                uint partitions = disk.TryGetProperty("partitions", out JsonElement partitionsElement) && partitionsElement.TryGetUInt32(out uint value) ? value : 0U;
                string partitionedBy = disk.TryGetProperty("partitionedBy", out JsonElement partitionedByElement) ? partitionedByElement.GetString() ?? string.Empty : string.Empty;
                string formattedBy = disk.TryGetProperty("formattedBy", out JsonElement formattedByElement) ? formattedByElement.GetString() ?? string.Empty : string.Empty;
                return partitions == 2U && string.Equals(partitionedBy, "kernel", StringComparison.OrdinalIgnoreCase) && string.Equals(formattedBy, "kernel", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return false;
        }
        return false;
    }

    /// <summary>
    /// Creates only the raw persistent backing store. Partitioning and filesystem formatting
    /// belong to the OS policy. Existing disks are preserved exactly as they are, except that an
    /// empty legacy two-partition layout created automatically by older Inu Test OS releases can
    /// be safely returned to a blank raw disk during the one-time policy migration.
    /// </summary>
    internal static bool TryEnsure(string path, string controller, bool migrateLegacyEmptyLayout, out bool created, out bool resetLegacyEmptyLayout, out string error)
    {
        created = false;
        resetLegacyEmptyLayout = false;
        error = string.Empty;
        try
        {
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length != (long)TotalSectors * SectorSize)
                {
                    error = $"Existing {controller} data disk has an unexpected size and was not overwritten: {path}";
                    return false;
                }
                if (migrateLegacyEmptyLayout && !TryResetLegacyEmptyAutoLayout(path, out resetLegacyEmptyLayout, out error)) return false;
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using FileStream image = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            image.SetLength((long)TotalSectors * SectorSize);
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

    private static bool TryResetLegacyEmptyAutoLayout(string path, out bool reset, out string error)
    {
        reset = false;
        error = string.Empty;
        try
        {
            using FileStream image = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            byte[] sector = new byte[SectorSize];
            if (!ReadExact(image, SectorSize, sector)) return true;
            if (!sector.AsSpan(0, 8).SequenceEqual("EFI PART"u8)) return true;
            if (BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(68, 4)) != LegacyInuGptMarker) return true;

            byte[] entries = new byte[32 * SectorSize];
            if (!ReadExact(image, 2L * SectorSize, entries)) return true;
            var partitions = new List<(ulong FirstLba, ulong LastLba)>();
            for (int index = 0; index < 128; index++)
            {
                ReadOnlySpan<byte> entry = entries.AsSpan(index * 128, 128);
                bool typeIsZero = true;
                for (int b = 0; b < 16; b++) if (entry[b] != 0) { typeIsZero = false; break; }
                if (typeIsZero) continue;
                ulong first = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(32, 8));
                ulong last = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(40, 8));
                if (first == 0UL || last < first) return true;
                partitions.Add((first, last));
            }
            if (partitions.Count != 2) return true;
            foreach ((ulong firstLba, ulong lastLba) in partitions)
                if (!IsEmptyFat32Root(image, firstLba, lastLba)) return true;

            image.Position = 0;
            image.SetLength(0);
            image.SetLength((long)TotalSectors * SectorSize);
            image.Flush(true);
            reset = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsEmptyFat32Root(FileStream image, ulong firstLba, ulong lastLba)
    {
        if (firstLba > (ulong)long.MaxValue / SectorSize || lastLba < firstLba) return false;
        byte[] boot = new byte[SectorSize];
        if (!ReadExact(image, checked((long)firstLba * SectorSize), boot)) return false;
        ushort bytesPerSector = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(11, 2));
        byte sectorsPerCluster = boot[13];
        ushort reservedSectors = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(14, 2));
        byte fatCount = boot[16];
        uint sectorsPerFat = BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(36, 4));
        uint rootCluster = BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(44, 4));
        if (bytesPerSector != SectorSize || sectorsPerCluster == 0 || reservedSectors == 0 || fatCount == 0 || sectorsPerFat == 0 || rootCluster < 2U) return false;
        if (!boot.AsSpan(82, 5).SequenceEqual("FAT32"u8)) return false;
        ulong dataStart = firstLba + reservedSectors + (ulong)fatCount * sectorsPerFat;
        ulong rootStart = dataStart + (ulong)(rootCluster - 2U) * sectorsPerCluster;
        ulong rootEnd = rootStart + sectorsPerCluster - 1UL;
        if (rootEnd > lastLba) return false;
        int rootBytes = sectorsPerCluster * SectorSize;
        byte[] root = new byte[rootBytes];
        if (!ReadExact(image, checked((long)rootStart * SectorSize), root)) return false;
        for (int offset = 0; offset + 32 <= root.Length; offset += 32)
        {
            byte first = root[offset];
            if (first == 0x00) return true;
            if (first == 0xE5) continue;
            byte attributes = root[offset + 11];
            if ((attributes & 0x08) != 0) continue; // volume label metadata is not user content.
            return false;
        }
        return true;
    }

    private static bool ReadExact(FileStream image, long position, byte[] buffer)
    {
        image.Position = position;
        int read = 0;
        while (read < buffer.Length)
        {
            int count = image.Read(buffer, read, buffer.Length - read);
            if (count <= 0) return false;
            read += count;
        }
        return true;
    }
}
