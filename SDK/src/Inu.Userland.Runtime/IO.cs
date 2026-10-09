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

/// <summary><inu.api>Thrown when an existing directory cannot be found.</inu.api></summary>
public class DirectoryNotFoundException : IOException
{
    public DirectoryNotFoundException() { }
    public DirectoryNotFoundException(String message) : base(message) { }
}

internal static unsafe class PathTransport
{
    internal const Int32 MaximumPathBytes = 1536;

    internal static Boolean IsValid(String path)
    {
        if (path == null || path.Length == 0 || path.Length > MaximumPathBytes) return false;
        for (Int32 index = 0; index < path.Length; index++)
            if (path[index] > 0x7F) return false;
        return true;
    }

    internal static void Validate(String path)
    {
        if (path == null) throw new ArgumentNullException();
        if (path.Length == 0) throw new ArgumentException("Path must not be empty.");
        if (path.Length > MaximumPathBytes) throw new ArgumentException("Path exceeds the current user/kernel transport limit.");
        for (Int32 index = 0; index < path.Length; index++)
            if (path[index] > 0x7F) throw new ArgumentException("The initial Inu System.IO transport supports ASCII paths.");
    }

    internal static Boolean CopyAscii(String path, Byte* destination)
    {
        if (!IsValid(path) || destination == null) return false;
        for (Int32 index = 0; index < path.Length; index++) destination[index] = (Byte)path[index];
        return true;
    }

    internal static void CopyAsciiChecked(String path, Byte* destination)
    {
        if (!CopyAscii(path, destination)) throw new ArgumentException("Invalid path.");
    }
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
    private const UInt32 IoChunkBytes = 4096U;

    /// <summary><inu.api>Returns true when an existing file can be opened for reading.</inu.api></summary>
    public static Boolean Exists(String path)
    {
        if (!PathTransport.IsValid(path)) return false;
        Byte* ascii = stackalloc Byte[path.Length];
        if (!PathTransport.CopyAscii(path, ascii)) return false;
        Int64 opened = UserlandFile.OpenAscii(ascii, (UInt32)path.Length, ReadAccess);
        if (opened <= 0L) return false;
        UserlandFile.Close((UInt64)opened);
        return true;
    }

    /// <summary><inu.api>Reads an entire existing file into a managed byte array.</inu.api></summary>
    public static Byte[] ReadAllBytes(String path)
    {
        PathTransport.Validate(path);
        Byte* ascii = stackalloc Byte[path.Length];
        PathTransport.CopyAsciiChecked(path, ascii);
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
        PathTransport.Validate(path);
        Byte* ascii = stackalloc Byte[path.Length];
        PathTransport.CopyAsciiChecked(path, ascii);

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
        PathTransport.Validate(path);
        if (!Exists(path)) return;
        Byte* ascii = stackalloc Byte[path.Length];
        PathTransport.CopyAsciiChecked(path, ascii);
        if (UserlandFile.DeleteAscii(ascii, (UInt32)path.Length) < 0L)
            throw new IOException("The file could not be deleted.");
    }

}

/// <summary>
/// <inu.api>Freestanding .NET-compatible directory API for ordinary ring-3 Inu applications.</inu.api>
/// Directory paths are passed unchanged to the OS-selected filesystem policy. Inu does not impose a path separator,
/// case-sensitivity rule, fixed directory layout, or filesystem format.
/// </summary>
public static unsafe class Directory
{
    /// <summary><inu.api>Returns true when the supplied path names an existing directory.</inu.api></summary>
    public static Boolean Exists(String path)
    {
        if (!PathTransport.IsValid(path)) return false;
        Byte* ascii = stackalloc Byte[path.Length];
        if (!PathTransport.CopyAscii(path, ascii)) return false;
        Int64 opened = UserlandDirectory.OpenAscii(ascii, (UInt32)path.Length);
        if (opened <= 0L) return false;
        UserlandDirectory.Close((UInt64)opened);
        return true;
    }

    /// <summary><inu.api>Gets the current working directory of the calling process.</inu.api></summary>
    public static String GetCurrentDirectory()
    {
        Byte* buffer = stackalloc Byte[PathTransport.MaximumPathBytes];
        Int32 length = UserlandDirectory.GetCurrentDirectoryAscii(buffer, (UInt32)PathTransport.MaximumPathBytes);
        if (length <= 0) throw new IOException("The current working directory could not be obtained.");
        Byte[] bytes = new Byte[length];
        for (Int32 index = 0; index < length; index++) bytes[index] = buffer[index];
        return Encoding.ASCII.GetString(bytes);
    }

