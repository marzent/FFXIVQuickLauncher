﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IDalamudRunner
{
    Process? Run(FileInfo runner, bool fakeLogin, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo);
}