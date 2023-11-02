using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using XIVLauncher.Common.Game.Launcher;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game
{
    public partial class Headlines
    {
        [JsonPropertyName("news")]
        public News[] News { get; set; }

        [JsonPropertyName("topics")]
        public News[] Topics { get; set; }

        [JsonPropertyName("pinned")]
        public News[] Pinned { get; set; }
    }

    public class Banner
    {
        [JsonPropertyName("lsb_banner")]
        public Uri LsbBanner { get; set; }

        [JsonPropertyName("link")]
        public Uri Link { get; set; }

        [JsonPropertyName("order_priority")]
        public int? OrderPriority { get; set; }

        [JsonPropertyName("fix_order")]
        public int? FixOrder { get; set; }
    }

    public class BannerRoot
    {
        [JsonPropertyName("banner")]
        public List<Banner> Banner { get; set; }
    }

    public class News
    {
        [JsonPropertyName("date")]
        public DateTimeOffset Date { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("tag")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Tag { get; set; }
    }

    public partial class Headlines
    {
        public static async Task<Headlines> GetNews(ILauncher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/news/headline.json?lang={langCode}&media=pcapp&_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonSerializer.Deserialize<Headlines>(json);
        }

        public static async Task<IReadOnlyList<Banner>> GetBanners(ILauncher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/v2/topics/{langCode}/banner.json?lang={langCode}&media=pcapp&_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonSerializer.Deserialize<BannerRoot>(json).Banner;
        }

        public static async Task<IReadOnlyCollection<Banner>> GetMessage(ILauncher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/v2/notice/{langCode}/message.json?_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonSerializer.Deserialize<BannerRoot>(json).Banner;
        }

        public static async Task<IReadOnlyCollection<Banner>> GetWorlds(ILauncher game, ClientLanguage language)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var url = $"https://frontier.ffxiv.com/v2/world/status.json?_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonSerializer.Deserialize<BannerRoot>(json).Banner;
        }
    }
}
