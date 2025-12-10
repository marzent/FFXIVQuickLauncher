using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XIVLauncher.Common.Dalamud;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(DalamudVersionInfo))]
internal partial class DalamudJsonContext : JsonSerializerContext
{
}
