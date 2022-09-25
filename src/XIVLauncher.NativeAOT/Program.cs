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
using System.Text.Json;
using System.Text.Json.Serialization;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Patching;
using Serilog.Events;
using MarshalUTF8Extensions;
using XIVLauncher.Common.Game.Patch.PatchList;

namespace NativeLibrary;

[JsonSerializable(typeof(LoginResult))]
[JsonSerializable(typeof(RepairProgress))]
[JsonSerializable(typeof(DalamudConsoleOutput))]
[JsonSerializable(typeof(PatchListEntry[]))]
internal partial class ProgramJsonContext : JsonSerializerContext
{
}

public class Program
{
    public static Storage? Storage;
    public static LauncherConfig? Config { get; private set; }
    public static CommonSettings? CommonSettings => CommonSettings.Instance;
    public static DirectoryInfo DotnetRuntime => Storage!.GetFolder("runtime");
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater? DalamudUpdater { get; private set; }
    public static CompatibilityTools? CompatibilityTools { get; private set; }
    public static ILauncher? Launcher { get; set; }
    public static CommonUniqueIdCache? UniqueIdCache;

    private const uint STEAM_APP_ID = 39210;
    private const uint STEAM_APP_ID_FT = 312060;

    [UnmanagedCallersOnly(EntryPoint = "initXL")]
    public static void Init(nint appName, nint storagePath, bool verboseLogging)
    {
        Storage = new Storage(Marshal.PtrToStringUTF8(appName)!, Marshal.PtrToStringUTF8(storagePath)!);

        var logLevel = verboseLogging ? LogEventLevel.Verbose : LogEventLevel.Information;
        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Async(a =>
                         a.File(Path.Combine(Storage.GetFolder("logs").FullName, "launcher.log")))
                     .WriteTo.Console()
                     .MinimumLevel.Is(logLevel)
                     .CreateLogger();

        Log.Information("========================================================");
        Log.Information("Starting a session({PtrToStringUtf8})", Marshal.PtrToStringUTF8(appName)!);

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
        DalamudUpdater = new DalamudUpdater(Storage.GetFolder("dalamud"), Storage.GetFolder("runtime"), Storage.GetFolder("dalamudAssets"), Storage.Root, null, "Control")
        {
            Overlay = dalamudLoadInfo
        };
        DalamudUpdater.Run();

        UniqueIdCache = new CommonUniqueIdCache(Storage.GetFile("uidCache.json"));
        Launcher = new SqexLauncher(UniqueIdCache, Program.CommonSettings);
        LaunchServices.EnsureLauncherAffinity((XIVLauncher.NativeAOT.Configuration.License)Config!.License!);
    }

    [UnmanagedCallersOnly(EntryPoint = "addEnvironmentVariable")]
    public static void AddEnvironmentVariable(nint key, nint value)
    {
        var kvp = new KeyValuePair<string, string>(Marshal.PtrToStringUTF8(key)!, Marshal.PtrToStringUTF8(value)!);
        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }

    [UnmanagedCallersOnly(EntryPoint = "createCompatToolsInstance")]
    public static void CreateCompatToolsInstance(nint winePath, nint wineDebugVars, bool esync)
    {
        var wineLogFile = new FileInfo(Path.Combine(Storage!.GetFolder("logs").FullName, "wine.log"));
        var winePrefix = Storage.GetFolder("wineprefix");
        var wineSettings = new WineSettings(WineStartupType.Custom, Marshal.PtrToStringUTF8(winePath), Marshal.PtrToStringUTF8(wineDebugVars), wineLogFile, winePrefix, esync, false);
        var toolsFolder = Storage.GetFolder("compatibilitytool");
        CompatibilityTools = new CompatibilityTools(wineSettings, DxvkHudType.None, false, true, toolsFolder);
    }

