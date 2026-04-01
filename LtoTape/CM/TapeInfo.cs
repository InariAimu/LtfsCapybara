namespace LtoTape.CM;

public class TapePhysicInfo
{
    public int NWraps { get; set; } = 0;
    public int SetsPerWrap { get; set; } = 0;
    public int TapDirLength { get; set; } = 0;
    public int KBytesPerSet { get; set; } = 0;
    public int CMLength { get; set; } = 0;
    public int LifeInFVE { get; set; } = 0;
    public int LoadUnloadLife => 20000;
}

public class TapeInfo
{
    public static TapePhysicInfo GetPhysicInfo(int generation)
    {
        TapePhysicInfo info = new();
        info.NWraps = GetNWraps(generation);
        info.SetsPerWrap = GetSetsPerWrap(generation);
        info.TapDirLength = GetTapDirLength(generation);
        info.KBytesPerSet = GetKBytesPerSet(generation);
        info.CMLength = GetCMLength(generation);
        info.LifeInFVE = GetLifeInFVE(generation);

        return info;
    }

    public static int GetNWraps(int generation)
    {
        return (generation) switch
        {
            1 => 48,
            2 => 64,
            3 => 44,
            4 => 56,
            5 => 80,
            6 => 136,
            7 => 112,
            8 => 208,
            9 => 280,
            _ => 0
        };
    }

    public static int GetSetsPerWrap(int generation)
    {
        return (generation) switch
        {
            1 => 5500,
            2 => 8200,
            3 => 6000,
            4 => 9500,
            5 => 7800,
            6 => 7805,
            7 => 10950,
            8 => 11660,
            9 => 6770,
            _ => 0
        };
    }

    public static int GetTapDirLength(int generation)
    {
        return (generation) switch
        {
            1 => 16,
            5 => 32,
            6 => 32,
            _ => 0
        };
    }

    public static int GetKBytesPerSet(int generation)
    {
        return (generation) switch
        {
            1 or 2 => 404,
            3 => 1617,
            4 => 1590,
            5 or 6 => 2473,
            7 or 8 => 5032,
            9 => 9806,
            _ => 0
        };
    }

    public static int GetCMLength(int generation)
    {
        return (generation) switch
        {
            >= 0 and <= 3 => 4096, // 0 for cleaning tape
            4 or 5 => 8160,
            >= 6 and <= 8 => 16352,
            9 => 32736,
            _ => 0
        };
    }

    public static int GetLifeInFVE(int generation)
    {
        return (generation) switch
        {
            >= 1 and <= 5 => 260,
            6 or 7 => 130,
            8 => 75,
            9 => 55,
            _ => 0
        };
    }
}
