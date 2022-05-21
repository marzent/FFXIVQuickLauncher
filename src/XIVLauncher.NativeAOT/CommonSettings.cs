using XIVLauncher.Common;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.NativeAOT.Configuration;

public class CommonSettings : ISettings
{
    private readonly LauncherConfig config;

    public CommonSettings(LauncherConfig config)
    {
        this.config = config;
    }

    public string AcceptLanguage => this.config.AcceptLanguage!;
    public ClientLanguage? ClientLanguage => this.config.ClientLanguage;
    public bool? KeepPatches => this.config.KeepPatches;
    public DirectoryInfo PatchPath => this.config.PatchPath!;
    public DirectoryInfo GamePath => this.config.GamePath!;
    public AcquisitionMethod? PatchAcquisitionMethod => AcquisitionMethod.NetDownloader;
    public long SpeedLimitBytes => this.config.PatchSpeedLimit;
    public int DalamudInjectionDelayMs => this.config.DalamudLoadDelay;
}