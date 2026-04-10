
public class SizeUtil
{
    public static int MBtoMiB(int mb)
    {
        return (int)(mb * 1000000L / 1048576L);
    }

    public static int MiBtoMB(int mib)
    {
        return (int)(mib * 1048576L / 1000000L);
    }
}
