using System.Text.Json;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Infrastructure.Scraping;

public class CardScraper : ICardScraper
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public async Task<List<Card>> ScrapeCardsAsync(string url)
    {
        if (!url.Contains("api.dotgg.gg"))
        {
            url = "https://api.dotgg.gg/cgfw/getcards?game=riftbound&mode=indexed&cache=8594";
        }

        try
        {
            if (!_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"))
            {
                 _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            }
            
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (!root.TryGetProperty("names", out var namesElement))
            {
                  Console.WriteLine("Error: 'names' property not found in JSON response.");
                  return new List<Card>();
            }

            var names = new List<string>();
            foreach (var name in namesElement.EnumerateArray())
            {
                names.Add(name.GetString() ?? "");
            }

            int idIndex = names.IndexOf("id");
            int nameIndex = names.IndexOf("name");
            int colorIndex = names.IndexOf("color");
            int costIndex = names.IndexOf("cost");
            int typeIndex = names.IndexOf("type");
            int supertypeIndex = names.IndexOf("supertype");
            int tagsIndex = names.IndexOf("tags");

            if (!root.TryGetProperty("data", out var dataElement))
            {
                 Console.WriteLine("Error: 'data' property not found in JSON response.");
                 return new List<Card>();
            }

            var cards = new List<Card>();

            foreach (var row in dataElement.EnumerateArray())
            {
                if (idIndex == -1 || nameIndex == -1) continue;
                if (row.GetArrayLength() <= Math.Max(idIndex, nameIndex)) continue;

                // 1. ID
                string id = row[idIndex].GetString() ?? "";
                if (string.IsNullOrEmpty(id)) continue;

                // 2. Name
                string name = row[nameIndex].GetString() ?? "Unknown";

                // 3. Domain (Color)
                string domain = "Colorless";
                if (colorIndex != -1 && row.GetArrayLength() > colorIndex)
                {
                    var colorVal = row[colorIndex];
                    if (colorVal.ValueKind == JsonValueKind.Array && colorVal.GetArrayLength() > 0)
                    {
                        domain = colorVal[0].GetString() ?? "Colorless";
                    }
                }

                // 4. EnergyCost
                int? energyCost = null;
                if (costIndex != -1 && row.GetArrayLength() > costIndex)
                {
                    var costVal = row[costIndex];
                    if (costVal.ValueKind == JsonValueKind.String && int.TryParse(costVal.GetString(), out int c))
                    {
                        energyCost = c;
                    }
                }

                // 5. CardType
                CardType cardType = CardType.Main;
                if (typeIndex != -1 && row.GetArrayLength() > typeIndex)
                {
                    string typeStr = row[typeIndex].GetString() ?? "";
                    if (string.Equals(typeStr, "Rune", StringComparison.OrdinalIgnoreCase)) cardType = CardType.Rune;
                    else if (string.Equals(typeStr, "Legend", StringComparison.OrdinalIgnoreCase)) cardType = CardType.Legend;
                }

                // 6. Keywords (Tags)
                var keywords = new List<string>();
                if (tagsIndex != -1 && row.GetArrayLength() > tagsIndex)
                {
                    var tagsVal = row[tagsIndex];
                    if (tagsVal.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tag in tagsVal.EnumerateArray())
                        {
                            var s = tag.GetString();
                            if (!string.IsNullOrEmpty(s)) keywords.Add(s);
                        }
                    }
                }

                // 7. ChampionTag
                string? championTag = null;
                if (supertypeIndex != -1 && row.GetArrayLength() > supertypeIndex)
                {
                    string supertype = row[supertypeIndex].GetString() ?? "";
                    if (string.Equals(supertype, "Champion", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = name.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                        championTag = parts.Length > 0 ? parts[0].Trim() : name;
                    }
                }

                cards.Add(new Card
                {
                    Id = id,
                    Name = name,
                    Domain = domain,
                    EnergyCost = energyCost,
                    CardType = cardType,
                    Keywords = keywords,
                    ChampionTag = championTag
                });
            }

            return cards;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scraping cards: {ex.Message}");
            return new List<Card>();
        }
    }
}
