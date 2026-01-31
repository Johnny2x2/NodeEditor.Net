namespace NodeEditor.Blazor.Services;

public static class PlatformGuard
{
    public static bool IsPluginLoadingSupported()
    {
#if IOS
        return false;
#else
        return true;
#endif
    }

    public static void ThrowIfPluginLoadingUnsupported()
    {
        if (!IsPluginLoadingSupported())
        {
            throw new PlatformNotSupportedException("Plugin loading is not supported on iOS.");
        }
    }
}
