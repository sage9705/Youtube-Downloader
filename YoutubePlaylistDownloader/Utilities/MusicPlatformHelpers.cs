using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeExplode.Videos;

namespace YoutubePlaylistDownloader.Utilities
{
    public static class MusicPlatformHelpers
    {
        private static readonly HttpClient httpClient;

        static MusicPlatformHelpers()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(20);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public static async Task<List<string>> GetSpotifyPlaylistTracksAsync(string url)
        {
            var tracks = new List<string>();
            try
            {
                var html = await httpClient.GetStringAsync(url);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var scriptNode = doc.DocumentNode.SelectSingleNode("//script[@id='initialState']");
                if (scriptNode != null)
                {
                    try
                    {
                        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(scriptNode.InnerText));
                        var jsonDoc = JsonDocument.Parse(decoded);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("entities", out var entities) &&
                            entities.TryGetProperty("items", out var items))
                        {
                            foreach (var prop in items.EnumerateObject())
                            {
                                if (!prop.Name.Contains("playlist")) continue;

                                if (prop.Value.TryGetProperty("content", out var content) &&
                                    content.TryGetProperty("items", out var playlistItems))
                                {
                                    foreach (var item in playlistItems.EnumerateArray())
                                    {
                                        try
                                        {
                                            var data = item.GetProperty("itemV2").GetProperty("data");
                                            var trackName = data.GetProperty("name").GetString();
                                            var artistName = data.GetProperty("artists")
                                                .GetProperty("items").EnumerateArray().First()
                                                .GetProperty("profile").GetProperty("name").GetString();
                                            if (!string.IsNullOrWhiteSpace(trackName))
                                                tracks.Add($"{artistName} {trackName}".Trim());
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                await GlobalConsts.Log(ex.ToString(), "GetSpotifyPlaylistTracksAsync");
            }
            return tracks.Distinct().ToList();
        }

        public static async Task<List<string>> GetAppleMusicPlaylistTracksAsync(string url)
        {
            var tracks = new List<string>();
            try
            {
                var html = await httpClient.GetStringAsync(url);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (scriptNodes != null)
                {
                    foreach (var node in scriptNodes)
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(node.InnerText);
                            ExtractAppleMusicTracks(jsonDoc.RootElement, tracks);
                        }
                        catch { }
                    }
                }

                if (!tracks.Any())
                {
                    var songNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'songs-list-row__song-name')]");
                    var artistNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'songs-list-row__by-line')]");

                    if (songNodes != null)
                    {
                        for (int i = 0; i < songNodes.Count; i++)
                        {
                            var trackName = songNodes[i].InnerText.Trim();
                            var artistName = artistNodes != null && i < artistNodes.Count ? artistNodes[i].InnerText.Trim() : "";
                            tracks.Add($"{artistName} {trackName}".Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await GlobalConsts.Log(ex.ToString(), "GetAppleMusicPlaylistTracksAsync");
            }
            return tracks.Distinct().ToList();
        }

        private static void ExtractAppleMusicTracks(JsonElement element, List<string> tracks)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("@type", out var typeElement) && typeElement.GetString() == "MusicRecording")
                {
                    if (element.TryGetProperty("name", out var nameElement))
                    {
                        var trackName = nameElement.GetString();
                        var artistName = "";
                        if (element.TryGetProperty("byArtist", out var artistElement))
                        {
                            if (artistElement.ValueKind == JsonValueKind.Array)
                            {
                                var firstArtist = artistElement.EnumerateArray().FirstOrDefault();
                                if (firstArtist.ValueKind == JsonValueKind.Object && firstArtist.TryGetProperty("name", out var artistNameElement))
                                    artistName = artistNameElement.GetString();
                            }
                            else if (artistElement.ValueKind == JsonValueKind.Object)
                            {
                                if (artistElement.TryGetProperty("name", out var artistNameElement))
                                    artistName = artistNameElement.GetString();
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(trackName))
                            tracks.Add($"{artistName} {trackName}".Trim());
                    }
                }

                foreach (var property in element.EnumerateObject())
                    ExtractAppleMusicTracks(property.Value, tracks);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    ExtractAppleMusicTracks(item, tracks);
            }
        }

        public static async Task<List<IVideo>> MatchTracksToYoutubeAsync(IEnumerable<string> tracks, Action<int, int, string> progressCallback = null)
        {
            var matchedVideos = new List<IVideo>();
            var client = GlobalConsts.YoutubeClient;

            var trackList = tracks.ToList();
            int total = trackList.Count;

            for (int i = 0; i < total; i++)
            {
                var track = trackList[i];
                progressCallback?.Invoke(i + 1, total, track);
                try
                {
                    var results = await client.Search.GetVideosAsync(track).CollectAsync(1);
                    var bestMatch = results.FirstOrDefault();
                    if (bestMatch != null)
                        matchedVideos.Add(bestMatch);
                }
                catch (Exception ex)
                {
                    await GlobalConsts.Log(ex.ToString(), $"MatchTracksToYoutubeAsync - Track: {track}");
                }
            }

            return matchedVideos;
        }
    }
}
