public class MetaSnapshotDto {
    public string LegendName { get; set; }
    public int SampleSize { get; set; }
    public double? AveragePlacement { get; set; }
    public int? BestPlacement { get; set; }
    public int? WorstPlacement { get; set; }
    
    public List<CardInsightDto> CoreCards { get; set; }
    public List<CardInsightDto> TechChoices { get; set; }
    
}