    [UnmanagedCallersOnly(EntryPoint = "ensurePrefix")]
    public static void EnsurePrefix()
    {
        try
        {
            CompatibilityTools?.EnsurePrefix();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't ensure Prefix");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "generateAcceptLanguage")]
    public static nint GenerateAcceptLanguage(int seed)
    {
        // Needs to be freed by the caller
        return MarshalUtf8.StringToHGlobal(ApiHelpers.GenerateAcceptLanguage(seed));
    }

    [UnmanagedCallersOnly(EntryPoint = "loadConfig")]
    public static void LoadConfig(nint acceptLanguage, nint gamePath, nint gameConfigPath, byte clientLanguage, bool isDx11, bool isEncryptArgs, bool isFt, byte license, nint patchPath, byte patchAcquisitionMethod, long patchSpeedLimit, bool dalamudEnabled, byte dalamudLoadMethod, int dalamudLoadDelay)
    {
        Config = new LauncherConfig
        {
            AcceptLanguage = Marshal.PtrToStringUTF8(acceptLanguage),

            GamePath = new DirectoryInfo(Marshal.PtrToStringUTF8(gamePath)!),
            GameConfigPath = new DirectoryInfo(Marshal.PtrToStringUTF8(gameConfigPath)!),
            ClientLanguage = (ClientLanguage)clientLanguage,

            IsDx11 = isDx11,
            IsEncryptArgs = isEncryptArgs,
            License = (XIVLauncher.NativeAOT.Configuration.License)license,
            IsFt = isFt,

            PatchPath = new DirectoryInfo(Marshal.PtrToStringUTF8(patchPath)!),
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
    public static nint TryLoginToGame(nint username, nint password, nint otp, bool repair)
    {
        try
        {
            return MarshalUtf8.StringToHGlobal(LaunchServices.TryLoginToGame(Marshal.PtrToStringUTF8(username)!, Marshal.PtrToStringUTF8(password)!, Marshal.PtrToStringUTF8(otp)!, repair).Result);
        }
        catch (AggregateException ex)
        {
            var lastException = "";

            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during login occured");
                lastException = iex.Message;
            }

            return MarshalUtf8.StringToHGlobal(lastException);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error during login occured");
            return MarshalUtf8.StringToHGlobal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getUserAgent")]
    public static nint GetUserAgent()
    {
        return MarshalUtf8.StringToHGlobal(Launcher!.GenerateUserAgent());
    }

    [UnmanagedCallersOnly(EntryPoint = "getPatcherUserAgent")]
    public static nint GetPatcherUserAgent()
    {
        return MarshalUtf8.StringToHGlobal(Constants.PatcherUserAgent);
    }

    [UnmanagedCallersOnly(EntryPoint = "getBootPatches")]
    public static nint GetBootPatches()
    {
        return MarshalUtf8.StringToHGlobal(LaunchServices.GetBootPatches().Result);
    }

    [UnmanagedCallersOnly(EntryPoint = "installPatch")]
    public static nint InstallPatch(nint patch, nint repo)
    {
        try
        {
            RemotePatchInstaller.InstallPatch(Marshal.PtrToStringUTF8(patch)!, Marshal.PtrToStringUTF8(repo)!);
            Log.Information("OK");
            return MarshalUtf8.StringToHGlobal("OK");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch installation failed");
            return MarshalUtf8.StringToHGlobal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "checkPatchValidity")]
    public static bool CheckPatchValidity(nint path, long patchLength, long hashBlockSize, nint hashType, nint hashes)
    {
        try
        {
            var pathInfo = new FileInfo(Marshal.PtrToStringUTF8(path)!);
            var splitHashes = Marshal.PtrToStringUTF8(hashes)!.Split(',');
            return LaunchServices.CheckPatchValidity(pathInfo, patchLength, hashBlockSize, Marshal.PtrToStringUTF8(hashType)!, splitHashes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch verification failed");
            return false;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "repairGame")]
    public static nint RepairGame(nint loginResultJson)
    {
        try
        {
            var loginResult = JsonSerializer.Deserialize(Marshal.PtrToStringUTF8(loginResultJson)!, ProgramJsonContext.Default.LoginResult);
            return MarshalUtf8.StringToHGlobal(LaunchServices.RepairGame(loginResult!).Result);
        }
        catch (AggregateException ex)
        {
            string lastException = "";

            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during game repair has occured");
                lastException = iex.Message;
            }

            return MarshalUtf8.StringToHGlobal(lastException);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error during game repair has occured");
            return MarshalUtf8.StringToHGlobal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "queryRepairProgress")]
    public static nint QueryRepairProgress()
    {
        try
        {
            var progress = new RepairProgress(LaunchServices.CurrentPatchVerifier);
            return MarshalUtf8.StringToHGlobal(JsonSerializer.Serialize(progress, ProgramJsonContext.Default.RepairProgress));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Querying Repair Progress Info failed");
            return MarshalUtf8.StringToHGlobal(JsonSerializer.Serialize(new RepairProgress(), ProgramJsonContext.Default.RepairProgress));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getDalamudInstallState")]
    public static bool GetDalamudInstallState()
    {
        try
        {
            return LaunchServices.GetDalamudInstallState() == DalamudLauncher.DalamudInstallState.Ok;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error getting the dalamud state has occured");
            return false;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "startGame")]
    public static nint StartGame(nint loginResultJson, bool dalamudOk)
    {
        try
        {
            var loginResult = JsonSerializer.Deserialize(Marshal.PtrToStringUTF8(loginResultJson)!, ProgramJsonContext.Default.LoginResult);
            var process = LaunchServices.StartGameAndAddon(loginResult!, dalamudOk);
            var ret = new DalamudConsoleOutput
            {
                Handle = (long)process.Handle,
                Pid = process.Id
            };
            return MarshalUtf8.StringToHGlobal(JsonSerializer.Serialize(ret, ProgramJsonContext.Default.DalamudConsoleOutput));
        }
        catch (AggregateException ex)
        {
            string lastException = "";

            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during game startup has occured");
                lastException = iex.Message;
            }

            return MarshalUtf8.StringToHGlobal(lastException);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error during game startup has occured");
            return MarshalUtf8.StringToHGlobal(ex.Message);
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
                Log.Error(iex, "An error occured getting the exit code of pid {Pid}", pid);
            }

            return -42069;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured getting the exit code of pid {Pid}", pid);
            return -69;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "writeLogLine")]
    public static void WriteLogLine(byte logLevel, nint message)
    {
        Log.Write((Serilog.Events.LogEventLevel)logLevel, Marshal.PtrToStringUTF8(message)!);
    }

    [UnmanagedCallersOnly(EntryPoint = "runInPrefix")]
    public static void RunInPrefix(nint command, bool blocking, bool wineD3D)
    {
        try
        {
            var commandStr = Marshal.PtrToStringUTF8(command)!;
            var process = CompatibilityTools!.RunInPrefix(commandStr, wineD3D: wineD3D);

            if (blocking)
            {
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An internal wine error occured");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "addRegistryKey")]
    public static void AddRegistryKey(nint key, nint value, nint data)
    {
        try
        {
            CompatibilityTools!.AddRegistryKey(Marshal.PtrToStringUTF8(key)!, Marshal.PtrToStringUTF8(value)!, Marshal.PtrToStringUTF8(data)!);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured adding the registry key");
        } 
    }

    [UnmanagedCallersOnly(EntryPoint = "getProcessIds")]
    public static nint GetProcessIds(nint executableName)
    {
        var pids = CompatibilityTools!.GetProcessIds(Marshal.PtrToStringUTF8(executableName)!);
        return MarshalUtf8.StringToHGlobal(string.Join(" ", pids));
    }

    [UnmanagedCallersOnly(EntryPoint = "killWine")]
    public static void KillWine()
    {
        CompatibilityTools!.Kill();
    }
}
