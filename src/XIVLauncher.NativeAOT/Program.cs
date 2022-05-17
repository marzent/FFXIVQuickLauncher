using System.Runtime.InteropServices;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.NativeAOT.Configuration;

namespace NativeLibrary;

public class Program
{
    public static LauncherConfig? Config { get; private set; }

    [UnmanagedCallersOnly(EntryPoint = "generateAcceptLanguage")]
    public static IntPtr GenerateAcceptLanguage()
    {
        // Needs to be freed by the caller
        return (IntPtr)Marshal.StringToHGlobalAnsi(Util.GenerateAcceptLanguage());
    }

    [UnmanagedCallersOnly(EntryPoint = "loadConfig")]
    public static void LoadConfig(IntPtr acceptLanguage, IntPtr gamePath, IntPtr gameConfigPath, byte clientLanguage, bool isDx11, bool isEncryptArgs, bool isFt, IntPtr patchPath, byte patchAcquisitionMethod, Int64 patchSpeedLimit, bool dalamudEnabled, byte dalamudLoadMethod)
    {
        Config = new LauncherConfig
        {
            AcceptLanguage = Marshal.PtrToStringAnsi(acceptLanguage),

            GamePath = new DirectoryInfo(Marshal.PtrToStringAnsi(gamePath)!),
            GameConfigPath = new DirectoryInfo(Marshal.PtrToStringAnsi(gameConfigPath)!),
            ClientLanguage = (ClientLanguage)clientLanguage,

            IsDx11 = isDx11,
            IsEncryptArgs = isEncryptArgs,
            IsFt = isFt,

            PatchPath = new DirectoryInfo(Marshal.PtrToStringAnsi(patchPath)!),
            PatchAcquisitionMethod = (AcquisitionMethod)patchAcquisitionMethod,
            PatchSpeedLimit = patchSpeedLimit,

            DalamudEnabled = dalamudEnabled,
            DalamudLoadMethod = (DalamudLoadMethod)dalamudLoadMethod
        };
    }
}

