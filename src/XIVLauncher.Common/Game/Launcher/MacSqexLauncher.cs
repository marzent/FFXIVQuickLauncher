#nullable enable

using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Game.Launcher;

public class MacSqexLauncher : SqexLauncher
{
    public MacSqexLauncher(IUniqueIdCache uniqueIdCache, ISettings? settings, string frontierUrl)
        : base(uniqueIdCache, settings, frontierUrl)
    {
    }

    public override string GenerateUserAgent()
    {
        return "macSQEXAuthor/2.0.0(MacOSX; ja-jp)";
    }
}

#nullable restore
