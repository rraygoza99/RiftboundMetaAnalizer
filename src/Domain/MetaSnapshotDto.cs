public class MetaSnapshotDto {
    public string LegendName { get; set; }
    public int SampleSize { get; set; }
    public double? AveragePlacement { get; set; }
    public int? BestPlacement { get; set; }
    public int? WorstPlacement { get; set; }
    public double PlayRate { get; set; }
    public double TopCutRate { get; set; }
    public Dictionary<int, double> EnergyCurve { get; set; } = new();
    public Dictionary<string, double> DomainDistribution { get; set; } = new();
    public List<CardPairDto> CardPairSynergies { get; set; } = new();
    
    public List<CardInsightDto> CoreCards { get; set; }
    public List<CardInsightDto> TechChoices { get; set; }
}

public class CardPairDto {
    public string Card1Id { get; set; }
    public string Card1Name { get; set; }
    public string Card2Id { get; set; }
    public string Card2Name { get; set; }
    /// <summary>How often these two cards appear together in the same deck (0-1).</summary>
    public double CoOccurrenceRate { get; set; }
}

public class TournamentTrendDto {
    public int TournamentId { get; set; }
    public string TournamentName { get; set; }
    public DateTime Date { get; set; }
    public int DecksEntered { get; set; }
    public double? AveragePlacement { get; set; }
    public int? BestPlacement { get; set; }
}

public class MatchupRowDto {
    public string LegendName { get; set; }
    public string LegendId { get; set; }
    /// <summary>Average placement delta vs this legend (negative = we do better).</summary>
    public double PlacementDelta { get; set; }
    /// <summary>Number of shared tournaments used to calculate the delta.</summary>
    public int SharedTournaments { get; set; }
}

public class LegendTierDto {
    public string LegendId { get; set; }
    public string LegendName { get; set; }
    public double PlayRate { get; set; }
    public double TopCutRate { get; set; }
    public double? AveragePlacement { get; set; }
    public double CompositeScore { get; set; }
    public string Tier { get; set; } // S, A, B, C, D
}