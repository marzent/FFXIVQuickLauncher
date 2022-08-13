using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudSettings
    {
        public string? DalamudBetaKey { get; set; } = null;
        public bool DoDalamudRuntime { get; set; } = false;
        public string DalamudBetaKind { get; set; }

        public static string GetConfigPath(DirectoryInfo configFolder) => Path.Combine(configFolder.FullName, "dalamudConfig.json");

        public static DalamudSettings GetSettings(DirectoryInfo configFolder)
        {
            var configPath = GetConfigPath(configFolder);
            DalamudSettings deserialized = null;

            try
            {
                deserialized = File.Exists(configPath) ? JsonSerializer.Deserialize(File.ReadAllText(configPath), DalamudJsonContext.Default.DalamudSettings) : new DalamudSettings();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Couldn't deserialize Dalamud settings");
            }

            deserialized ??= new DalamudSettings(); // In case the .json is corrupted
            return deserialized;
        }
    }

    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(DalamudVersionInfo))]
    [JsonSerializable(typeof(DalamudSettings))]
    internal partial class DalamudJsonContext: JsonSerializerContext
    {
    }
}