

#nullable enable

#if NET6_0_OR_GREATER && !WIN32
using System.Net.Security;
#endif

using XIVLauncher.Common.PlatformAbstractions;
#nullable enable

namespace XIVLauncher.Common.Game.Launcher;

public class MacSqexLauncher : SqexLauncher
{
    public MacSqexLauncher(IUniqueIdCache uniqueIdCache, ISettings settings) : base(uniqueIdCache, settings)
    {
    }

    public override string GenerateUserAgent()
    {
        return "macSQEXAuthor/2.0.0(MacOSX; ja-jp)";
    }
}

#nullable restore
