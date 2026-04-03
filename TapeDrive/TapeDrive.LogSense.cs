using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;
using LtoTape;
using System.Linq;
using System.Globalization;
using TapeDrive.SCSICommands;
using TapeDrive.Utils;
using TapeDrive.SCSICommands.LogSensePages;

namespace TapeDrive;

public partial class LTOTapeDrive : IDisposable
{
    public Dictionary<byte, byte[]> Pages = new();

    public LogSensePageHeader GetLogSensePageHeader(byte pageCode, byte pageControl = 0x01)
    {
        byte[] r = ScsiRead(StructParser.ToBytes(new LogSense()
        {
            PageCode = pageCode,
            PC = pageControl,
            AllocationLength = 4
        }), 4);

        return StructParser.Parse<LogSensePageHeader>(r);
    }

    public byte[] LogSense(byte pageCode, byte pageControl = 0x01)
    {
        byte[] header = ScsiRead([0x4d, 0, (byte)((pageControl << 6) | pageCode), 0, 0, 0, 0, 0, 4, 0], 4);

        if (header.Length < 4)
            return [0, 0, 0, 0];

        int pageLength = (header[2] << 8) | header[3];
        if (pageLength == 0)
            return [];

        pageLength += 4; // include header length

        return ScsiRead([
            0x4d, 0,
            (byte)((pageControl << 6) | pageCode),
            0, 0, 0, 0,
            (byte)((pageLength >> 8) & 0xff), (byte)(pageLength & 0xff), 0], pageLength + 4);
    }

    public byte[] ReadWERLPage()
    {
        byte[] header = ScsiRead(
            [0x1c, 0x01, 0x88, 0, 0x04, 0], 4);

        if (header.Length != 4)
            return [];

        int pageLength = (header[2] << 8) | header[3];
        if (pageLength == 0)
            return [];

        pageLength += 4; // include header length

        byte[] pageData = ScsiRead(
            [0x1c, 0x01, 0x88, (byte)((pageLength >> 8) & 0xff), (byte)(pageLength & 0xff), 0], pageLength);

        return pageData;
    }

    public int[] LastC1Err = new int[32];
    public int[] LastNoCCPs = new int[32];
    public float[] LastChannelErrRate = new float[32];

    public string GetReadableChannelErrorRates()
    {
        StringBuilder sb = new();
        for (int i = 0; i < LastChannelErrRate.Length / 2; i++)
        {
            sb.Append($"{LastChannelErrRate[i]:F2} ");
        }
        return sb.ToString();
    }

    public double ReadErrorRate()
    {
        double result = double.NegativeInfinity;

        byte[] werlPage = ReadWERLPage();
        string[] werlData =
            Encoding.ASCII.GetString(werlPage, 4, werlPage.Length - 4)
                .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);

        List<double> allResult = new();
        bool brokenChannel = false;

        for (int ch = 4; ch < werlData.Length; ch += 5)
        {
            int channel = (ch - 4) / 5;
            int c1Err = int.Parse(werlData[ch + 0], NumberStyles.HexNumber);
            //int c1CWErr = int.Parse(werlData[ch + 1], NumberStyles.HexNumber);
            //int headerErr = int.Parse(werlData[ch + 2], NumberStyles.HexNumber);
            //int wrPassErr = int.Parse(werlData[ch + 3], NumberStyles.HexNumber);
            int noCCPs = int.Parse(werlData[ch + 4], NumberStyles.HexNumber);

            if (noCCPs - LastNoCCPs[channel] > 0)
            {
                double errRateLog = 0;
                try
                {
                    errRateLog = Math.Log10((double)(c1Err - LastC1Err[channel]) / (noCCPs - LastNoCCPs[channel]) / 2 / 1920);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                allResult.Add(errRateLog);
                if (errRateLog < 0)
                {
                    result = Math.Max(result, errRateLog);
                }

                LastChannelErrRate[channel] = (float)errRateLog;
            }
            else
            {
                brokenChannel = true;
                LastChannelErrRate[channel] = -2.98f;
            }

            LastC1Err[channel] = c1Err;
            LastNoCCPs[channel] = noCCPs;

            if (brokenChannel)
                result = Math.Max(result, -2.98);
        }

        if (result < -10)
            result = 0;

        return result;
    }
}
