// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Wdk.Storage.FileSystem;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.IO;
using Windows.Win32.System.Memory;

namespace Multitarget;

public unsafe abstract class FileFinderBase : CriticalFinalizerObject, IDisposable
{
    private readonly HANDLE _directoryHandle;
    private readonly void* _buffer;
    private readonly uint _bufferLength;
    private int _disposedValue;
    public string Directory { get; }

    protected Span<byte> Buffer => new((byte*)_buffer, (int)_bufferLength);

    protected FileFinderBase(string directory)
    {
        Directory = directory;

        fixed (char* p = directory)
        {
            HANDLE handle = PInvoke.CreateFile(
            (PCWSTR)p,
                (uint)FILE_ACCESS_RIGHTS.FILE_LIST_DIRECTORY,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                (SECURITY_ATTRIBUTES*)null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS,
                HANDLE.Null);

            if (handle.IsNull || handle == (void*)(-1))
            {
                WIN32_ERROR error = (WIN32_ERROR)Marshal.GetLastWin32Error();
                throw new Win32Exception((int)error);
            }

            _directoryHandle = handle;
        }

        _bufferLength = 1024;
        _buffer = PInvoke.HeapAlloc(PInvoke.GetProcessHeap(), HEAP_FLAGS.HEAP_NONE, _bufferLength);
    }

    protected unsafe T* FindNext<T>(ReadOnlySpan<char> pattern, bool restartScan) where T : unmanaged
    {
        FILE_INFORMATION_CLASS infoClass;

        if (typeof(T) == typeof(FILE_FULL_DIR_INFORMATION))
        {
            infoClass = FILE_INFORMATION_CLASS.FileFullDirectoryInformation;
        }
        else if (typeof(T) == typeof(FILE_NAMES_INFORMATION))
        {
            infoClass = FILE_INFORMATION_CLASS.FileNamesInformation;
        }
        else
        {
            throw new NotSupportedException();
        }

        fixed (char* p = pattern)
        {
            UNICODE_STRING ustring = new()
            {
                Length = (ushort)(pattern.Length * sizeof(char)),
                MaximumLength = (ushort)(pattern.Length * sizeof(char)),
                Buffer = (PWSTR)p
            };

            IO_STATUS_BLOCK statusBlock;

            // Unfortunately there is no way to make the FileName match case sensitive.
            NTSTATUS status = Windows.Wdk.PInvoke.NtQueryDirectoryFile(
                FileHandle: _directoryHandle,
                Event: HANDLE.Null,
                ApcRoutine: default,
                ApcContext: null,
                IoStatusBlock: &statusBlock,
                FileInformation: _buffer,
                Length: (uint)Buffer.Length,
                FileInformationClass: infoClass,
                ReturnSingleEntry: true,
                FileName: &ustring,
                RestartScan: restartScan);

            if (status == NTSTATUS.STATUS_SUCCESS)
            {
                Debug.Assert(statusBlock.Information != 0);
                return (T*)_buffer;
            }

            if (status == NTSTATUS.STATUS_NO_MORE_FILES
                || status == NTSTATUS.STATUS_NO_SUCH_FILE
                // #define STATUS_FILE_NOT_FOUND               0xE0031004
                || status == 0xE0031004)
            {
                // FILE_NOT_FOUND can occur when there are NO files in a volume root (usually there are hidden system files).
                return null;
            }

            int error = (int)PInvoke.RtlNtStatusToDosError(status);
            throw new Win32Exception(error);
        }
    }

    ~FileFinderBase() => Dispose();

    public void Dispose()
    {
        // As handles can be reused, guard against double disposal.
        if (Interlocked.Exchange(ref _disposedValue, 1) == 0)
        {
            if (!_directoryHandle.IsNull)
            {
                PInvoke.CloseHandle(_directoryHandle);
            }

            if (_buffer is not null)
            {
                PInvoke.HeapFree(PInvoke.GetProcessHeap(), HEAP_FLAGS.HEAP_NONE, _buffer);
            }
        }

        GC.SuppressFinalize(this);
    }
}
