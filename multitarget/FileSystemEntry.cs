// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Wdk.Storage.FileSystem;

namespace Multitarget;

// This is a copy of FileSystemEntry from .NET. It isn't easy to reflect with ref types (to initialize), so we copy it here.

/// <summary>Provides a lower level view of <see cref="FileSystemInfo" /> to help process and filter find results.</summary>
public unsafe ref partial struct CustomFileSystemEntry
{
    /// <summary>Returns the full path for the find results, based on the initially provided path.</summary>
    /// <returns>A string representing the full path.</returns>
    public string ToSpecifiedFullPath()
    {
        // We want to provide the enumerated segment of the path appended to the originally specified path. This is
        // the behavior of the various Directory APIs that return a list of strings.
        //
        // RootDirectory has the final separator trimmed, OriginalRootDirectory does not. Our legacy behavior would
        // effectively account for this by appending subdirectory names as it recursed. As such we need to trim one
        // separator when combining with the relative path (Directory.Slice(RootDirectory.Length)).
        //
        //   Original  =>  Root   => Directory    => FileName => relativePath => Specified
        //   C:\foo        C:\foo    C:\foo          bar         ""              C:\foo\bar
        //   C:\foo\       C:\foo    C:\foo          bar         ""              C:\foo\bar
        //   C:\foo/       C:\foo    C:\foo          bar         ""              C:\foo/bar
        //   C:\foo\\      C:\foo    C:\foo          bar         ""              C:\foo\\bar
        //   C:\foo        C:\foo    C:\foo\bar      jar         "bar"           C:\foo\bar\jar
        //   C:\foo\       C:\foo    C:\foo\bar      jar         "bar"           C:\foo\bar\jar
        //   C:\foo/       C:\foo    C:\foo\bar      jar         "bar"           C:\foo/bar\jar


        // If we're at the top level directory the Directory and RootDirectory will be identical. As there are no
        // trailing slashes in play, once we're in a subdirectory, slicing off the root will leave us with an
        // initial separator. We need to trim that off if it exists, but it isn't needed if the original root
        // didn't have a separator. Join() would handle it if we did trim it, not doing so is an optimization.

        ReadOnlySpan<char> relativePath = Directory[RootDirectory.Length..];
        if (Path.EndsInDirectorySeparator(OriginalRootDirectory) && relativePath.Length > 0 && relativePath[0] == Path.DirectorySeparatorChar)
            relativePath = relativePath[1..];

        return Join(OriginalRootDirectory, relativePath, FileName);
    }

    internal static void Initialize(
        ref CustomFileSystemEntry entry,
        FILE_FULL_DIR_INFORMATION* info,
        ReadOnlySpan<char> directory,
        ReadOnlySpan<char> rootDirectory,
        ReadOnlySpan<char> originalRootDirectory)
    {
        entry._info = info;
        entry.Directory = directory;
        entry.RootDirectory = rootDirectory;
        entry.OriginalRootDirectory = originalRootDirectory;
    }

    internal FILE_FULL_DIR_INFORMATION* _info;

    /// <summary>Gets the full path of the directory this entry resides in.</summary>
    /// <value>The full path of this entry's directory.</value>
    public ReadOnlySpan<char> Directory { get; private set; }

    /// <summary>Gets the full path of the root directory used for the enumeration.</summary>
    /// <value>The root directory.</value>
    public ReadOnlySpan<char> RootDirectory { get; private set; }

    /// <summary>Gets the root directory for the enumeration as specified in the constructor.</summary>
    /// <value>The original root directory.</value>
    public ReadOnlySpan<char> OriginalRootDirectory { get; private set; }

    /// <summary>Gets the file name for this entry.</summary>
    /// <value>This entry's file name.</value>
    public ReadOnlySpan<char> FileName =>
        // _nameInfo is not null ? _nameInfo->FileName.AsSpan((int)_nameInfo->FileNameLength) :
        _info->FileName.AsSpan((int)_info->FileNameLength / sizeof(char));

    /// <summary>Gets the attributes for this entry.</summary>
    /// <value>The attributes for this entry.</value>
    public FileAttributes Attributes => (FileAttributes)_info->FileAttributes;

    /// <summary>Gets the length of the file, in bytes.</summary>
    /// <value>The file length in bytes.</value>
    public long Length => _info->EndOfFile;

    /// <summary>Gets the creation time for the entry or the oldest available time stamp if the operating system does not support creation time stamps.</summary>
    /// <value>The creation time for the entry.</value>
    public DateTimeOffset CreationTimeUtc => new(DateTime.FromFileTimeUtc(_info->CreationTime));

    /// <summary>Gets a datetime offset that represents the last access time in UTC.</summary>
    /// <value>The last access time in UTC.</value>
    public DateTimeOffset LastAccessTimeUtc => new(DateTime.FromFileTimeUtc(_info->LastAccessTime));

    /// <summary>Gets a datetime offset that represents the last write time in UTC.</summary>
    /// <value>The last write time in UTC.</value>
    public DateTimeOffset LastWriteTimeUtc => new(DateTime.FromFileTimeUtc(_info->LastWriteTime));

    /// <summary>Gets a value that indicates whether this entry is a directory.</summary>
    /// <value><see langword="true" /> if the entry is a directory; otherwise, <see langword="false" />.</value>
    public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;

    /// <summary>Gets a value that indicates whether the file has the hidden attribute.</summary>
    /// <value><see langword="true" /> if the file has the hidden attribute; otherwise, <see langword="false" />.</value>
    public bool IsHidden => (Attributes & FileAttributes.Hidden) != 0;

    /// <summary>Returns the full path of the find result.</summary>
    /// <returns>A string representing the full path.</returns>
    public string ToFullPath() => Path.Join(Directory, FileName);

    private static string Join(
        ReadOnlySpan<char> originalRootDirectory,
        ReadOnlySpan<char> relativePath,
        ReadOnlySpan<char> fileName)
    {
        if (originalRootDirectory.Length == 2 && originalRootDirectory[1] == Path.VolumeSeparatorChar)
        {
#if NETFRAMEWORK
            return $"{originalRootDirectory.ToString()}{Path.Join(relativePath, fileName)}";
#else
            return $"{originalRootDirectory}{Path.Join(relativePath, fileName.ToString())}";
#endif
        }

        return Path.Join(originalRootDirectory, relativePath, fileName);
    }
}
