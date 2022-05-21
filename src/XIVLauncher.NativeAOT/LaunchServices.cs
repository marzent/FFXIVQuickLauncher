using System;
using System.Diagnostics;
using CheapLoc;
using NativeLibrary;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Launcher;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Windows;

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

    public static async Task<bool> Login(string username, string password, string otp, bool isSteam, byte loginAction)
    {
        var action = (LoginAction)loginAction;

        if (action == LoginAction.Fake)
        {
            EnsureLauncherAffinity(false);
            IGameRunner gameRunner;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                gameRunner = new WindowsGameRunner(null, false);
            else
                gameRunner = new UnixGameRunner(Program.CompatibilityTools, null, false);

            Program.Launcher!.LaunchGame(gameRunner, "0", 1, 2, "", Program.Config!.GamePath!, true, ClientLanguage.Japanese, true, DpiAwareness.Unaware);

            return true;
        }

        var bootRes = await HandleBootCheck().ConfigureAwait(false);

        if (!bootRes)
            return false;

        var isOtp = !string.IsNullOrEmpty(otp);

        var loginResult = await TryLoginToGame(username, password, otp, isSteam, action).ConfigureAwait(false);

        return await TryProcessLoginResult(loginResult, isSteam, action).ConfigureAwait(false);
    }

    public static void EnsureLauncherAffinity(bool isSteam)
    {
        var isSteamLauncher = Program.Launcher is SteamSqexLauncher;

        if (isSteamLauncher && !isSteam)
        {
            Program.Launcher = new SqexLauncher(Program.UniqueIdCache!, Program.CommonSettings);
        }
        else if (!isSteamLauncher && isSteam)
        {
            Program.Launcher = new SteamSqexLauncher(Program.Steam, Program.UniqueIdCache!, Program.CommonSettings);
        }
    }

    private static async Task<bool> HandleBootCheck()
    {
        try
        {
            if (Program.Config!.PatchPath is { Exists: false })
            {
                Directory.CreateDirectory(Program.Config.PatchPath.FullName);
            }

            PatchListEntry[] bootPatches = null;

            try
            {
                bootPatches = await Program.Launcher!.CheckBootVersion(Program.Config.GamePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to check boot version");
                return false;
            }

            if (bootPatches.Length == 0)
                return true;

            return false;
            //return await TryHandlePatchAsync(Repository.Boot, bootPatches, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch Boot exception");
            return false;
        }
    }

    private static async Task<LoginResult> TryLoginToGame(string username, string password, string otp, bool isSteam, LoginAction action)
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
            /*
            CustomMessageBox.Builder.NewFrom(Loc.Localize("GateClosed", "FFXIV is currently under maintenance. Please try again later or see official sources for more information."))
                            .WithImage(MessageBoxImage.Asterisk)
                            .WithButtons(MessageBoxButton.OK)
                            .WithCaption("XIVLauncher")
                            .WithParentWindow(_window)
                            .Show();*/

            Log.Error("Maintenance is in progress.");

            return null;
        }
