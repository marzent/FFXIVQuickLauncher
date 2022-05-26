using System.Diagnostics;
using CheapLoc;
using NativeLibrary;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Common.Game.Launcher;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.NativeAOT.Configuration;

namespace XIVLauncher.NativeAOT;

public class LaunchServices
{
    public enum LoginAction
    {
        Game,
        GameNoDalamud,
        GameNoLaunch,
        Repair,
        Fake,
    }

    public static async Task<string> TryLoginToGame(string username, string password, string otp)
    {
        var action = LoginAction.Game;

        var result = await TryLoginToGame(username, password, otp, action).ConfigureAwait(false);

        return JsonConvert.SerializeObject(result, Formatting.Indented); ;
    }

    public static void EnsureLauncherAffinity(License license)
    {
        switch (license)
        {
            case License.Windows:
                PlatformHelpers.IsMac = false;
                Program.Launcher = new SqexLauncher(Program.UniqueIdCache!, Program.CommonSettings);
                return;
            case License.Mac:
                PlatformHelpers.IsMac = true;
                if (Program.Launcher is MacSqexLauncher)
                    return;
                Program.Launcher = new MacSqexLauncher(Program.UniqueIdCache!, Program.CommonSettings);
                return;
            case License.Steam:
                PlatformHelpers.IsMac = false;
                if (Program.Launcher is SteamSqexLauncher)
                    return;
                Program.Launcher = new SteamSqexLauncher(Program.Steam, Program.UniqueIdCache!, Program.CommonSettings);
                return;
        }
    }

    public static async Task<string> GetBootPatches()
    {
        try
        {
            var bootPatches = await Program.Launcher!.CheckBootVersion(Program.Config!.GamePath).ConfigureAwait(false);

            return JsonConvert.SerializeObject(bootPatches, Formatting.Indented);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to check boot version");
            return ex.Message;
        }
    }

    private static async Task<LoginResult> TryLoginToGame(string username, string password, string otp, LoginAction action)
    {
        bool? gateStatus = null;

#if !DEBUG
        try
        {
            // TODO: Also apply the login status fix here
            var gate = await Program.Launcher.GetGateStatus(Program.Config.ClientLanguage ?? ClientLanguage.English).ConfigureAwait(false);
            gateStatus = gate.Status;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not obtain gate status");
        }

        if (gateStatus == null)
        {
            Log.Error("Login servers could not be reached or maintenance is in progress. This might be a problem with your connection.");

            return null;
        }

        if (gateStatus == false)
        {
            Log.Error(Loc.Localize("GateClosed", "FFXIV is currently under maintenance. Please try again later or see official sources for more information."));

            return null;
        }
#endif

        try
        {
            var enableUidCache = Program.Config.IsUidCacheEnabled ?? false;
            var gamePath = Program.Config.GamePath;

            EnsureLauncherAffinity((License)Program.Config.License);
            if (action == LoginAction.Repair)
                return await Program.Launcher.Login(username, password, otp, false, gamePath, true, Program.Config.IsFt.GetValueOrDefault(false)).ConfigureAwait(false);
            else
                return await Program.Launcher.Login(username, password, otp, enableUidCache, gamePath, false, Program.Config.IsFt.GetValueOrDefault(false)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not login to game");
            throw;
        }
    }

    //private static async Task<bool> TryProcessLoginResult(LoginResult loginResult, bool isSteam, LoginAction action)
    //{
    //    if (loginResult.State == LoginState.NoService)
    //    {
    //        throw new Exception(Loc.Localize("LoginNoServiceMessage",
    //                "This Square Enix account cannot play FINAL FANTASY XIV. Please make sure that you have an active subscription and that it is paid up.\n\nIf you bought FINAL FANTASY XIV on Steam, make sure to check the \"Use Steam service account\" checkbox while logging in.\nIf Auto-Login is enabled, hold shift while starting to access settings."));

    //        return false;
    //    }

    //    if (loginResult.State == LoginState.NoTerms)
    //    {

    //        throw new Exception(Loc.Localize("LoginAcceptTermsMessage",
    //                "Please accept the FINAL FANTASY XIV Terms of Use in the official launcher."));

    //        return false;
    //    }

    //    if (loginResult.State == LoginState.NeedsPatchBoot)
    //    {
    //        throw new Exception("Boot conflict, need reinstall");

    //        return false;
    //    }

    //    if (action == LoginAction.Repair)
    //    {
    //        try
    //        {
    //            if (loginResult.State == LoginState.NeedsPatchGame)
    //            {
    //                //if (!await RepairGame(loginResult).ConfigureAwait(false))
    //                    return false;

    //                loginResult.State = LoginState.Ok;
    //                action = LoginAction.Game;
    //            }
    //            else
    //            {
    //                throw new Exception(Loc.Localize("LoginRepairResponseIsNotNeedsPatchGame",
    //                        "The server sent an incorrect response - the repair cannot proceed."));

    //                return false;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            /*
    //             * We should never reach here.
    //             * If server responds badly, then it should not even have reached this point, as error cases should have been handled before.
    //             * If RepairGame was unsuccessful, then it should have handled all of its possible errors, instead of propagating it upwards.
    //             */
    //            throw;
    //            return false;
    //        }
    //    }

    //    if (loginResult.State == LoginState.NeedsPatchGame)
    //    {
    //        //if (!await InstallGamePatch(loginResult).ConfigureAwait(false))
    //        {
    //            Log.Error("patchSuccess != true");
    //            return false;
    //        }

    //        loginResult.State = LoginState.Ok;
    //        action = LoginAction.Game;
    //    }

    //    if (action == LoginAction.GameNoLaunch)
    //    {
    //        return false;
    //    }

    //    Debug.Assert(loginResult.State == LoginState.Ok);

    //    while (true)
    //    {
    //        List<Exception> exceptions = new();

    //        try
    //        {
    //            using var process = await StartGameAndAddon(loginResult, isSteam, action == LoginAction.GameNoDalamud).ConfigureAwait(false);

    //            if (process is null)
    //                throw new Exception("Could not obtain Process Handle");

    //            return true;
    //        }
    //        catch (Exception ex)
    //        {
    //            Log.Error(ex, "StartGameAndError resulted in an exception.");

    //            exceptions.Add(ex);
    //            throw;
    //        }
    //    }
    //}

    public static async Task<Process> StartGameAndAddon(LoginResult loginResult)
    {
        var dalamudOk = false;

        IDalamudRunner dalamudRunner;
        IDalamudCompatibilityCheck dalamudCompatCheck;

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                dalamudRunner = new WindowsDalamudRunner();
                dalamudCompatCheck = new WindowsDalamudCompatibilityCheck();
                break;
            case PlatformID.Unix:
                dalamudRunner = new UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime);
                dalamudCompatCheck = new UnixDalamudCompatibilityCheck();
                break;
            default:
                throw new NotImplementedException();
        }

