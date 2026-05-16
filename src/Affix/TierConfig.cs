namespace DriftRelics.Affixes;

public sealed class TierConfig
{
    public string Code { get; set; } = "";
    public int Weight { get; set; } = 50;
    public string Color { get; set; } = "#aaaaaa";
    public int AffixCount { get; set; } = 1;
    public double ValueScale { get; set; } = 1.0;
    public bool Signature { get; set; } = false;
}
