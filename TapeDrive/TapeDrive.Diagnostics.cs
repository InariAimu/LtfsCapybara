namespace TapeDrive;

public partial class LTOTapeDrive
{
    public string GetLastError()
    {
        return ParseSenseData(Sense);
    }

    public static string ParseSenseData(byte[] sense)
    {
        string msg = string.Empty;
        bool fix = false;
        int addCode = 0;
        bool valid = (sense[0] >> 7) == 1;

        if ((sense[0] & 0x7f) == 0x70)
        {
            msg += "Current error: ";
            fix = true;
        }
        else
        {
            msg += "Deferred error: ";
            fix = true;
        }
        msg += "\n";

        if (fix)
        {
            if ((sense[2] >> 7) == 1)
                msg += "Filemark detected; ";

            if (((sense[2] >> 6) & 1) == 1)
                msg += "End of Media detected; ";

            if (((sense[2] >> 5) & 1) == 1)
                msg += "Blocklen mismatch; ";

            byte sensekey = (byte)(sense[2] & 0x0F);

            msg += "Sense Key: ";
            msg += "\n";
            msg += sensekey switch
            {
                0x0 => "No Sense",
                0x1 => "Recovered Error",
                0x2 => "Not Ready",
                0x3 => "Medium Error",
                0x4 => "Hardware Error",
                0x5 => "Illegal Request",
                0x6 => "Unit Attention",
                0x7 => "Data Protect",
                0x8 => "Blank Check",
                0x9 => "Vendor Specific",
                0xA => "Copy Aborted",
                0xB => "Aborted Command",
                0xC => "Equal",
                0xD => "Volume Overflow",
                0xE => "Miscompare",
                0xF => "Reserved",
                _ => string.Empty
            };

            msg += "\n";
            if (valid)
                msg += $"Info bytes: {sense[3]:x} {sense[4]:x} {sense[5]:x} {sense[6]:x}";

            byte addLen = sense[7];
            addCode = (sense[12] << 8) | sense[13];
            bool sksv = (sense[15] >> 7) == 1;
            bool cd = ((sense[15] >> 6) & 1) == 1;
            bool bpv = ((sense[15] >> 3) & 1) == 1;

            if (sksv)
            {
                if (sensekey == 5)
                {
                    msg += $"error byte = {(sense[16] << 8) | sense[17]:x} b = {sense[15] & 7:x}";
                }
                else if (sensekey == 3 || sensekey == 2)
                {
                    msg += $"progress = {(sense[16] << 8) | sense[17]:x}";
                }
            }
            else
            {
                msg += $"drive error code = {sense[16]:x} {sense[17]:x}";
            }

            if (((sense[21] >> 3) & 1) == 1)
            {
                msg += "clean is required; ";
            }
        }
        msg += "\n";
        msg += ParseAdditionalSenseCode((ushort)addCode);
        return msg;
    }

}