        var dalamudLauncher = new DalamudLauncher(dalamudRunner, Program.DalamudUpdater, Program.Config!.DalamudLoadMethod.GetValueOrDefault(DalamudLoadMethod.DllInject),
            Program.Config.GamePath, Program.storage!.Root, Program.Config.ClientLanguage ?? ClientLanguage.English, Program.Config.DalamudLoadDelay);

        try
        {
            dalamudCompatCheck.EnsureCompatibility();
        }
        catch (IDalamudCompatibilityCheck.NoRedistsException ex)
        {
            Log.Error(ex, "No Dalamud Redists found");
        }
        catch (IDalamudCompatibilityCheck.ArchitectureNotSupportedException ex)
        {
            Log.Error(ex, "Architecture not supported");
        }

        if (true)
        {
            try
            {
                Log.Information("Waiting for Dalamud to be ready...", "This may take a little while. Please hold!");
                dalamudOk = dalamudLauncher.HoldForUpdate(Program.Config.GamePath) == DalamudLauncher.DalamudInstallState.Ok;
            }
            catch (DalamudRunnerException ex)
            {
                Log.Error(ex, "Couldn't ensure Dalamud runner");

                var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
                    "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

                throw;
            }
        }

        IGameRunner runner;

        var gameArgs = Program.Config.AdditionalArgs ?? string.Empty;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            runner = new WindowsGameRunner(dalamudLauncher, dalamudOk, Program.DalamudUpdater!.Runtime);
        }
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            Log.Information("Starting game...", "Have fun!");

            runner = new UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk);

            var userPath = Program.CompatibilityTools!.UnixToWinePath(Program.Config!.GameConfigPath!.FullName);
            if (Program.Config!.IsEncryptArgs.GetValueOrDefault(true))
                gameArgs += $" UserPath={userPath}";
            else
                gameArgs += $" UserPath=\"{userPath}\"";
        }
        else
        {
            throw new NotImplementedException();
        }

        // We won't do any sanity checks here anymore, since that should be handled in StartLogin
        var launchedProcess = Program.Launcher!.LaunchGame(runner,
            loginResult.UniqueId,
            loginResult.OauthLogin.Region,
            loginResult.OauthLogin.MaxExpansion,
            gameArgs,
            Program.Config.GamePath,
            Program.Config.IsDx11 ?? true,
            Program.Config.ClientLanguage.GetValueOrDefault(ClientLanguage.English),
            Program.Config.IsEncryptArgs.GetValueOrDefault(true),
            DpiAwareness.Unaware);

        if (launchedProcess == null)
        {
            Log.Information("GameProcess was null...");
            return null;
        }

        return launchedProcess!;
    }

    public static async Task<int> GetExitCode(int pid)
    {
        var process = Process.GetProcessById(pid);

        Log.Debug($"Waiting for game process with handle {process.Handle} and pid {pid} to exit");

        await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);

        Log.Verbose("Game has exited");

        try
        {
            if (Program.Steam.IsValid)
            {
                Program.Steam.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not shut down Steam");
        }

        return process.ExitCode;
    }

    public static async Task<string> RepairGame(LoginResult loginResult)
    {
        Log.Information("STARTING REPAIR");

        using var verify = new PatchVerifier(CommonSettings.Instance, loginResult, 20, loginResult.OauthLogin.MaxExpansion);
        verify.Start();
        await verify.WaitForCompletion().ConfigureAwait(false);

        switch (verify.State)
        {
            case PatchVerifier.VerifyState.Done:
                return verify.NumBrokenFiles switch
                {
                    0 => Loc.Localize("GameRepairSuccess0", "All game files seem to be valid."),
                    1 => Loc.Localize("GameRepairSuccess1", "XIVLauncher has successfully repaired 1 game file."),
                    _ => string.Format(Loc.Localize("GameRepairSuccessPlural", "XIVLauncher has successfully repaired {0} game files.")),
                };

            case PatchVerifier.VerifyState.Error:
                if (verify.LastException is NoVersionReferenceException)
                    return Loc.Localize("NoVersionReferenceError", "The version of the game you are on cannot be repaired by XIVLauncher yet, as reference information is not yet available.\nPlease try again later.");
                return Loc.Localize("GameRepairError", "An error occurred while repairing the game files.\nYou may have to reinstall the game.");

            case PatchVerifier.VerifyState.Cancelled:
                return "Cancelled"; //should not reach
        }
        return string.Empty;
    }
}


