internal static class DataDiskImage
{
    private const int SectorSize = 512;
    private const uint TotalSectors = 524288; // 256 MiB

    /// <summary>
    /// Creates only the raw persistent backing store. Partitioning and filesystem formatting
    /// belong to the Inu kernel so the same storage path is exercised in QEMU and on hardware.
    /// Existing disks are preserved exactly as they are.
    /// </summary>
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
}
