// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Windows.Wdk.Storage.FileSystem;

namespace Multitarget;

public unsafe abstract class FileFinder<TResult> : FileFinderBase
{
    public FileFinder(string directory) : base(directory) { }

    public bool FindMatch(
        ReadOnlySpan<char> pattern,
        [NotNullWhen(true)] out TResult? result) =>
        FindMatch(pattern, restartScan: false, out result);

    public bool FindMatch(
        ReadOnlySpan<char> pattern,
        bool restartScan,
        [NotNullWhen(true)] out TResult? result)
    {
        FILE_FULL_DIR_INFORMATION* info = FindNext<FILE_FULL_DIR_INFORMATION>(pattern, restartScan);
        if (info is null)
        {
            result = default;
            return false;
        }

        CustomFileSystemEntry entry = new();
        CustomFileSystemEntry.Initialize(ref entry, info, Directory.AsSpan(), Directory.AsSpan(), Directory.AsSpan());
        result = TransformResult(ref entry);
        return result is not null;
    }

    protected abstract TResult TransformResult(ref CustomFileSystemEntry entry);
}
