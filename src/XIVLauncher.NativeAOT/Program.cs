using System.Runtime.InteropServices;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.NativeAOT.Configuration;
using static XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Game.Launcher;
using XIVLauncher.PlatformAbstractions;
using XIVLauncher.NativeAOT;
using Newtonsoft.Json;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Patching;
using System.ComponentModel;

namespace NativeLibrary;

public class Program
{
    public static Storage? storage;
    public static LauncherConfig? Config { get; private set; }
    public static CommonSettings CommonSettings => CommonSettings.Instance;
    public static DirectoryInfo DotnetRuntime => storage!.GetFolder("runtime");
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater? DalamudUpdater { get; private set; }
    public static CompatibilityTools? CompatibilityTools { get; private set; }
    public static ILauncher? Launcher { get; set; }
    public static CommonUniqueIdCache? UniqueIdCache;

    public const uint STEAM_APP_ID = 39210;
    public const uint STEAM_APP_ID_FT = 312060;

    [UnmanagedCallersOnly(EntryPoint = "initXL")]
    public static void Init(IntPtr appName, IntPtr storagePath)
    {
        storage = new Storage(Marshal.PtrToStringAnsi(appName)!, Marshal.PtrToStringAnsi(storagePath)!);

        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Async(a =>
                         a.File(Path.Combine(storage.GetFolder("logs").FullName, "launcher.log")))
                     .WriteTo.Console()
#if DEBUG
                     .MinimumLevel.Verbose()
#else
                     .MinimumLevel.Verbose()
#endif
                     .CreateLogger();

        Log.Information("========================================================");
        Log.Information($"Starting a session({Marshal.PtrToStringAnsi(appName)!})");

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
                Log.Error(ex, "Couldn't init Steam with game AppIds");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Steam couldn't load");
        }