#endif

        try
        {
            var enableUidCache = Program.Config.IsUidCacheEnabled ?? false;
            var gamePath = Program.Config.GamePath;

            EnsureLauncherAffinity(isSteam);
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

    private static async Task<bool> TryProcessLoginResult(LoginResult loginResult, bool isSteam, LoginAction action)
    {
        if (loginResult.State == LoginState.NoService)
        {
            throw new Exception(Loc.Localize("LoginNoServiceMessage",
                    "This Square Enix account cannot play FINAL FANTASY XIV. Please make sure that you have an active subscription and that it is paid up.\n\nIf you bought FINAL FANTASY XIV on Steam, make sure to check the \"Use Steam service account\" checkbox while logging in.\nIf Auto-Login is enabled, hold shift while starting to access settings."));

            return false;
        }

        if (loginResult.State == LoginState.NoTerms)
        {

            throw new Exception(Loc.Localize("LoginAcceptTermsMessage",
                    "Please accept the FINAL FANTASY XIV Terms of Use in the official launcher."));

            return false;
        }

        if (loginResult.State == LoginState.NeedsPatchBoot)
        {
            throw new Exception("Boot conflict, need reinstall");

            return false;
        }

        if (action == LoginAction.Repair)
        {
            try
            {
                if (loginResult.State == LoginState.NeedsPatchGame)
                {
                    //if (!await RepairGame(loginResult).ConfigureAwait(false))
                        return false;

                    loginResult.State = LoginState.Ok;
                    action = LoginAction.Game;
                }
                else
                {
                    throw new Exception(Loc.Localize("LoginRepairResponseIsNotNeedsPatchGame",
                            "The server sent an incorrect response - the repair cannot proceed."));

                    return false;
                }
            }
            catch (Exception ex)
            {
                /*
                 * We should never reach here.
                 * If server responds badly, then it should not even have reached this point, as error cases should have been handled before.
                 * If RepairGame was unsuccessful, then it should have handled all of its possible errors, instead of propagating it upwards.
                 */
                throw;
                return false;
            }
        }

        if (loginResult.State == LoginState.NeedsPatchGame)
        {
            //if (!await InstallGamePatch(loginResult).ConfigureAwait(false))
            {
                Log.Error("patchSuccess != true");
                return false;
            }

            loginResult.State = LoginState.Ok;
            action = LoginAction.Game;
        }

        if (action == LoginAction.GameNoLaunch)
        {
            return false;
        }

        Debug.Assert(loginResult.State == LoginState.Ok);

        while (true)
        {
            List<Exception> exceptions = new();

            try
            {
                using var process = await StartGameAndAddon(loginResult, isSteam, action == LoginAction.GameNoDalamud).ConfigureAwait(false);

                if (process is null)
                    throw new Exception("Could not obtain Process Handle");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StartGameAndError resulted in an exception.");

                exceptions.Add(ex);
                throw;
            }
        }
    }

    public static async Task<Process> StartGameAndAddon(LoginResult loginResult, bool isSteam, bool forceNoDalamud)
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

        var dalamudLauncher = new DalamudLauncher(dalamudRunner, Program.DalamudUpdater, Program.Config.DalamudLoadMethod.GetValueOrDefault(DalamudLoadMethod.DllInject),
            Program.Config.GamePath, Program.storage.Root, Program.Config.ClientLanguage ?? ClientLanguage.English, Program.Config.DalamudLoadDelay);

        try
        {
            dalamudCompatCheck.EnsureCompatibility();
        }
        catch (IDalamudCompatibilityCheck.NoRedistsException ex)
        {
            Log.Error(ex, "No Dalamud Redists found");

            throw;
            /*
            CustomMessageBox.Show(
                Loc.Localize("DalamudVc2019RedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015-2019 redistributable to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: _window);
                */
        }
        catch (IDalamudCompatibilityCheck.ArchitectureNotSupportedException ex)
        {
            Log.Error(ex, "Architecture not supported");

            throw;
            /*
            CustomMessageBox.Show(
                Loc.Localize("DalamudArchError",
                    "Dalamud cannot run your computer's architecture. Please make sure that you are running a 64-bit version of Windows.\nIf you are using Windows on ARM, please make sure that x64-Emulation is enabled for XIVLauncher."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: _window);
                */
        }

        if (Program.Config.DalamudEnabled.GetValueOrDefault(true) && !forceNoDalamud && Program.Config.IsDx11.GetValueOrDefault(true))
        {
            try
            {
                Log.Information("Waiting for Dalamud to be ready...", "This may take a little while. Please hold!");
                dalamudOk = dalamudLauncher.HoldForUpdate(Program.Config.GamePath);
            }
            catch (DalamudRunnerException ex)
            {
                Log.Error(ex, "Couldn't ensure Dalamud runner");

                var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
                    "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

                throw;
                /*
                CustomMessageBox.Builder
                                .NewFrom(runnerErrorMessage)
                                .WithImage(MessageBoxImage.Error)
                                .WithButtons(MessageBoxButton.OK)
                                .WithShowHelpLinks()
                                .WithParentWindow(_window)
                                .Show();
                                */
            }
        }

        IGameRunner runner;

        var gameArgs = Program.Config.AdditionalArgs ?? string.Empty;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            runner = new WindowsGameRunner(dalamudLauncher, dalamudOk);
        }
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var signal = new ManualResetEvent(false);
            var isFailed = false;

            Log.Information("Starting game...", "Have fun!");

            runner = new UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk);

            gameArgs += $" UserPath=\"{Program.CompatibilityTools.UnixToWinePath(Program.Config.GameConfigPath.FullName)}\"";
            gameArgs = gameArgs.Trim();
        }
        else
        {
            throw new NotImplementedException();
        }

        // We won't do any sanity checks here anymore, since that should be handled in StartLogin
        var launchedProcess = Program.Launcher.LaunchGame(runner,
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

        Log.Debug("Waiting for game to exit");

        await Task.Run(() => launchedProcess!.WaitForExit()).ConfigureAwait(false);

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

        return launchedProcess!;
    }
}


