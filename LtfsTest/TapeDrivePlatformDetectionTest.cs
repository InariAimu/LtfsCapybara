using System.Reflection;

public class TapeDrivePlatformDetectionTest
{
    [Fact]
    public void NativePlatformFlagsFollowActualRuntime()
    {
        var nativeMethodsType = typeof(TapeDrive.LTOTapeDrive).Assembly.GetType("TapeDrive.NativeMethods");

        Assert.NotNull(nativeMethodsType);

        var isWindowsProperty = nativeMethodsType!.GetProperty(
            "IsWindowsTapePlatform",
            BindingFlags.Public | BindingFlags.Static);
        var isLinuxProperty = nativeMethodsType.GetProperty(
            "IsLinuxTapePlatform",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(isWindowsProperty);
        Assert.NotNull(isLinuxProperty);

        var isWindows = Assert.IsType<bool>(isWindowsProperty!.GetValue(null));
        var isLinux = Assert.IsType<bool>(isLinuxProperty!.GetValue(null));

        Assert.Equal(OperatingSystem.IsWindows(), isWindows);
        Assert.Equal(OperatingSystem.IsLinux(), isLinux);
        Assert.False(isWindows && isLinux);
    }
}