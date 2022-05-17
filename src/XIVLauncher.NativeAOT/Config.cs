using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.NativeAOT.Configuration;

public class LauncherConfig
{
    public string? AcceptLanguage { get; set; }

    public DirectoryInfo? GamePath { get; set; }

    public DirectoryInfo? GameConfigPath { get; set; }

    public string? AdditionalArgs { get; set; }

    public ClientLanguage? ClientLanguage { get; set; }

    public bool? IsUidCacheEnabled { get; set; }

    public bool? IsDx11 { get; set; }

    public bool? IsEncryptArgs { get; set; }

    public bool? IsFt { get; set; }

    #region Patching

    public DirectoryInfo? PatchPath { get; set; }

    public bool? KeepPatches { get; set; }

    public AcquisitionMethod? PatchAcquisitionMethod { get; set; }

    public long PatchSpeedLimit { get; set; }

    #endregion

    #region Dalamud

    public bool? DalamudEnabled { get; set; }

    public DalamudLoadMethod? DalamudLoadMethod { get; set; }

    public int DalamudLoadDelay { get; set; }

    #endregion
}