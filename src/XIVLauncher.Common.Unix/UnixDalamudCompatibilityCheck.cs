using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Unix;

public class UnixDalamudCompatibilityCheck : IDalamudCompatibilityCheck
{
    public void EnsureCompatibility()
    {
        //Dalamud will work with wines built-in vcrun, so no need to check that or the architecture
    }
}