using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LtoTape.CM;

public enum Particle
{
    Unknown,
    MP,
    BaFe,
}

public enum Substrate
{
    Unknown,
    PEN,
    SPALTAN,
}

public enum ServoBandID
{
    Unknown,
    Legacy_UDIM,
    Non_UDIM,
}

public class TapePhysicInfo
{
    public int NWraps = 0;
    public int SetsPerWrap = 0;
    public int TapDirLength = 0;
}

public class TapeInfo
{
    public static TapePhysicInfo GetPhysicInfoByGeneration(int generation)
    {
        return (generation) switch
        {
            1 => new TapePhysicInfo { NWraps = 48, SetsPerWrap = 5500, TapDirLength = 16 },
            5 => new TapePhysicInfo { NWraps = 80, SetsPerWrap = 7800, TapDirLength = 32 },
            6 => new TapePhysicInfo { NWraps = 136, SetsPerWrap = 7805, TapDirLength = 32 },
            _ => new TapePhysicInfo()
        };
    }
}

public class Manufacturer
{
    public string TapeVendor { get; set; } = "";
    public string CartridgeSN { get; set; } = "";
    public int CartridgeType { get; set; }
    public string Format { get; set; } = "";
    public int Gen { get; set; } = 0;
    public string MfgDate { get; set; } = "";
    public int TapeLength { get; set; }
    public int MediaCode { get; set; }

    public Particle ParticleType { get; set; } = Particle.Unknown;
    public Substrate SubstrateType { get; set; } = Substrate.Unknown;
    public ServoBandID ServoBandID { get; set; } = ServoBandID.Unknown;


    public bool IsCleanExpired { get; set; } = false;
    public bool IsCleaningTape { get => Gen == 0; }


    public void Parse(byte[] data, int startOffset)
    {
        TapeVendor = Encoding.ASCII.GetString(data, startOffset + 4, 8);
        CartridgeSN = Encoding.ASCII.GetString(data, startOffset + 12, 10);
        CartridgeType = BigEndianBitConverter.ToUInt16(data, startOffset + 22);
        MfgDate = Encoding.ASCII.GetString(data, startOffset + 24, 8);
        TapeLength = BigEndianBitConverter.ToUInt16(data, startOffset + 32);
        MediaCode = BigEndianBitConverter.ToUInt16(data, startOffset + 46);

        byte pageRevision = data[startOffset];
        byte particle = data[startOffset + 42];
        if (pageRevision >= 0x40)
        {
            ParticleType = (particle & 0x0f) > 0 ? Particle.BaFe : Particle.MP;
            SubstrateType = (particle & 0xf0) == 0x10 ? Substrate.SPALTAN : Substrate.PEN;
        }
        else
        {
            ParticleType = particle > 0 ? Particle.BaFe : Particle.MP;
        }
        if ((CartridgeType >> 15 & 0x01) == 1)
        {
            Format = "Cleaning Tape";
            Gen = 0;
        }
        else if (CartridgeType == 1)
        {
            Gen = 1;
            Format = $"LTO-{Gen}";
        }
        else if (CartridgeType == 2)
        {
            Gen = 2;
            Format = $"LTO-{Gen}";
        }
        else
        {
            Gen = (CartridgeType & 0xff) switch
            {
                4 => 3,
                8 => 4,
                16 => 5,
                32 => 6,
                64 => 7,
                128 => 8,
                129 => 9,
                _ => 0,
            };
            Format = $"LTO-{Gen}";
            if ((CartridgeType >> 13 & 0x01) == 1)
            {
                Format += " WORM";
            }
        }

    }
}
