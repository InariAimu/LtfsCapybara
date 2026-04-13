using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace TapeDrive;

internal static class NativeMethods
{

    public static bool IsWindowsTapePlatform
    {
        get
        {
#if TAPEDRIVE_WINDOWS
            return true;
#elif TAPEDRIVE_LINUX
            return false;
#else
            return OperatingSystem.IsWindows();
#endif
        }
    }

    public static bool IsLinuxTapePlatform
    {
        get
        {
#if TAPEDRIVE_LINUX
            return true;
#elif TAPEDRIVE_WINDOWS
            return false;
#else
            return OperatingSystem.IsLinux();
#endif
        }
    }

    public const uint LinuxSgIo = 0x2285;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFileWindows(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);


    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControlWindows(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        ref int lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoctlLinux(
        SafeFileHandle fd,
        nuint request,
        IntPtr argp);

    public static SafeFileHandle OpenTapeDevice(string devicePath)
    {
        if (IsWindowsTapePlatform)
        {
            return CreateFileWindows(devicePath,
                0xC0000000,
                3,
                IntPtr.Zero,
                3,
                0,
                IntPtr.Zero);
        }

        if (IsLinuxTapePlatform)
        {
            return File.OpenHandle(devicePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        throw new PlatformNotSupportedException("TapeDrive only supports Windows and Linux tape I/O backends.");
    }

    public static bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        ref int lpBytesReturned,
        IntPtr lpOverlapped)
    {
        if (!IsWindowsTapePlatform)
            throw new PlatformNotSupportedException("DeviceIoControl is only available for the Windows tape backend.");

        return DeviceIoControlWindows(
            hDevice,
            dwIoControlCode,
            lpInBuffer,
            nInBufferSize,
            lpOutBuffer,
            nOutBufferSize,
            ref lpBytesReturned,
            lpOverlapped);
    }

    public static int Ioctl(
        SafeFileHandle hDevice,
        uint request,
        IntPtr argp)
    {
        if (!IsLinuxTapePlatform)
            throw new PlatformNotSupportedException("ioctl is only available for the Linux tape backend.");

        return IoctlLinux(hDevice, request, argp);
    }
}
