using Microsoft.EntityFrameworkCore;
using RiftboundMetaAnalizer.Domain;
using System.IO;
using System.Text.RegularExpressions;

namespace RiftboundMetaAnalizer.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(RiftContext context, string filePath)
    {
        if (await context.Cards.AnyAsync())
        {
            return; // DB has been seeded
        }

        var cards = new List<Card>();
        
        var csvSplit = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);

        using (var reader = new StreamReader(filePath))
        {
            await reader.ReadLineAsync(); // Skip header
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line is not null)
                {
                    // Split line handling quotes
                    var rawValues = csvSplit.Split(line);
                    
                    if (rawValues.Length < 14) continue;

                    // 10: Card Type (Unit, Spell, Rune, Gear)
                    // 12: Domain
                    // 14: Ability (Contains [Keywords])

                    var id = rawValues[0];
                    var name = rawValues[1].Trim('"');
                    var energyStr = rawValues[7];
                    var rawType = rawValues[10];
                    var domain = rawValues[12];
                    
                    var abilityText = rawValues.Length > 14 ? rawValues[14] : string.Empty;

                    var cardType = CardType.Main;
                    if (string.Equals(rawType, "Rune", StringComparison.OrdinalIgnoreCase))
                    {
                        cardType = CardType.Rune;
                    }
                    else if (name.Contains(','))
                    {
                        cardType = CardType.Legend;
                    }

                    // Extract keywords from brackets in Ability text
                    List<string> keywords = new List<string>();
                    var matches = Regex.Matches(abilityText, @"\[(.*?)\]");
                    foreach (Match match in matches)
                    {
                        keywords.Add(match.Groups[1].Value);
                    }
                    keywords = keywords.Distinct().ToList();

                    cards.Add(new Card
                    {
                        Id = id,
                        Name = name,
                        CardType = cardType,
                        Domain = domain,
                        EnergyCost = int.TryParse(energyStr, out var cost) ? cost : null,
                        Keywords = keywords,
                        ChampionTag = null // Cannot determine easily from CSV
                    });
                }
            }
        }

        await context.Cards.AddRangeAsync(cards);
        await context.SaveChangesAsync();
    }
}
