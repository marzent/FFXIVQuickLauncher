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
using static XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Game.Launcher;
using XIVLauncher.PlatformAbstractions;
using XIVLauncher.NativeAOT;

namespace NativeLibrary;

public class Program
{
    public static Storage? storage;
    public static LauncherConfig? Config { get; private set; }
    public static CommonSettings CommonSettings => new(Config!);
    public static DirectoryInfo DotnetRuntime => storage!.GetFolder("runtime");
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater? DalamudUpdater { get; private set; }
    public static CompatibilityTools? CompatibilityTools { get; private set; }
    public static ILauncher? Launcher { get; set; }
    public static CommonUniqueIdCache? UniqueIdCache;

    public const uint STEAM_APP_ID = 39210;
    public const uint STEAM_APP_ID_FT = 312060;

    [UnmanagedCallersOnly(EntryPoint = "xl_init")]
    public static void Init(IntPtr storagePath)
    {
        storage = new Storage("XIVLauncher.NativeAOT", Marshal.PtrToStringAnsi(storagePath)!);

        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Async(a =>
                         a.File(Path.Combine(storage.GetFolder("logs").FullName, "launcher.log")))
                     .WriteTo.Console()
                     .WriteTo.Debug()
                     .MinimumLevel.Verbose()
                     .CreateLogger();

        Log.Information("========================================================");
        Log.Information("Starting a session(XIVLauncher.NativeAOT)");

        try
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Steam = new WindowsSteam();
                    break;

                case PlatformID.Unix:
                    Steam = new UnixSteam();
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }

            try
            {
                var appId = Config!.IsFt == true ? STEAM_APP_ID_FT : STEAM_APP_ID;
                Steam.Initialize(appId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Couldn't init Steam with game AppIds, trying FT");
                Steam.Initialize(STEAM_APP_ID_FT);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Steam couldn't load");
        }

        DalamudUpdater = new DalamudUpdater(storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), storage.Root, null);
        DalamudUpdater.Run();

        UniqueIdCache = new CommonUniqueIdCache(storage.GetFile("uidCache.json"));
        Launcher = new SqexLauncher(UniqueIdCache, Program.CommonSettings);
    }

    [UnmanagedCallersOnly(EntryPoint = "xl_createCompatToolsInstance")]
    public static void CreateCompatToolsInstance(IntPtr winePath, IntPtr wineDebugVars, bool esync)
    {
        var wineLogFile = new FileInfo(Path.Combine(storage!.GetFolder("logs").FullName, "wine.log"));
        var winePrefix = storage.GetFolder("wineprefix");
        var wineSettings = new WineSettings(WineStartupType.Custom, Marshal.PtrToStringAnsi(winePath), Marshal.PtrToStringAnsi(wineDebugVars), wineLogFile, winePrefix, esync, false);
        var toolsFolder = storage.GetFolder("compatibilitytool");
        CompatibilityTools = new CompatibilityTools(wineSettings, DxvkHudType.None, false, true, toolsFolder);
    }

    [UnmanagedCallersOnly(EntryPoint = "xl_generateAcceptLanguage")]
    public static IntPtr GenerateAcceptLanguage(int seed)
    {
        // Needs to be freed by the caller
        return (IntPtr)Marshal.StringToHGlobalAnsi(Util.GenerateAcceptLanguage(seed));
    }

    [UnmanagedCallersOnly(EntryPoint = "xl_loadConfig")]
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

    [UnmanagedCallersOnly(EntryPoint = "xl_login")]
    public static bool Login(IntPtr username, IntPtr password, IntPtr otp, bool isSteam, byte loginAction)
    {
        return LaunchServices.Login(Marshal.PtrToStringAnsi(username)!, Marshal.PtrToStringAnsi(password)!, Marshal.PtrToStringAnsi(otp)!, isSteam, loginAction).Result;
    }
}

