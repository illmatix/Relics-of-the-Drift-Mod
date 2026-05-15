using System.Collections.Generic;

namespace Baubles.Affixes;

public sealed class AffixConfig
{
    public AffixRollChances RollChances { get; set; } = new();
    public List<Affix> Prefixes { get; set; } = new();
    public List<Affix> Suffixes { get; set; } = new();
}

public sealed class AffixRollChances
{
    public double Prefix { get; set; } = 0.75;
    public double Suffix { get; set; } = 0.75;
}
