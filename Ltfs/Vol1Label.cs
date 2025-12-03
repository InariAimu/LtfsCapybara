using System;
using System.Text.Encodings;
using System.Runtime.InteropServices;
using System.Text;

namespace Ltfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Pack=1 ensures no padding between fields
public struct Vol1Label
{
    // Offset 0, Length 3
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] LabelIdentifier;

    // Offset 3, Length 1
    [MarshalAs(UnmanagedType.U1)]
    public byte LabelNumber;

    // Offset 4, Length 6
    // Typically matches the physical cartridge label.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public byte[] VolumeIdentifier;

    // Offset 10, Length 1
    // Accessibility limited to conformance to LTFS standard.
    [MarshalAs(UnmanagedType.U1)]
    public byte VolumeAccessibility;

    // Offset 11, Length 13
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    public byte[] Reserved1;

    // Offset 24, Length 13
    // Value is left-aligned and padded with spaces to length.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    public byte[] ImplementationIdentifier;

    // Offset 37, Length 14
    // Any printable characters. Typically reflects some user specified content oriented identification. 
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
    public byte[] OwnerIdentifier;

    // Offset 51, Length 28
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
    public byte[] Reserved2;

    // Offset 79, Length 1
    [MarshalAs(UnmanagedType.U1)]
    public byte LabelStandardVersion;

    // Constructor to initialize with default values
    public Vol1Label(
        string volumeIdentifier = "<volume serial number>",
        string ownerIdentifier = "")
    {
        LabelIdentifier = Encoding.ASCII.GetBytes("VOL".PadRight(3, ' ')[..3]);
        LabelNumber = (byte)'1';
        VolumeIdentifier = Encoding.ASCII.GetBytes(volumeIdentifier.PadRight(6, ' ')[..6]);
        VolumeAccessibility = (byte)'L';
        Reserved1 = Encoding.ASCII.GetBytes(new string(' ', 13));
        ImplementationIdentifier = Encoding.ASCII.GetBytes("LTFS".PadRight(13, ' ')[..13]);
        OwnerIdentifier = Encoding.ASCII.GetBytes(ownerIdentifier.PadRight(14, ' ')[..14]);
        Reserved2 = Encoding.ASCII.GetBytes(new string(' ', 28));
        LabelStandardVersion = (byte)'4';
    }


    // Optional: Method to convert the struct to a byte array
    public static byte[] ToByteArray(Vol1Label vol1Label)
    {
        int size = Marshal.SizeOf(typeof(Vol1Label));
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(vol1Label, ptr, false);
            byte[] bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    // Optional: Static method to create a struct from a byte array
    public static Vol1Label FromByteArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length < Marshal.SizeOf(typeof(Vol1Label)))
            throw new ArgumentException("Byte array is too small.");

        Vol1Label result;
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            result = (Vol1Label)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(Vol1Label));
        }
        finally
        {
            handle.Free();
        }

        return result;
    }
}
