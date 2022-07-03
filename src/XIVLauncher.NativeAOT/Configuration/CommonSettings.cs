using XIVLauncher.Common;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.NativeAOT.Configuration;

public class CommonSettings : ISettings
{
    private readonly LauncherConfig config;

    public CommonSettings()
    {
        this.config = NativeLibrary.Program.Config!;
    }

    private static CommonSettings? instance;

    public static CommonSettings? Instance
    {
        get
        {
            instance ??= new CommonSettings();
            return instance;
        }
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