        var dalamudLoadInfo = new DalamudOverlayInfoProxy();
        DalamudUpdater = new DalamudUpdater(storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), storage.Root, null)
        {
            Overlay = dalamudLoadInfo
        };
        DalamudUpdater.Run();

        UniqueIdCache = new CommonUniqueIdCache(storage.GetFile("uidCache.json"));
        Launcher = new SqexLauncher(UniqueIdCache, Program.CommonSettings);
        LaunchServices.EnsureLauncherAffinity((XIVLauncher.NativeAOT.Configuration.License)Config!.License!);
    }

    [UnmanagedCallersOnly(EntryPoint = "addEnviromentVariable")]
    public static void AddEnviromentVariable(IntPtr key, IntPtr value)
    {
        var kvp = new KeyValuePair<string, string>(Marshal.PtrToStringAnsi(key)!, Marshal.PtrToStringAnsi(value)!);
        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }

    [UnmanagedCallersOnly(EntryPoint = "createCompatToolsInstance")]
    public static void CreateCompatToolsInstance(IntPtr winePath, IntPtr wineDebugVars, bool esync)
    {
        var wineLogFile = new FileInfo(Path.Combine(storage!.GetFolder("logs").FullName, "wine.log"));
        var winePrefix = storage.GetFolder("wineprefix");
        var wineSettings = new WineSettings(WineStartupType.Custom, Marshal.PtrToStringAnsi(winePath), Marshal.PtrToStringAnsi(wineDebugVars), wineLogFile, winePrefix, esync, false);
        var toolsFolder = storage.GetFolder("compatibilitytool");
        CompatibilityTools = new CompatibilityTools(wineSettings, DxvkHudType.None, false, true, toolsFolder);
    }

    [UnmanagedCallersOnly(EntryPoint = "generateAcceptLanguage")]
    public static IntPtr GenerateAcceptLanguage(int seed)
    {
        // Needs to be freed by the caller
        return Marshal.StringToHGlobalAnsi(ApiHelpers.GenerateAcceptLanguage(seed));
    }

    [UnmanagedCallersOnly(EntryPoint = "loadConfig")]
    public static void LoadConfig(IntPtr acceptLanguage, IntPtr gamePath, IntPtr gameConfigPath, byte clientLanguage, bool isDx11, bool isEncryptArgs, bool isFt, byte license, IntPtr patchPath, byte patchAcquisitionMethod, Int64 patchSpeedLimit, bool dalamudEnabled, byte dalamudLoadMethod, int dalamudLoadDelay)
    {
        Config = new LauncherConfig
        {
            AcceptLanguage = Marshal.PtrToStringAnsi(acceptLanguage),

            GamePath = new DirectoryInfo(Marshal.PtrToStringAnsi(gamePath)!),
            GameConfigPath = new DirectoryInfo(Marshal.PtrToStringAnsi(gameConfigPath)!),
            ClientLanguage = (ClientLanguage)clientLanguage,

            IsDx11 = isDx11,
            IsEncryptArgs = isEncryptArgs,
            License = (XIVLauncher.NativeAOT.Configuration.License)license,
            IsFt = isFt,

            PatchPath = new DirectoryInfo(Marshal.PtrToStringAnsi(patchPath)!),
            PatchAcquisitionMethod = (AcquisitionMethod)patchAcquisitionMethod,
            PatchSpeedLimit = patchSpeedLimit,

            DalamudEnabled = dalamudEnabled,
            DalamudLoadMethod = (DalamudLoadMethod)dalamudLoadMethod,
            DalamudLoadDelay = dalamudLoadDelay
        };
    }

    [UnmanagedCallersOnly(EntryPoint = "fakeLogin")]
    public static void FakeLogin()
    {
        LaunchServices.EnsureLauncherAffinity((XIVLauncher.NativeAOT.Configuration.License)Config!.License!);
        IGameRunner gameRunner;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            gameRunner = new WindowsGameRunner(null, false, Program.DalamudUpdater!.Runtime);
        else
            gameRunner = new UnixGameRunner(Program.CompatibilityTools, null, false);

        Launcher!.LaunchGame(gameRunner, "0", 1, 2, "", Program.Config!.GamePath!, true, ClientLanguage.Japanese, true, DpiAwareness.Unaware);
    }

    [UnmanagedCallersOnly(EntryPoint = "tryLoginToGame")]
    public static IntPtr TryLoginToGame(IntPtr username, IntPtr password, IntPtr otp, bool repair)
    {
        try
        {
            return Marshal.StringToHGlobalAnsi(LaunchServices.TryLoginToGame(Marshal.PtrToStringAnsi(username)!, Marshal.PtrToStringAnsi(password)!, Marshal.PtrToStringAnsi(otp)!, repair).Result);
        }
        catch (AggregateException ex)
        {
            string lastException = "";
            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during login occured");
                lastException = iex.Message;
            }
            return Marshal.StringToHGlobalAnsi(lastException);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getUserAgent")]
    public static IntPtr GetUserAgent()
    {
        return Marshal.StringToHGlobalAnsi(Launcher!.GenerateUserAgent());
    }

    [UnmanagedCallersOnly(EntryPoint = "getPatcherUserAgent")]
    public static IntPtr GetPatcherUserAgent()
    {
        return Marshal.StringToHGlobalAnsi(Constants.PatcherUserAgent);
    }

    [UnmanagedCallersOnly(EntryPoint = "getBootPatches")]
    public static IntPtr GetBootPatches()
    {
        return Marshal.StringToHGlobalAnsi(LaunchServices.GetBootPatches().Result);
    }

    [UnmanagedCallersOnly(EntryPoint = "installPatch")]
    public static IntPtr InstallPatch(IntPtr patch, IntPtr repo)
    {
        try
        {
            RemotePatchInstaller.InstallPatch(Marshal.PtrToStringAnsi(patch)!, Marshal.PtrToStringAnsi(repo)!);
            Log.Information("OK");
            return Marshal.StringToHGlobalAnsi("OK");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch installation failed");
            return Marshal.StringToHGlobalAnsi(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "repairGame")]
    public static IntPtr RepairGame(IntPtr loginResultJSON)
    {
        try
        {
            var loginResult = JsonConvert.DeserializeObject<LoginResult>(Marshal.PtrToStringAnsi(loginResultJSON)!);
            return Marshal.StringToHGlobalAnsi(LaunchServices.RepairGame(loginResult).Result);
        }
        catch (AggregateException ex)
        {
            string lastException = "";
            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during game repair has occured");
                lastException = iex.Message;
            }
            return Marshal.StringToHGlobalAnsi(lastException);
        }
        catch (Exception ex)
        {
            return Marshal.StringToHGlobalAnsi(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "queryRepairProgress")]
    public static IntPtr QueryRepairProgress()
    {
        try
        {
            var progress = new RepairProgress(LaunchServices.CurrentPatchVerifier);
            return Marshal.StringToHGlobalAnsi(JsonConvert.SerializeObject(progress));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Querying Repair Progress Info failed");
            return Marshal.StringToHGlobalAnsi(JsonConvert.SerializeObject(new RepairProgress()));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "startGame")]
    public static IntPtr StartGame(IntPtr loginResultJSON)
    {
        try
        {
            var loginResult = JsonConvert.DeserializeObject<LoginResult>(Marshal.PtrToStringAnsi(loginResultJSON)!);
            var process = LaunchServices.StartGameAndAddon(loginResult).Result;
            var ret = new DalamudConsoleOutput
            {
                Handle = (long)process.Handle,
                Pid = process.Id
            };
            return Marshal.StringToHGlobalAnsi(JsonConvert.SerializeObject(ret));
        }
        catch (AggregateException ex)
        {
            string lastException = "";
            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during game startup has occured");
                lastException = iex.Message;
            }
            return Marshal.StringToHGlobalAnsi(lastException);
        }
        catch (Exception ex)
        {
            return Marshal.StringToHGlobalAnsi(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getExitCode")]
    public static int GetExitCode(int pid)
    {
        try
        {
            return LaunchServices.GetExitCode(pid).Result;
        }
        catch (AggregateException ex)
        {
            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, $"An error occured getting the exit code of pid {pid}");
            }
            return -42069;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "writeLogLine")]
    public static void WriteLogLine(byte logLevel, IntPtr message)
    {
        Log.Write((Serilog.Events.LogEventLevel)logLevel, Marshal.PtrToStringAnsi(message)!);
    }

    [UnmanagedCallersOnly(EntryPoint = "runInPrefix")]
    public static void RunInPrefix(IntPtr command)
    {
        CompatibilityTools!.RunInPrefix(Marshal.PtrToStringAnsi(command)!);
    }

    [UnmanagedCallersOnly(EntryPoint = "runInPrefixBlocking")]
    public static void RunInPrefixBlocking(IntPtr command)
    {
        CompatibilityTools!.RunInPrefix(Marshal.PtrToStringAnsi(command)!).WaitForExit();
    }

    [UnmanagedCallersOnly(EntryPoint = "addRegistryKey")]
    public static void AddRegistryKey(IntPtr key, IntPtr value, IntPtr data)
    {
        try
        {
            CompatibilityTools!.AddRegistryKey(Marshal.PtrToStringAnsi(key)!, Marshal.PtrToStringAnsi(value)!, Marshal.PtrToStringAnsi(data)!);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured adding the registry key");
        } 
    }

    [UnmanagedCallersOnly(EntryPoint = "getProcessIds")]
    public static IntPtr GetProcessIds(IntPtr executableName)
    {
        var pids = CompatibilityTools!.GetProcessIds(Marshal.PtrToStringAnsi(executableName)!);
        return Marshal.StringToHGlobalAnsi(string.Join(" ", pids));
    }

    [UnmanagedCallersOnly(EntryPoint = "killWine")]
    public static void KillWine()
    {
        CompatibilityTools!.Kill();
    }
}