    /// <summary><inu.api>Sets the current working directory of the calling process after the kernel validates that the directory exists.</inu.api></summary>
    public static void SetCurrentDirectory(String path)
    {
        PathTransport.Validate(path);
        Byte* ascii = stackalloc Byte[path.Length];
        PathTransport.CopyAsciiChecked(path, ascii);
        if (UserlandDirectory.SetCurrentDirectoryAscii(ascii, (UInt32)path.Length) < 0L)
            throw new DirectoryNotFoundException("The requested working directory could not be selected.");
    }

    /// <summary><inu.api>Requests that the parent process adopt a new working directory. This is intended for shell-state commands such as cd; ordinary applications should use SetCurrentDirectory.</inu.api></summary>
    public static void SetParentCurrentDirectory(String path)
    {
        PathTransport.Validate(path);
        Byte* ascii = stackalloc Byte[path.Length];
        PathTransport.CopyAsciiChecked(path, ascii);
        if (UserlandDirectory.SetParentCurrentDirectoryAscii(ascii, (UInt32)path.Length) < 0L)
            throw new DirectoryNotFoundException("The requested parent working directory could not be selected.");
    }

    /// <summary><inu.api>Creates a directory when it does not already exist and returns information for that path.</inu.api></summary>
    public static DirectoryInfo CreateDirectory(String path)
    {
        PathTransport.Validate(path);
        if (!Exists(path))
        {
            Byte* ascii = stackalloc Byte[path.Length];
            PathTransport.CopyAsciiChecked(path, ascii);
            if (UserlandDirectory.CreateAscii(ascii, (UInt32)path.Length) < 0L)
                throw new IOException("The directory could not be created.");
        }
        return new DirectoryInfo(path);
    }

    /// <summary><inu.api>Returns the names of all entries in an existing directory.</inu.api></summary>
    public static String[] GetFileSystemEntries(String path)
    {
        PathTransport.Validate(path);Byte* ascii=stackalloc Byte[path.Length];PathTransport.CopyAsciiChecked(path,ascii);
        Int64 opened=UserlandDirectory.OpenAscii(ascii,(UInt32)path.Length);if(opened<=0L)throw new DirectoryNotFoundException("The requested directory could not be found.");
        List<String> entries=new List<String>();Byte* name=stackalloc Byte[512];
        for(;;)
        {
            Int32 length=UserlandDirectory.ReadAscii((UInt64)opened,name,512U);if(length<0){UserlandDirectory.Close((UInt64)opened);throw new IOException("The directory could not be read.");}if(length==0)break;
            Byte[] bytes=new Byte[length];for(Int32 i=0;i<length;i++)bytes[i]=name[i];String entry=Encoding.ASCII.GetString(bytes);
            entries.Add(FileSystemPaths.Combine(path,entry));
        }
        UserlandDirectory.Close((UInt64)opened);return entries.ToArray();
    }

    /// <summary><inu.api>Deletes an existing empty directory.</inu.api></summary>
    public static void Delete(String path)
    {
        PathTransport.Validate(path);
        if (!Exists(path)) throw new DirectoryNotFoundException("The requested directory could not be found.");
        Byte* ascii = stackalloc Byte[path.Length];
        PathTransport.CopyAsciiChecked(path, ascii);
        if (UserlandDirectory.DeleteAscii(ascii, (UInt32)path.Length) < 0L)
            throw new IOException("The directory could not be deleted. It may not be empty or the OS policy may deny removal.");
    }
}

/// <summary><inu.api>Minimal freestanding information object for one directory path.</inu.api></summary>
public sealed class DirectoryInfo
{
    private readonly String _fullName;

    /// <summary><inu.api>Creates a directory information object for the supplied OS-policy path.</inu.api></summary>
    public DirectoryInfo(String path)
    {
        PathTransport.Validate(path);
        _fullName = path;
    }

    /// <summary><inu.api>Gets the path supplied to this directory information object.</inu.api></summary>
    public String FullName => _fullName;

    /// <summary><inu.api>Reports whether the directory currently exists.</inu.api></summary>
    public Boolean Exists => Directory.Exists(_fullName);

    /// <summary><inu.api>Creates the directory if it does not already exist.</inu.api></summary>
    public void Create() => Directory.CreateDirectory(_fullName);

    /// <summary><inu.api>Deletes the directory. The initial Inu surface requires it to be empty.</inu.api></summary>
    public void Delete() => Directory.Delete(_fullName);
}
