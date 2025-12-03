using System;

namespace LtoTape;

public static class BigEndianBitConverter
{
    public static byte[] GetBytes(short value)
        => [(byte)(value >> 8), (byte)(value)];

    public static byte[] GetBytes(ushort value)
        => [(byte)(value >> 8), (byte)(value)];

    public static byte[] GetBytes(int value)
        =>
        [
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)(value)
        ];

    public static byte[] GetBytes(uint value)
        =>
        [
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)(value)
        ];

    public static byte[] GetBytes(long value)
        =>
        [
            (byte)(value >> 56),
            (byte)(value >> 48),
            (byte)(value >> 40),
            (byte)(value >> 32),
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)(value)
        ];

    public static byte[] GetBytes(ulong value)
        =>
        [
            (byte)(value >> 56),
            (byte)(value >> 48),
            (byte)(value >> 40),
            (byte)(value >> 32),
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)(value)
        ];



    public static short ToInt16(byte[] data, int startIndex = 0)
        => (short)((data[startIndex] << 8) | data[startIndex + 1]);

    public static ushort ToUInt16(byte[] data, int startIndex = 0)
        => (ushort)((data[startIndex] << 8) | data[startIndex + 1]);

    public static int ToInt32(byte[] data, int startIndex = 0)
        => (data[startIndex] << 24) |
           (data[startIndex + 1] << 16) |
           (data[startIndex + 2] << 8) |
            data[startIndex + 3];

    public static uint ToUInt32(byte[] data, int startIndex = 0)
        => ((uint)data[startIndex] << 24) |
           ((uint)data[startIndex + 1] << 16) |
           ((uint)data[startIndex + 2] << 8) |
            data[startIndex + 3];

    public static long ToInt64(byte[] data, int startIndex = 0)
        => ((long)data[startIndex] << 56) |
           ((long)data[startIndex + 1] << 48) |
           ((long)data[startIndex + 2] << 40) |
           ((long)data[startIndex + 3] << 32) |
           ((long)data[startIndex + 4] << 24) |
           ((long)data[startIndex + 5] << 16) |
           ((long)data[startIndex + 6] << 8) |
            data[startIndex + 7];

    public static ulong ToUInt64(byte[] data, int startIndex = 0)
        => ((ulong)data[startIndex] << 56) |
           ((ulong)data[startIndex + 1] << 48) |
           ((ulong)data[startIndex + 2] << 40) |
           ((ulong)data[startIndex + 3] << 32) |
           ((ulong)data[startIndex + 4] << 24) |
           ((ulong)data[startIndex + 5] << 16) |
           ((ulong)data[startIndex + 6] << 8) |
            data[startIndex + 7];
}
