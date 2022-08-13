using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NativeLibrary;
using System.Text.Json;
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

public static class LaunchServices
{
    public static PatchVerifier? CurrentPatchVerifier { get; private set; } = null;

    private enum LoginAction
    {
        Game,
        GameNoDalamud,
        GameNoLaunch,
        Repair,
        Fake,
    }

    public static async Task<string> TryLoginToGame(string username, string password, string otp, bool repair)
    {
        var action = repair ? LoginAction.Repair : LoginAction.Game;

        var result = await TryLoginToGame(username, password, otp, action).ConfigureAwait(false);

        return JsonSerializer.Serialize(result, ProgramJsonContext.Default.LoginResult);
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

            return JsonSerializer.Serialize(bootPatches, ProgramJsonContext.Default.PatchListEntryArray);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to check boot version");
            return ex.Message;
        }
    }

    private static async Task<LoginResult> TryLoginToGame(string username, string password, string otp, LoginAction action)
    {
        try
        {
            var enableUidCache = Program.Config?.IsUidCacheEnabled ?? false;
            var gamePath = Program.Config!.GamePath;

            EnsureLauncherAffinity((License)Program.Config.License);
            if (action == LoginAction.Repair)
                return await Program.Launcher!.Login(username, password, otp, false, gamePath, true, Program.Config.IsFt.GetValueOrDefault(false)).ConfigureAwait(false);
            else
                return await Program.Launcher!.Login(username, password, otp, enableUidCache, gamePath, false, Program.Config.IsFt.GetValueOrDefault(false)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not login to game");
            throw;
        }
    }

    public static bool CheckPatchValidity(FileInfo path, long patchLength, long hashBlockSize, string hashType, string[] hashes)
    {
        if (hashType != "sha1")
        {
            Log.Error("??? Unknown HashType: {0} for {1}", hashType, path.FullName);
            return true;
        }

        var stream = path.OpenRead();

        if (stream.Length != patchLength)
        {
            Log.Error("Bad length for patch {0}: {1} instead of {2}", path.FullName, stream.Length, patchLength);
            return false;
        }

        var parts = (int)Math.Ceiling((double)patchLength / hashBlockSize);
        var block = new byte[hashBlockSize];

        for (var i = 0; i < parts; i++)
        {
            var read = stream.Read(block, 0, (int)hashBlockSize);

            if (read < hashBlockSize)
            {
                var trimmedBlock = new byte[read];
                Array.Copy(block, 0, trimmedBlock, 0, read);
                block = trimmedBlock;
            }

            using var sha1 = new SHA1Managed();

            var hash = sha1.ComputeHash(block);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            if (sb.ToString() == hashes[i])
                continue;

            stream.Close();
            return false;
        }

        stream.Close();
        return true;
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

    public static DalamudLauncher.DalamudInstallState GetDalamudInstallState()
    {
        IDalamudRunner dalamudRunner = null!;
        IDalamudCompatibilityCheck dalamudCompatCheck = null!;

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
        }

        var dalamudLauncher = new DalamudLauncher(dalamudRunner, Program.DalamudUpdater, Program.Config!.DalamudLoadMethod.GetValueOrDefault(DalamudLoadMethod.DllInject),
            Program.Config.GamePath, Program.Storage!.Root, Program.Config.ClientLanguage ?? ClientLanguage.English, Program.Config.DalamudLoadDelay, false, false, false);

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

        try
        {
            Log.Information("Waiting for Dalamud to be ready...", "This may take a little while. Please hold!");
            return dalamudLauncher.HoldForUpdate(Program.Config.GamePath);
        }
        catch (DalamudRunnerException ex)
        {
            Log.Error(ex, "Couldn't ensure Dalamud runner");

            var runnerErrorMessage = "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".";

            throw;
        }
    }

    public static Process StartGameAndAddon(LoginResult loginResult, bool dalamudOk)
    {
        IDalamudRunner dalamudRunner = null!;

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                dalamudRunner = new WindowsDalamudRunner();
                break;

            case PlatformID.Unix:
                dalamudRunner = new UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime);
                break;
        }

        var dalamudLauncher = new DalamudLauncher(dalamudRunner, Program.DalamudUpdater, Program.Config!.DalamudLoadMethod.GetValueOrDefault(DalamudLoadMethod.DllInject),
            Program.Config.GamePath, Program.Storage!.Root, Program.Config.ClientLanguage ?? ClientLanguage.English, Program.Config.DalamudLoadDelay, false, false, false);

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
            Log.Information("GameProcess was null...");

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
            if (Program.Steam?.IsValid ?? false)
            {
                Program.Steam?.Shutdown();
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

        using var verify = new PatchVerifier(CommonSettings.Instance, loginResult, 20, loginResult.OauthLogin.MaxExpansion, false);
        CurrentPatchVerifier = verify;
        verify.Start();
        await verify.WaitForCompletion().ConfigureAwait(false);

        switch (verify.State)
        {
            case PatchVerifier.VerifyState.Done:
                return verify.NumBrokenFiles switch
                {
                    0 => "All game files seem to be valid.",
                    1 => "XIV on Mac has successfully repaired 1 game file.",
                    _ => string.Format("XIV on Mac has successfully repaired {0} game files.", verify.NumBrokenFiles),
                };

            case PatchVerifier.VerifyState.Error:
                if (verify.LastException is NoVersionReferenceException)
                    return "The version of the game you are on cannot be repaired by XIV on Mac yet, as reference information is not yet available.\nPlease try again later.";
                return "An error occurred while repairing the game files.\nYou may have to reinstall the game.";

            case PatchVerifier.VerifyState.Cancelled:
                return "Cancelled"; //should not reach
        }

        return string.Empty;
    }
}


