using System;
using System.Collections.Generic;
using System.Text;
using Inu.Userland.Runtime;

namespace System.IO;

/// <summary><inu.api>Base exception for freestanding Inu file I/O failures.</inu.api></summary>
public class IOException : SystemException
{
    public IOException() { }
    public IOException(String message) : base(message) { }
}

/// <summary><inu.api>Thrown when an existing file cannot be opened for reading.</inu.api></summary>
public class FileNotFoundException : IOException
{
    public FileNotFoundException() { }
    public FileNotFoundException(String message) : base(message) { }
}

/// <summary>
/// <inu.api>Freestanding .NET-compatible whole-file API for ordinary ring-3 Inu applications.</inu.api>
/// Paths are passed unchanged to the OS-selected filesystem policy; this layer does not impose a separator,
/// case-sensitivity rule, fixed directory layout, or filesystem format.
/// </summary>
public static unsafe class File
{
    private const UInt32 ReadAccess = 1U;
    private const UInt32 WriteAccess = 2U;
    private const Int32 MaximumTransportPathBytes = 1536;
    private const UInt32 IoChunkBytes = 4096U;

    /// <summary><inu.api>Returns true when an existing file can be opened for reading.</inu.api></summary>
    public static Boolean Exists(String path)
    {
        if (!TryValidatePath(path)) return false;
        Byte* ascii = stackalloc Byte[path.Length];
        if (!CopyAsciiPath(path, ascii)) return false;
        Int64 opened = UserlandFile.OpenAscii(ascii, (UInt32)path.Length, ReadAccess);
        if (opened <= 0L) return false;
        UserlandFile.Close((UInt64)opened);
        return true;
    }

    /// <summary><inu.api>Reads an entire existing file into a managed byte array.</inu.api></summary>
    public static Byte[] ReadAllBytes(String path)
    {
        ValidatePath(path);
        Byte* ascii = stackalloc Byte[path.Length];
        CopyAsciiPathChecked(path, ascii);
        Int64 opened = UserlandFile.OpenAscii(ascii, (UInt32)path.Length, ReadAccess);
        if (opened <= 0L) throw new FileNotFoundException("The requested file could not be opened.");

        UInt64 handle = (UInt64)opened;
        List<Byte> bytes = new List<Byte>();
        Byte* chunk = stackalloc Byte[(Int32)IoChunkBytes];

        for (;;)
        {
            Int64 read = UserlandFile.Read(handle, chunk, IoChunkBytes);
            if (read < 0L)
            {
                UserlandFile.Close(handle);
                throw new IOException("The file could not be read.");
            }
            if (read == 0L) break;
            for (Int32 index = 0; index < (Int32)read; index++) bytes.Add(chunk[index]);
        }

        if (UserlandFile.Close(handle) < 0L) throw new IOException("The file could not be closed.");
        return bytes.ToArray();
    }

    /// <summary><inu.api>Reads an entire file as UTF-8 text.</inu.api></summary>
    public static String ReadAllText(String path) => Encoding.UTF8.GetString(ReadAllBytes(path));

    /// <summary><inu.api>Creates or replaces a file with the supplied bytes.</inu.api></summary>
    public static void WriteAllBytes(String path, Byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException();
        ValidatePath(path);
        Byte* ascii = stackalloc Byte[path.Length];
        CopyAsciiPathChecked(path, ascii);

        if (UserlandFile.CreateAscii(ascii, (UInt32)path.Length, true) < 0L)
            throw new IOException("The file could not be created or replaced.");

        Int64 opened = UserlandFile.OpenAscii(ascii, (UInt32)path.Length, WriteAccess);
        if (opened <= 0L) throw new IOException("The file could not be opened for writing.");
        UInt64 handle = (UInt64)opened;

        Int32 offset = 0;
        Byte* chunk = stackalloc Byte[(Int32)IoChunkBytes];
        while (offset < bytes.Length)
        {
            UInt32 count = (UInt32)(bytes.Length - offset);
            if (count > IoChunkBytes) count = IoChunkBytes;
            for (UInt32 index = 0; index < count; index++) chunk[index] = bytes[offset + (Int32)index];
            Int64 written = UserlandFile.Write(handle, chunk, count);
            if (written != (Int64)count)
            {
                UserlandFile.Close(handle);
                throw new IOException("The complete file could not be written.");
            }
            offset += (Int32)count;
        }

        if (UserlandFile.Close(handle) < 0L) throw new IOException("The file could not be closed.");
    }

    /// <summary><inu.api>Creates or replaces a file with UTF-8 text.</inu.api></summary>
    public static void WriteAllText(String path, String contents)
    {
        WriteAllBytes(path, Encoding.UTF8.GetBytes(contents ?? ""));
    }

    /// <summary><inu.api>Deletes a file if it exists.</inu.api></summary>
    public static void Delete(String path)
    {
        ValidatePath(path);
        if (!Exists(path)) return;
        Byte* ascii = stackalloc Byte[path.Length];
        CopyAsciiPathChecked(path, ascii);
        if (UserlandFile.DeleteAscii(ascii, (UInt32)path.Length) < 0L)
            throw new IOException("The file could not be deleted.");
    }

    private static Boolean TryValidatePath(String path)
    {
        if (path == null || path.Length == 0 || path.Length > MaximumTransportPathBytes) return false;
        for (Int32 index = 0; index < path.Length; index++)
            if (path[index] > 0x7F) return false;
        return true;
    }

    private static void ValidatePath(String path)
    {
        if (path == null) throw new ArgumentNullException();
        if (path.Length == 0) throw new ArgumentException("Path must not be empty.");
        if (path.Length > MaximumTransportPathBytes) throw new ArgumentException("Path exceeds the current user/kernel transport limit.");
        for (Int32 index = 0; index < path.Length; index++)
            if (path[index] > 0x7F) throw new ArgumentException("The initial Inu System.IO transport supports ASCII paths.");
    }

    private static Boolean CopyAsciiPath(String path, Byte* destination)
    {
        if (!TryValidatePath(path) || destination == null) return false;
        for (Int32 index = 0; index < path.Length; index++) destination[index] = (Byte)path[index];
        return true;
    }

    private static void CopyAsciiPathChecked(String path, Byte* destination)
    {
        if (!CopyAsciiPath(path, destination)) throw new ArgumentException("Invalid path.");
    }
}
