using GVFS.Common;
using GVFS.Platform.Windows;

namespace GVFS.PlatformLoader
{
    public static class GVFSPlatformLoader
    {
        public static void Initialize()
        {
            GVFSPlatform.Register(new WindowsPlatform());
            return;
        }
    }
}