using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public sealed class AffixConfig
{
    public List<TierConfig> Tiers { get; set; } = new();
    public Dictionary<string, SignatureAffix> Signatures { get; set; } = new();
    public List<Affix> Prefixes { get; set; } = new();
    public List<Affix> Suffixes { get; set; } = new();
}
